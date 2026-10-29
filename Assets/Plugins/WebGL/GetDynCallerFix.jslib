// Il WebSocket.jslib del pacchetto com.unity.services.wire (eventi in tempo reale
// delle Lobby/Sessions) usa makeDynCall, che Emscripten traduce in getDynCaller(...),
// ma non dichiara la dipendenza: Emscripten quindi non include getDynCaller nella
// build e al primo ws.onopen esplode con "ReferenceError: getDynCaller is not defined".
// Questa funzione vuota esiste solo per dichiarare quella dipendenza (__deps):
// quando viene usata da C# (WebGLDynCallerFix.cs), Emscripten include anche getDynCaller.
mergeInto(LibraryManager.library, {
    IncludeGetDynCaller__deps: ['$getDynCaller'],
    IncludeGetDynCaller: function () {}
});
