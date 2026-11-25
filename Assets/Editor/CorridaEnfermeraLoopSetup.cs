using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Crea el AnimatorController con la animacion del modelo "corrida enfermera 1"
/// (Assets/corrida enfermera 1.fbx) en bucle y lo asigna al objeto que esta
/// colocado en la escena abierta. El bucle empieza en cuanto se pulsa Play.
///
/// Menu manual: Tools > Animacion > Configurar "corrida enfermera" en Loop
/// (si el modelo esta dentro de otro prefab, busca por nombre en toda la escena).
/// </summary>
[InitializeOnLoad]
public static class CorridaEnfermeraLoopSetup
{
    private const string FbxPath        = "Assets/corrida enfermera 1.fbx";
    private const string FolderPath     = "Assets/Animators";
    private const string ClipPath       = FolderPath + "/CorridaEnfermera_Loop.anim";
    private const string ControllerPath = FolderPath + "/CorridaEnfermera_Loop.controller";
    private const string AvatarPath     = FolderPath + "/CorridaEnfermera_Avatar.asset";
    private const string StateName      = "Correr Loop";

    private const string TempDir  = @"C:\Users\User\AppData\Local\Temp\opencode\";
    private const string Report   = TempDir + "corrida-informe.txt";
    private const string Flag     = TempDir + "corrida-setup.flag";
    private const string PlaceFlag = TempDir + "corrida-place.flag";
    private const string WatchFlag = TempDir + "corrida-watch.flag";

    static CorridaEnfermeraLoopSetup()
    {
        EditorApplication.delayCall += AutoSetup;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += ReintentarPendiente;
    }

    /// <summary>
    /// Canal de control por archivos (sin depender del teclado): cada cierto tiempo
    /// escribe un latido con el estado del editor y ejecuta el comando que haya en
    /// corrida-cmd.txt (play, stop, setup, place, watch).
    /// </summary>
    private static double _siguienteLatido;

    private static void ReintentarPendiente()
    {
        ProcesarComando();

        if (EditorApplication.timeSinceStartup >= _siguienteLatido)
        {
            _siguienteLatido = EditorApplication.timeSinceStartup + 1.0;

            try
            {
                File.WriteAllText(TempDir + "corrida-latido.txt",
                    "hora=" + DateTime.Now.ToString("HH:mm:ss")
                    + "  isPlaying=" + EditorApplication.isPlaying
                    + "  isPaused=" + EditorApplication.isPaused
                    + "  isCompiling=" + EditorApplication.isCompiling
                    + "  isUpdating=" + EditorApplication.isUpdating
                    + "  escena='" + SceneManager.GetActiveScene().name + "'"
                    + "  frame=" + Time.frameCount);
            }
            catch
            {
                // sin importancia
            }
        }

        if (!File.Exists(Flag) && !File.Exists(PlaceFlag))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        AutoSetup();
    }

    private static void ProcesarComando()
    {
        string path = TempDir + "corrida-cmd.txt";
        if (!File.Exists(path))
            return;

        string cmd;
        try
        {
            cmd = File.ReadAllText(path).Trim().ToLowerInvariant();
        }
        catch
        {
            return;
        }

        File.Delete(path);
        File.AppendAllText(TempDir + "corrida-cmd.log",
            DateTime.Now.ToString("HH:mm:ss") + " -> '" + cmd + "'\n");

        switch (cmd)
        {
            case "play":
                if (!EditorApplication.isPlaying)
                    EditorApplication.isPlaying = true;
                break;

            case "stop":
                if (EditorApplication.isPlaying)
                    EditorApplication.isPlaying = false;
                break;

            case "setup":
                EnsureSetup(true, false);
                break;

            case "place":
                EnsureSetup(true, true);
                break;

            case "watch":
                File.WriteAllText(WatchFlag, "1");
                PlayWatcher.Start();
                break;
        }
    }

    private static void AutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        bool setup = File.Exists(Flag);
        bool place = File.Exists(PlaceFlag);

