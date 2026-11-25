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
/// Crea una escena nueva con la enfermera (<c>corrida enfermera.fbx</c>) animada con el
/// ciclo real de <c>Female Walk.fbx</c>, piso, NavMesh y control con la flecha derecha.
///
/// No toca la escena que tengas abierta: primero pide guardarla, después crea y abre
/// <c>Assets/Scenes/EnfermeraCaminando.unity</c>.
///
/// Menú: Enfermeria > 3. Crear escena de la enfermera caminando
/// </summary>
public static class ConfigurarEnfermeraCaminando
{
    private const string RutaEnfermera = "Assets/corrida enfermera.fbx";
    private const string RutaAnimacion = "Assets/Female Walk.fbx";

    private const string CarpetaGenerados = "Assets/Caminata";
    private const string RutaController = CarpetaGenerados + "/Enfermera.controller";
    private const string RutaClipCaminar = CarpetaGenerados + "/CaminarEnfermera.anim";
    private const string RutaClipIdle = CarpetaGenerados + "/IdleEnfermera.anim";

    private const string RutaEscena = "Assets/Scenes/EnfermeraCaminando.unity";

    private const string NombrePersonaje = "Enfermera";
    private const string NombrePiso = "Piso";
    private const string NombreNavMesh = "NavMesh";

    private const string ParametroMoviendose = "Moviendose";
    private const string EstadoCaminar = "Caminar";
    private const string EstadoIdle = "Idle";

    private const float LadoPisoMetros = 8f;
    private const float DistanciaAutoMetros = 2f;
    private const float DuracionIdle = 2f;

