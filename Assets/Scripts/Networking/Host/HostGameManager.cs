using System;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

// Controparte di ClientGameManager lato host. StartHostAsync crea una Session
// (Unity Multiplayer Services SDK), che dietro le quinte crea la Lobby, alloca il
// Relay e avvia da sola NetworkManager come Host, poi carica la scena di gioco.
// Chiamato da MainMenu.StartHost tramite HostSingleton.GameManager.
// Nota: a differenza del corso, qui non serve nessun riferimento al pacchetto
// Unity.Services.Lobbies (deprecato): Lobby e Relay sono inglobati in
// Unity.Services.Multiplayer e si usano solo attraverso la Session.
public class HostGameManager
{
    // session: stato della sessione corrente, salvato sull'istanza (non come
    // variabile locale del metodo) perche' servira' anche altrove in futuro — es.
    // una UI che mostri session.Code a schermo, cosi' un altro giocatore possa
    // copiarlo e usarlo per unirsi (lato ClientGameManager). Sostituisce anche il
    // campo lobbyId del corso: session.Id e' l'id della Lobby sottostante, quello
    // che i client useranno con JoinSessionByIdAsync dalla lista lobby.
    // gameSceneName/maxConnections sono invece semplice configurazione.
    private ISession session;
    private const string gameSceneName = "Game";
    private const int maxConnections = 20;

    private NetworkServer networkServer;

    public async Task StartHostAsync()
    {
        string playerName = PlayerPrefs.GetString(NameSelector.playerNameKey, "Unknown");

        // NetworkServer e ConnectionData vanno preparati PRIMA di CreateSessionAsync:
        // la chiamata avvia gia' l'Host, e l'Host passa subito dall'ApprovalCheck
        // con il proprio payload.
        networkServer = new NetworkServer(NetworkManager.Singleton);

        UserData userData = new UserData
        {
            username = PlayerPrefs.GetString(NameSelector.playerNameKey, "Missing name")
        };
        string payload = JsonUtility.ToJson(userData);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;

        // CreateSessionAsync con WithRelayNetwork() fa, in una sola chiamata, quello
        // che prima richiedeva Relay.CreateAllocationAsync + GetJoinCodeAsync +
        // UnityTransport.SetRelayServerData + NetworkManager.StartHost: alloca il
        // Relay, genera il join code e avvia gia' questa istanza come Host (server
        // autorevole + client locale). E' una chiamata di rete, quindi in try/catch.
        //
        // La Session E' anche la Lobby: dietro le quinte l'SDK crea una Lobby
        // (Lobbies.CreateLobbyAsync nel corso), ci salva dentro il join code del
        // Relay (il "JoinCode" nei Data della lobby nel corso) e la tiene in vita con
        // l'heartbeat automatico (la coroutine HeartbeatLobby del corso). Per questo
        // qui bastano Name e IsPrivate: niente lobbyId, niente coroutine.
        try
        {
            // WithRelayNetwork(): dice all'SDK di collegare i giocatori via Relay
            // (connessione host-client attraverso i server Unity, niente port
            // forwarding). Senza, la Session sarebbe solo una Lobby senza rete.
            var options = new SessionOptions
            {
                // Name: il nome mostrato nella lista lobby (QuerySessionsAsync lato client).
                Name = $"{playerName}'s Lobby",
                // MaxPlayers: capienza della Lobby, host compreso. Il Relay viene
                // dimensionato di conseguenza (nel corso era il parametro di
                // CreateAllocationAsync e di CreateLobbyAsync, qui e' uno solo).
                MaxPlayers = maxConnections,
                // IsPrivate = false: la sessione compare nelle ricerche pubbliche;
                // se fosse true si potrebbe entrare solo tramite session.Code.
                IsPrivate = false
            }.WithRelayNetwork();

            session = await MultiplayerService.Instance.CreateSessionAsync(options);

            // Per ora viene solo loggato in console: non esiste ancora una UI che lo
            // mostri al giocatore (prossimo passo naturale, insieme al ramo "Join"
            // lato client).
            Debug.Log($"Session created. Join code: {session.Code}");
        }
        // Exception generica (non LobbyServiceException come nel corso): la chiamata
        // puo' fallire sia lato Lobby sia lato Relay, e l'SDK le riporta come
        // SessionException. Il return evita di caricare la scena "Game" senza una
        // sessione valida.
        catch (Exception e)
        {
            Debug.LogError(e);
            return;
        }

        // NetworkManager.Singleton.SceneManager.LoadScene (NON
        // UnityEngine.SceneManagement.SceneManager, quello usato invece da
        // ClientGameManager.goToMenu): e' la versione "di rete" del cambio scena,
        // l'unica che porta con se' anche tutti i client gia' connessi all'Host
        // verso la stessa scena "Game".
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}
