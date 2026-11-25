using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class AplicarEnfermeraWASD : EditorWindow
{
    [MenuItem("Enfermeria/14. Aplicar WASD a enfermera seleccionada", false, 14)]
    public static void Abrir() => GetWindow<AplicarEnfermeraWASD>("Enfermera WASD");

    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Aplica movimiento WASD a la enfermera", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Aplicar AL OBJETO SELECCIONADO", GUILayout.Height(40)))
        {
            Aplicar();
            Close();
        }
    }

    static void Aplicar()
    {
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Nada seleccionado", "Selecciona la enfermera en el Hierarchy.", "Aceptar");
            return;
        }

        // Limpiar scripts viejos
        MoverHumanoideWASD old1 = go.GetComponent<MoverHumanoideWASD>();
        if (old1) DestroyImmediate(old1);
        MoverDirecto old2 = go.GetComponent<MoverDirecto>();
        if (old2) DestroyImmediate(old2);
        CaminarDosMetros old3 = go.GetComponent<CaminarDosMetros>();
        if (old3) old3.enabled = false;
        ControlFlechaDerecha old4 = go.GetComponent<ControlFlechaDerecha>();
        if (old4) old4.enabled = false;

        // Collider
        if (go.GetComponent<Collider>() == null)
        {
            CapsuleCollider cap = go.AddComponent<CapsuleCollider>();
            cap.height = 1.8f; cap.radius = 0.25f; cap.center = new Vector3(0,0.9f,0);
        }

        // NavMeshAgent
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent == null) agent = go.AddComponent<NavMeshAgent>();
        agent.radius = 0.25f; agent.height = 1.8f; agent.baseOffset = 0f;
        agent.speed = 1.8f; agent.angularSpeed = 720f; agent.acceleration = 12f;
        agent.stoppingDistance = 0f; agent.autoBraking = false; agent.updateRotation = true;

        // Animator
        Animator anim = go.GetComponent<Animator>();
        if (anim) anim.applyRootMotion = false;

        // EnfermeraWASD
        EnfermeraWASD wasd = go.GetComponent<EnfermeraWASD>();
        if (wasd == null) wasd = go.AddComponent<EnfermeraWASD>();
        wasd.velocidad = 1.8f;
        wasd.velocidadGiro = 720f;
        wasd.parametroMover = "Moviendose";
        wasd.usarDireccionCamara = true;

        EditorUtility.SetDirty(go);
        Scene s = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(s);
        EditorSceneManager.SaveScene(s);

        EditorUtility.DisplayDialog("Listo", "Enfermera con WASD aplicado.\n\nPulsa Play y usa WASD o flechas para moverla.", "Aceptar");
    }
}