        if (!setup && !place)
            return;

        if (setup) File.Delete(Flag);
        if (place) File.Delete(PlaceFlag);

        EnsureSetup(true, place);
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            EnsureSetup(false);
        else if (state == PlayModeStateChange.EnteredPlayMode)
            PlayWatcher.Start();
    }

    [MenuItem("Tools/Animacion/Configurar \"corrida enfermera\" en Loop")]
    private static void SetupFromMenu()
    {
        EnsureSetup(true);
    }

    private static void EnsureSetup(bool log, bool place = false)
    {
        StringBuilder sb = new StringBuilder();

        try
        {
            EnsureSetupInner(log, place, sb);
        }
        catch (Exception e)
        {
            sb.AppendLine();
            sb.AppendLine("EXCEPCION: " + e);
            File.WriteAllText(Report, sb.ToString());
            Debug.LogError("[Corrida] " + e);
        }
    }

    private static void EnsureSetupInner(bool log, bool place, StringBuilder sb)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject target = FindTarget(scene);

        sb.AppendLine("=== corrida enfermera " + DateTime.Now.ToString("HH:mm:ss") + " ===");
        sb.AppendLine("Escena: '" + scene.name + "'  (" + scene.path + ")");

        if (target == null)
        {
            sb.AppendLine("No se encontro ningun objeto con 'corrida' y 'enfermera' en el nombre.");
            sb.AppendLine("Objetos de la escena que contienen 'enfermera' o 'corrida':");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("enfermera") || n.Contains("corrida"))
                        sb.AppendLine("  - '" + t.name + "'  activo=" + t.gameObject.activeInHierarchy);
                }
            }

            File.WriteAllText(Report, sb.ToString());
            if (log) Debug.LogWarning("[Corrida] No se encontro el modelo.\n" + sb);
            return;
        }

        EnsureFolder();

        AnimationClip[] available = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().ToArray();
        sb.AppendLine("Clips dentro del FBX: " + available.Length);
        foreach (AnimationClip c in available)
        {
            sb.AppendLine("  - '" + c.name + "' tipo=" + c.GetType().Name
                          + " largo=" + c.length.ToString("0.###")
                          + " fps=" + c.frameRate.ToString("0.#")
                          + " curvas=" + AnimationUtility.GetCurveBindings(c).Length);
        }

        AnimationClip sourceClip = available
            .Where(c => c != null && c.length > 0f)
            .OrderByDescending(c => c.GetType() == typeof(AnimationClip) ? 1 : 0)
            .ThenByDescending(c => c.length)
            .FirstOrDefault();

        if (sourceClip == null)
        {
            sb.AppendLine("ERROR: el FBX no contiene ninguna AnimationClip.");
            File.WriteAllText(Report, sb.ToString());
            return;
        }

        sb.AppendLine("Clip elegido: '" + sourceClip.name + "' largo=" + sourceClip.length.ToString("0.###"));

        AnimationClip loopClip = LoadOrCreateLoopClip(sourceClip, sb);
        AnimatorController controller = LoadOrCreateController(loopClip, sb);

        Avatar avatar = ResolveAvatar(target, sb);
        sb.AppendLine("Avatar: " + (avatar == null ? "NINGUNO" : avatar.name + " valido=" + avatar.isValid));

        Animator animator = target.GetComponent<Animator>();
        bool changed = false;

        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(target);
            changed = true;
        }

        if (avatar != null && animator.avatar != avatar)
        {
            animator.avatar = avatar;
            changed = true;
        }

        if (animator.runtimeAnimatorController != controller)
        {
            animator.runtimeAnimatorController = controller;
            changed = true;
        }

        if (animator.applyRootMotion) { animator.applyRootMotion = false; changed = true; }
        if (animator.updateMode != AnimatorUpdateMode.Normal) { animator.updateMode = AnimatorUpdateMode.Normal; changed = true; }
        if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate) { animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; changed = true; }
        if (!animator.enabled) { animator.enabled = true; changed = true; }

        if (place)
        {
            if (PlaceInFrontOfCamera(target, sb))
                changed = true;
        }

        // --- Diagnostico: posicion, tamano y visibilidad ---
        sb.AppendLine();
        sb.AppendLine("Objeto: '" + target.name + "'");
        sb.AppendLine("  activoEnJerarquia=" + target.activeInHierarchy);
        sb.AppendLine("  posicion mundo=" + target.transform.position.ToString("0.###"));
        sb.AppendLine("  escala mundo=" + target.transform.lossyScale.ToString("0.####"));
        sb.AppendLine("  rotacion=" + target.transform.eulerAngles.ToString("0.#"));

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true).Where(r => r != null).ToArray();
        sb.AppendLine("  renderers=" + renderers.Length);

        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
                b.Encapsulate(r.bounds);

            sb.AppendLine("  bounds mundo centro=" + b.center.ToString("0.###") + " tamano=" + b.size.ToString("0.###"));

            Camera cam = FindCamera();

            if (cam != null)
            {
                Vector3 v = cam.WorldToViewportPoint(b.center);
                bool inside = v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f && v.z > 0f;
                sb.AppendLine("  camara '" + cam.name + "' pos=" + cam.transform.position.ToString("0.##"));
                sb.AppendLine("  viewport=(" + v.x.ToString("0.###") + ", " + v.y.ToString("0.###") + ", z=" + v.z.ToString("0.###") + ")"
                              + "  -> " + (inside ? "DENTRO de la vista" : "FUERA de la vista"));
            }
        }

        sb.AppendLine();
        sb.AppendLine("Animator: asignado=True  controller=" + controller.name
                      + "  avatar=" + (animator.avatar == null ? "null" : animator.avatar.name)
                      + "  rootMotion=" + animator.applyRootMotion);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState st = sm.states.Select(c => c.state).FirstOrDefault(s => s != null && s.name == StateName);
        sb.AppendLine("Estado '" + StateName + "': motion=" + (st != null && st.motion != null ? st.motion.name : "null")
                      + "  porDefecto=" + (st != null && sm.defaultState == st));
        sb.AppendLine("Loop del clip: " + AnimationUtility.GetAnimationClipSettings(loopClip).loopTime
                      + "  wrapMode=" + loopClip.wrapMode);

        if (changed)
        {
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            sb.AppendLine("Escena guardada: " + saved);
        }
        else
        {
            sb.AppendLine("Sin cambios que guardar (ya estaba configurado).");
        }

        File.WriteAllText(Report, sb.ToString());
        if (log) Debug.Log("[Corrida] " + sb);
    }

    // ------------------------------------------------------------------ busqueda

    private static GameObject FindTarget(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("corrida") && n.Contains("enfermera"))
                    return t.gameObject;
            }
        }

        return null;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder("Assets", "Animators");
    }

    private static Camera FindCamera()
    {
        return Resources.FindObjectsOfTypeAll<Camera>()
            .Where(c => c != null && c.isActiveAndEnabled && c.gameObject.scene.IsValid())
            .OrderByDescending(c => c.name == "CenterEyeAnchor" ? 1 : 0)
            .ThenByDescending(c => c.depth)
            .FirstOrDefault();
    }

    private static Bounds CombinedBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => r != null).ToArray();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        return b;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                return t;

        return null;
    }

    /// <summary>Vector "adelante" del personaje deducido de los huesos (1 solo elemento).</summary>
    private static Vector3[] CalcularEjeAdelante(Transform root)
    {
        Transform head = FindDeep(root, "mixamorig:Head");
        Transform hips = FindDeep(root, "mixamorig:Hips");
        Transform pieIzq = FindDeep(root, "mixamorig:LeftFoot");
        Transform pieDer = FindDeep(root, "mixamorig:RightFoot");

        if (head == null || hips == null || pieIzq == null || pieDer == null)
            return null;

        Vector3 arriba = (head.position - hips.position).normalized;
        Vector3 derecha = (pieDer.position - pieIzq.position).normalized;
        Vector3 adelante = Vector3.Cross(derecha, arriba);

        if (adelante.sqrMagnitude < 0.0001f)
            return null;

        return new[] { adelante.normalized };
    }

    /// <summary>
    /// Saca el modelo de su jerarquia, le pone escala uniforme (~1,5 m de alto),
    /// lo gira para que mire a la camara y lo coloca 1,8 m delante, a la altura
    /// de los ojos, para que el bucle se vea en cuanto se pulsa Play.
    /// </summary>
    private static bool PlaceInFrontOfCamera(GameObject target, StringBuilder sb)
    {
        Camera cam = FindCamera();
        if (cam == null)
        {
            sb.AppendLine("Colocacion omitida: no hay camara activa en la escena.");
            return false;
        }

        Transform t = target.transform;
        Bounds antes = CombinedBounds(target);
        float alturaNativa = antes.size.y / Mathf.Max(0.0001f, Mathf.Abs(t.lossyScale.y));
        int ordenOriginal = t.GetSiblingIndex();

        sb.AppendLine();
        sb.AppendLine("--- Colocacion delante de la camara ---");
        sb.AppendLine("Antes: padre='" + (t.parent != null ? t.parent.name : "(raiz)") + "'"
                      + "  posLocal=" + t.localPosition.ToString("0.####")
                      + "  rotLocal=" + t.localEulerAngles.ToString("0.##")
                      + "  escalaLocal=" + t.localScale.ToString("0.####")
                      + "  posMundo=" + t.position.ToString("0.####")
                      + "  alturaNativa=" + alturaNativa.ToString("0.###"));

        Undo.RegisterFullObjectHierarchyUndo(target, "Colocar enfermera delante de la camara");
        t.SetParent(null, true);

        float escala = 1.5f / Mathf.Max(0.0001f, alturaNativa);
        t.localScale = Vector3.one * escala;

        Vector3[] eje = CalcularEjeAdelante(t);
        Vector3 haciaCamara = cam.transform.position - t.position;
        haciaCamara.y = 0f;
        if (haciaCamara.sqrMagnitude < 0.0001f)
            haciaCamara = Vector3.forward;

        if (eje != null)
        {
            Vector3 adelante = new Vector3(eje[0].x, 0f, eje[0].z);
            if (adelante.sqrMagnitude > 0.0001f)
            {
                float angulo = Mathf.DeltaAngle(
                    Mathf.Atan2(adelante.x, adelante.z) * Mathf.Rad2Deg,
                    Mathf.Atan2(haciaCamara.x, haciaCamara.z) * Mathf.Rad2Deg);

                t.Rotate(0f, angulo, 0f, Space.World);
                sb.AppendLine("Giro para mirar a la camara: " + angulo.ToString("0.#") + " grados");
            }
        }
        else
        {
            sb.AppendLine("Aviso: no se encontraron los huesos mixamorig para calcular la orientacion.");
        }

        Vector3 adelanteCam = cam.transform.forward;
        adelanteCam.y = 0f;
        if (adelanteCam.sqrMagnitude < 0.0001f)
            adelanteCam = Vector3.forward;
        adelanteCam.Normalize();

        t.position = new Vector3(cam.transform.position.x, t.position.y, cam.transform.position.z) + adelanteCam * 1.8f;

        Bounds despues = CombinedBounds(target);
        t.position += Vector3.up * ((cam.transform.position.y - 0.05f) - despues.center.y);
        t.SetSiblingIndex(ordenOriginal);

        sb.AppendLine("Despues: padre='" + (t.parent != null ? t.parent.name : "(raiz)") + "'"
                      + "  posMundo=" + t.position.ToString("0.####")
                      + "  rotMundo=" + t.eulerAngles.ToString("0.##")
                      + "  escala=" + t.localScale.ToString("0.####"));

        return true;
    }

    // ------------------------------------------------------------------ clip

    private static AnimationClip LoadOrCreateLoopClip(AnimationClip sourceClip, StringBuilder sb)
    {
        AnimationClip loopClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);

        if (loopClip == null || !IsStandardClipAsset(ClipPath))
        {
            loopClip = CreateStandardClip(sourceClip);
            sb.AppendLine("Clip creado como AnimationClip estandar en " + ClipPath);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(loopClip);
        if (!settings.loopTime || settings.loopBlend || loopClip.wrapMode != WrapMode.Loop)
        {
            settings.loopTime = true;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(loopClip, settings);
            loopClip.wrapMode = WrapMode.Loop;

            EditorUtility.SetDirty(loopClip);
            AssetDatabase.SaveAssets();
        }

        // Diagnostico del movimiento de las caderas (por si la animacion "corre"
        // hacia delante y hubiera que anularla).
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(loopClip);
        sb.AppendLine("Curvas del clip definitivo: " + bindings.Length);

        bool clipModificado = false;

        foreach (EditorCurveBinding binding in bindings)
        {
            if (binding.path.IndexOf("Hips", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (!binding.propertyName.StartsWith("m_LocalPosition", StringComparison.Ordinal))
                continue;

            AnimationCurve curve = AnimationUtility.GetEditorCurve(loopClip, binding);
            if (curve == null || curve.keys.Length == 0)
                continue;

            float min = curve.keys.Min(k => k.value);
            float max = curve.keys.Max(k => k.value);
            sb.AppendLine("  Hips " + binding.propertyName + ": min=" + min.ToString("0.###") + " max=" + max.ToString("0.###")
                          + " rango=" + (max - min).ToString("0.###"));

            // La toma de Mixamo trae el avance del cuerpo en Z (unos 5 m por vuelta):
            // en un bucle el personaje se alejaria de la camara. Se deja el valor
            // inicial para que corra en el sitio.
            if (binding.propertyName == "m_LocalPosition.z" && (max - min) > 0.3f)
            {
                float fijo = curve.keys[0].value;
                AnimationCurve plana = new AnimationCurve();
                foreach (Keyframe k in curve.keys)
                    plana.AddKey(new Keyframe(k.time, fijo, 0f, 0f));

                AnimationUtility.SetEditorCurve(loopClip, binding, plana);
                clipModificado = true;
                sb.AppendLine("  -> avance en Z anulado (fijado en " + fijo.ToString("0.###") + ") para que corra en el sitio");
            }
        }

        if (clipModificado)
        {
            EditorUtility.SetDirty(loopClip);
            AssetDatabase.SaveAssets();
        }

        return loopClip;
    }

    private static bool IsStandardClipAsset(string path)
    {
        string metaPath = path + ".meta";
        if (!File.Exists(metaPath))
            return false;

        foreach (string line in File.ReadAllLines(metaPath))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("mainObjectFileID:", StringComparison.Ordinal))
                continue;

            return trimmed.Substring("mainObjectFileID:".Length).Trim() == "7400000";
        }

        return false;
    }

    private static AnimationClip CreateStandardClip(AnimationClip source)
    {
        AnimationClip clip = new AnimationClip
        {
            name = "CorridaEnfermera_Loop",
            frameRate = source.frameRate,
            legacy = false
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
        settings.loopTime = true;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath) != null)
            AssetDatabase.DeleteAsset(ClipPath);

        AssetDatabase.CreateAsset(clip, ClipPath);
        AssetDatabase.SaveAssets();

        return AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
    }

    // ------------------------------------------------------------------ controller

    private static AnimatorController LoadOrCreateController(AnimationClip loopClip, StringBuilder sb)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            sb.AppendLine("Controller creado en " + ControllerPath);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        AnimatorState state = stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(s => s != null && s.name == StateName);

        bool changed = false;

        if (state == null)
        {
            state = stateMachine.AddState(StateName, new Vector3(300f, 40f, 0f));
            changed = true;
        }

        if (state.motion != loopClip) { state.motion = loopClip; changed = true; }
        if (!Mathf.Approximately(state.speed, 1f)) { state.speed = 1f; changed = true; }
        if (!state.writeDefaultValues) { state.writeDefaultValues = true; changed = true; }
        if (stateMachine.defaultState != state) { stateMachine.defaultState = state; changed = true; }

        if (changed)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            sb.AppendLine("Controller actualizado: estado unico '" + StateName + "' como estado por defecto.");
        }

        return controller;
    }

    private static Avatar ResolveAvatar(GameObject target, StringBuilder sb)
    {
        Avatar fbxAvatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<Avatar>()
            .FirstOrDefault(a => a != null && a.isValid);

        if (fbxAvatar != null)
            return fbxAvatar;

        Avatar saved = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
        if (saved != null && saved.isValid)
            return saved;

        if (File.Exists(AvatarPath))
            AssetDatabase.DeleteAsset(AvatarPath);

        Avatar generated = AvatarBuilder.BuildGenericAvatar(target, string.Empty);
        if (generated == null || !generated.isValid)
        {
            sb.AppendLine("ERROR: no se pudo generar el Avatar generico.");
            return null;
        }

        generated.name = "CorridaEnfermera_Avatar";
        AssetDatabase.CreateAsset(generated, AvatarPath);
        AssetDatabase.SaveAssets();

        return AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
    }

    // ------------------------------------------------------------------ vigilancia en Play

    /// <summary>
    /// Durante Play escribe cada segundo el avance de la animacion y si el modelo
    /// esta dentro de la vista. Solo se activa si existe corrida-watch.flag.
    /// </summary>
    private static class PlayWatcher
    {
        private static double _next;
        private static double _start;
        private static int _ticks;
        private static readonly string PlayReport = TempDir + "corrida-play.txt";

        public static void Start()
        {
            if (!File.Exists(WatchFlag))
                return;

            _start = EditorApplication.timeSinceStartup;
            _ticks = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= Tick;
                return;
            }

            double t = EditorApplication.timeSinceStartup - _start;
            if (t < _next)
                return;

            _next = t + 1.0;
            _ticks++;
            Scene scene = SceneManager.GetActiveScene();
            GameObject target = FindTarget(scene);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("t=" + t.ToString("0.0") + "s  isPaused=" + EditorApplication.isPaused
                          + "  Play.time=" + Time.time.ToString("0.00") + "  frame=" + Time.frameCount);

            if (target != null)
            {
                Animator animator = target.GetComponent<Animator>();
                if (animator != null)
                {
                    AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                    sb.AppendLine("  normalizedTime=" + info.normalizedTime.ToString("0.###")
                                  + "  length=" + info.length.ToString("0.###")
                                  + "  velocidad=" + info.speed);

                    Transform cadera = FindDeep(target.transform, "mixamorig:Hips");
                    if (cadera != null)
                        sb.AppendLine("  cadera mundo=" + cadera.position.ToString("0.####")
                                      + "  cadera local=" + cadera.localPosition.ToString("0.####"));
                }

                Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    foreach (Renderer r in renderers)
                        b.Encapsulate(r.bounds);

                    Camera cam = FindCamera();

                    if (cam != null)
                    {
                        Vector3 v = cam.WorldToViewportPoint(b.center);
                        bool inside = v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f && v.z > 0f;
                        sb.AppendLine("  bounds centro=" + b.center.ToString("0.###") + " tamano=" + b.size.ToString("0.###"));
                        sb.AppendLine("  viewport=(" + v.x.ToString("0.###") + ", " + v.y.ToString("0.###") + ")"
                                      + " -> " + (inside ? "DENTRO" : "FUERA"));
                    }
                }
            }

            if (_ticks == 1)
                File.WriteAllText(PlayReport, "=== vigilancia en Play " + DateTime.Now.ToString("HH:mm:ss") + " ===\n" + sb + Environment.NewLine);
            else
                File.AppendAllText(PlayReport, sb.ToString() + Environment.NewLine);

            if (_ticks >= 20)
            {
                EditorApplication.update -= Tick;
                File.Delete(WatchFlag);
            }
        }
    }
}
