using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class AnadirHumanoidesDirecto : EditorWindow
{
    [MenuItem("Enfermeria/6. Añadir los 2 humanoides AHORA", false, 6)]
    public static void ShowWindow()
    {
        GetWindow<AnadirHumanoidesDirecto>("Añadir humanoides");
    }

    void OnGUI()
    {
        GUILayout.Label("Añade Enfermera + OtroPersonaje", EditorStyles.boldLabel);
        if (GUILayout.Button("Añadirlos a escena actual", GUILayout.Height(40)))
        {
            Anadir();
            Close();
        }
    }

    static void Anadir()
    {
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        Scene escena = SceneManager.GetActiveScene();
        Crear("Assets/corrida enfermera.fbx", "Enfermera", new Vector3(0,0,0), Color.white);
        Crear("Assets/yourMesh@Sitting Laughing.fbx", "OtroPersonaje", new Vector3(2,0,1), Color.yellow);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        EditorUtility.DisplayDialog("Listo", "Añadidos. Pulsa Play y usa WASD/flechas.", "Aceptar");
    }

    static void Crear(string ruta, string nombre, Vector3 offset, Color col)
    {
        if (!System.IO.File.Exists(ruta)) { Debug.LogWarning("Falta: " + ruta); return; }
        if (GameObject.Find(nombre)) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (prefab == null) return;

        Avatar avatar = null;
        foreach (Object a in AssetDatabase.LoadAllAssetsAtPath(ruta))
            if (a is Avatar av && av.isValid && av.isHuman) { avatar = av; break; }
        if (avatar == null) return;

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = nombre;

        Vector3 pos = offset;
        if (Camera.main) pos = Camera.main.transform.position + Camera.main.transform.forward * 3f + offset;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(pos, out hit, 30f, -1)) go.transform.position = hit.position;
        else go.transform.position = pos;
        go.transform.rotation = Quaternion.identity;

        if (PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        Animator anim = go.GetComponent<Animator>();
        if (!anim) anim = go.AddComponent<Animator>();
        anim.avatar = avatar;
        anim.applyRootMotion = false;
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Caminata/Enfermera.controller");

        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (!agent) agent = go.AddComponent<NavMeshAgent>();
        agent.speed = 1.8f; agent.angularSpeed = 720f; agent.acceleration = 12f; agent.stoppingDistance = 0f; agent.autoBraking = false;
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(go.transform.position, out hit, 20f, -1)) agent.Warp(hit.position);

        MoverHumanoideWASD m = go.GetComponent<MoverHumanoideWASD>();
        if (!m) m = go.AddComponent<MoverHumanoideWASD>();
        m.velocidad = 1.8f; m.velocidadGiro = 720f; m.aceleracion = 12f; m.nombreParametroMover = "Moviendose"; m.nombreTriggerMover = "Caminar"; m.usarDireccionCamara = true; m.mostrarLogs = false;

        CaminarDosMetros c = go.GetComponent<CaminarDosMetros>(); if (c) c.enabled = false;
        ControlFlechaDerecha cf = go.GetComponent<ControlFlechaDerecha>(); if (cf) cf.enabled = false;
    }
}
