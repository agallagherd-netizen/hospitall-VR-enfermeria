using UnityEditor;

/// <summary>
/// Evita que el editor arranque el modo Play en pausa.
///
/// En este proyecto, al entrar en Play se lanza un error del paquete Meta XR
/// (MR Utility Kit no puede cargar su libreria nativa en Windows) y el editor
/// se queda pausado en el primer fotograma: parece que la animacion no
/// funciona. Este script quita esa pausa inicial. Si despues pulsas tu el
/// boton Pausa, la pausa se respeta.
///
/// Si no lo quieres, borra este archivo.
/// </summary>
[InitializeOnLoad]
public static class PlaySinPausa
{
    static PlaySinPausa()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && EditorApplication.isPaused)
                EditorApplication.isPaused = false;
        };
    }
}
