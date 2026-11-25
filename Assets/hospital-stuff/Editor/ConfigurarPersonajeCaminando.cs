using System.Collections.Generic;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta en la escena, de forma reproducible, todo lo necesario para que el personaje
/// humanoide camine 2 metros: piso, NavMesh, AnimatorController y los clips de
/// caminar / idle generados por código.
///
/// Menú: Enfermeria > 1. Configurar personaje caminando (2 m)
/// </summary>
public static class ConfigurarPersonajeCaminando
{
    private const string RutaModelo = "Assets/yourMesh@Sitting Laughing.fbx";

    private const string CarpetaGenerados = "Assets/Caminata";
    private const string RutaController = CarpetaGenerados + "/Humanoid.controller";
    private const string RutaClipCaminar = CarpetaGenerados + "/Caminar.anim";
    private const string RutaClipIdle = CarpetaGenerados + "/Idle.anim";
    private const string RutaAvatar = CarpetaGenerados + "/AvatarGenerico.asset";

    private const string NombrePersonaje = "Personaje";
    private const string NombrePiso = "Piso";
    private const string NombreMarcador = "Destino 2m";
    private const string NombreNavMesh = "NavMesh";

    public const string NombreParametroCaminando = "Moviendose";

    private const string NombreTrigger = "Caminar";
    private const string EstadoCaminar = "Caminar";
    private const string EstadoIdle = "Idle";

    private const float DistanciaObjetivo = 2f;
    private const float LadoPisoMetros = 6f;
    private const float DuracionCicloCaminar = 1f;
    private const float DuracionCicloIdle = 2f;

    [MenuItem("Enfermeria/1. Configurar personaje caminando (2 m)", false, 1)]
    public static void Configurar()
    {
        var informe = new StringBuilder();
        Scene escena = SceneManager.GetActiveScene();

        PrepararCarpetas();
        Limpiar(informe);
        CorregirImportador(informe);

        GameObject personaje = InstanciarPersonaje(informe);

        if (personaje == null)
        {
            Debug.LogError("[ConfigurarPersonajeCaminando] No se pudo instanciar el modelo. Abortando.");
            return;
        }

        CrearPiso(informe);

        if (!GenerarNavMesh(informe))
        {
            EditorUtility.DisplayDialog(
                "No se pudo generar el NavMesh", "Revisa el Log de la consola para ver el motivo.", "Aceptar");
            return;
        }

        Animator animator = AsegurarAnimator(personaje, informe);
        Dictionary<string, string> rutas = GeneradorCaminata.ResolverRutas(personaje.transform);
        InformarSobreHuesos(rutas, informe);

        ConfigurarAgente(personaje);
        ConfigurarScriptCaminar(personaje);
        AsegurarControlFlecha(personaje);
        informe.AppendLine("- Control con la flecha derecha añadido al personaje.");

        // El FBX trae Avatar humanoide. Si el Animator no llegara a aplicar los clips,
        // se rehace todo con un Avatar genérico, que siempre funciona con rutas de Transform.
        if (!MontarAnimacion(personaje, animator, rutas, informe))
        {
            informe.AppendLine(
                "- El Animator no aplicó el clip con el Avatar humanoide del FBX. " +
                "Se regenera todo con un Avatar genérico.");

            if (CambiarAAvatarGenerico(personaje, animator, informe))
            {
                MontarAnimacion(personaje, animator, rutas, informe);
            }
        }

        CrearMarcadorDestino();
        EncuadrarCamara();

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();

        informe.AppendLine();
        informe.AppendLine(
            "Listo. Pulsa Play: el personaje camina 2 m y se detiene en Idle. " +
            "Después, mantén pulsada la FLECHA DERECHA para moverlo; al soltarla vuelve a Idle.");
        Debug.Log("[ConfigurarPersonajeCaminando]\n" + informe, null);
    }

    // =====================================================================
    //  Preparación
    // =====================================================================

    private static void PrepararCarpetas()
    {
        if (!AssetDatabase.IsValidFolder(CarpetaGenerados))
        {
            AssetDatabase.CreateFolder("Assets", "Caminata");
        }
    }

