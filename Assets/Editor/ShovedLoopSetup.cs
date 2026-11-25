using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Crea un AnimatorController con una única animación en loop para el modelo "Shoved"
/// y lo asigna al objeto que ya está colocado en la escena abierta.
///
/// Se ejecuta automáticamente al compilar el script y, como respaldo, justo antes
/// de entrar en Play Mode. También puede lanzarse a mano desde:
/// Tools > Animacion > Configurar Shoved en Loop
/// </summary>
[InitializeOnLoad]
public static class ShovedLoopSetup
{
    private const string FbxPath        = "Assets/Scenes/Shoved.fbx";
    private const string FolderPath     = "Assets/Animators";
    private const string ClipPath       = FolderPath + "/Shoved_Loop.anim";
    private const string ControllerPath = FolderPath + "/Shoved_Loop.controller";
    private const string AvatarPath     = FolderPath + "/Shoved_Avatar.asset";
    private const string StateName      = "Shoved Loop";

    static ShovedLoopSetup()
    {
        EditorApplication.delayCall += AutoSetup;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            EnsureSetup(false);
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        // Garantiza que el Animator esté listo incluso si el usuario pulsa Play
        // nada más volver al editor.
        if (state == PlayModeStateChange.ExitingEditMode)
            EnsureSetup(false);
    }

    private static int _autoRetries;

    private static void AutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // No tocar AssetDatabase mientras Unity importa o compila: es la causa
        // típica de que el editor se quede atascado reimportando en bucle.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            if (++_autoRetries > 100)
            {
                _autoRetries = 0;
                return;
            }

