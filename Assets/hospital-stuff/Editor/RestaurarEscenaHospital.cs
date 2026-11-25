using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Vuelve a abrir la escena del hospital (<c>Assets/_Recovery/0.unity</c>) una sola vez.
///
/// La escena de prueba de la enfermera se guardó aparte en
/// <c>Assets/Scenes/EnfermeraCaminando.unity</c>, así que no se pierde nada.
/// </summary>
[InitializeOnLoad]
public static class RestaurarEscenaHospital
{
    private const string ClaveSesion = "RestaurarEscenaHospital.v1";
    private const string RutaHospital = "Assets/_Recovery/0.unity";
    private const string RutaEnfermera = "Assets/Scenes/EnfermeraCaminando.unity";

    static RestaurarEscenaHospital()
    {
        if (SessionState.GetBool(ClaveSesion, false))
        {
            return;
        }

        EditorApplication.delayCall += IntentarRestaurar;
    }

    private static void IntentarRestaurar()
    {
        if (SessionState.GetBool(ClaveSesion, false))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += IntentarRestaurar;
            return;
        }

        SessionState.SetBool(ClaveSesion, true);

        // Si ya estás en tu escena, no hacemos nada.
        if (EditorSceneManager.GetActiveScene().path == RutaHospital)
        {
            return;
        }

        if (!System.IO.File.Exists(RutaHospital))
        {
            EditorUtility.DisplayDialog(
                "No encuentro tu escena",
                $"No existe el archivo:\n\n{RutaHospital}",
                "Aceptar");
            return;
        }

        // Si la escena de prueba tiene cambios sin guardar, Unity pregunta.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(RutaHospital, OpenSceneMode.Single);

        EditorUtility.DisplayDialog(
            "Escena del hospital restaurada",
            "He vuelto a abrir tu escena del hospital, tal y como la tenías.\n\n" +
            "La escena de la enfermera NO se ha perdido: está guardada aparte en\n" +
            $"«{RutaEnfermera}» y puedes abrirla cuando quieras.",
            "Vale");
    }
}
