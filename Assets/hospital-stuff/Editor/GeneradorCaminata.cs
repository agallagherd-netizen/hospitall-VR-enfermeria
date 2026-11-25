using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera por código los <see cref="AnimationClip"/> de "caminar" e "idle" a partir de
/// los huesos reales de un modelo humanoide, sin depender de ningún archivo de animación externo.
///
/// Los clips se escriben con rutas de Transform ("LeftUpLeg", "mixamorig:LeftUpLeg", ...),
/// por lo que sirven tanto con avatares Humanoid como Genéricos.
///
/// Para no depender de la convención de ejes de cada hueso, el generador mide sobre el
/// modelo instanciado cuál de los tres ejes locales de cada hueso corresponde al lateral,
/// vertical y frontal del personaje, y expresa todos los giros en esos ejes. Además cuelga
/// los brazos si el modelo viene en T-pose, de forma que el ciclo sale bien tanto con un
/// esqueleto en A-pose como en T-pose.
/// </summary>
public static class GeneradorCaminata
{
    // ---------------------------------------------------------------------
    //  Amplitudes del ciclo de caminar. En grados, salvo la elevación.
    //  Se pueden ajustar aquí y volver a ejecutar el menú del editor.
    // ---------------------------------------------------------------------
    private const float AmpMuslo = 32f;      // balanceo del muslo (adelante/atrás)
    private const float AmpRodilla = 58f;    // flexión máxima de la rodilla
    private const float AmpTobillo = 15f;    // flexión del tobillo
    private const float AmpPunta = 9f;       // flexión de los dedos del pie
    private const float AmpBrazo = 26f;      // balanceo del brazo
    private const float AmpCodo = 20f;       // flexión base del codo
    private const float AmpCodoVar = 7f;     // variación de la flexión del codo
    private const float AmpHombro = 7f;      // participación del hombro en el balanceo
    private const float BrazoCaido = 6f;     // separación lateral de los brazos colgando
    private const float ElevacionCaderas = 0.035f; // balanceo vertical de la pelvis (metros)
    private const float GiroPelvisY = 7f;    // giro de la pelvis sobre el eje vertical
    private const float InclinaPelvisZ = 4f; // balanceo lateral de la pelvis
    private const float InclinaTorsoX = 5f;  // inclinación hacia delante del tronco
    private const float GiroTorsoY = -4f;    // contraposición del tronco
    private const float GiroCabezaY = 3f;    // estabilización de la cabeza

    // Muestras por ciclo. Con 60 muestras el interpolado lineal es indistinguible del
    // suavizado y el bucle queda perfecto.
    private const int MuestrasPorCiclo = 60;

