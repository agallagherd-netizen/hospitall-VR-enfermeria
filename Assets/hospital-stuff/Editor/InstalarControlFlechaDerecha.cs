using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Añade el control con la flecha derecha a un personaje que ya esté montado en la escena.
///
/// Menú: Enfermeria > 2. Activar control con flecha derecha
/// </summary>
public static class InstalarControlFlechaDerecha
{
    [MenuItem("Enfermeria/2. Activar control con flecha derecha", false, 2)]
    public static void Instalar()
    {
        GameObject personaje = GameObject.Find("Personaje");

        if (personaje == null || !personaje.TryGetComponent(out UnityEngine.AI.NavMeshAgent _))
        {
            EditorUtility.DisplayDialog(
                "Falta el personaje",
                "No se encontró «Personaje» con NavMeshAgent en la escena.\n\n" +
                "Ejecuta primero: Enfermeria > 1. Configurar personaje caminando (2 m).",
                "Aceptar");
            return;
        }

        ConfigurarPersonajeCaminando.AsegurarControlFlecha(personaje);

        Animator animator = personaje.GetComponent<Animator>();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning(
                "[InstalarControlFlechaDerecha] El personaje no tiene AnimatorController; " +
                "se moverá sin animación.");
        }
        else if (!TieneParametro(animator, ConfigurarPersonajeCaminando.NombreParametroCaminando))
        {
            Debug.LogWarning(
                $"[InstalarControlFlechaDerecha] El AnimatorController no tiene el bool " +
                $"«{ConfigurarPersonajeCaminando.NombreParametroCaminando}». " +
                "Vuelve a ejecutar Enfermeria > 1 para regenerarlo.");
        }

        var escena = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        Selection.activeGameObject = personaje;

        Debug.Log(
            "[InstalarControlFlechaDerecha] Control con flecha derecha listo. " +
            "Pulsa Play y mantén pulsada la flecha derecha.");
    }

    private static bool TieneParametro(Animator animator, string nombre)
    {
        foreach (AnimatorControllerParameter parametro in animator.parameters)
        {
            if (parametro.name == nombre)
            {
                return true;
            }
        }

        return false;
    }
}
