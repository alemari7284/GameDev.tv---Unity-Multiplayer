using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
// AuthState (sotto) e' un enum ANNIDATO dentro AuthenticationWrapper (FLUSSO 77):
// senza questo "using static" andrebbe scritto per esteso come
// AuthenticationWrapper.AuthState ad ogni utilizzo. Senza, il progetto non compilava.
using static AuthenticationWrapper;

/// <summary>
/// Classe C# pura (non un MonoBehaviour: niente Update/eventi Unity, e' pensata
/// per essere creata con "new" da <see cref="ClientSingleton"/>, FLUSSO 68) che
/// racchiude la logica di bootstrap lato client: inizializzare i servizi Unity
/// Gaming Services e autenticarsi, poi far entrare il giocatore nel menu.
/// </summary>
public class ClientGameManager
{
    private const string menuSceneName = "Menu";
    // [FLUSSO 69] UnityServices.InitializeAsync() va chiamato UNA volta per processo
    // prima di usare qualsiasi servizio UGS (Authentication, Relay, Lobby, ...):
    // se non e' gia' inizializzato, AuthenticationService lancerebbe un'eccezione.
    // Solo dopo si passa la palla ad AuthenticationWrapper.doAuth (FLUSSO 72-74),
    // che e' una classe static condivisa da tutto il processo.
    public async Task<bool> initAsync()
    {
        await UnityServices.InitializeAsync();

        AuthState authState = await doAuth();

        if (authState == AuthState.Authenticated)
        {
            return true;
        }
        return false;
    }

    // [FLUSSO 70] Chiamato da ApplicationController solo se authenticated == true
    // (FLUSSO 65): carica la scena "Menu" in modo NON additivo (LoadScene di default
    // scarica la scena corrente, "NetBootstrap"), lasciando pero' vivi gli oggetti
    // con DontDestroyOnLoad (ApplicationController, ClientSingleton, HostSingleton).
    public void goToMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }
}