    /// <summary>
    /// Borra lo que hubiera creado una ejecución anterior, para que el menú sea idempotente.
    /// </summary>
    private static void Limpiar(StringBuilder informe)
    {
        GameObject navMesh = GameObject.Find(NombreNavMesh);

        if (navMesh != null && navMesh.TryGetComponent(out NavMeshSurface superficie))
        {
            superficie.RemoveData();
        }

        int borrados = 0;

        foreach (string nombre in new[] { NombrePersonaje, NombrePiso, NombreMarcador, NombreNavMesh })
        {
            GameObject objetivo = GameObject.Find(nombre);

            if (objetivo == null)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(objetivo);
            borrados++;
        }

        if (borrados > 0)
        {
            informe.AppendLine($"- Se eliminaron {borrados} objeto(s) de una ejecución anterior.");
        }
    }

    /// <summary>
    /// Asegura que el FBX se importe como humanoide con Avatar propio, que es lo que
    /// necesita el Animator para funcionar.
    /// </summary>
    private static void CorregirImportador(StringBuilder informe)
    {
        ModelImporter importador = AssetImporter.GetAtPath(RutaModelo) as ModelImporter;

        if (importador == null)
        {
            Debug.LogError($"[ConfigurarPersonajeCaminando] No se encuentra el modelo en «{RutaModelo}».");
            return;
        }

        bool cambios = false;

        if (importador.animationType != ModelImporterAnimationType.Human)
        {
            importador.animationType = ModelImporterAnimationType.Human;
            cambios = true;
        }

        if (importador.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importador.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            cambios = true;
        }

        if (!importador.importAnimation)
        {
            importador.importAnimation = true;
            cambios = true;
        }

        if (cambios)
        {
            importador.SaveAndReimport();
            informe.AppendLine("- Importador del FBX corregido (Animation Type = Humanoid, Create Avatar).");
        }
        else
        {
            informe.AppendLine("- El FBX ya estaba configurado como humanoide con Avatar.");
        }
    }

