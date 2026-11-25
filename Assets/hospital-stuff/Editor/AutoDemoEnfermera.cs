using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Aviso automático que aparece una vez al cargar los scripts.
///
/// El menú manual estaba disponible, pero la escena de prueba nunca llegaba a crearse.
/// Este aviso lo pone en primer plano: si aceptas, guarda la escena actual (sin tocar
/// nada de lo que ya tengas) y crea/abre <c>Assets/Scenes/EnfermeraCaminando.unity</c>
/// usando <see cref="ConfigurarEnfermeraCaminando"/>.
/// </summary>
[InitializeOnLoad]
public static class AutoDemoEnfermera
{
    private const string ClaveSesion = "AutoDemoEnfermera.v1";
    private const string RutaEscena = "Assets/Scenes/EnfermeraCaminando.unity";
    private const string RutaOtroPersonaje = "Assets/yourMesh@Sitting Laughing.fbx";
    private const string RutaController = "Assets/Caminata/Enfermera.controller";
    private const string ParametroMoviendose = "Moviendose";
    private const string TriggerCaminar = "Caminar";

    static AutoDemoEnfermera()
    {
        // SessionState sobrevive a las recargas de scripts, así que el aviso no se
        // repite en bucle mientras Unity esté abierto.
        if (SessionState.GetBool(ClaveSesion, false))
        {
            return;
        }

        EditorApplication.delayCall += IntentarMostrarAviso;
    }

    private static void IntentarMostrarAviso()
    {
        if (SessionState.GetBool(ClaveSesion, false))
        {
            return;
        }

        // Espera a que Unity termine de compilar e importar.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += IntentarMostrarAviso;
            return;
        }

        SessionState.SetBool(ClaveSesion, true);

        // Si ya existe, no molestamos.
        if (System.IO.File.Exists(RutaEscena))
        {
            return;
        }

        bool crear = EditorUtility.DisplayDialog(
            "Enfermera caminando",
            "Ya encontré por qué no se veía la enfermera:\n\n" +
            "· La escena usaba «corrida enfermera 1.fbx», que estaba en la Papelera.\n" +
            "  Ya lo he recuperado.\n" +
            "· La cámara del hospital miraba hacia otro lado, por eso el otro\n" +
            "  personaje tampoco se veía en la vista de Juego.\n\n" +
            "¿Creo una escena de prueba con la enfermera caminando y la abro?\n\n" +
            "Antes se guarda tu escena actual: no se pierde nada.",
            "Crear y abrir",
            "Ahora no");

        if (!crear)
        {
            return;
        }

