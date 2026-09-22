using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

// Controparte di ClientGameManager lato host. StartHostAsync crea una Session
// (Unity Multiplayer Services SDK), che dietro le quinte alloca il Relay e avvia
// da sola NetworkManager come Host, poi carica la scena di gioco. Chiamato da
// MainMenu.StartHost tramite HostSingleton.GameManager
public class HostGameManager
{
    // session: stato della sessione corrente, salvato sull'istanza (non come
    // variabile locale del metodo) perche' servira' anche altrove in futuro — es.
    // una UI che mostri session.Code a schermo, cosi' un altro giocatore possa
    // copiarlo e usarlo per unirsi (lato ClientGameManager). gameSceneName/
    // maxConnections sono invece semplice configurazione.
    private ISession session;
    private const string gameSceneName = "Game";
    private const int maxConnections = 20;

    public async Task StartHostAsync()
    {
        // CreateSessionAsync con WithRelayNetwork() fa, in una sola chiamata, quello
        // che prima richiedeva Relay.CreateAllocationAsync + GetJoinCodeAsync +
        // UnityTransport.SetRelayServerData + NetworkManager.StartHost: alloca il
        // Relay, genera il join code e avvia gia' questa istanza come Host (server
        // autorevole + client locale). E' una chiamata di rete, quindi in try/catch.
        try
        {
            var options = new SessionOptions
            {
                MaxPlayers = maxConnections
            }.WithRelayNetwork();

            session = await MultiplayerService.Instance.CreateSessionAsync(options);

            // Per ora viene solo loggato in console: non esiste ancora una UI che lo
            // mostri al giocatore (prossimo passo naturale, insieme al ramo "Join"
            // lato client).
            Debug.Log($"Session created. Join code: {session.Code}");
        }
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
