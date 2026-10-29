using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Fix per le build WebGL: costringe Emscripten a includere getDynCaller,
/// che serve al WebSocket del pacchetto Wire (eventi delle Sessions) ma che
/// altrimenti verrebbe tolto dalla build (vedi Assets/Plugins/WebGL/GetDynCallerFix.jslib).
/// Fuori da WebGL (Editor compreso) non fa nulla.
/// </summary>
public static class WebGLDynCallerFix
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void IncludeGetDynCaller();

    // Emscripten include una funzione di un .jslib (e le sue __deps) solo se il
    // codice C# la usa davvero: la chiamiamo una volta all'avvio, prima di
    // qualunque scena, e la chiamata in se' non fa niente.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void init()
    {
        IncludeGetDynCaller();
    }
#endif
}
