using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepara el carro "015" de la escena abierta: le pone el componente
/// Carro015Control (W/A/S/D) y escribe un informe con la jerarquia del modelo
/// (para saber que piezas se pueden usar como cajones).
///
/// Menu manual: Tools > Carro 015 > Configurar carro
/// </summary>
[InitializeOnLoad]
public static class Carro015Setup
{
    private const string TempDir = @"C:\Users\User\AppData\Local\Temp\opencode\";
    private const string Report  = TempDir + "carro-informe.txt";
    private const string Flag    = TempDir + "carro-setup.flag";

    static Carro015Setup()
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
        Configurar(true);
    }

    [MenuItem("Tools/Carro 015/Configurar carro")]
    private static void DesdeMenu()
    {
        Configurar(true);
    }

    private static void Configurar(bool log)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== carro 015 " + DateTime.Now.ToString("HH:mm:ss") + " ===");

        Scene scene = SceneManager.GetActiveScene();
        sb.AppendLine("Escena: '" + scene.name + "'  (" + scene.path + ")");

        GameObject carro = BuscarCarro(scene);

        if (carro == null)
        {
            sb.AppendLine("No se encontro ningun objeto llamado '015'.");
            File.WriteAllText(Report, sb.ToString());
            if (log) Debug.LogWarning("[Carro015] No se encontro el objeto 015.");
            return;
        }

        sb.AppendLine("Objeto: '" + carro.name + "'  posicion mundo=" + carro.transform.position.ToString("0.####")
                      + "  escala=" + carro.transform.lossyScale.ToString("0.#####")
                      + "  rotacion=" + carro.transform.eulerAngles.ToString("0.##"));

        Renderer[] renderers = carro.GetComponentsInChildren<Renderer>(true);

        sb.AppendLine("Renderers: " + renderers.Length);

        foreach (Transform t in carro.GetComponentsInChildren<Transform>(true))
        {
            MeshFilter mf = t.GetComponent<MeshFilter>();
            string infoMesh = "";

            if (mf != null && mf.sharedMesh != null)
            {
                infoMesh = "  mesh='" + mf.sharedMesh.name + "'"
                           + " verts=" + mf.sharedMesh.vertexCount
                           + " submeshes=" + mf.sharedMesh.subMeshCount;
            }

            Renderer r = t.GetComponent<Renderer>();
            string infoMat = "";
            if (r != null && r.sharedMaterial != null)
                infoMat = "  material='" + r.sharedMaterial.name + "'";

            string bounds = "";
            if (r != null)
                bounds = "  boundsTam=" + r.bounds.size.ToString("0.####");

            sb.AppendLine("  - '" + t.name + "'  ruta='" + Ruta(t, carro.transform) + "'"
                          + "  localPos=" + t.localPosition.ToString("0.####")
                          + "  localScale=" + t.localScale.ToString("0.####")
                          + infoMesh + infoMat + bounds);
        }

        Carro015Control control = carro.GetComponent<Carro015Control>();
        bool changed = false;

        if (control == null)
        {
            control = Undo.AddComponent<Carro015Control>(carro);
            changed = true;
            sb.AppendLine("Componente Carro015Control anadido.");
        }
        else
        {
            sb.AppendLine("El componente Carro015Control ya estaba.");
        }

        sb.AppendLine("Cajones asignados: " + (control.cajones == null ? 0 : control.cajones.Length));

        if (changed)
        {
            EditorUtility.SetDirty(carro);
            EditorSceneManager.MarkSceneDirty(scene);
            bool guardado = EditorSceneManager.SaveScene(scene);
            sb.AppendLine("Escena guardada: " + guardado);
        }

        File.WriteAllText(Report, sb.ToString());
        if (log) Debug.Log("[Carro015] " + sb);
    }

    private static string Ruta(Transform t, Transform raiz)
    {
        if (t == raiz) return t.name;

        string ruta = t.name;
        Transform p = t.parent;

        while (p != null && p != raiz)
        {
            ruta = p.name + "/" + ruta;
            p = p.parent;
        }

        return ruta;
    }

    private static GameObject BuscarCarro(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Trim() == "015")
                    return t.gameObject;
            }
        }

        return null;
    }
}
