using System;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
// AuthState (sotto) e' un enum ANNIDATO dentro AuthenticationWrapper:
// senza questo "using static" andrebbe scritto per esteso come
// AuthenticationWrapper.AuthState ad ogni utilizzo. Senza, il progetto non compilava.
using static AuthenticationWrapper;

/// <summary>
/// Classe C# pura (non un MonoBehaviour: niente Update/eventi Unity,
/// racchiude la logica di bootstrap lato client: inizializzare i servizi Unity
/// Gaming Services e autenticarsi, poi far entrare il giocatore nel menu.
/// </summary>
public class ClientGameManager
{
    private const string menuSceneName = "Menu";
    private ISession session;

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

    public void goToMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }

    public async Task startClientAsync(string joinCode)
    {
        // JoinSessionByCodeAsync fa, in una sola chiamata, quello che prima
        // richiedeva Relay.JoinAllocationAsync + UnityTransport.SetRelayServerData +
        // NetworkManager.StartClient: entra nella sessione creata dall'host e avvia
        // gia' questa istanza come Client connesso via Relay.
        setConnectionData();

        try
        {
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    // Gemello di startClientAsync, ma per entrare in una lobby scelta dalla lista
    // (LobbiesList) invece che digitando il codice. Nel corso qui c'erano tre passi:
    // Lobbies.JoinLobbyByIdAsync, lettura di lobby.Data["JoinCode"] e poi
    // startClientAsync(joinCode). JoinSessionByIdAsync fa tutto in una chiamata:
    // entra nella Lobby, recupera da sola il join code del Relay (salvato
    // dall'host in CreateSessionAsync) e avvia questa istanza come Client.
    // sessionId e' ISessionInfo.Id, cioe' l'id della Lobby sottostante.
    public async Task startClientByIdAsync(string sessionId)
    {
        setConnectionData();

        try
        {
            session = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    // Va chiamato PRIMA di JoinSessionBy*Async: quelle chiamate avviano gia' il
    // NetworkManager come Client e inviano subito la richiesta di connessione,
    // quindi ConnectionData impostato dopo arriverebbe vuoto all'ApprovalCheck.
    private void setConnectionData()
    {
        UserData userData = new UserData
        {
            username = PlayerPrefs.GetString(NameSelector.playerNameKey, "Missing name")
        };
        string payload = JsonUtility.ToJson(userData);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
    }
}