    /// <summary>Huesos que se usan para construir el esqueleto.</summary>
    private static readonly HashSet<string> NombresHuesos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
            "LeftShoulder", "LeftArm", "LeftForeArm",
            "RightShoulder", "RightArm", "RightForeArm",
            "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
            "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase"
        };

    /// <summary>
    /// Estado de reposo corregido de un hueso: su rotación local base y qué eje local
    /// corresponde a cada eje del personaje.
    /// </summary>
    private class Reposo
    {
        public Quaternion Base;
        public Vector3 Origen;    // posición local original del hueso
        public Vector3 Lateral;   // eje local más cercano al X del personaje (adelante/atrás)
        public Vector3 Vertical;  // eje local más cercano al Y del personaje
        public Vector3 Frontal;   // eje local más cercano al Z del personaje
    }

    private class Canal
    {
        public string Propiedad;
        public List<Keyframe> Keys;
    }

    // =====================================================================
    //  Localización de huesos
    // =====================================================================

    /// <summary>
    /// Devuelve un mapa "nombre del hueso" -> ruta relativa de Transform respecto a
    /// <paramref name="raiz"/> (normalmente el GameObject que lleva el Animator).
    /// Tolera que el importador haya quitado o no el prefijo del exportador
    /// ("mixamorig:LeftUpLeg" o "LeftUpLeg").
    /// </summary>
    public static Dictionary<string, string> ResolverRutas(Transform raiz)
    {
        var rutas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (raiz == null)
        {
            return rutas;
        }

        foreach (Transform hueso in raiz.GetComponentsInChildren<Transform>(true))
        {
            string nombre = LimpiarPrefijo(hueso.name);

            if (!NombresHuesos.Contains(nombre) || rutas.ContainsKey(nombre))
            {
                continue;
            }

            string ruta = CalcularRuta(raiz, hueso);

            if (ruta != null)
            {
                rutas[nombre] = ruta;
            }
        }

        return rutas;
    }

    /// <summary>Ruta de un hueso, o null si el modelo no lo tiene.</summary>
    public static string Ruta(Dictionary<string, string> rutas, string nombre)
    {
        return rutas.TryGetValue(nombre, out string ruta) ? ruta : null;
    }

    private static string LimpiarPrefijo(string nombre)
    {
        int separador = nombre.IndexOf(':');
        return separador >= 0 ? nombre.Substring(separador + 1) : nombre;
    }

    private static string CalcularRuta(Transform raiz, Transform hueso)
    {
        var partes = new List<string>();
        Transform actual = hueso;

        while (actual != null && actual != raiz)
        {
            partes.Add(actual.name);
            actual = actual.parent;
        }

        if (actual == null)
        {
            return null;
        }

        partes.Reverse();
        return string.Join("/", partes.ToArray());
    }

    /// <summary>Resuelve una ruta relativa de Transform a partir de la raíz.</summary>
    private static Transform BuscarPorRuta(Transform raiz, string ruta)
    {
        if (string.IsNullOrEmpty(ruta) || raiz == null)
        {
            return null;
        }

        Transform actual = raiz;

        foreach (string parte in ruta.Split('/'))
        {
            actual = actual.Find(parte);

            if (actual == null)
            {
                return null;
            }
        }

        return actual;
    }

    // =====================================================================
    //  Estado de reposo corregido
    // =====================================================================

    /// <summary>
    /// Coloca los brazos colgando (para corregir una T-pose), mide la orientación resulting
    /// de cada hueso y devuelve su estado de reposo. El modelo se deja tal y como estaba:
    /// los cambios se deshacen antes de salir.
    /// </summary>
    private static Dictionary<string, Reposo> MedirReposo(Transform raiz, Dictionary<string, string> rutas)
    {
        Dictionary<string, Transform> transforms = LocalizarHuesos(raiz, rutas);

        // Se guarda la rotación local original de todos los huesos para poder deshacer
        // la corrección de brazos y dejar el modelo intacto.
        var originales = new Dictionary<Transform, Quaternion>();

        foreach (Transform hueso in transforms.Values)
        {
            if (hueso != null)
            {
                originales[hueso] = hueso.localRotation;
            }
        }

        try
        {
            ColgarBrazos(transforms);

            var reposo = new Dictionary<string, Reposo>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, Transform> par in transforms)
            {
                Transform hueso = par.Value;

                if (hueso == null || hueso.parent == null)
                {
                    continue;
                }

                reposo[par.Key] = new Reposo
                {
                    Base = Quaternion.Inverse(hueso.parent.rotation) * hueso.rotation,
                    Origen = hueso.localPosition,
                    Lateral = EjeLocalMasParecido(hueso.rotation, Vector3.right),
                    Vertical = EjeLocalMasParecido(hueso.rotation, Vector3.up),
                    Frontal = EjeLocalMasParecido(hueso.rotation, Vector3.forward)
                };
            }

            return reposo;
        }
        finally
        {
            foreach (KeyValuePair<Transform, Quaternion> par in originales)
            {
                par.Key.localRotation = par.Value;
            }
        }
    }

    private static Dictionary<string, Transform> LocalizarHuesos(
        Transform raiz, Dictionary<string, string> rutas)
    {
        var transforms = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, string> par in rutas)
        {
            transforms[par.Key] = BuscarPorRuta(raiz, par.Value);
        }

        return transforms;
    }

    /// <summary>
    /// Rota el húmero para que apunte hacia abajo. Es lo que arregla un modelo exportado
    /// en T-pose, en el que los brazos salen horizontales.
    /// </summary>
    private static void ColgarBrazos(Dictionary<string, Transform> transforms)
    {
        ColgarBrazo(transforms, "LeftArm", "LeftForeArm", BrazoCaido);
        ColgarBrazo(transforms, "RightArm", "RightForeArm", -BrazoCaido);
    }

    private static void ColgarBrazo(
        Dictionary<string, Transform> transforms, string brazo, string antebrazo, float separacion)
    {
        if (!transforms.TryGetValue(brazo, out Transform hueso) || hueso == null || hueso.parent == null)
        {
            return;
        }

        if (!transforms.TryGetValue(antebrazo, out Transform hijo) || hijo == null)
        {
            return;
        }

        Vector3 dirActual = hijo.position - hueso.position;

        if (dirActual.sqrMagnitude < 1e-6f)
        {
            return;
        }

        // Dirección objetivo: hacia el suelo, ligeramente separada del cuerpo.
        Vector3 objetivo = Quaternion.AngleAxis(separacion, Vector3.forward) * Vector3.down;
        Quaternion mundoObjetivo = Quaternion.FromToRotation(dirActual.normalized, objetivo) * hueso.rotation;

        hueso.localRotation = Quaternion.Inverse(hueso.parent.rotation) * mundoObjetivo;
    }

    /// <summary>
    /// Devuelve el eje local (right/up/forward) que mejor se alinea con un eje del personaje.
    /// Así el mismo código funciona con la convención de ejes de cualquier esqueleto.
    /// </summary>
    private static Vector3 EjeLocalMasParecido(Quaternion rotacion, Vector3 ejePersonaje)
    {
        float aX = Mathf.Abs(Vector3.Dot(rotacion * Vector3.right, ejePersonaje));
        float aY = Mathf.Abs(Vector3.Dot(rotacion * Vector3.up, ejePersonaje));
        float aZ = Mathf.Abs(Vector3.Dot(rotacion * Vector3.forward, ejePersonaje));

        if (aX >= aY && aX >= aZ)
        {
            return Vector3.right;
        }

        return aY >= aZ ? Vector3.up : Vector3.forward;
    }

    // =====================================================================
    //  Creación de los clips
    // =====================================================================

    /// <summary>
    /// Crea un clip de caminar en bucle. Un ciclo completo son dos pasos.
    /// </summary>
    public static AnimationClip CrearClipCaminar(Transform raiz, Dictionary<string, string> rutas, float duracionCiclo)
    {
        Dictionary<string, Reposo> reposo = MedirReposo(raiz, rutas);
        var canales = new Dictionary<string, List<Canal>>();

        // --- Pelvis y tronco ---
        Rotacion(canales, reposo, rutas, "Hips", duracionCiclo, (p, r) => Eje(
            0f,                                        // sin cabeceo de la pelvis
            GiroPelvisY * Sen(p),                      // giro sobre el eje vertical
            InclinaPelvisZ * Sen(p)));                 // balanceo lateral (la cadera baja)

        Posicion(canales, reposo, rutas, "Hips", duracionCiclo, (p, r) => new Vector3(
            0f,
            // Sube y baja dos veces por ciclo; el punto bajo coincide con el apoyo de los pies.
            ElevacionCaderas * (1f - Cos(2f * p)) * 0.5f,
            0f));

        Rotacion(canales, reposo, rutas, "Spine", duracionCiclo, (p, r) => Eje(
            InclinaTorsoX, GiroTorsoY * Sen(p), 0f));

        Rotacion(canales, reposo, rutas, "Spine1", duracionCiclo, (p, r) => Eje(
            InclinaTorsoX * 0.5f, GiroTorsoY * 0.5f * Sen(p), 0f));

        Rotacion(canales, reposo, rutas, "Spine2", duracionCiclo, (p, r) => Eje(
            InclinaTorsoX * 0.25f, GiroTorsoY * 0.25f * Sen(p), 0f));

        Rotacion(canales, reposo, rutas, "Neck", duracionCiclo, (p, r) => Eje(
            0f, -GiroCabezaY * 0.4f * Sen(p), 0f));

        Rotacion(canales, reposo, rutas, "Head", duracionCiclo, (p, r) => Eje(
            0f, -GiroCabezaY * Sen(p), 0f));

        // --- Pierna y brazo derechos (fase p) ---
        AnadirPiernaYBrazo(canales, reposo, rutas, duracionCiclo,
            pierna: "RightUpLeg", rodilla: "RightLeg", pie: "RightFoot", punta: "RightToeBase",
            hombro: "RightShoulder", brazo: "RightArm", antebrazo: "RightForeArm",
            desfasePierna: 0f, desfaseBrazo: 0.5f);

        // --- Pierna y brazo izquierdos (medio ciclo por delante) ---
        AnadirPiernaYBrazo(canales, reposo, rutas, duracionCiclo,
            pierna: "LeftUpLeg", rodilla: "LeftLeg", pie: "LeftFoot", punta: "LeftToeBase",
            hombro: "LeftShoulder", brazo: "LeftArm", antebrazo: "LeftForeArm",
            desfasePierna: 0.5f, desfaseBrazo: 0f);

        return ConstruirClip("Caminar", canales, duracionCiclo, enBucle: true);
    }

    /// <summary>
    /// Añade las curvas de una pierna y del brazo del lado contrario, que es como
    /// funciona de verdad el brazo de Swing.
    /// </summary>
    private static void AnadirPiernaYBrazo(
        Dictionary<string, List<Canal>> canales,
        Dictionary<string, Reposo> reposo,
        Dictionary<string, string> rutas,
        float duracionCiclo,
        string pierna, string rodilla, string pie, string punta,
        string hombro, string brazo, string antebrazo,
        float desfasePierna, float desfaseBrazo)
    {
        // El muslo ya cuelga de la cadera en un esqueleto normal; si el modelo viniera
        // sentado, la medición de reposo lo deja igualmente colgando.
        Rotacion(canales, reposo, rutas, pierna, duracionCiclo, (p, r) => Eje(
            -AmpMuslo * Sen(p + desfasePierna), 0f, 0f));

        Rotacion(canales, reposo, rutas, rodilla, duracionCiclo, (p, r) => Eje(
            AmpRodilla * Flexion(p + desfasePierna), 0f, 0f));

        Rotacion(canales, reposo, rutas, pie, duracionCiclo, (p, r) => Eje(
            -AmpTobillo * Sen(p + desfasePierna + 0.35f), 0f, 0f));

        Rotacion(canales, reposo, rutas, punta, duracionCiclo, (p, r) => Eje(
            AmpPunta * Sen(p + desfasePierna + 0.9f), 0f, 0f));

        Rotacion(canales, reposo, rutas, hombro, duracionCiclo, (p, r) => Eje(
            -AmpHombro * Sen(p + desfaseBrazo), 0f, 0f));

        Rotacion(canales, reposo, rutas, brazo, duracionCiclo, (p, r) => Eje(
            -AmpBrazo * Sen(p + desfaseBrazo), 0f, 0f));

        Rotacion(canales, reposo, rutas, antebrazo, duracionCiclo, (p, r) => Eje(
            -(AmpCodo + AmpCodoVar * Sen(p + desfaseBrazo)), 0f, 0f));
    }

    /// <summary>
    /// Crea un clip de reposo en bucle: la pose neutra corregida más una respiración suave.
    /// </summary>
    public static AnimationClip CrearClipIdle(Transform raiz, Dictionary<string, string> rutas, float duracionCiclo)
    {
        Dictionary<string, Reposo> reposo = MedirReposo(raiz, rutas);
        var canales = new Dictionary<string, List<Canal>>();

        // Se generan curvas para TODOS los huesos a propósito: un hueso sin curva mantiene
        // el último valor puesto, así que al volver de "Caminar" a "Idle" las piernas y los
        // brazos se quedarían congelados en la última pose del paso.
        foreach (string hueso in NombresHuesos)
        {
            Rotacion(canales, reposo, rutas, hueso, duracionCiclo, (p, r) => Respiracion(hueso, p));
        }

        Posicion(canales, reposo, rutas, "Hips", duracionCiclo, (p, r) =>
            new Vector3(0f, 0.006f * Sen(p), 0f));

        return ConstruirClip("Idle", canales, duracionCiclo, enBucle: true);
    }

    /// <summary>
    /// Movimiento de respiración por hueso en reposo. Lo que no aparece aquí se queda en cero,
    /// es decir, en la pose neutra.
    /// </summary>
    private static Vector3 Respiracion(string hueso, float fase)
    {
        switch (hueso)
        {
            case "Spine":
                return Eje(1.2f * Sen(fase), 0f, 0f);

            case "Neck":
                return Eje(-1.5f * Sen(fase), 0f, 0f);

            case "Head":
                return Eje(-1f * Sen(fase + 0.15f), 0f, 0f);

            case "LeftArm":
            case "RightArm":
                return Eje(-1.5f * Sen(fase), 0f, 0f);

            default:
                return Vector3.zero;
        }
    }

    // =====================================================================
    //  Utilidades de curvas
    // =====================================================================

    private static Reposo ObtenerReposo(Dictionary<string, Reposo> reposo, string hueso)
    {
        return reposo.TryGetValue(hueso, out Reposo valor) ? valor : null;
    }

    /// <summary>
    /// Añade las cuatro curvas de rotación local de un hueso.
    /// Los tres ángulos se aplican, en este orden, sobre los ejes frontal, lateral y
    /// vertical del personaje ya medidos sobre el modelo.
    /// </summary>
    private static void Rotacion(
        Dictionary<string, List<Canal>> canales,
        Dictionary<string, Reposo> reposo,
        Dictionary<string, string> rutas,
        string hueso,
        float duracionCiclo,
        Func<float, Reposo, Vector3> angular)
    {
        string ruta = Ruta(rutas, hueso);

        if (string.IsNullOrEmpty(ruta) || angular == null)
        {
            return;
        }

        Reposo estado = ObtenerReposo(reposo, hueso);

        if (estado == null)
        {
            return;
        }

        string[] propiedades =
        {
            "m_LocalRotation.x", "m_LocalRotation.y",
            "m_LocalRotation.z", "m_LocalRotation.w"
        };

        var muestras = new Quaternion[MuestrasPorCiclo + 1];

        for (int i = 0; i <= MuestrasPorCiclo; i++)
        {
            float fase = Fase(duracionCiclo * i / MuestrasPorCiclo, duracionCiclo);
            Vector3 angulos = angular(fase, estado);

            Quaternion q = estado.Base
                * Quaternion.AngleAxis(angulos.z, estado.Frontal)
                * Quaternion.AngleAxis(angulos.x, estado.Lateral)
                * Quaternion.AngleAxis(angulos.y, estado.Vertical);

            // w >= 0 para que el interpolado no dé la vuelta al cruzar los 180 grados.
            muestras[i] = q.w < 0f
                ? new Quaternion(-q.x, -q.y, -q.z, -q.w)
                : q;
        }

        for (int c = 0; c < 4; c++)
        {
            var claves = new List<Keyframe>(MuestrasPorCiclo + 1);

            for (int i = 0; i <= MuestrasPorCiclo; i++)
            {
                Quaternion q = muestras[i];
                float valor = c == 0 ? q.x : c == 1 ? q.y : c == 2 ? q.z : q.w;
                claves.Add(new Keyframe(duracionCiclo * i / MuestrasPorCiclo, valor));
            }

            AgregarCanal(canales, ruta, propiedades[c], claves);
        }
    }

    private static void Posicion(
        Dictionary<string, List<Canal>> canales,
        Dictionary<string, Reposo> reposo,
        Dictionary<string, string> rutas,
        string hueso,
        float duracionCiclo,
        Func<float, Reposo, Vector3> evaluar)
    {
        string ruta = Ruta(rutas, hueso);

        if (string.IsNullOrEmpty(ruta) || evaluar == null)
        {
            return;
        }

        Reposo estado = ObtenerReposo(reposo, hueso);

        if (estado == null)
        {
            return;
        }

        string[] propiedades = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };

        for (int c = 0; c < 3; c++)
        {
            var claves = new List<Keyframe>(MuestrasPorCiclo + 1);

            for (int i = 0; i <= MuestrasPorCiclo; i++)
            {
                float t = duracionCiclo * i / MuestrasPorCiclo;

                // La curva es una posición absoluta: se parte de la del hueso en reposo
                // y se le suma el desplazamiento, o el personaje se hundaría en el suelo.
                Vector3 v = estado.Origen + evaluar(Fase(t, duracionCiclo), estado);
                float valor = c == 0 ? v.x : c == 1 ? v.y : v.z;
                claves.Add(new Keyframe(t, valor));
            }

            AgregarCanal(canales, ruta, propiedades[c], claves);
        }
    }

    private static void AgregarCanal(
        Dictionary<string, List<Canal>> canales, string ruta, string propiedad, List<Keyframe> claves)
    {
        if (!canales.TryGetValue(ruta, out List<Canal> lista))
        {
            lista = new List<Canal>(4);
            canales[ruta] = lista;
        }

        lista.Add(new Canal { Propiedad = propiedad, Keys = claves });
    }

    private static AnimationClip ConstruirClip(
        string nombre, Dictionary<string, List<Canal>> canales, float duracionCiclo, bool enBucle)
    {
        var clip = new AnimationClip { name = nombre, frameRate = 60f };

        foreach (KeyValuePair<string, List<Canal>> entrada in canales)
        {
            foreach (Canal canal in entrada.Value)
            {
                var binding = EditorCurveBinding.FloatCurve(entrada.Key, typeof(Transform), canal.Propiedad);
                var curve = new AnimationCurve(canal.Keys.ToArray());
                curve.SmoothTangents(0, 0f);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
        }

        AnimationClipSettings ajustes = AnimationUtility.GetAnimationClipSettings(clip);
        ajustes.loopTime = enBucle;
        ajustes.cycleOffset = 0f;
        AnimationUtility.SetAnimationClipSettings(clip, ajustes);

        return clip;
    }

    // =====================================================================
    //  Matemáticas del ciclo
    // =====================================================================

    /// <summary>Fase normalizada 0..1 dentro del ciclo, en bucle.</summary>
    private static float Fase(float t, float duracionCiclo) => Mathf.Repeat(t / duracionCiclo, 1f);

    private static float Sen(float fase) => Mathf.Sin(2f * Mathf.PI * fase);

    private static float Cos(float fase) => Mathf.Cos(2f * Mathf.PI * fase);

    /// <summary>Flexión de rodilla: 0 en el apoyo, máxima en mitad de cada medio ciclo.</summary>
    private static float Flexion(float fase) => 0.5f - 0.5f * Cos(2f * fase);

    /// <summary>
    /// Ángulos en grados sobre los ejes del personaje medidos en el hueso:
    /// x = eje lateral (adelante/atrás), y = eje vertical (giro), z = eje frontal (lado/lado).
    /// </summary>
    private static Vector3 Eje(float x, float y, float z) => new Vector3(x, y, z);
}