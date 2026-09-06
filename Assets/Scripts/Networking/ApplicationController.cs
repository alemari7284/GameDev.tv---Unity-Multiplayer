using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Vero e proprio punto di ingresso della rete: vive nella scena "NetBootstrap",
/// caricata per prima all'avvio del gioco, PRIMA di qualunque scena con gameplay
/// o UI (es. ConnectionButtons, §2 nella guida). Il suo unico compito e' capire
/// se questa istanza e' un dedicated server o un giocatore, e nel secondo caso
/// autenticarsi presso Unity Gaming Services prima di lasciar entrare nel menu.
/// </summary>
// [FLUSSO 60] I due prefab sono i "capostipiti" delle due catene di singleton:
// ClientSingleton (lato giocatore) e HostSingleton (lato host). Sono prefab e non
// riferimenti a oggetti di scena perche' vengono creati dinamicamente qui sotto,
// una volta capito in che modalita' gira questa istanza.
public class ApplicationController : MonoBehaviour
{
    [SerializeField] private ClientSingleton clientPrefab;
    [SerializeField] private HostSingleton hostPrefab;
    // Start is called before the first frame update
    private async Task Start()
    {
        // [FLUSSO 61] DontDestroyOnLoad su questo GameObject (e sui singleton che crea
        // piu' sotto, FLUSSO 66/78) fa si' che tutta la macchina di bootstrap sopravviva
        // al caricamento della scena Menu (FLUSSO 70): senza, verrebbe distrutta insieme
        // alla scena NetBootstrap e si perderebbe lo stato di autenticazione appena ottenuto.
        DontDestroyOnLoad(gameObject);
        // Un dedicated server non ha una GPU/finestra: e' il modo standard per
        // distinguere a runtime un build server (headless) da un client giocabile,
        // senza bisogno di flag o argomenti da riga di comando.
        bool isDedicatedServer = SystemInfo.graphicsDeviceType ==
            UnityEngine.Rendering.GraphicsDeviceType.Null; //a ded. server has no graphics
        await launchInMode(isDedicatedServer);
    }

    private async Task launchInMode(bool isDedicatedServer)
    {
        if (isDedicatedServer)
        {
            // [FLUSSO 62] Ramo dedicated server: ancora uno stub vuoto ("sticazzi per ora").
            // Un dedicated server headless non deve autenticarsi come giocatore ne'
            // mostrare un menu: qui andra' in futuro l'avvio diretto della sessione
            // di rete lato server (es. HostGameManager, FLUSSO 81, ma in modalita' server-only).
        }
        else
        {
            // [FLUSSO 64] L'HostSingleton viene creato SEMPRE, anche per un client puro
            // che non ospitera' mai nessuno: e' preparazione per quando questa stessa
            // istanza dovesse diventare Host in seguito (es. premendo "Host" nel menu,
            // MainMenu.StartHost, FLUSSO 83). Spostato PRIMA del ramo client (FLUSSO 63,
            // sotto): createHost() e' sincrono e non dipende in alcun modo dall'esito
            // dell'autenticazione, quindi non ha senso farlo aspettare in mezzo a un
            // await. Cosi' hostSingleton.Instance (FLUSSO 78) e' pronto il prima possibile,
            // invece che solo dopo l'intero giro di rete dell'autenticazione client.
            HostSingleton hostSingleton = Instantiate(hostPrefab);
            hostSingleton.createHost();
            // [FLUSSO 63] Ramo client: si istanzia il prefab ClientSingleton e si
            // aspetta (await) l'intera procedura di autenticazione. "authenticated"
            // riflette il valore restituito da ClientGameManager.initAsync (FLUSSO 69),
            // che a sua volta dipende da AuthenticationWrapper.doAuth (FLUSSO 72-74).
            ClientSingleton clientSingleton = Instantiate(clientPrefab);
            bool authenticated = await clientSingleton.createClient();

            // [FLUSSO 65] Solo se l'autenticazione e' andata a buon fine si passa dalla
            // scena di bootstrap al menu vero e proprio (ClientGameManager.goToMenu,
            // FLUSSO 70). Se authenticated e' false (login fallito dopo i tentativi
            // previsti, FLUSSO 76), l'app resta bloccata qui: non c'e' ancora un
            // messaggio d'errore o un retry visibile per l'utente.
            if (authenticated)
            {
                clientSingleton.gameManager.goToMenu();
            }
        }
    }
}
