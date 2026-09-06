using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

// [FLUSSO 81] Controparte di ClientGameManager (FLUSSO 69-70) lato host. Non e' piu'
// un placeholder vuoto: StartHostAsync (FLUSSO 85-89) alloca davvero una sessione su
// Unity Relay, configura il transport di Netcode per passarci attraverso, avvia
// l'Host e carica la scena di gioco. Chiamato da MainMenu.StartHost (FLUSSO 83)
// tramite HostSingleton.GameManager (FLUSSO 82) — non piu' "a mano" da
// ConnectionButtons (§3 nella guida), che restava utile solo per test locali senza Relay.
public class HostGameManager
{
    // [FLUSSO 84] allocation/joinCode: stato della sessione Relay corrente, salvato
    // sull'istanza (non come variabile locale del metodo) perche' servira' anche
    // altrove in futuro — es. una UI che mostri joinCode a schermo, cosi' un altro
    // giocatore possa copiarlo e usarlo per unirsi (lato ClientGameManager, ancora
    // da scrivere). gameSceneName/maxConnections sono invece semplice configurazione.
    private Allocation allocation;
    private string joinCode;
    private const string gameSceneName = "Game";
    private const int maxConnections = 20;

    public async Task StartHostAsync()
    {
        // [FLUSSO 85] Relay.Instance.CreateAllocationAsync: chiede ai server Relay di
        // Unity di riservare risorse per una partita fino a maxConnections client,
        // SENZA che l'host debba avere un IP pubblico o aprire porte sul router (tutto
        // il traffico passa dai server Relay). E' una chiamata di rete, quindi in
        // try/catch: puo' fallire per motivi fuori dal nostro controllo (rete assente,
        // servizio non raggiungibile, quota del progetto UGS esaurita, ecc.).
        try
        {
            allocation = await Relay.Instance.CreateAllocationAsync(maxConnections);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return;
        }

        // [FLUSSO 86] GetJoinCodeAsync trasforma l'allocation (dati tecnici, scomodi
        // da condividere a mano) in un codice breve leggibile da un umano — lo stesso
        // meccanismo dei giochi commerciali per invitare amici in partita. Per ora
        // viene solo loggato in console: non esiste ancora una UI che lo mostri al
        // giocatore (prossimo passo naturale, insieme al ramo "Join" lato client).
        try
        {
            joinCode = await Relay.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log(joinCode);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return;
        }

        // [FLUSSO 87] Il NetworkManager di Netcode, di default, non sa nulla di Relay:
        // va "istruito" passando al suo UnityTransport i dati della allocation appena
        // ottenuta (RelayServerData), cosi' che instradi i pacchetti attraverso i
        // server Relay invece che con una connessione diretta IP:porta.
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData relayServerData = new RelayServerData(allocation, "udp");
        transport.SetRelayServerData(relayServerData);

        // [FLUSSO 88] Solo ORA, con il transport gia' pronto per passare da Relay, si
        // avvia davvero la sessione: questa istanza diventa contemporaneamente server
        // (autorevole) e client (gioca anche lei) — cioe' un Host (vedi §1 in guida).
        NetworkManager.Singleton.StartHost();
        // [FLUSSO 89] NetworkManager.Singleton.SceneManager.LoadScene (NON
        // UnityEngine.SceneManagement.SceneManager, quello usato invece da
        // ClientGameManager.goToMenu, FLUSSO 70): e' la versione "di rete" del cambio
        // scena, l'unica che porta con se' anche tutti i client gia' connessi
        // all'Host verso la stessa scena "Game".
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}
