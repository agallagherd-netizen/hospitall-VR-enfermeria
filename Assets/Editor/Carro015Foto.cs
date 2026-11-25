using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hace dos fotos del carro "015" sin entrar en Play: una con los cajones
/// cerrados y otra con los cajones abiertos (aplicando el mismo desplazamiento
/// que hace Carro015Control en tiempo de ejecucion).
///
/// Se dispara creando el archivo carro-foto.flag.
/// Menu: Tools > Carro 015 > Hacer fotos
/// </summary>
[InitializeOnLoad]
public static class Carro015Foto
{
    private const string TempDir = @"C:\Users\User\AppData\Local\Temp\opencode\";
    private const string Flag    = TempDir + "carro-foto.flag";
    private const string Report  = TempDir + "carro-foto.txt";
    private const string FotoCerrada = TempDir + "carro-cerrado.png";
    private const string FotoAbierta = TempDir + "carro-abierto.png";

    static Carro015Foto()
    {
        EditorApplication.update += Vigilar;
    }

    private static void Vigilar()
    {
        if (!File.Exists(Flag))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        File.Delete(Flag);
        Hacer(true);
    }

    [MenuItem("Tools/Carro 015/Hacer fotos")]
    private static void DesdeMenu()
    {
        Hacer(true);
    }

    private static void Hacer(bool log)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== fotos carro 015 " + DateTime.Now.ToString("HH:mm:ss") + " ===");

        GameObject carro = Buscar("015");

        if (carro == null)
        {
            sb.AppendLine("ERROR: no se encontro '015'.");
            File.WriteAllText(Report, sb.ToString());
            return;
        }

        Carro015Control control = carro.GetComponent<Carro015Control>();
        Transform[] cajones = control != null ? control.cajones : null;

        sb.AppendLine("carro: pos=" + carro.transform.position.ToString("0.####")
                      + " rot=" + carro.transform.eulerAngles.ToString("0.##")
                      + " escala=" + carro.transform.lossyScale.ToString("0.#####"));
        sb.AppendLine("cajones asignados: " + (cajones == null ? 0 : cajones.Length));

        Vector3 frente = carro.transform.localToWorldMatrix.MultiplyVector(Vector3.forward);
        frente.y = 0f;
        frente.Normalize();
        sb.AppendLine("frente del carro (mundo) = " + frente.ToString("0.####"));

        // --- foto cerrada ---
        Bounds caja = Caja(carro);
        sb.AppendLine("bounds cerrado: centro=" + caja.center.ToString("0.####") + " tam=" + caja.size.ToString("0.####"));
        Render(caja.center, frente, FotoCerrada);
        sb.AppendLine("foto cerrada: " + FotoCerrada);

        // --- abrir cajones con la misma cuenta que el componente ---
        Vector3[] cerradas = new Vector3[0];
        Vector3[] abiertas = new Vector3[0];

        if (cajones != null && cajones.Length > 0)
        {
            float apertura = control != null ? control.apertura : 0.07f;
            float sentido = control != null ? control.sentidoApertura : 1f;

            cerradas = new Vector3[cajones.Length];
            abiertas = new Vector3[cajones.Length];

            for (int i = 0; i < cajones.Length; i++)
            {
                Transform c = cajones[i];
                if (c == null || c.parent == null) continue;

                cerradas[i] = c.localPosition;

                Vector3 desplazamientoMundo = frente * (apertura * sentido);
                Vector3 desplazamientoLocal = c.parent.InverseTransformVector(desplazamientoMundo);
                abiertas[i] = cerradas[i] + desplazamientoLocal;

                Vector3 antes = c.position;
                c.localPosition = abiertas[i];

                sb.AppendLine("cajon " + i + " '" + c.name + "': local " + cerradas[i].ToString("0.###")
                              + " -> " + abiertas[i].ToString("0.###")
                              + "  mundo " + antes.ToString("0.####") + " -> " + c.position.ToString("0.####")
                              + "  desplazamiento=" + (c.position - antes).magnitude.ToString("0.####") + " m");
            }

            caja = Caja(carro);
            sb.AppendLine("bounds abierto: centro=" + caja.center.ToString("0.####") + " tam=" + caja.size.ToString("0.####"));
            Render(caja.center, frente, FotoAbierta);
            sb.AppendLine("foto abierta: " + FotoAbierta);

            for (int i = 0; i < cajones.Length; i++)
            {
                if (cajones[i] != null)
                    cajones[i].localPosition = cerradas[i];
            }
        }

        File.WriteAllText(Report, sb.ToString());
        if (log) Debug.Log("[Carro015Foto] " + sb);
    }

    private static Bounds Caja(GameObject carro)
    {
        Renderer[] rs = carro.GetComponentsInChildren<Renderer>(true);
        Bounds b = new Bounds(carro.transform.position, Vector3.zero);
        bool primero = true;

        foreach (Renderer r in rs)
        {
            if (r == null || !r.enabled) continue;

            if (primero) { b = r.bounds; primero = false; }
            else b.Encapsulate(r.bounds);
        }

        return b;
    }

    private static void Render(Vector3 centro, Vector3 frente, string ruta)
    {
        GameObject go = new GameObject("CamaraCarro015");
        Camera cam = go.AddComponent<Camera>();

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
        cam.fieldOfView = 32f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        Vector3 posicion = centro + frente * 1.2f + Vector3.up * 0.10f;
        cam.transform.position = posicion;
        cam.transform.LookAt(centro);

        RenderTexture rt = new RenderTexture(900, 700, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(900, 700, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 900, 700), 0, 0);
        tex.Apply();

        RenderTexture.active = null;
        cam.targetTexture = null;

        File.WriteAllBytes(ruta, tex.EncodeToPNG());

        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(go);
    }

    private static GameObject Buscar(string nombre)
    {
        Scene scene = SceneManager.GetActiveScene();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Trim() == nombre)
                    return t.gameObject;
            }
        }

        return null;
    }
}
