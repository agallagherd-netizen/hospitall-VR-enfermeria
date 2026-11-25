using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class ConfigurarEnfermeraConFemaleWalk
{
    [MenuItem("Enfermeria/12. Configurar enfermera con Female Walk", false, 12)]
    public static void Configurar()
    {
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;

        // 1. Asegurar que Female Walk importe animación
        var importWalk = AssetImporter.GetAtPath("Assets/Female Walk.fbx") as ModelImporter;
        if (importWalk != null)
        {
            importWalk.animationType = ModelImporterAnimationType.Human;
            importWalk.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importWalk.importAnimation = true;
            importWalk.SaveAndReimport();
        }

        // 2. Asegurar que corrida enfermera 1 importe como Humanoid
        var importEnf = AssetImporter.GetAtPath("Assets/corrida enfermera 1.fbx") as ModelImporter;
        if (importEnf != null)
        {
            importEnf.animationType = ModelImporterAnimationType.Human;
            importEnf.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importEnf.importAnimation = true;
            importEnf.SaveAndReimport();
        }

        // 3. Crear/usar el controller existente
        string controllerPath = "Assets/Caminata/Enfermera.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Moviendose", AnimatorControllerParameterType.Bool);
            var sm = controller.layers[0].stateMachine;
            var idle = sm.AddState("Idle");
            var walk = sm.AddState("Caminar");
            sm.defaultState = idle;
            var t1 = idle.AddTransition(walk); t1.hasExitTime = false; t1.AddCondition(AnimatorConditionMode.If, 0, "Moviendose");
            var t2 = walk.AddTransition(idle); t2.hasExitTime = false; t2.AddCondition(AnimatorConditionMode.IfNot, 0, "Moviendose");
        }

        // 4. Buscar o crear la enfermera en escena
        GameObject enf = GameObject.Find("Enfermera");
        if (enf == null)
        {
            // Instanciar corrida enfermera 1
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/corrida enfermera 1.fbx");
            if (prefab == null) { EditorUtility.DisplayDialog("Error", "No encuentro corrida enfermera 1.fbx", "OK"); return; }
            
            Avatar avatar = null;
            foreach (Object a in AssetDatabase.LoadAllAssetsAtPath("Assets/corrida enfermera 1.fbx"))
                if (a is Avatar av && av.isValid && av.isHuman) { avatar = av; break; }

            enf = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            enf.name = "Enfermera";
            
            if (avatar != null)
            {
                Animator anim = enf.GetComponent<Animator>();
                if (anim == null) anim = enf.AddComponent<Animator>();
                anim.avatar = avatar;
                anim.applyRootMotion = false;
                anim.runtimeAnimatorController = controller;
            }

            // NavMeshAgent + MoverDirecto
            NavMeshAgent agent = enf.GetComponent<NavMeshAgent>();
            if (agent == null) agent = enf.AddComponent<NavMeshAgent>();
            agent.speed = 1.8f; agent.angularSpeed = 720f; agent.acceleration = 12f; agent.stoppingDistance = 0f; agent.autoBraking = false;
            
            MoverDirecto md = enf.GetComponent<MoverDirecto>();
            if (md == null) md = enf.AddComponent<MoverDirecto>();
            md.paramMover = "Moviendose";
        }
        else
        {
            // Ya existe, asegurar controller y parámetro
            Animator anim = enf.GetComponent<Animator>();
            if (anim != null) anim.runtimeAnimatorController = controller;
        }

        // 5. Asignar clip Female Walk al estado Caminar
        AnimationClip walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Caminata/CaminarEnfermera.anim");
        if (walkClip == null)
        {
            // Intentar cargar del FBX Female Walk
            foreach (Object a in AssetDatabase.LoadAllAssetsAtPath("Assets/Female Walk.fbx"))
                if (a is AnimationClip ac) { walkClip = ac; break; }
        }
        if (walkClip != null)
        {
            var sm = controller.layers[0].stateMachine;
            foreach (var state in sm.states)
            {
                if (state.state.name == "Caminar")
                {
                    state.state.motion = walkClip;
                    break;
                }
            }
        }

        EditorUtility.DisplayDialog("Listo", "Enfermera configurada con Female Walk.\n\nPulsa Play -> Click sobre ella -> WASD", "Aceptar");
    }
}