    [MenuItem("Enfermeria/3. Crear escena de la enfermera caminando", false, 3)]
    public static void Crear()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }

        // Si hay cambios sin guardar, Unity pregunta. Si el usuario cancela, no se hace nada.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var informe = new StringBuilder();

        if (!System.IO.File.Exists(RutaEnfermera) || !System.IO.File.Exists(RutaAnimacion))
        {
            EditorUtility.DisplayDialog(
                "Faltan modelos",
                "No se encuentran:\n\n" +
                $"· {RutaEnfermera}\n" +
                $"· {RutaAnimacion}\n\n" +
                "Comprueba que los dos FBX están en la carpeta Assets.",
                "Aceptar");
            return;
        }

        PrepararCarpetas();

        Scene escena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        ConfigurarHumanoide(RutaEnfermera, informe);
        ConfigurarHumanoide(RutaAnimacion, informe);

        CrearPiso(informe);

        // El NavMesh se hornea ANTES de añadir a la enfermera para que su malla no se
        // cuele como superficie caminable.
        if (!GenerarNavMesh(informe))
        {
            EditorUtility.DisplayDialog(
                "No se pudo generar el NavMesh",
                "Revisa el Log de la consola para ver el motivo.",
                "Aceptar");
            return;
        }

        GameObject enfermera = Instanciar(RutaEnfermera, informe);

        if (enfermera == null)
        {
            EditorUtility.DisplayDialog("No se pudo instanciar la enfermera", "Revisa la consola.", "Aceptar");
            return;
        }

        Avatar avatar = CargarAvatarHumanoide(RutaEnfermera);

        if (avatar == null)
        {
            EditorUtility.DisplayDialog(
                "Avatar no válido",
                "Unity no pudo crear un Avatar humanoide para la enfermera.\n\n" +
                "Abre el FBX, pestaña Rig, y elige Animation Type = Humanoid / Create From This Model.",
                "Aceptar");
            return;
        }

        Animator animator = enfermera.GetComponent<Animator>();

        if (animator == null)
        {
            animator = enfermera.AddComponent<Animator>();
        }

        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        float velocidadNatural;
        AnimationClip clipCaminar = CrearClipCaminar(enfermera, out velocidadNatural, informe);
        AnimationClip clipIdle = CrearClipIdle(enfermera, informe);

        if (clipCaminar == null || clipIdle == null)
        {
            EditorUtility.DisplayDialog("No se pudieron crear los clips", "Revisa la consola.", "Aceptar");
            return;
        }

        AnimatorController controller = CrearController(clipIdle, clipCaminar, informe);
        animator.runtimeAnimatorController = controller;

        ConfigurarAgente(enfermera, velocidadNatural, informe);
        ConfigurarCaminataAutomatica(enfermera);
        ConfigurarControlFlecha(enfermera);
        VerificarClip(animator, enfermera, informe);

        EncuadrarCamara();

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena, RutaEscena);
        AssetDatabase.SaveAssets();

        informe.AppendLine();
        informe.AppendLine(
            $"Escena creada en «{RutaEscena}» y ya abierta. " +
            $"Pulsa Play: la enfermera camina {DistanciaAutoMetros:0} m con la animación real de «Female Walk» " +
            "y después responde a la FLECHA DERECHA.");

        Debug.Log("[ConfigurarEnfermeraCaminando]\n" + informe, null);
    }

    // =====================================================================
    //  Importación
    // =====================================================================

    private static bool ConfigurarHumanoide(string ruta, StringBuilder informe)
    {
        var importador = AssetImporter.GetAtPath(ruta) as ModelImporter;

        if (importador == null)
        {
            informe.AppendLine($"- ERROR: no se encuentra el importador de «{ruta}».");
            return false;
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

        if (importador.importCameras)
        {
            importador.importCameras = false;
            cambios = true;
        }

        if (importador.importLights)
        {
            importador.importLights = false;
            cambios = true;
        }

        if (cambios)
        {
            importador.SaveAndReimport();
            informe.AppendLine($"- «{System.IO.Path.GetFileName(ruta)}» reimportado como Humanoid.");
        }
        else
        {
            informe.AppendLine($"- «{System.IO.Path.GetFileName(ruta)}» ya era Humanoid.");
        }

        return true;
    }

    private static Avatar CargarAvatarHumanoide(string ruta)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ruta))
        {
            if (asset is Avatar avatar && avatar.isValid && avatar.isHuman)
            {
                return avatar;
            }
        }

        return null;
    }

    // =====================================================================
    //  Escena
    // =====================================================================

    private static void PrepararCarpetas()
    {
        if (!AssetDatabase.IsValidFolder(CarpetaGenerados))
        {
            AssetDatabase.CreateFolder("Assets", "Caminata");
        }
    }

    private static GameObject Instanciar(string ruta, StringBuilder informe)
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);

        if (modelo == null)
        {
            informe.AppendLine($"- ERROR: no se pudo cargar «{ruta}».");
            return null;
        }

        var personaje = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        personaje.name = NombrePersonaje;
        personaje.transform.position = Vector3.zero;
        personaje.transform.rotation = Quaternion.identity;

        if (PrefabUtility.IsPartOfPrefabInstance(personaje))
        {
            PrefabUtility.UnpackPrefabInstance(
                personaje, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        informe.AppendLine("- Enfermera instanciada en el origen, mirando hacia +Z.");
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

        informe.AppendLine($"- Piso creado ({LadoPisoMetros:0.#} x {LadoPisoMetros:0.#} m) en y = 0.");
    }

    private static bool GenerarNavMesh(StringBuilder informe)
    {
        var contenedor = new GameObject(NombreNavMesh);
        var superficie = contenedor.AddComponent<NavMeshSurface>();
        superficie.collectObjects = CollectObjects.All;
        superficie.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        superficie.layerMask = ~0;
        superficie.ignoreNavMeshAgent = true;

        superficie.BuildNavMesh();

        int vertices = NavMesh.CalculateTriangulation().vertices.Length;

        if (vertices == 0)
        {
            informe.AppendLine("- ERROR: el NavMesh quedó vacío.");
            return false;
        }

        informe.AppendLine($"- NavMesh generado ({vertices} vértices).");
        return true;
    }

    private static void EncuadrarCamara()
    {
        Camera camara = Camera.main;

        if (camara == null)
        {
            return;
        }

        var objetivo = new Vector3(0f, 1f, 1.1f);
        camara.transform.position = new Vector3(-3.2f, 2.2f, -4.2f);
        camara.transform.rotation = Quaternion.LookRotation(objetivo - camara.transform.position, Vector3.up);
        camara.fieldOfView = 50f;
    }

    // =====================================================================
    //  Clips
    // =====================================================================

    private static AnimationClip CargarClipMasLargo(string ruta)
    {
        AnimationClip mejor = null;

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ruta))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                if (mejor == null || clip.length > mejor.length)
                {
                    mejor = clip;
                }
            }
        }

        return mejor;
    }

    /// <summary>
    /// Velocidad a la que la animación avanza de forma natural (metros por segundo).
    /// Si el clip está "en el sitio" devuelve 0.
    /// </summary>
    private static float CalcularVelocidadNatural(AnimationClip clip, StringBuilder informe)
    {
        AnimationCurve curvaX = null;
        AnimationCurve curvaZ = null;

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.propertyName == "RootT.x")
            {
                curvaX = AnimationUtility.GetEditorCurve(clip, binding);
            }
            else if (binding.propertyName == "RootT.z")
            {
                curvaZ = AnimationUtility.GetEditorCurve(clip, binding);
            }
        }

        if (curvaX == null || curvaZ == null || clip.length < 0.01f)
        {
            informe.AppendLine("- El clip no trae desplazamiento de raíz (se camina en el sitio).");
            return 0f;
        }

        float dx = curvaX.Evaluate(clip.length) - curvaX.Evaluate(0f);
        float dz = curvaZ.Evaluate(clip.length) - curvaZ.Evaluate(0f);
        float velocidad = Mathf.Sqrt(dx * dx + dz * dz) / clip.length;

        informe.AppendLine($"- Desplazamiento de raíz del clip: {velocidad:0.00} m/s.");
        return velocidad;
    }

    /// <summary>
    /// Copia el clip real de «Female Walk» y le quita el avance de la raíz para que el
    /// NavMeshAgent sea el único que mueve al personaje (si no, se sumarían los dos y
    /// la enfermera se deslizaría o se saldría del piso).
    /// </summary>
    private static AnimationClip CrearClipCaminar(
        GameObject enfermera, out float velocidadNatural, StringBuilder informe)
    {
        AnimationClip original = CargarClipMasLargo(RutaAnimacion);

        if (original == null)
        {
            informe.AppendLine("- ERROR: «Female Walk.fbx» no contiene ningún clip.");
            velocidadNatural = 0f;
            return null;
        }

        velocidadNatural = CalcularVelocidadNatural(original, informe);

        AnimationClip copia = Object.Instantiate(original);
        copia.name = "CaminarEnfermera";
        copia.wrapMode = WrapMode.Loop;

        int curvasRaiz = 0;

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(copia))
        {
            if (binding.propertyName != "RootT.x" && binding.propertyName != "RootT.z")
            {
                continue;
            }

            AnimationCurve curva = AnimationUtility.GetEditorCurve(copia, binding);

            if (curva == null || curva.length == 0)
            {
                continue;
            }

            // Se deja el valor del primer fotograma para que no haya un salto al empezar.
            float valor = curva.Evaluate(0f);
            AnimationCurve plana = AnimationCurve.Constant(
                curva.keys[0].time, curva.keys[curva.length - 1].time, valor);

            AnimationUtility.SetEditorCurve(copia, binding, plana);
            curvasRaiz++;
        }

        AssetDatabase.DeleteAsset(RutaClipCaminar);
        AssetDatabase.CreateAsset(copia, RutaClipCaminar);

        AnimationClip guardado = AssetDatabase.LoadAssetAtPath<AnimationClip>(RutaClipCaminar);

        informe.AppendLine(
            $"- Clip «{guardado.name}» generado a partir de «{original.name}» " +
            $"({guardado.length:0.00} s, {curvasRaiz} curva(s) de raíz neutralizada(s)).");

        return guardado;
    }

    private static AnimationClip CrearClipIdle(GameObject enfermera, StringBuilder informe)
    {
        Dictionary<string, string> rutas = GeneradorCaminata.ResolverRutas(enfermera.transform);

        AnimationClip idle = GeneradorCaminata.CrearClipIdle(enfermera.transform, rutas, DuracionIdle);

        AssetDatabase.DeleteAsset(RutaClipIdle);
        AssetDatabase.CreateAsset(idle, RutaClipIdle);

        AnimationClip guardado = AssetDatabase.LoadAssetAtPath<AnimationClip>(RutaClipIdle);
        informe.AppendLine($"- Clip «{guardado.name}» de reposo ({guardado.length:0.00} s).");

        return guardado;
    }

    private static AnimatorController CrearController(
        AnimationClip clipIdle, AnimationClip clipCaminar, StringBuilder informe)
    {
        AssetDatabase.DeleteAsset(RutaController);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(RutaController);
        controller.AddParameter(ParametroMoviendose, AnimatorControllerParameterType.Bool);

        AnimatorStateMachine maquina = controller.layers[0].stateMachine;

        AnimatorState estadoIdle = maquina.AddState(EstadoIdle, new Vector3(260f, 0f, 0f));
        estadoIdle.motion = clipIdle;
        maquina.defaultState = estadoIdle;

        AnimatorState estadoCaminar = maquina.AddState(EstadoCaminar, new Vector3(260f, 90f, 0f));
        estadoCaminar.motion = clipCaminar;

        AnimatorStateTransition haciaCaminar = estadoIdle.AddTransition(estadoCaminar);
        haciaCaminar.hasExitTime = false;
        haciaCaminar.hasFixedDuration = true;
        haciaCaminar.duration = 0.2f;
        haciaCaminar.AddCondition(AnimatorConditionMode.If, 0f, ParametroMoviendose);

        AnimatorStateTransition haciaIdle = estadoCaminar.AddTransition(estadoIdle);
        haciaIdle.hasExitTime = false;
        haciaIdle.hasFixedDuration = true;
        haciaIdle.duration = 0.2f;
        haciaIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, ParametroMoviendose);

        EditorUtility.SetDirty(controller);

        informe.AppendLine(
            $"- AnimatorController «{controller.name}» con «{EstadoIdle}» y «{EstadoCaminar}».");

        return controller;
    }

    // =====================================================================
    //  Componentes
    // =====================================================================

    private static void ConfigurarAgente(GameObject enfermera, float velocidadNatural, StringBuilder informe)
    {
        NavMeshAgent agente = enfermera.GetComponent<NavMeshAgent>();

        if (agente == null)
        {
            agente = enfermera.AddComponent<NavMeshAgent>();
        }

        // Si el clip trae avance de raíz, esa es la velocidad a la que los pies no patinan.
        float velocidad = velocidadNatural > 0.2f ? velocidadNatural : 1.4f;
        velocidad = Mathf.Clamp(velocidad, 0.8f, 2.2f);

        agente.radius = 0.25f;
        agente.height = 1.8f;
        agente.baseOffset = 0f;
        agente.speed = velocidad;
        agente.acceleration = 12f;
        agente.angularSpeed = 720f;
        agente.stoppingDistance = 0.05f;
        agente.autoBraking = true;
        agente.autoRepath = true;
        agente.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

        informe.AppendLine($"- NavMeshAgent configurado a {velocidad:0.00} m/s.");
    }

    private static void ConfigurarCaminataAutomatica(GameObject enfermera)
    {
        CaminarDosMetros script = enfermera.GetComponent<CaminarDosMetros>();

        if (script == null)
        {
            script = enfermera.AddComponent<CaminarDosMetros>();
        }

        var serializado = new SerializedObject(script);
        serializado.FindProperty("distanciaMetros").floatValue = DistanciaAutoMetros;
        serializado.FindProperty("nombreParametroCaminando").stringValue = ParametroMoviendose;
        serializado.FindProperty("nombreTriggerCaminar").stringValue = EstadoCaminar;
        serializado.FindProperty("caminarAlIniciar").boolValue = true;
        serializado.FindProperty("repetirEnBucle").boolValue = false;
        serializado.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurarControlFlecha(GameObject enfermera)
    {
        ControlFlechaDerecha control = enfermera.GetComponent<ControlFlechaDerecha>();

        if (control == null)
        {
            control = enfermera.AddComponent<ControlFlechaDerecha>();
        }

        var serializado = new SerializedObject(control);
        serializado.FindProperty("distanciaMaxima").floatValue = 30f;
        serializado.FindProperty("margenBordeNavMesh").floatValue = 0.45f;
        serializado.FindProperty("usarDireccionDeCamara").boolValue = true;
        serializado.FindProperty("nombreParametroCaminando").stringValue = ParametroMoviendose;
        serializado.FindProperty("nombreTriggerCaminar").stringValue = EstadoCaminar;
        serializado.FindProperty("tomarControlDeLaCaminataAutomatica").boolValue = true;
        serializado.ApplyModifiedPropertiesWithoutUndo();
    }

    // =====================================================================
    //  Verificación
    // =====================================================================

    private static void VerificarClip(Animator animator, GameObject enfermera, StringBuilder informe)
    {
        Dictionary<string, string> rutas = GeneradorCaminata.ResolverRutas(enfermera.transform);
        Transform hueso = BuscarPorRuta(enfermera.transform, GeneradorCaminata.Ruta(rutas, "LeftUpLeg"));

        if (hueso == null)
        {
            informe.AppendLine("- No se pudo verificar el clip (falta LeftUpLeg).");
            return;
        }

        int hashMovimiento = Animator.StringToHash(ParametroMoviendose);

        animator.Rebind();
        animator.Update(0f);

        Quaternion antes = hueso.localRotation;
        float movimiento = 0f;

        animator.SetBool(hashMovimiento, true);
        animator.Update(0f);

        foreach (float momento in new[] { 0.15f, 0.35f, 0.6f })
        {
            animator.Play(EstadoCaminar, 0, momento);
            animator.Update(0f);
            movimiento = Mathf.Max(movimiento, Quaternion.Angle(antes, hueso.localRotation));
        }

        animator.SetBool(hashMovimiento, false);
        animator.Play(EstadoIdle, 0, 0f);
        animator.Update(0f);

        bool funciona = movimiento > 0.5f;

        informe.AppendLine(
            $"- Verificación: «LeftUpLeg» se movió {movimiento:0.0}° con el clip real " +
            (funciona ? "-> OK." : "-> el clip NO se aplica."));

        if (!funciona)
        {
            Debug.LogError(
                "[ConfigurarEnfermeraCaminando] El Animator no aplicó el clip de caminar.", enfermera);
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
}