            EditorApplication.delayCall += AutoSetup;
            return;
        }

        _autoRetries = 0;
        EnsureSetup(false);
    }

    [MenuItem("Tools/Animacion/Configurar Shoved en Loop")]
    private static void SetupFromMenu()
    {
        EnsureSetup(true);
    }

    private static void EnsureSetup(bool log)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject shoved = FindShoved(scene);
        if (shoved == null)
        {
            if (log)
                Debug.Log("[Shoved] No se encontro ningun objeto llamado 'Shoved' en la escena abierta.");
            return;
        }

        EnsureFolder();

        AnimationClip sourceClip = LoadSourceClip();
        if (sourceClip == null)
        {
            Debug.LogWarning("[Shoved] No se encontro ninguna AnimationClip dentro de " + FbxPath +
                             ". Revisa la pestana Animation del modelo y vuelve a ejecutar " +
                             "Tools > Animacion > Configurar Shoved en Loop.");
            return;
        }

        AnimationClip loopClip        = LoadOrCreateLoopClip(sourceClip);
        AnimatorController controller = LoadOrCreateController(loopClip);
        if (controller == null)
            return;

        Avatar avatar = ResolveAvatar(shoved);

        Animator animator = shoved.GetComponent<Animator>();
        bool changed = false;

        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(shoved);
            changed  = true;
        }

        if (avatar != null && animator.avatar != avatar)
        {
            animator.avatar = avatar;
            changed         = true;
        }

        if (animator.runtimeAnimatorController != controller)
        {
            animator.runtimeAnimatorController = controller;
            changed                            = true;
        }

        if (animator.applyRootMotion)
        {
            animator.applyRootMotion = false;
            changed                  = true;
        }

        if (animator.updateMode != AnimatorUpdateMode.Normal)
        {
            animator.updateMode = AnimatorUpdateMode.Normal;
            changed             = true;
        }

        if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            changed              = true;
        }

        if (!animator.enabled)
        {
            animator.enabled = true;
            changed          = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(shoved);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        if (log)
        {
            Selection.activeObject = controller;
            EditorGUIUtility.PingObject(controller);
            Debug.Log("[Shoved] Listo. Controller '" + controller.name + "' con el clip '" + loopClip.name +
                      "' en loop asignado a '" + shoved.name + "'. Pulsa Play para verlo.", controller);
        }
    }

    private static GameObject FindShoved(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, "Shoved", StringComparison.OrdinalIgnoreCase))
                return root;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(t.name, "Shoved", StringComparison.OrdinalIgnoreCase))
                    return t.gameObject;
            }
        }

        return null;
    }

    private static AnimationClip LoadSourceClip()
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(c => c != null && c.length > 0f)
            .ToArray();

        if (clips.Length == 0)
            return null;

        // El importador puede exponer un PreviewAnimationClip (subtipo usado por el
        // editor). Se prefiere siempre un AnimationClip normal y, si no lo hay,
        // se copian las curvas a un clip nuevo.
        AnimationClip standard = clips
            .Where(c => c.GetType() == typeof(AnimationClip))
            .OrderByDescending(c => c.length)
            .FirstOrDefault();

        return standard != null
            ? standard
            : clips.OrderByDescending(c => c.length).First();
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder("Assets", "Animators");
    }

    private static AnimationClip LoadOrCreateLoopClip(AnimationClip sourceClip)
    {
        AnimationClip loopClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);

        // Un PreviewAnimationClip es un subtipo que solo debería existir en el editor.
        // Se convierte en un AnimationClip normal para que el controller sea portable
        // y para que el asset no dependa de un tipo de editor.
        if (loopClip == null || !IsStandardClipAsset(ClipPath))
        {
            loopClip = CreateStandardClip(sourceClip);
            Debug.Log("[Shoved] El clip se genero como AnimationClip estandar.", loopClip);
        }

        // El loop del Animator se controla con los ajustes del clip.
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(loopClip);
        if (!settings.loopTime || settings.loopBlend || loopClip.wrapMode != WrapMode.Loop)
        {
            settings.loopTime  = true;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(loopClip, settings);
            loopClip.wrapMode = WrapMode.Loop;

            EditorUtility.SetDirty(loopClip);
            AssetDatabase.SaveAssets();
        }

        return loopClip;
    }

    /// <summary>
    /// Comprueba si el asset guardado en el proyecto es un AnimationClip normal
    /// (!u!74) y no un PreviewAnimationClip (!u!1108), que solo sirve en el editor
    /// y no se reproduce en Play Mode.
    /// </summary>
    private static bool IsStandardClipAsset(string path)
    {
        string metaPath = path + ".meta";
        if (!System.IO.File.Exists(metaPath))
            return false;

        foreach (string line in System.IO.File.ReadAllLines(metaPath))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("mainObjectFileID:", StringComparison.Ordinal))
                continue;

            string value = trimmed.Substring("mainObjectFileID:".Length).Trim();
            return value == "7400000"; // AnimationClip
        }

        return false;
    }

    /// <summary>
    /// Crea en ClipPath un AnimationClip normal copiando curvas y eventos del clip
    /// de origen. Así no se guarda un PreviewAnimationClip como asset del proyecto.
    /// </summary>
    private static AnimationClip CreateStandardClip(AnimationClip source)
    {
        AnimationClip clip = new AnimationClip
        {
            name     = "Shoved_Loop",
            frameRate = source.frameRate,
            legacy   = false
        };

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
            if (curve != null)
                AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
        {
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
            if (keys != null)
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        }

        AnimationUtility.SetAnimationEvents(clip, AnimationUtility.GetAnimationEvents(source));

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.loopTime  = true;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // Se elimina el asset anterior para que el GUID nuevo sea el del clip estandar.
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath) != null)
            AssetDatabase.DeleteAsset(ClipPath);

        AssetDatabase.CreateAsset(clip, ClipPath);
        AssetDatabase.SaveAssets();

        return AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
    }

    private static AnimatorController LoadOrCreateController(AnimationClip loopClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        AnimatorState state = stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(s => s != null && s.name == StateName);

        bool changed = false;

        if (state == null)
        {
            state   = stateMachine.AddState(StateName, new Vector3(300f, 40f, 0f));
            changed = true;
        }

        if (state.motion != loopClip)
        {
            state.motion = loopClip;
            changed      = true;
        }

        if (!Mathf.Approximately(state.speed, 1f))
        {
            state.speed = 1f;
            changed     = true;
        }

        if (!state.writeDefaultValues)
        {
            state.writeDefaultValues = true;
            changed                  = true;
        }

        if (stateMachine.defaultState != state)
        {
            stateMachine.defaultState = state;
            changed                   = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        return controller;
    }

    /// <summary>
    /// Devuelve el Avatar del FBX si existe. Si el modelo se importó sin Avatar,
    /// genera uno genérico a partir del propio esqueleto para que el Animator
    /// pueda aplicar el clip sin tocar la configuración de importación.
    /// </summary>
    private static Avatar ResolveAvatar(GameObject shoved)
    {
        Avatar fbxAvatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<Avatar>()
            .FirstOrDefault(a => a != null && a.isValid);

        if (fbxAvatar != null)
            return fbxAvatar;

        Avatar savedAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
        if (savedAvatar != null && savedAvatar.isValid)
            return savedAvatar;

        AssetDatabase.DeleteAsset(AvatarPath);

        Avatar generated = AvatarBuilder.BuildGenericAvatar(shoved, string.Empty);
        if (generated == null || !generated.isValid)
        {
            Debug.LogWarning("[Shoved] El FBX no trae Avatar y no se pudo generar uno generico. " +
                             "El Animator se asignara igualmente.", shoved);
            return null;
        }

        generated.name = "Shoved_Avatar";
        AssetDatabase.CreateAsset(generated, AvatarPath);
        AssetDatabase.SaveAssets();

        return AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
    }
}