    private static GameObject InstanciarPersonaje(StringBuilder informe)
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaModelo);

        if (modelo == null)
        {
            Debug.LogError($"[ConfigurarPersonajeCaminando] No se pudo cargar «{RutaModelo}».");
            return null;
        }

        GameObject personaje = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        Undo.RegisterCreatedObjectUndo(personaje, "Configurar personaje caminando");

        personaje.name = NombrePersonaje;
        personaje.transform.position = Vector3.zero;
        personaje.transform.rotation = Quaternion.identity;

        // Los assets de FBX no se pueden modificar sin desregistrarlos antes.
        if (PrefabUtility.IsPartOfPrefabInstance(personaje))
        {
            PrefabUtility.UnpackPrefabInstance(
                personaje, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        informe.AppendLine("- Personaje instanciado en el origen, mirando hacia +Z.");
        return personaje;
    }

    private static void CrearPiso(StringBuilder informe)
    {
        GameObject piso = GameObject.CreatePrimitive(PrimitiveType.Plane);
        piso.name = NombrePiso;
        piso.transform.position = Vector3.zero;

        // El plano primitivo mide 10x10 unidades.
        float escala = LadoPisoMetros / 10f;
        piso.transform.localScale = new Vector3(escala, 1f, escala);

        Undo.RegisterCreatedObjectUndo(piso, "Configurar personaje caminando");

        informe.AppendLine($"- Piso creado ({LadoPisoMetros:0.#} x {LadoPisoMetros:0.#} m) en y = 0.");
    }

    // =====================================================================
    //  NavMesh
    // =====================================================================

    private static bool GenerarNavMesh(StringBuilder informe)
    {
        GameObject contenedor = new GameObject(NombreNavMesh);
        Undo.RegisterCreatedObjectUndo(contenedor, "Configurar personaje caminando");

        NavMeshSurface superficie = contenedor.AddComponent<NavMeshSurface>();
        superficie.collectObjects = CollectObjects.All;
        superficie.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        superficie.layerMask = ~0;
        superficie.ignoreNavMeshAgent = true;

        superficie.BuildNavMesh();

        int vertices = NavMesh.CalculateTriangulation().vertices.Length;

        if (vertices == 0)
        {
            informe.AppendLine("- ERROR: el NavMesh quedó vacío. Revisa que el Piso tenga MeshRenderer.");
            Debug.LogError("[ConfigurarPersonajeCaminando] El NavMesh se generó vacío.");
            return false;
        }

        informe.AppendLine($"- NavMesh generado correctamente ({vertices} vértices).");
        return true;
    }

    // =====================================================================
    //  Avatar
    // =====================================================================

    private static Animator AsegurarAnimator(GameObject personaje, StringBuilder informe)
    {
        Animator animator = personaje.GetComponent<Animator>();

        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(personaje);
            informe.AppendLine("- Se añadió un Animator al personaje (el FBX no traía ninguno).");
        }

        Avatar avatar = animator.avatar;

        if (avatar != null && avatar.isValid && avatar.isHuman)
        {
            informe.AppendLine($"- Avatar humanoide válido del FBX: «{avatar.name}».");
            return animator;
        }

        if (CambiarAAvatarGenerico(personaje, animator, informe))
        {
            informe.AppendLine("- El FBX no traía Avatar válido; se generó uno genérico a partir del esqueleto.");
        }
        else
        {
            informe.AppendLine("- AVISO: no se pudo crear ningún Avatar. El Animator no podrá animar.");
        }

        return animator;
    }

    private static bool CambiarAAvatarGenerico(GameObject personaje, Animator animator, StringBuilder informe)
    {
        AssetDatabase.DeleteAsset(RutaAvatar);

        Avatar generado = AvatarBuilder.BuildGenericAvatar(personaje, string.Empty);

        if (generado == null || !generado.isValid)
        {
            Debug.LogWarning(
                "[ConfigurarPersonajeCaminando] AvatarBuilder no produjo un Avatar válido.", personaje);
            return false;
        }

        AssetDatabase.CreateAsset(generado, RutaAvatar);
        AssetDatabase.SaveAssets();

        Avatar guardado = AssetDatabase.LoadAssetAtPath<Avatar>(RutaAvatar);

        if (guardado == null || !guardado.isValid)
        {
            return false;
        }

        animator.avatar = guardado;
        return true;
    }

    // =====================================================================
    //  Clips, controller y verificación
    // =====================================================================

    /// <summary>
    /// Genera los clips, construye el AnimatorController, lo asigna y comprueba que el
    /// Animator realmente los aplique. Devuelve false si el clip no se mueve.
    /// </summary>
    private static bool MontarAnimacion(
        GameObject personaje, Animator animator, Dictionary<string, string> rutas, StringBuilder informe)
    {
        AnimationClip clipCaminar = CrearYGuardarClip(
            GeneradorCaminata.CrearClipCaminar(personaje.transform, rutas, DuracionCicloCaminar),
            RutaClipCaminar, informe);

        AnimationClip clipIdle = CrearYGuardarClip(
            GeneradorCaminata.CrearClipIdle(personaje.transform, rutas, DuracionCicloIdle),
            RutaClipIdle, informe);

        AnimatorController controller = CrearController(clipIdle, clipCaminar, informe);

        animator.runtimeAnimatorController = controller;

        // El NavMeshAgent es quien mueve al personaje: el Animator no debe generar
        // movimiento por raíz o se pelean los dos.
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;

        // Para que siga caminando aunque se salga del encuadre de la cámara.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.enabled = true;

        return VerificarClip(animator, personaje, rutas, informe);
    }

    /// <summary>
    /// Comprueba de verdad que el Animator aplica el clip: reproduce el estado "Caminar"
    /// fuera del modo juego y mide cuánto se ha movido un hueso. Si el clip no llegara a
    ///.bindearse (Avatar mal configurado, rutas que no casan...) el hueso no se movería.
    /// Al terminar se restaura la pose original del modelo.
    /// </summary>
    private static bool VerificarClip(
        Animator animator, GameObject personaje, Dictionary<string, string> rutas, StringBuilder informe)
    {
        Transform hueso = BuscarPorRuta(personaje.transform, GeneradorCaminata.Ruta(rutas, "LeftUpLeg"));

        if (hueso == null)
        {
            informe.AppendLine("- No se ha podido verificar el clip (no se encuentra el hueso LeftUpLeg).");
            return true;
        }

        List<PoseHueso> poseOriginal = CapturarPose(personaje.transform, rutas);
        Quaternion antes = hueso.localRotation;

        int hashMovimiento = Animator.StringToHash(NombreParametroCaminando);

        animator.Rebind();

        // El estado Caminar depende del bool: sin activarlo, la máquina de estados
        // volvería a Idle en el mismo fotograma y la verificación fallaría.
        animator.SetBool(hashMovimiento, true);
        animator.Update(0f);

        float movimiento = 0f;

        foreach (float momento in new[] { 0.15f, 0.35f, 0.6f })
        {
            animator.Play(EstadoCaminar, 0, momento);
            animator.Update(0f);

            movimiento = Mathf.Max(movimiento, Quaternion.Angle(antes, hueso.localRotation));
        }

        animator.SetBool(hashMovimiento, false);
        animator.Play(EstadoIdle, 0, 0f);
        animator.Update(0f);
        RestaurarPose(poseOriginal);

        bool funciona = movimiento > 0.5f;

        informe.AppendLine(
            $"- Verificación del Animator: «LeftUpLeg» se movió {movimiento:0.0}° al reproducir «{EstadoCaminar}» " +
            (funciona ? "-> OK." : "-> el clip NO se está aplicando."));

        if (!funciona)
        {
            Debug.LogError(
                "[ConfigurarPersonajeCaminando] El Animator no aplicó el clip de caminar.", personaje);
        }

        return funciona;
    }

    private struct PoseHueso
    {
        public Transform Hueso;
        public Vector3 Posicion;
        public Quaternion Rotacion;
    }

    private static List<PoseHueso> CapturarPose(Transform raiz, Dictionary<string, string> rutas)
    {
        var pose = new List<PoseHueso>();

        foreach (string ruta in rutas.Values)
        {
            Transform hueso = BuscarPorRuta(raiz, ruta);

            if (hueso != null)
            {
                pose.Add(new PoseHueso
                {
                    Hueso = hueso,
                    Posicion = hueso.localPosition,
                    Rotacion = hueso.localRotation
                });
            }
        }

        return pose;
    }

    private static void RestaurarPose(List<PoseHueso> pose)
    {
        foreach (PoseHueso entrada in pose)
        {
            entrada.Hueso.localRotation = entrada.Rotacion;
            entrada.Hueso.localPosition = entrada.Posicion;
        }
    }

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

    private static void InformarSobreHuesos(Dictionary<string, string> rutas, StringBuilder informe)
    {
        string[] imprescindibles =
        {
            "Hips", "Spine", "Head",
            "LeftUpLeg", "LeftLeg", "LeftFoot",
            "RightUpLeg", "RightLeg", "RightFoot",
            "LeftArm", "LeftForeArm", "RightArm", "RightForeArm"
        };

        var faltan = new List<string>();

        foreach (string hueso in imprescindibles)
        {
            if (!rutas.ContainsKey(hueso))
            {
                faltan.Add(hueso);
            }
        }

        if (faltan.Count == 0)
        {
            informe.AppendLine($"- Se localizaron los {rutas.Count} huesos del esqueleto.");
            return;
        }

        informe.AppendLine(
            $"- AVISO: faltan {faltan.Count} hueso(s): {string.Join(", ", faltan)}. " +
            "El ciclo se generará solo con los que sí existen.");

        Debug.LogWarning(
            "[ConfigurarPersonajeCaminando] El esqueleto no tiene: " + string.Join(", ", faltan));
    }

    private static AnimationClip CrearYGuardarClip(AnimationClip clip, string ruta, StringBuilder informe)
    {
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(clip, ruta);

        AnimationClip guardado = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
        informe.AppendLine($"- Clip generado: «{guardado.name}» ({guardado.length:0.00} s).");
        return guardado;
    }

    private static AnimatorController CrearController(
        AnimationClip clipIdle, AnimationClip clipCaminar, StringBuilder informe)
    {
        AssetDatabase.DeleteAsset(RutaController);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(RutaController);
        controller.AddParameter(NombreParametroCaminando, AnimatorControllerParameterType.Bool);

        AnimatorStateMachine maquina = controller.layers[0].stateMachine;

        AnimatorState estadoIdle = maquina.AddState(EstadoIdle, new Vector3(260f, 0f, 0f));
        estadoIdle.motion = clipIdle;
        maquina.defaultState = estadoIdle;

        AnimatorState estadoCaminar = maquina.AddState(EstadoCaminar, new Vector3(260f, 90f, 0f));
        estadoCaminar.motion = clipCaminar;

        // Idle -> Caminar en cuanto el personaje empieza a moverse.
        AnimatorStateTransition haciaCaminar = estadoIdle.AddTransition(estadoCaminar);
        haciaCaminar.hasExitTime = false;
        haciaCaminar.hasFixedDuration = true;
        haciaCaminar.duration = 0.15f;
        haciaCaminar.AddCondition(AnimatorConditionMode.If, 0f, NombreParametroCaminando);

        // Caminar -> Idle en cuanto se detiene, sin esperar al final del clip, para que
        // la animación responda igual de rápido que el control por teclado.
        AnimatorStateTransition haciaIdle = estadoCaminar.AddTransition(estadoIdle);
        haciaIdle.hasExitTime = false;
        haciaIdle.hasFixedDuration = true;
        haciaIdle.duration = 0.2f;
        haciaIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, NombreParametroCaminando);

        EditorUtility.SetDirty(controller);

        informe.AppendLine(
            $"- AnimatorController creado con los estados «{EstadoIdle}» y «{EstadoCaminar}» " +
            $"(parámetro bool «{NombreParametroCaminando}»).");

        return controller;
    }

    // =====================================================================
    //  NavMeshAgent y script de caminar
    // =====================================================================

    private static void ConfigurarAgente(GameObject personaje)
    {
        NavMeshAgent agente = personaje.GetComponent<NavMeshAgent>();

        if (agente == null)
        {
            agente = Undo.AddComponent<NavMeshAgent>(personaje);
        }

        agente.radius = 0.25f;
        agente.height = 1.8f;
        agente.baseOffset = 0f;
        agente.speed = 1.4f;
        agente.acceleration = 12f;
        agente.angularSpeed = 720f;
        agente.stoppingDistance = 0.05f;
        agente.autoBraking = true;
        agente.autoRepath = true;
        agente.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
    }

    private static void ConfigurarScriptCaminar(GameObject personaje)
    {
        CaminarDosMetros script = personaje.GetComponent<CaminarDosMetros>();

        if (script == null)
        {
            script = Undo.AddComponent<CaminarDosMetros>(personaje);
        }

        // Los campos son privados: se escriben con SerializedObject para que Unity los
        // marque como modificados y los guarde con la escena.
        var serializado = new SerializedObject(script);
        serializado.FindProperty("distanciaMetros").floatValue = DistanciaObjetivo;
        serializado.FindProperty("nombreParametroCaminando").stringValue = NombreParametroCaminando;
        serializado.FindProperty("nombreTriggerCaminar").stringValue = NombreTrigger;
        serializado.FindProperty("caminarAlIniciar").boolValue = true;
        serializado.FindProperty("repetirEnBucle").boolValue = false;
        serializado.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Añade (o reutiliza) el script que mueve al personaje con la flecha derecha.
    /// Es público porque también lo usa el menú «2. Activar control con flecha derecha».
    /// </summary>
    public static ControlFlechaDerecha AsegurarControlFlecha(GameObject personaje)
    {
        ControlFlechaDerecha control = personaje.GetComponent<ControlFlechaDerecha>();

        if (control == null)
        {
            control = Undo.AddComponent<ControlFlechaDerecha>(personaje);
        }

        var serializado = new SerializedObject(control);
        serializado.FindProperty("distanciaMaxima").floatValue = 30f;
        serializado.FindProperty("margenBordeNavMesh").floatValue = 0.45f;
        serializado.FindProperty("usarDireccionDeCamara").boolValue = true;
        serializado.FindProperty("nombreParametroCaminando").stringValue = NombreParametroCaminando;
        serializado.FindProperty("nombreTriggerCaminar").stringValue = NombreTrigger;
        serializado.FindProperty("tomarControlDeLaCaminataAutomatica").boolValue = true;
        serializado.ApplyModifiedPropertiesWithoutUndo();

        return control;
    }

    // =====================================================================
    //  Ayudas de escena
    // =====================================================================

    /// <summary>
    /// Marcador visual del punto de llegada. Se crea DESPUÉS de hornear el NavMesh
    /// para no alterar la navegación con su malla.
    /// </summary>
    private static void CrearMarcadorDestino()
    {
        GameObject marcador = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marcador.name = NombreMarcador;
        marcador.transform.position = new Vector3(0f, 0.05f, DistanciaObjetivo);
        marcador.transform.localScale = new Vector3(0.2f, 0.1f, 0.2f);

        if (marcador.TryGetComponent(out Collider colisionador))
        {
            Object.DestroyImmediate(colisionador);
        }

        Undo.RegisterCreatedObjectUndo(marcador, "Configurar personaje caminando");
    }

    private static void EncuadrarCamara()
    {
        Camera camara = Camera.main;

        if (camara == null)
        {
            return;
        }

        var objetivo = new Vector3(0f, 0.9f, DistanciaObjetivo * 0.5f);
        camara.transform.position = new Vector3(-3.4f, 2f, -3.4f);
        camara.transform.rotation = Quaternion.LookRotation(objetivo - camara.transform.position, Vector3.up);
        camara.fieldOfView = 55f;
    }
}