        try
        {
            // Guarda la escena actual en silencio para no perder el hospital.
            if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            {
                EditorSceneManager.SaveOpenScenes();
            }
            else if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            ConfigurarEnfermeraCaminando.Crear();
            AnadirSegundoPersonaje();
        }
        catch (System.Exception excepcion)
        {
            Debug.LogException(excepcion);
            EditorUtility.DisplayDialog(
                "No se pudo crear la escena",
                excepcion.Message + "\n\nRevisa la consola (Window > General > Console).",
                "Aceptar");
        }
    }

    /// <summary>
    /// Añade el otro personaje de Mixamo junto a la enfermera, con el mismo Animator y
    /// el mismo control, para que también se le vea caminar.
    /// </summary>
    private static void AnadirSegundoPersonaje()
    {
        // Solo se añade si la escena de prueba se creó y está abierta.
        Scene escena = SceneManager.GetActiveScene();

        if (escena.path != RutaEscena)
        {
            return;
        }

        // El FBX no trae texturas y sus materiales son del pipeline antiguo: en URP se
        // verían magenta. Se sustituyen por un material URP liso.
        ArreglarMateriales(GameObject.Find("Enfermera"), "MaterialEnfermera",
            new Color(0.86f, 0.9f, 1f));

        GameObject otro = CrearSegundoPersonaje();

        if (otro != null)
        {
            ArreglarMateriales(otro, "MaterialOtro", new Color(0.92f, 0.78f, 0.68f));
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    private static GameObject CrearSegundoPersonaje()
    {
        if (!System.IO.File.Exists(RutaOtroPersonaje))
        {
            return null;
        }

        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaOtroPersonaje);

        if (modelo == null)
        {
            return null;
        }

        Avatar avatar = null;

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(RutaOtroPersonaje))
        {
            if (asset is Avatar posible && posible.isValid && posible.isHuman)
            {
                avatar = posible;
                break;
            }
        }

        if (avatar == null)
        {
            Debug.LogWarning(
                "[AutoDemoEnfermera] El otro personaje no tiene un Avatar humanoide válido; se omite.");
            return null;
        }

        var otro = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        otro.name = "OtroPersonaje";
        otro.transform.position = new Vector3(1.6f, 0f, 0.2f);
        otro.transform.rotation = Quaternion.identity;

        if (PrefabUtility.IsPartOfPrefabInstance(otro))
        {
            PrefabUtility.UnpackPrefabInstance(
                otro, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        Animator animator = otro.GetComponent<Animator>();

        if (animator == null)
        {
            animator = otro.AddComponent<Animator>();
        }

        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.runtimeAnimatorController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RutaController);

        NavMeshAgent agente = otro.GetComponent<NavMeshAgent>();

        if (agente == null)
        {
            agente = otro.AddComponent<NavMeshAgent>();
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

        CaminarDosMetros caminar = otro.GetComponent<CaminarDosMetros>();

        if (caminar == null)
        {
            caminar = otro.AddComponent<CaminarDosMetros>();
        }

        var serializadoCaminar = new SerializedObject(caminar);
        serializadoCaminar.FindProperty("distanciaMetros").floatValue = 2f;
        serializadoCaminar.FindProperty("nombreParametroCaminando").stringValue = ParametroMoviendose;
        serializadoCaminar.FindProperty("nombreTriggerCaminar").stringValue = TriggerCaminar;
        serializadoCaminar.FindProperty("caminarAlIniciar").boolValue = true;
        serializadoCaminar.FindProperty("repetirEnBucle").boolValue = false;
        serializadoCaminar.ApplyModifiedPropertiesWithoutUndo();

        ControlFlechaDerecha control = otro.GetComponent<ControlFlechaDerecha>();

        if (control == null)
        {
            control = otro.AddComponent<ControlFlechaDerecha>();
        }

        var serializadoControl = new SerializedObject(control);
        serializadoControl.FindProperty("distanciaMaxima").floatValue = 30f;
        serializadoControl.FindProperty("margenBordeNavMesh").floatValue = 0.45f;
        serializadoControl.FindProperty("usarDireccionDeCamara").boolValue = true;
        serializadoControl.FindProperty("nombreParametroCaminando").stringValue = ParametroMoviendose;
        serializadoControl.FindProperty("nombreTriggerCaminar").stringValue = TriggerCaminar;
        serializadoControl.FindProperty("tomarControlDeLaCaminataAutomatica").boolValue = true;
        serializadoControl.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[AutoDemoEnfermera] Segundo personaje añadido a la escena de prueba.");
        return otro;
    }

    /// <summary>
    /// Si los materiales vienen del pipeline antiguo (Standard), en URP se ven magenta.
    /// Se sustituyen por un material URP Lit liso del color indicado.
    /// </summary>
    private static void ArreglarMateriales(GameObject personaje, string nombre, Color color)
    {
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");

        if (personaje == null || urp == null)
        {
            return;
        }

        bool haceFalta = false;

        foreach (Renderer renderer in personaje.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                string shader = material != null ? material.shader.name : string.Empty;

                if (!shader.StartsWith("Universal Render Pipeline"))
                {
                    haceFalta = true;
                    break;
                }
            }

            if (haceFalta)
            {
                break;
            }
        }

        if (!haceFalta)
        {
            return;
        }

        string ruta = "Assets/Caminata/" + nombre + ".mat";

        AssetDatabase.DeleteAsset(ruta);

        var materialUrp = new Material(urp) { name = nombre };
        materialUrp.SetColor("_BaseColor", color);

        AssetDatabase.CreateAsset(materialUrp, ruta);
        materialUrp = AssetDatabase.LoadAssetAtPath<Material>(ruta);

        if (materialUrp == null)
        {
            return;
        }

        foreach (Renderer renderer in personaje.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materiales = renderer.sharedMaterials;

            if (materiales.Length == 0)
            {
                materiales = new Material[1];
            }

            for (int i = 0; i < materiales.Length; i++)
            {
                materiales[i] = materialUrp;
            }

            renderer.sharedMaterials = materiales;
        }

        Debug.Log($"[AutoDemoEnfermera] Materiales de «{personaje.name}» convertidos a URP.");
    }
}
