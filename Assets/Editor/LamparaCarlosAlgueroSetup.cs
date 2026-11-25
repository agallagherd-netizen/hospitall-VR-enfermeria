using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Asigna el script LamparaCarlosAlguero a "Lampara - Carlos Alguero".
/// Menu: Tools > Lampara Carlos Alguero > Asignar
/// </summary>
[InitializeOnLoad]
public static class LamparaCarlosAlgueroSetup
{
    private const string TempDir = @"C:\Users\User\AppData\Local\Temp\opencode\";
    private const string Report  = TempDir + "lampara-carlos.txt";
    private const string Flag    = TempDir + "lampara-carlos.flag";

    static LamparaCarlosAlgueroSetup()
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

    [MenuItem("Tools/Lampara Carlos Alguero/Asignar")]
    private static void DesdeMenu()
    {
        Configurar(true);
    }

    private static void Configurar(bool log)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== lampara carlos alguero " + DateTime.Now.ToString("HH:mm:ss") + " ===");

        Scene scene = SceneManager.GetActiveScene();
        GameObject lampara = Buscar("Lampara - Carlos Alguero");

        if (lampara == null)
        {
            sb.AppendLine("ERROR: no se encontro 'Lampara - Carlos Alguero'.");
            File.WriteAllText(Report, sb.ToString());
            if (log) Debug.LogWarning("[LamparaCarlosAlguero] " + sb);
            return;
        }

        LamparaCarlosAlguero ctrl = lampara.GetComponent<LamparaCarlosAlguero>();
        bool anadido = false;

        if (ctrl == null)
        {
            ctrl = Undo.AddComponent<LamparaCarlosAlguero>(lampara);
            anadido = true;
            sb.AppendLine("Componente anadido.");
        }
        else
        {
            sb.AppendLine("El componente ya estaba.");
        }

        // Aseguramos que el script cree la luz si no existe
        ctrl.enabled = true;

        EditorUtility.SetDirty(lampara);
        EditorSceneManager.MarkSceneDirty(scene);
        bool guardado = EditorSceneManager.SaveScene(scene);
        sb.AppendLine("Escena guardada: " + guardado);
        sb.AppendLine("Objeto: '" + lampara.name + "'  pos=" + lampara.transform.position);
        sb.AppendLine("Pulsa ENTER para encender/apagar.");

        File.WriteAllText(Report, sb.ToString());
        if (log) Debug.Log("[LamparaCarlosAlguero] " + sb);
    }

    private static GameObject Buscar(string nombre)
    {
        Scene scene = SceneManager.GetActiveScene();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == nombre)
                    return t.gameObject;
            }
        }

        return null;
    }
}
