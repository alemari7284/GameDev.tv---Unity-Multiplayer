# Guida Multiplayer — Versione Facile (con il codice vero dentro)

> Questa è la STESSA guida di `GUIDA_MULTIPLAYER.md`, ma "gonfiata": sotto ogni spiegazione trovi
> il codice VERO copiato dal file, non solo il riassunto. Ci sono anche più esempi stupidi,
> frasi più corte, e ogni parola difficile viene spiegata subito, tra parentesi, la prima volta
> che compare.
>
> Se `GUIDA_MULTIPLAYER.md` è il "manuale tecnico", questo è il "manuale per un tuo amico che
> non ha mai sentito parlare di multiplayer e ha paura del codice".

---

## 0. Come leggere questo documento

Ogni riga importante del codice ha scritto sopra un commento tipo `// [FLUSSO 12]`.
`FLUSSO` vuol dire "flusso di esecuzione", cioè: **l'ordine in cui le cose succedono davvero**,
non l'ordine in cui i file compaiono nel progetto.

**Esempio stupidissimo**: immagina di girare un film. Le scene si girano in un ordine (magari il
finale prima dell'inizio, per comodità di set), ma lo SPETTATORE le vede in un altro ordine (1, 2,
3...). I numeri `FLUSSO` sono l'ordine dello spettatore: seguili in salita (0, 1, 2, 3...) e la
partita ti si racconta da sola come una storia, dall'accensione del gioco fino a quando una
moneta ricompare in un altro punto della mappa.

Due eccezioni da ricordare, altrimenti ti confondi:

1. `ClientNetworkTransform.cs` ha una numerazione TUTTA SUA (`FLUSSO 0` → `8`), diversa da quella
   del gameplay. È come un capitolo a parte che spiega "come si muove un oggetto in rete" — le
   altre parti del gioco lo richiamano come un pacchetto chiuso ("vedi FLUSSO 0-8 di quel file"),
   non c'entra con i numeri del resto.
2. `FLUSSO 60` → `81` (avvio dell'app + login) sono in realtà le prime cose che succedono
   quando apri il gioco — PRIMA ancora di `FLUSSO 0` — ma sono numerate "alte" solo perché sono
   state aggiunte dopo, e la regola del progetto dice "continua a numerare da dove eri arrivato,
   non tornare indietro a rinumerare tutto". Quindi: numero alto ≠ successo dopo. Qui, numero
   alto = scritto dopo, ma eseguito prima.

**Indice di questo documento**:

1. Concetti base di Netcode, spiegati come a un bambino (§1)
2. Avvio dell'app e login (`FLUSSO 60 → 81`) — cosa succede nell'istante in cui apri il gioco (§2)
3. Il vecchio sistema "Host/Join" con IP diretto, usato solo per test (§3)
4. Le fondamenta: come si sincronizza il movimento in rete (§4)
5. Il vero flusso di gioco (`FLUSSO 0 → 59`): input → mira → sparo → danno → vita → monete (§5)
6. Tabella riassuntiva di TUTTI i FLUSSO, per cercare velocemente (§6)
7. Ricette pronte da copiare in un gioco nuovo (§7)
8. Checklist mentale prima di scrivere codice di rete (§8)
9. Glossario (§9)

---

## 1. Concetti base — spiegati come a un bambino

Prima di leggere anche una sola riga di codice, devi avere in testa queste parole. Se non le
capisci, TUTTO il resto del documento ti sembrerà arabo.

| Concetto | Cos'è, in una frase | Esempio stupido #1 | Esempio stupido #2 |
|---|---|---|---|
| **NetworkManager** | L'oggetto che sa "chi è connesso" e accende Host/Server/Client. | Il centralino di un call center: smista le chiamate, ma non parla lui coi clienti. | Il portiere di un condominio: sa chi abita dove, ma non entra in casa tua a darti da mangiare. |
| **NetworkObject** | Un componente che dà a un GameObject un "codice a barre" unico, uguale su tutti i computer. | Il codice a barre su un pacco: se io e te guardiamo lo stesso codice, sappiamo di parlare dello STESSO pacco, anche se siamo in due negozi diversi. | Il numero di targa di un'auto: ovunque la vedi (davanti a casa tua o dall'altra parte della città), sai che è SEMPRE la stessa auto. |
| **NetworkBehaviour** | Uno script "consapevole della rete": sa dirti se questa copia del gioco è il server, un client, o il proprietario di quell'oggetto. | Un dipendente che sa sempre rispondere a "oggi lavoro come capo o come impiegato?". | Un attore che sa sempre se sta recitando la parte del "regista" o quella del "comparsa" in quella scena. |
| **NetworkVariable\<T\>** | Una variabile che si copia da sola su tutti i computer collegati. Di solito: solo il server può scriverla, tutti possono leggerla. | Una lavagna in classe: solo il maestro (server) ci scrive sopra, tutti gli alunni (client) la leggono. Se un alunno scrive sul SUO quaderno, la lavagna vera non cambia. | Il tabellone dei voli in aeroporto: solo lo staff (server) lo aggiorna, tu (client) lo guardi e basta. Non puoi cambiarlo scrivendoci sopra col dito. |
| **ServerRpc** | Una chiamata di funzione che parte da un client e viene ESEGUITA sul server. | Come compilare un modulo e imbucarlo in un ufficio: tu non fai l'azione, CHIEDI che la faccia l'ufficio. | Ordinare al ristorante: tu (client) dici "una pizza", ma è il cuoco (server) a farla davvero. Tu non entri in cucina. |
| **ClientRpc** | Una chiamata di funzione che parte dal server e viene eseguita su tutti i client (o alcuni). | Un annuncio alla radio: lo trasmette solo l'emittente (server), lo sentono tutti gli ascoltatori (client). | La campanella di scuola: la suona solo il bidello (server), ma la sentono TUTTE le classi (client) contemporaneamente. |
| **Server authority** ("il server comanda") | Il server è l'unica fonte di verità: decide se un'azione è valida. | L'arbitro di una partita a carte: un giocatore può DIRE "peschi", ma è l'arbitro a decidere se è il suo turno. | Il PIN del bancomat: tu proponi un numero, ma è la banca (server) a dire sì o no. Non decidi tu se il PIN è giusto. |
| **Client authority** | Un client specifico ha il permesso di decidere lui stesso un valore, di solito per farlo sembrare istantaneo. | Quando scrivi un messaggio in chat, le lettere appaiono sul TUO schermo mentre le premi, senza aspettare nessuno. | Quando disegni con un dito su un tablet: la linea appare SUBITO sotto il tuo dito, non un secondo dopo. |
| **Ownership / IsOwner** | Ogni oggetto di rete ha un "proprietario": di solito il giocatore che lo controlla. | Le chiavi di un'auto a noleggio: solo chi le ha in mano guida; gli altri la vedono muoversi ma non guidano loro. | Il tuo zaino a scuola: è tuo, lo apri tu. I compagni lo VEDONO sul tuo banco ma non ci mettono le mani dentro. |
| **Host** | Un'istanza che è CONTEMPORANEAMENTE server e client: gioca E allo stesso tempo arbitra. | Il tavolo dei giochi in casa tua: tu ospiti gli amici (sei il server) ma giochi anche tu (sei anche client). | Il maestro che gioca a dama CON la classe ma è anche lui a dire "hai sbagliato mossa": arbitro e giocatore nella stessa persona. |

> **Regola d'oro di TUTTO il progetto, da imparare a memoria**:
> **"il client mostra, il server decide."**
>
> Tradotto ancora più facile: quando vedi un giocatore fare qualcosa a schermo PRIMA che la rete
> abbia risposto (una moneta che sparisce subito, un proiettile che parte subito), è solo un
> trucco visivo, una specie di "controfigura". La verità vera, quella che conta per il punteggio
> e la vita, arriva sempre — anche solo un attimo dopo — dal server.

### 1.1 Come si scrivono queste cose nel codice (mini-esempio da zero)

Prima ancora di guardare il progetto vero, ecco la sintassi minima, scollegata da tutto, solo per
farti vedere come si scrivono queste parole in C#:

```csharp
using Unity.Netcode;

public class Esempio : NetworkBehaviour   // <- "consapevole della rete"
{
    // Solo il server può scriverla, tutti possono leggerla (lavagna in classe)
    public NetworkVariable<int> puntiVita = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)          // "sono il maestro?"
            puntiVita.Value = 100;
    }

    [ServerRpc]                // il client la chiama, ma gira sul server (ordina al ristorante)
    public void ChiediDiCurartiServerRpc()
    {
        puntiVita.Value = 100; // solo qui, sul server, ha senso modificarla davvero
    }

    [ClientRpc]                // il server la chiama, gira su TUTTI i client (campanella)
    public void SuonaCampanellaClientRpc()
    {
        Debug.Log("Din don! Lo dice il server a tutti quanti.");
    }
}
```

Tienilo a mente: da qui in poi vedrai sempre queste 4 parole (`NetworkVariable`, `[ServerRpc]`,
`[ClientRpc]`, `IsOwner`/`IsServer`/`IsClient`) ripetute in salsa diversa in ogni file del gioco.

---

## 2. Avvio dell'app e login (`FLUSSO 60 → 81`)

Questa è la PRIMA cosa che succede quando premi "Play": prima ancora del menu, prima ancora del
bottone "Host". Vive nella scena `NetBootstrap`, che si carica per prima di tutte.

> **Esempio stupido**: è il controllo documenti all'ingresso di un concerto, PRIMA della sala
> vera e propria. Se sei dello staff (dedicated server, cioè un computer che ospita la partita
> senza che nessuno ci giochi sopra) passi da un'altra porta. Se sei uno spettatore normale
> (giocatore) devi prima farti timbrare il biglietto (login anonimo) — solo dopo ti aprono la
> porta della sala (il Menu).
>
> **Ancora più stupido**: è come entrare in un parco divertimenti. Prima del primo giro sulle
> giostre (il Menu, la partita), c'è sempre la biglietteria (il login). Nessuno salta la fila.

**File coinvolti** (li vediamo uno per uno, con dentro il codice vero):

- `Assets/Scripts/Networking/ApplicationController.cs`
- `Assets/Scripts/Networking/Client/ClientSingleton.cs` + `ClientGameManager.cs`
- `Assets/Scripts/Networking/Client/AuthenticationWrapper.cs`
- `Assets/Scripts/Networking/Host/HostSingleton.cs` + `HostGameManager.cs`

### 2.1 `ApplicationController.cs` — il primo script che parte in assoluto

| FLUSSO | In parole facili facili |
|---|---|
| **60** | Ci sono due "modelli" di partenza (`clientPrefab` e `hostPrefab`): sono come due stampi da cui, più sotto, si crea o un client o un host. |
| **61** | Appena il gioco parte: "non distruggermi quando cambio scena" (`DontDestroyOnLoad`) e controllo se questo computer ha una scheda grafica. Se NON ce l'ha, è un dedicated server (un computer "cieco" che serve solo a ospitare la partita, come un server internet, non un giocatore). |
| **62** | Se è un dedicated server: per ora non facciamo niente (uno "sticazzi" vuoto, da riempire in futuro). |
| **64** | SEMPRE, anche se sei solo un giocatore normale: viene creato un `HostSingleton` (pronto nel caso tu debba diventare host più avanti, es. premendo "Host" nel menu). |
| **63** | Se NON è un dedicated server (cioè sei un giocatore vero): si crea un `ClientSingleton` e si aspetta che finisca il login (`createClient()`). |
| **65** | Solo se il login è andato bene, si va al Menu. Se fallisce, per ora il gioco resta bloccato lì, senza un messaggio d'errore (un difetto noto, da sistemare in futuro). |

**Il codice vero** (`Assets/Scripts/Networking/ApplicationController.cs`):

```csharp
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// [FLUSSO 60] I due prefab sono i "capostipiti" delle due catene di singleton:
// ClientSingleton (lato giocatore) e HostSingleton (lato host).
public class ApplicationController : MonoBehaviour
{
    [SerializeField] private ClientSingleton clientPrefab;
    [SerializeField] private HostSingleton hostPrefab;

    private async Task Start()
    {
        // [FLUSSO 61] DontDestroyOnLoad fa sopravvivere tutto al cambio scena verso il Menu.
        DontDestroyOnLoad(gameObject);

        // Un dedicated server non ha una GPU/finestra: è cosi' che distinguiamo
        // a runtime un build server (headless) da un client giocabile.
        bool isDedicatedServer = SystemInfo.graphicsDeviceType ==
            UnityEngine.Rendering.GraphicsDeviceType.Null; //a ded. server has no graphics
        await launchInMode(isDedicatedServer);
    }

    private async Task launchInMode(bool isDedicatedServer)
    {
        if (isDedicatedServer)
        {
            // [FLUSSO 62] Ramo dedicated server: ancora uno stub vuoto.
        }
        else
        {
            // [FLUSSO 63] L'HostSingleton viene creato SEMPRE, anche per un client puro,
            // per essere pronti se questa istanza dovesse diventare Host in seguito.
            HostSingleton hostSingleton = Instantiate(hostPrefab);
            hostSingleton.createHost();

            // [FLUSSO 64] Ramo client: si istanzia ClientSingleton e si aspetta
            // l'intera procedura di autenticazione.
            ClientSingleton clientSingleton = Instantiate(clientPrefab);
            bool authenticated = await clientSingleton.createClient();

            // [FLUSSO 65] Solo se autenticato, si passa al menu vero.
            if (authenticated)
            {
                clientSingleton.gameManager.goToMenu();
            }
        }
    }
}
```

> **Nota sull'ordine 64→63 nella tabella vs 63→64 nel codice**: nel codice, la creazione
> dell'host viene PRIMA del login del client (perché è un'operazione istantanea, non ha senso
> farla aspettare). Nella tabella li abbiamo lasciati nell'ordine "di significato" (63 = ramo
> client, 64 = host sempre creato). Non è un errore: è solo che il codice esegue prima la parte
> "veloce e senza attese" (host) e poi quella "lenta e con un `await`" (client).

### 2.2 `ClientSingleton.cs` + `ClientGameManager.cs` — il "borsone" del client

`ClientSingleton` è come un borsone vuoto messo in scena solo per portarsi dietro
`ClientGameManager`, che è pura logica C# (creata con `new`, non con `Instantiate`, quindi non
potrebbe esistere da sola come oggetto di scena).

**`ClientSingleton.cs`** (FLUSSO 66-68):

```csharp
public class ClientSingleton : MonoBehaviour
{
    // [FLUSSO 66] Pattern "singleton pigro": lo cerchiamo solo quando serve,
    // non subito in Awake.
    private static ClientSingleton instance;
    public static ClientSingleton Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = FindAnyObjectByType<ClientSingleton>();
            if (instance == null)
            {
                Debug.LogError("No ClientSingleton in the scene");
                return null;
            }
            return instance;
        }
    }

    public ClientGameManager gameManager { get; private set; }

    private void Start()
    {
        // [FLUSSO 67] Stesso motivo del FLUSSO 61: deve sopravvivere al cambio scena.
        DontDestroyOnLoad(gameObject);
    }

    // [FLUSSO 68] Crea il ClientGameManager e gli affida subito il login.
    public async Task<bool> createClient()
    {
        gameManager = new ClientGameManager();
        return await gameManager.initAsync();
    }
}
```

**`ClientGameManager.cs`** (FLUSSO 69-70):

```csharp
public class ClientGameManager
{
    private const string menuSceneName = "Menu";

    // [FLUSSO 69] UnityServices.InitializeAsync() va chiamata UNA volta per processo
    // prima di usare qualsiasi servizio online (login, Relay, ecc.).
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

    // [FLUSSO 70] Chiamato solo se il login è andato bene: carica la scena "Menu".
    public void goToMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }
}
```

> **Esempio stupido**: `ClientSingleton` è il portaborse che tiene in mano la valigetta
> (`ClientGameManager`) del vero impiegato (la logica di login). Il portaborse non fa nulla di
> intelligente da solo: serve solo perché la valigetta, da sola, in scena, non saprebbe stare
> in piedi.

### 2.3 `AuthenticationWrapper.cs` — il buttafuori del login

Questa classe è `static`: non esiste "un'istanza per ogni giocatore", esiste UNA SOLA copia per
tutto il gioco, condivisa da chiunque la usi.

```csharp
// [FLUSSO 71] Essendo la classe "static", authState e' un solo valore condiviso
// da tutto il processo, che sopravvive ai cambi scena.
public static class AuthenticationWrapper
{
    public static AuthState authState { get; private set; }

    // [FLUSSO 72] Prima guardia: se sei già autenticato, non rifare il login.
    public static async Task<AuthState> doAuth(int maxTries = 5)
    {
        if (authState == AuthState.Authenticated) return authState;

        // [FLUSSO 73] Seconda guardia: se un altro sta già facendo login,
        // non partire con un secondo tentativo in parallelo: aspetta e basta.
        if (authState == AuthState.Authenticating)
        {
            Debug.LogWarning("Already authenticating!");
            await authenticating();
            return authState;
        }

        // [FLUSSO 74] Nessun login in corso ne' gia' fatto: si parte per davvero.
        await SignInAnonimouslyAsync(maxTries);
        return authState;
    }

    // [FLUSSO 75] Attesa passiva: controlla ogni 200ms se il login e' finito,
    // invece di far partire un secondo tentativo.
    private static async Task<AuthState> authenticating()
    {
        while (authState == AuthState.Authenticating || authState == AuthState.NonAuthenticated)
        {
            await Task.Delay(200);
        }
        return authState;
    }

    // [FLUSSO 76] Vera logica di login: prova, ritenta, gestisce gli errori.
    private static async Task SignInAnonimouslyAsync(int maxRetries)
    {
        authState = AuthState.Authenticating;
        int retries = 0;

        while (authState == AuthState.Authenticating && retries < maxRetries)
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
                {
                    authState = AuthState.Authenticated;
                    break;
                }
            }
            catch (AuthenticationException ex)
            {
                Debug.LogError(ex);
                authState = AuthState.Error;
            }
            catch (RequestFailedException ex)
            {
                Debug.LogError(ex);
                authState = AuthState.Error;
            }
            retries++;
            await Task.Delay(1000);
        }

        if (authState != AuthState.Authenticated)
        {
            Debug.LogWarning($"Player wasn't signed in succesfully after {retries} retries");
            authState = AuthState.Timeout;
        }
    }

    // [FLUSSO 77] I 5 stati possibili. NonAuthenticated e' quello di partenza.
    public enum AuthState
    {
        NonAuthenticated,
        Authenticating,
        Authenticated,
        Error,
        Timeout
    }
}
```

**Il bug corretto, spiegato con parole facili**: prima, `doAuth` aveva un secondo pezzo di codice
quasi identico a `SignInAnonimouslyAsync`, ma DIMENTICAVA di scrivere
`authState = AuthState.Authenticating;` prima di iniziare il ciclo `while`. Risultato: la prima
volta che il gioco partiva, `authState` era ancora `NonAuthenticated`, il ciclo `while` diceva
subito "no, non entro" e il login non partiva MAI. È come cercare di entrare in un negozio ma la
porta è ancora chiusa perché nessuno ha girato la chiave: tu bussi (il `while`), ma non succede
niente, perché la condizione per entrare non si è mai avverata.

> **Esempio stupido sul login**: `doAuth` è un buttafuori con tre domande in testa, in ordine:
> "sei già dentro? (72)" → "c'è già qualcuno che sta entrando adesso? aspetta il suo turno (73)"
> → "nessuno dei due? allora prova ad entrare tu adesso (74)".

### 2.4 `HostSingleton.cs` + `HostGameManager.cs` — il gemello lato host

```csharp
public class HostSingleton : MonoBehaviour
{
    // [FLUSSO 78] Stesso pattern "singleton pigro" di ClientSingleton (FLUSSO 66).
    private static HostSingleton instance;
    public static HostSingleton Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = FindAnyObjectByType<HostSingleton>();
            if (instance == null)
            {
                Debug.LogError("No HostSingleton in the scene");
                return null;
            }
            return instance;
        }
    }

    // [FLUSSO 82] Proprieta' pubblica: serve a MainMenu.StartHost (FLUSSO 83)
    // per raggiungere HostGameManager dall'esterno.
    public HostGameManager GameManager { get; private set; }

    private void Start()
    {
        // [FLUSSO 79] Stesso motivo del FLUSSO 67.
        DontDestroyOnLoad(gameObject);
    }

    // [FLUSSO 80] Qui non c'e' ancora nessuna logica asincrona: HostGameManager
    // era vuoto, quindi ci si limita a istanziarlo.
    public void createHost()
    {
        GameManager = new HostGameManager();
    }
}
```

### 2.5 `MainMenu.cs` + `HostGameManager.StartHostAsync` — quando premi "Host" per davvero

Fino a qui `HostGameManager` era una scatola vuota. Da qui in poi, premendo "Host" nel menu, si
apre DAVVERO una partita, usando **Unity Relay** invece di un IP diretto.

> **Perché non un IP diretto?** Un IP diretto richiede che il tuo computer sia raggiungibile da
> internet — quasi mai vero (router, firewall, NAT di mezzo). Relay fa da "postino neutrale": sia
> tu (host) che i tuoi amici (client) parlate CON LUI, mai direttamente tra voi. In cambio di un
> pizzico di ritardo in più, funziona ovunque senza configurare nulla.
>
> **Esempio stupido**: è come prenotare un tavolo al ristorante tramite un centralino telefonico
> invece di dare il tuo indirizzo di casa agli invitati. Il centralino ti dà un numero di
> prenotazione (il join code) da passare agli amici; nessuno deve sapere dove abiti davvero.

**`MainMenu.cs`**:

```csharp
public class MainMenu : MonoBehaviour
{
    // [FLUSSO 83] Metodo agganciato all'OnClick del bottone "Host" nella scena Menu.
    // "async void" va bene SOLO qui, perche' e' un event handler UI: nessuno
    // "aspetta" il completamento.
    public async void StartHost()
    {
        await HostSingleton.Instance.GameManager.StartHostAsync();
    }
}
```

**`HostGameManager.cs`** — la parte più interessante di tutto il capitolo 2:

```csharp
public class HostGameManager
{
    // [FLUSSO 84] Stato della sessione Relay corrente.
    private Allocation allocation;
    private string joinCode;
    private const string gameSceneName = "Game";
    private const int maxConnections = 20;

    public async Task StartHostAsync()
    {
        // [FLUSSO 85] Si chiede ai server Relay di Unity di riservare posto per
        // fino a maxConnections giocatori. E' una chiamata di rete: puo' fallire.
        try
        {
            allocation = await Relay.Instance.CreateAllocationAsync(maxConnections);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return;
        }

        // [FLUSSO 86] Trasforma l'allocation in un codice breve, condivisibile
        // con gli amici (per ora solo loggato, non ancora mostrato a schermo).
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

        // [FLUSSO 87] Si "istruisce" il transport di rete a passare da Relay.
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData relayServerData = new RelayServerData(allocation, "udp");
        transport.SetRelayServerData(relayServerData);

        // [FLUSSO 88] Solo ORA si avvia davvero: questa istanza diventa Host
        // (server + client insieme).
        NetworkManager.Singleton.StartHost();

        // [FLUSSO 89] Cambio scena "di rete": porta con se' anche i client
        // gia' connessi verso la scena "Game".
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}
```

Diagramma (uguale a quello della guida originale, per riferimento visivo veloce):

```
Giocatore preme "Host" nel Menu
      |
MainMenu.StartHost (83)
      |
HostSingleton.Instance.GameManager.StartHostAsync()
      |
      v
CreateAllocationAsync (85) --------> [server Relay di Unity]
      |  riserva risorse per maxConnections giocatori
      v
GetJoinCodeAsync (86) --------------> joinCode (per ora solo loggato)
      |
      v
transport.SetRelayServerData (87)   <- il NetworkManager ora "sa" passare da Relay
      |
      v
NetworkManager.Singleton.StartHost() (88)   <- questa istanza è Host: server + client
      |
      v
NetworkManager.Singleton.SceneManager.LoadScene("Game") (89)   <- tutti i client connessi seguono
```

**Cosa manca ancora**: un bottone "Join" nel Menu che chieda il `joinCode` e lo passi a un
`ClientGameManager.StartClientAsync` (che oggi non esiste ancora), usando `JoinAllocationAsync`
al posto di `CreateAllocationAsync` (vedi §7.10 per un esempio già pronto da copiare).

---

## 3. Il vecchio sistema di test — `ConnectionButtons.cs`

Questo file NON è più collegato a nessun bottone reale nelle scene attuali (il bottone "Host" del
Menu oggi passa da Relay, §2.5). Resta nel progetto come "rete di sicurezza": se un giorno vuoi
testare la sincronizzazione senza passare da internet, con due finestre Unity aperte sullo stesso
computer, questo file ti basta.

```csharp
public class ConnectionButtons : MonoBehaviour
{
    // Avvia l'istanza come Host: agisce contemporaneamente da server e da client.
    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
    }

    // Avvia l'istanza come Client puro: si connette a un Host/server esistente.
    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
    }
}
```

> **Esempio stupido**: per testare in locale, apri due copie del gioco (due finestre): una preme
> "Host" (apre la partita), l'altra preme "Join" (si siede al tavolo). Se ne apri una terza e
> preme "Join", si aggiunge un terzo giocatore. Non c'è nessuna sicurezza, nessun indirizzo da
> digitare: funziona solo perché sono sullo stesso computer, quindi si "vedono" da soli.

---

## 4. Le fondamenta: sincronizzare un movimento in rete — `ClientNetworkTransform.cs`

File: `Assets/Scripts/Utils/ClientNetworkTransform.cs`.
Va messo sui prefab **Player**, **Treads** (i cingoli) e **TurretPivot** (la torretta), al posto
del `NetworkTransform` normale di Unity.

### Il problema che risolve

Il `NetworkTransform` normale è **server-authoritative** (decide tutto il server): sicuro contro
i trucchi (es. teletrasportarsi), ma LENTO da usare, perché ogni movimento deve fare avanti e
indietro fino al server prima di essere visibile — sembra di guidare un'auto "gommosa", con un
ritardo fastidioso tra quando premi il tasto e quando l'auto si muove davvero.

`ClientNetworkTransform` capovolge la regola SOLO per il proprio tank: il proprietario scrive
DIRETTAMENTE il proprio movimento, il server lo riceve e lo passa agli altri. Zero ritardo per chi
guida, in cambio di un rischio accettabile (un giocatore disonesto potrebbe barare col teleport).

> **Esempio stupido**: è la differenza tra scrivere su una lavagna condivisa passando SEMPRE dal
> preside (lento, ma sicuro) e avere il permesso di scrivere direttamente sul TUO angolo di
> lavagna (veloce, ma se sei disonesto ci scrivi cose false).
>
> **Ancora più stupido**: guidare un'auto vera vs guidare un'auto radiocomandata con un secondo di
> ritardo nel telecomando. L'auto vera (client-authoritative) risponde subito al volante. L'auto
> radiocomandata (server-authoritative) ha sempre quel fastidioso mezzo secondo di ritardo tra
> "giro il volante" e "l'auto gira davvero".

### Il codice vero, riga per riga

```csharp
using Unity.Netcode.Components;
using UnityEngine;

public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        // [FLUSSO 0] Diciamo a Netcode: "l'authority NON e' del server, e' del client owner".
        // E' questa unica riga a trasformare un NetworkTransform normale in uno
        // client-authoritative.
        return false;
    }

    public override void OnNetworkSpawn()
    {
        // [FLUSSO 1] Si chiama prima il comportamento standard, per non romperlo.
        base.OnNetworkSpawn();

        // [FLUSSO 2] "Posso scrivere/inviare il mio transform?" = "sono il proprietario?"
        CanCommitToTransform = IsOwner;
    }

    protected override void Update()
    {
        // [FLUSSO 3] Ricalcolato OGNI FRAME, non solo allo spawn: resta corretto
        // anche se la proprieta' dell'oggetto cambiasse durante la partita.
        CanCommitToTransform = IsOwner;

        // [FLUSSO 4] Se sono owner: applico lo stato locale.
        // Se NON lo sono: INTERPOLO verso i valori ricevuti dalla rete (movimento fluido).
        base.Update();

        // [FLUSSO 5] Guard: prima dello spawn NetworkManager potrebbe essere nullo.
        if (NetworkManager != null)
        {
            // [FLUSSO 6] Invio solo se sono davvero "in rete".
            if (NetworkManager.IsConnectedClient || NetworkManager.IsListening)
            {
                // [FLUSSO 7] Solo il proprietario arriva fin qui.
                if (CanCommitToTransform)
                {
                    // [FLUSSO 8] Mando al server il MIO transform, con un timestamp
                    // che serve agli altri per interpolare bene nel tempo.
                    TryCommitTransformToServer(transform, NetworkManager.LocalTime.Time);
                }
            }
        }
    }
}
```

```
Owner del tank                Server                     Altri client
     |  muove localmente        |                             |
     |  (nessun lag: è suo)     |                             |
     |------ transform+time --->|                             |
     |                          |------ sincronizza --------->|
     |                          |                              |  interpola
     |                          |                              |  (FLUSSO 4, ramo "else")
```

Lo stesso script viene riusato pari pari per **TurretPivot** (che sincronizza solo la rotazione)
e **Treads** (i cingoli): stessa identica logica, cambia solo QUALE asse viene sincronizzato,
impostato nell'Inspector di Unity, non nel codice.

---

## 5. Il flusso di gioco vero e proprio (`FLUSSO 0 → 59`)

Qui seguiamo l'ordine cronologico di una singola partita: premi un tasto → la torretta mira → spari
→ il proiettile fa danno → la vita scende → raccogli/spendi monete.

### 5.1 Input del giocatore — `InputReader.cs` (FLUSSO 0 → 7c)

`InputReader` è uno **ScriptableObject**, cioè un file/asset condiviso, NON un componente
attaccato a un oggetto della scena. Chiunque nel gioco può "collegarsi" a lui per sapere cosa sta
premendo il giocatore, senza dover gestire da solo l'Input System di Unity.

> **Esempio stupido**: `InputReader` è il telecomando universale di casa. Premi un tasto una
> volta sola, e chiunque sia "sintonizzato" (movimento, mira, sparo) riceve il segnale, senza che
> il telecomando sappia o si preoccupi di chi lo sta ascoltando.
>
> **Ancora più stupido**: è come il gruppo WhatsApp della classe. Il prof (input fisico: tastiera,
> mouse) manda UN messaggio nel gruppo. Tutti gli alunni iscritti (script del gioco) lo leggono
> nello stesso momento, ognuno reagisce a modo suo. Il prof non sa nemmeno chi legge il messaggio.

```csharp
using UnityEngine.InputSystem;
// [FLUSSO 0] "Controls" e' la classe GENERATA AUTOMATICAMENTE dall'asset
// Controls.inputactions: non la scriviamo noi a mano.
using static Controls;

// [FLUSSO 1] Implementando IPlayerActions ci "impegniamo per contratto" a fornire
// OnMove, OnPrimaryFire, OnAim. Sara' l'Input System a chiamarli.
[CreateAssetMenu(fileName = "InputReader", menuName = "Input/Input Reader")]
public class InputReader : ScriptableObject, IPlayerActions
{
    // [FLUSSO 2] La nostra istanza runtime della classe generata.
    private Controls controls;

    // [FLUSSO 3] Eventi = il "megafono" verso il resto del gioco.
    public event Action<bool> PrimaryFireEvent;
    public event Action<Vector2> MoveEvent;

    // [FLUSSO 3b] La mira NON e' un evento ma una proprieta' "sempre leggibile":
    // il mouse cambia posizione di continuo, e chi mira legge sempre l'ultimo valore.
    public Vector2 AimPosition { get; private set; }

    private void OnEnable()
    {
        // [FLUSSO 4] Creiamo Controls solo se non esiste gia'.
        if (controls == null)
        {
            controls = new Controls();

            // [FLUSSO 5] Colleghiamo i nostri metodi (OnMove, OnPrimaryFire, OnAim)
            // a tutte le fasi delle azioni della action map "Player".
            controls.Player.SetCallbacks(this);
        }

        // [FLUSSO 6] Senza questa riga, nessuna callback scatterebbe mai.
        controls.Enable();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        // [FLUSSO 7a] Ad ogni cambio dell'azione "Move", rilanciamo il valore come evento.
        MoveEvent?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnPrimaryFire(InputAction.CallbackContext context)
    {
        // [FLUSSO 7b] "performed" = tasto premuto, "canceled" = tasto rilasciato.
        if (context.performed)
        {
            PrimaryFireEvent?.Invoke(true);
        }
        else if (context.canceled)
        {
            PrimaryFireEvent?.Invoke(false);
        }
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        // [FLUSSO 7c] Qui NON solleviamo un evento: salviamo solo l'ultima posizione
        // del mouse, che PlayerAiming leggera' da sola ogni frame.
        AimPosition = context.ReadValue<Vector2>();
    }
}
```

**Perché conviene fare così**: se domani cambi dispositivo (gamepad, touch), tocchi SOLO
`InputReader`. Tutto il resto del gioco non si accorge di nulla, perché dipende solo dagli eventi
(`MoveEvent`, `PrimaryFireEvent`) e non dai tasti fisici veri. È come cambiare il telecomando di
casa senza dover ri-programmare tutti gli elettrodomestici.

### 5.2 Mira della torretta — `PlayerAiming.cs` (FLUSSO 8 → 13)

```csharp
public class PlayerAiming : NetworkBehaviour
{
    // [FLUSSO 8] Riferimenti da Inspector.
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform turretTransform;

    // [FLUSSO 9] LateUpdate (non Update!): la mira va calcolata DOPO che il corpo
    // si e' gia' mosso, altrimenti la torretta punterebbe al tank di un frame prima.
    private void LateUpdate()
    {
        // [FLUSSO 10] Solo il proprietario decide dove punta la propria torretta.
        if (!IsOwner) return;

        // [FLUSSO 11] Posizione del mouse in coordinate SCHERMO (pixel).
        Vector2 aimScreenPos = inputReader.AimPosition;

        // [FLUSSO 12] Convertita in coordinate MONDO, per confrontarla con la torretta.
        Vector2 aimWorldPos = Camera.main.ScreenToWorldPoint(aimScreenPos);

        // [FLUSSO 13] Orientiamo la torretta verso il mouse.
        turretTransform.up = aimWorldPos - (Vector2)turretTransform.position;
    }
}
```

> **Esempio stupido**: è come un girasole che gira sempre verso il sole (il mouse). Non importa
> dove sia il resto della pianta (il corpo del tank): la testa (la torretta) trova sempre il modo
> di puntare nella direzione giusta.
>
> **Ancora più stupido**: è la freccia di una bussola che punta sempre al Nord, qualunque cosa tu
> faccia col resto del corpo della bussola.

### 5.3 Sparo — `ProjectileLauncher.cs` (FLUSSO 14 → 21, + 29)

Qui c'è IL pattern più importante di tutto il progetto: **due proiettili per ogni sparo**.

- `serverProjectilePrefab` → il proiettile **vero**, esiste SOLO sul server, fa danno reale.
- `clientProjectilePrefab` → un proiettile **finto** ("dummy"), solo grafico, mostrato subito in
  locale per far sembrare lo sparo istantaneo.

> **Esempio stupido**: un mago che finge di sparare a salve per l'effetto scenico immediato
> (il dummy, che vedi subito e sembra vero), mentre il "colpo vero" viene deciso ed eseguito dal
> regista dietro le quinte (il server) un attimo dopo, senza che tu te ne accorga.
>
> **Ancora più stupido**: quando tiri un sasso in uno stagno, VEDI subito lo splash (il dummy,
> istantaneo, solo grafica). Ma se stessi giocando a un gioco da tavolo con arbitro, il punto
> vero verrebbe assegnato solo quando l'arbitro (server) conferma "sì, hai colpito il bersaglio".

```csharp
public class ProjectileLauncher : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;
    // [FLUSSO 56] Riferimento al portafoglio del proprietario (costo dello sparo).
    [SerializeField] private CoinWallet wallet;
    [SerializeField] private Transform projectileSpawnPoint;
    // [FLUSSO 14] Due prefab diversi per lo stesso sparo (vedi sopra).
    [SerializeField] private GameObject serverProjectilePrefab;
    [SerializeField] private GameObject clientProjectilePrefab;
    [SerializeField] private GameObject muzzleFlash;
    [SerializeField] private Collider2D playerCollider;

    [Header("Settings")]
    [SerializeField] private float projectileSpeed;
    [SerializeField] private float fireRate;
    [SerializeField] private float muzzleFlashDuration;
    // [FLUSSO 56b] Quante monete costa ogni sparo.
    [SerializeField] private int costToFire;

    private bool shouldFire;
    // [FLUSSO 57] Cooldown tra due spari: si scala ogni frame, si ricarica dopo aver sparato.
    private float timer;
    private float muzzleFlashTimer;

    public override void OnNetworkSpawn()
    {
        // [FLUSSO 15] Solo il proprietario si iscrive al proprio input.
        if (!IsOwner) return;
        inputReader.PrimaryFireEvent += HandlePrimaryFire;
    }

    override public void OnNetworkDespawn()
    {
        // [FLUSSO 16] Disiscrizione a specchio del FLUSSO 15.
        if (!IsOwner) return;
        inputReader.PrimaryFireEvent -= HandlePrimaryFire;
    }

    void Update()
    {
        if (muzzleFlashTimer > 0)
        {
            muzzleFlashTimer -= Time.deltaTime;
        }
        else
        {
            muzzleFlash.SetActive(false);
        }

        if (!IsOwner) return;

        if (timer > 0) timer -= Time.deltaTime;

        if (!shouldFire) return;

        if (timer > 0) return;

        // [FLUSSO 57b] Controllo COSMETICO lato client: non e' autorevole, serve
        // solo a non sprecare rete se sappiamo gia' che non abbastanza monete.
        if (wallet.totalCoins.Value < costToFire) return;

        // [FLUSSO 17] Sparo su due binari: chiediamo al server il proiettile vero
        // E mostriamo SUBITO in locale il dummy. La ServerRpc non blocca: ritorna
        // subito, per questo il dummy sembra apparire "nello stesso istante".
        PrimaryFireServerRpc(projectileSpawnPoint.position, projectileSpawnPoint.up);
        SpawnDummyProjectile(projectileSpawnPoint.position, projectileSpawnPoint.up);
        timer = 1 / fireRate;
    }

    [ServerRpc]
    private void PrimaryFireServerRpc(Vector2 spawnPos, Vector2 direction)
    {
        // [FLUSSO 18] Eseguito SOLO sul server.
        // [FLUSSO 58] Controllo AUTOREVOLE: ripete la stessa verifica del client
        // (57b), perche' quella era solo un'ottimizzazione, non una garanzia.
        if (wallet.totalCoins.Value < costToFire) return;

        wallet.spendCoins(costToFire);

        GameObject projectileInstance = Instantiate(
            serverProjectilePrefab,
            spawnPos,
            Quaternion.identity);

        projectileInstance.transform.up = direction;
        Physics2D.IgnoreCollision(playerCollider, projectileInstance.GetComponent<Collider2D>());

        // [FLUSSO 29] Il proiettile deve sapere chi lo ha sparato, per non colpire
        // il proprio proprietario (vedi DealDamageOnContact).
        if (projectileInstance.TryGetComponent<DealDamageOnContact>(out DealDamageOnContact dealDamage))
        {
            dealDamage.setOwnerClientId(OwnerClientId);
        }

        if (projectileInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.velocity = rb.transform.up * projectileSpeed;
        }
        SpawnDummyProjectileClientRpc(spawnPos, direction);
    }

    [ClientRpc]
    private void SpawnDummyProjectileClientRpc(Vector2 spawnPos, Vector2 direction)
    {
        // [FLUSSO 19] Eseguito su TUTTI i client. Il proprietario si esclude
        // (ha gia' mostrato il suo dummy al FLUSSO 17): questa callback serve
        // solo agli ALTRI client.
        if (IsOwner) return;
        SpawnDummyProjectile(spawnPos, direction);
    }

    private void HandlePrimaryFire(bool shouldFire)
    {
        // [FLUSSO 20] Aggiorna solo il flag letto dal FLUSSO 17.
        this.shouldFire = shouldFire;
    }

    private void SpawnDummyProjectile(Vector2 spawnPos, Vector2 direction)
    {
        muzzleFlash.SetActive(true);
        muzzleFlashTimer = muzzleFlashDuration;

        // [FLUSSO 21] Helper condiviso: istanzia solo l'effetto visivo, senza danno.
        GameObject projectileInstance = Instantiate(
            clientProjectilePrefab,
            spawnPos,
            Quaternion.identity);

        projectileInstance.transform.up = direction;
        Physics2D.IgnoreCollision(playerCollider, projectileInstance.GetComponent<Collider2D>());

        if (projectileInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.velocity = rb.transform.up * projectileSpeed;
        }
    }
}
```

```
Client (owner)                          Server                       Altri client
   | preme fuoco                          |                              |
   | mostra dummy locale (17,21) ---------+------------------------------|  (istantaneo, nessuna attesa)
   | invia PrimaryFireServerRpc --------->|                              |
   |                                      | spawna proiettile VERO (18)  |
   |                                      | invia ClientRpc ------------>|
   |                                      |                              | mostra il proprio dummy (19,21)
```

### 5.4 Fine vita dei proiettili — `DestroySelfOnContact.cs` + `Lifetime.cs` (FLUSSO 22 → 23)

```csharp
// Assets/Scripts/Utils/DestroySelfOnContact.cs
public class DestroySelfOnContact : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // [FLUSSO 22] Il proiettile "vero" del server si autodistrugge al primo
        // contatto. Essendo il server ad averlo istanziato, e' autorevole.
        if (collision.gameObject.layer == LayerMask.NameToLayer("Projectile")) return;
        Destroy(gameObject);
    }
}
```

```csharp
// Assets/Scripts/Utils/Lifetime.cs
public class Lifetime : MonoBehaviour
{
    [SerializeField] private float lifetime;

    void Start()
    {
        // [FLUSSO 23] Rete di sicurezza per entrambi i tipi di proiettile:
        // se non colpiscono nulla entro "lifetime" secondi, si autodistruggono comunque.
        Destroy(gameObject, lifetime);
    }
}
```

> **Esempio stupido**: un palloncino che scoppia se tocca uno spillo (FLUSSO 22), ma che comunque
> si sgonfia da solo dopo un minuto anche se non tocca niente (FLUSSO 23) — così non resta a
> fluttuare in scena per sempre, sprecando memoria del computer.

### 5.5 Movimento del corpo — `PlayerMovement.cs` (FLUSSO 24 → 28)

```csharp
public class PlayerMovement : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private Rigidbody2D rb;

    [Header("Settings")]
    [SerializeField] private float movementSpeed;
    // [FLUSSO 24b] Gradi al secondo massimi. Con input parziale, la rotazione scala
    // proporzionalmente (FLUSSO 26).
    [SerializeField] private float turningRate;
    private Vector2 previousMovementInput;

    // [FLUSSO 24] OnNetworkSpawn/OnNetworkDespawn, non Start/OnDestroy: sono
    // troppo presto/tardi nel ciclo di vita di rete. Solo il proprietario reagisce.
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        inputReader.MoveEvent += handleMove;
    }

    // [FLUSSO 25] Disiscrizione a specchio del FLUSSO 24.
    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        inputReader.MoveEvent -= handleMove;
    }

    private void Update()
    {
        if (!IsOwner) return;

        // [FLUSSO 26] La rotazione va in Update (visiva, ad ogni frame renderizzato),
        // moltiplicata per Time.deltaTime perche' Update non gira a intervalli fissi.
        float zRotation = previousMovementInput.x * -turningRate * Time.deltaTime;
        bodyTransform.Rotate(0, 0, zRotation);
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        // [FLUSSO 27] Il movimento vero passa dal Rigidbody2D, quindi va nel
        // FixedUpdate (fisica). "bodyTransform.up" fa avanzare il tank sempre
        // "in avanti" rispetto a come e' ruotato in quel momento.
        rb.velocity = (Vector2)bodyTransform.up * previousMovementInput.y * movementSpeed;
    }

    // [FLUSSO 28] Aggiorna solo l'input memorizzato, letto da Update/FixedUpdate.
    private void handleMove(Vector2 movementInput)
    {
        previousMovementInput = movementInput;
    }
}
```

> **Esempio stupido**: sterzare un'auto. Giri il volante (Update, effetto visivo immediato) e
> intanto l'auto avanza nel motore fisico (FixedUpdate) sempre nella direzione in cui è puntato
> il muso — non magicamente verso nord, come farebbe se usassi gli assi del mondo invece di
> "avanti rispetto a me stesso".
>
> **Ancora più stupido**: è come un carrello della spesa che spingi sempre "davanti a te", non
> "verso l'uscita del supermercato" a prescindere da dove sei girato tu.

### 5.6 Danno da contatto — `DealDamageOnContact.cs` (FLUSSO 29 → 34)

Presente **solo** sul proiettile vero (`serverProjectilePrefab`): esiste solo sul server, quindi
tutta questa logica gira per forza solo lì.

```csharp
public class DealDamageOnContact : MonoBehaviour
{
    [SerializeField] private int damage = 5;
    private ulong ownerClientId;

    // [FLUSSO 31] Chiamato dal server subito dopo lo spawn (FLUSSO 29), per ricordare
    // chi ha sparato.
    public void setOwnerClientId(ulong clientId)
    {
        ownerClientId = clientId;
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        // [FLUSSO 32] Se il bersaglio non ha un Rigidbody2D, non e' valido (es. un muro).
        if (collider.attachedRigidbody == null) return;

        if (collider.attachedRigidbody.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            // [FLUSSO 33] Non ci si può ferire da soli.
            if (ownerClientId == netObj.OwnerClientId) return;
        }

        // [FLUSSO 34] Se il bersaglio ha vita (Health), gli infliggiamo danno.
        if (collider.attachedRigidbody.TryGetComponent<Health>(out Health health))
        {
            health.takeDamage(damage);
        }
    }
}
```

> **Esempio stupido**: appena esce dalla canna, un proiettile non può tornare indietro e colpire
> chi l'ha sparato — sarebbe come un boomerang impazzito che ferisce il lanciatore, cosa che
> nessun gioco vorrebbe davvero.

### 5.7 Vita e barra vita — `Health.cs` + `HealthDisplay.cs` (FLUSSO 35 → 40)

```csharp
// Assets/Scripts/Core/Combat/Health.cs
public class Health : NetworkBehaviour
{
    [field: SerializeField] public int MaxHealth { get; private set; } = 100;
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>();

    private bool isDead;
    public Action<Health> OnDie;

    public override void OnNetworkSpawn()
    {
        // [FLUSSO 39] Solo il server inizializza la vita: e' lui l'unico autorevole.
        if (!IsServer) return;
        currentHealth.Value = MaxHealth;
    }

    public void takeDamage(int damageValue) => modifyHealth(-damageValue);
    public void restoreHealth(int healValue) => modifyHealth(healValue);

    private void modifyHealth(int value)
    {
        // [FLUSSO 40] Nessun controllo IsServer esplicito: il metodo va chiamato
        // solo da codice gia' server-side. Anche per errore, Netcode rifiuterebbe
        // comunque la scrittura non autorizzata sulla NetworkVariable.
        if (isDead) return;

        int newHealth = currentHealth.Value + value;
        currentHealth.Value = Mathf.Clamp(newHealth, 0, MaxHealth);

        if (currentHealth.Value == 0)
        {
            OnDie?.Invoke(this);
            isDead = true;
        }
    }
}
```

```csharp
// Assets/Scripts/Core/Combat/HealthDisplay.cs
public class HealthDisplay : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private Image healthBarImage;

    public override void OnNetworkSpawn()
    {
        // [FLUSSO 36] IsClient (non IsOwner, non IsServer): OGNI client deve vedere
        // la barra vita aggiornata, anche guardando i tank altrui.
        if (!IsClient) return;

        health.currentHealth.OnValueChanged += handleHealthChanged;
        handleHealthChanged(0, health.currentHealth.Value); // inizializza la barra
    }

    public override void OnNetworkDespawn()
    {
        // [FLUSSO 37] Disiscrizione a specchio del FLUSSO 36.
        if (!IsClient) return;
        health.currentHealth.OnValueChanged -= handleHealthChanged;
    }

    private void handleHealthChanged(int oldHealth, int newHealth)
    {
        // [FLUSSO 38] fillAmount va da 0 a 1: normalizziamo sul massimo.
        healthBarImage.fillAmount = (float)newHealth / health.MaxHealth;
    }
}
```

> **Esempio stupido**: l'indicatore di carburante in macchina. Solo il benzinaio (server) può
> davvero cambiare quanto carburante c'è nel serbatoio; la lancetta sul cruscotto
> (`HealthDisplay`) si limita a MOSTRARE il livello vero, non decide lei quanta benzina c'è.
>
> **Ancora più stupido**: è come il termometro digitale in salotto. Il termometro (client) MOSTRA
> la temperatura, ma non è lui a decidere che tempo fa fuori (server = il meteo vero).

### 5.8 Sistema monete — `Coin.cs`, `CoinWallet.cs`, `RespawningCoin.cs`, `CoinSpawner.cs` (FLUSSO 41 → 54)

Questa è la catena più lunga: riassume TUTTI i pattern visti finora insieme (server authority,
feedback client immediato, eventi, respawn).

```csharp
// Assets/Scripts/Core/Coins/Coin.cs — classe base astratta
public abstract class Coin : NetworkBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    protected int coinValue = 10;
    protected bool alreadyCollected;

    // [FLUSSO 45] Il valore di ritorno conta solo se calcolato dal server:
    // le sottoclassi devono restituire 0 se eseguite su un client.
    public abstract int collect();

    public void setValue(int value) => coinValue = value;
    protected void showCoin(bool show) => spriteRenderer.enabled = show;
}
```

```csharp
// Assets/Scripts/Core/Coins/CoinWallet.cs
public class CoinWallet : NetworkBehaviour
{
    public NetworkVariable<int> totalCoins = new NetworkVariable<int>();

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.TryGetComponent<Coin>(out Coin coin))
        {
            // [FLUSSO 41] Il trigger avviene in locale su OGNI client. Chiamiamo
            // collect() ovunque: sul server e' autorevole, sui client serve solo
            // a dare un feedback visivo immediato.
            int coinValue = coin.collect();

            // [FLUSSO 44] Solo il server può aggiornare totalCoins.
            if (!IsServer) return;
            totalCoins.Value += coinValue;
        }
    }

    // [FLUSSO 59] Chiamato solo dal server, dopo aver gia' verificato che ci
    // fossero abbastanza monete: sottrae il costo dello sparo.
    public void spendCoins(int costToFire)
    {
        totalCoins.Value -= costToFire;
    }
}
```

```csharp
// Assets/Scripts/Core/Coins/RespawningCoin.cs
public class RespawningCoin : Coin
{
    // [FLUSSO 46] Sollevato solo quando collect() gira lato server (FLUSSO 43):
    // avvisa il CoinSpawner di doverla riposizionare.
    public event Action<RespawningCoin> onCollected;

    // [FLUSSO 47] Posizione dell'ultimo frame, per rilevare un teleport (FLUSSO 54).
    private Vector3 previousPosition;

    public override int collect()
    {
        // [FLUSSO 42] Ramo CLIENT: nasconde localmente per feedback immediato,
        // ritorna sempre 0 (non e' autorevole, non decide nulla di vero).
        if (!IsServer)
        {
            showCoin(false);
            return 0;
        }

        // [FLUSSO 43] Ramo SERVER: controllo autorevole, evita di accreditare due volte.
        if (alreadyCollected) return 0;
        else
        {
            alreadyCollected = true;
            // [FLUSSO 51] Notifica il CoinSpawner.
            onCollected?.Invoke(this);
            return coinValue;
        }
    }

    // [FLUSSO 53] Chiamato dal CoinSpawner dopo aver ricollocato la moneta.
    public void Reset()
    {
        alreadyCollected = false;
    }

    private void Update()
    {
        // [FLUSSO 54] Se la posizione e' cambiata rispetto al frame prima, e' il
        // segnale che il server l'ha rispawnata altrove: la rimostriamo (showCoin(true)),
        // perche' la visibilita' dello SpriteRenderer NON e' sincronizzata dalla rete,
        // solo la posizione lo e'.
        if (previousPosition != transform.position)
        {
            showCoin(true);
        }

        previousPosition = transform.position;
    }
}
```

```csharp
// Assets/Scripts/Core/Coins/CoinSpawner.cs
public class CoinSpawner : NetworkBehaviour
{
    [SerializeField] private RespawningCoin coinPrefab;
    [SerializeField] private int maxCoins = 50;
    [SerializeField] private int coinValue = 10;
    [SerializeField] private Vector2 xSpawnRange;
    [SerializeField] private Vector2 ySpawnRange;
    [SerializeField] private LayerMask layerMask;
    private Collider2D[] coinBuffer = new Collider2D[1];
    private float coinRadius;

    public override void OnNetworkSpawn()
    {
        // [FLUSSO 49] Solo il server decide dove spawnare le monete.
        if (!IsServer) return;

        coinRadius = coinPrefab.GetComponent<CircleCollider2D>().radius;

        for (int i = 0; i < maxCoins; i++)
        {
            spawnCoin();
        }
    }

    private void spawnCoin()
    {
        RespawningCoin coinInstance = Instantiate(
            coinPrefab,
            getSpawnPoint(),
            Quaternion.identity);

        coinInstance.setValue(coinValue);
        coinInstance.GetComponent<NetworkObject>().Spawn();

        // [FLUSSO 50] Ci iscriviamo a onCollected di QUESTA istanza specifica.
        coinInstance.onCollected += handleCoinCollected;
    }

    private void handleCoinCollected(RespawningCoin coin)
    {
        // [FLUSSO 52] Sposta la moneta invece di distruggerla: la nuova posizione
        // si propaga da sola ai client tramite NetworkTransform.
        coin.transform.position = getSpawnPoint();
        coin.Reset();
    }

    private Vector2 getSpawnPoint()
    {
        float x = 0;
        float y = 0;
        while (true)
        {
            x = Random.Range(xSpawnRange.x, xSpawnRange.y);
            y = Random.Range(ySpawnRange.x, ySpawnRange.y);
            Vector2 spawnPoint = new Vector2(x, y);
            int numColliders = Physics2D.OverlapCircleNonAlloc(spawnPoint, coinRadius, coinBuffer, layerMask);
            if (numColliders == 0)
                return spawnPoint;
        }
    }
}
```

```
CoinSpawner (server)                RespawningCoin                     CoinWallet (ogni client)
      |  spawna N monete (48-50)         |                                    |
      |  si iscrive a onCollected  ------>|                                    |
      |                                   |            <---- trigger 2D -------|  (41, su OGNI client)
      |                                   |  ramo CLIENT (42): nasconde,       |
      |                                   |  ritorna 0                         |
      |                                   |  ramo SERVER (43): valida,         |
      |                                   |  ritorna coinValue                 |
      |                                   |  invoca onCollected (51) --------->|
      |  <----------------------------- evento                                 |
      |                                                                        |  se server: totalCoins += (44)
      |  handleCoinCollected (52):                                             |
      |    - sposta la moneta                                                  |
      |    - coin.Reset() (53)                                                 |
      |                                   |                                    |
      |         nuova posizione si propaga via NetworkTransform                |
      |                                   |  Update rileva il cambio (54):     |
      |                                   |  showCoin(true) — la fa ricomparire|
```

> **Esempio stupido**: un gettone da sala giochi. Lo infili in una macchina e SPARISCE SUBITO ai
> tuoi occhi (feedback client immediato, FLUSSO 42) — non stai lì a fissare un gettone già usato.
> Solo il gestore della sala (server) sa davvero se il gettone era valido, lo conta nel cassetto
> (FLUSSO 44) e decide di farne ricomparire uno identico in un'altra macchina della sala
> (FLUSSO 52-54), senza doverne stampare uno nuovo da zero.
>
> **Ancora più stupido**: è come un gioco a premi in TV dove il concorrente preme subito il
> pulsante e la lucina si spegne (feedback immediato), ma è la giuria dietro le quinte a
> confermare davvero se il punto vale, un secondo dopo.

### 5.9 Costo in monete per sparare, e polvere sui proiettili distrutti (FLUSSO 55 → 59)

Prima le monete servivano solo come punteggio; ora sparare **costa** monete, riusando lo stesso
`CoinWallet` sia per accreditarle (§5.8) sia per scalarle.

```csharp
// Assets/Scripts/Utils/SpawnOnDestroy.cs
public class SpawnOnDestroy : MonoBehaviour
{
    [SerializeField] private GameObject prefab;

    private void OnDestroy()
    {
        // [FLUSSO 55] Componente puramente estetico, sul proiettile dummy: quando
        // viene distrutto, lascia al suo posto un effetto locale (es. una nuvola
        // di polvere), creato in modo indipendente da ogni client, SENZA alcuno
        // Spawn() di rete.
        Instantiate(prefab, transform.position, Quaternion.identity);
    }
}
```

I campi aggiunti a `ProjectileLauncher.cs` per il costo (già visti nel blocco completo in §5.3):

```csharp
// [FLUSSO 56] Riferimento al portafoglio del proprietario.
[SerializeField] private CoinWallet wallet;
// [FLUSSO 56b] Costo in monete di ogni sparo.
[SerializeField] private int costToFire;
// [FLUSSO 57] Cooldown tra due spari.
private float timer;
```

E il controllo, in due punti diversi (uno cosmetico, uno vero):

```csharp
// [FLUSSO 57b] Lato CLIENT, dentro Update(): controllo cosmetico, evitabile con un client modificato.
if (wallet.totalCoins.Value < costToFire) return;

// [FLUSSO 58] Lato SERVER, dentro PrimaryFireServerRpc(): controllo AUTOREVOLE, quello vero.
if (wallet.totalCoins.Value < costToFire) return;
wallet.spendCoins(costToFire);   // [FLUSSO 59]
```

> **Esempio stupido**: come pagare un biglietto del bus convalidandolo alla macchinetta. Tu vedi
> il gesto (client, FLUSSO 57b: "ho abbastanza soldi? provo a salire"), ma è la macchinetta
> (server, FLUSSO 58) a controllare davvero il credito e a scalarlo (FLUSSO 59) — se provi a
> salire senza credito, il gesto non ha alcun effetto.
>
> **Ancora più stupido**: è come pagare in un distributore automatico di merendine. Puoi fare
> finta di infilare la mano in tasca (client), ma se non c'è la moneta vera dentro la macchina
> (server), la merendina non scende. Punto.

---

## 6. Tabella riepilogativa di tutti i FLUSSO

Uguale alla guida originale — utile per cercare velocemente un numero senza rileggere tutto.
"∞" = catena locale a `ClientNetworkTransform.cs` (numerazione indipendente).

| # | File | In breve |
|---|---|---|
| ∞0-8 | `ClientNetworkTransform.cs` | Meccanismo di sync client-authoritative del transform (§4) |
| 0 | `InputReader.cs` | `using static Controls` |
| 1 | `InputReader.cs` | Contratto `IPlayerActions` |
| 2 | `InputReader.cs` | Campo `controls` |
| 3 | `InputReader.cs` | Eventi `PrimaryFireEvent`/`MoveEvent` |
| 3b | `InputReader.cs` | `AimPosition` a polling |
| 4-6 | `InputReader.cs` | `OnEnable`: crea/registra/abilita i controlli |
| 7a-7c | `InputReader.cs` | `OnMove` / `OnPrimaryFire` / `OnAim` |
| 8 | `PlayerAiming.cs` | Riferimenti Inspector |
| 9 | `PlayerAiming.cs` | Perché `LateUpdate` |
| 10 | `PlayerAiming.cs` | Solo owner mira |
| 11-12 | `PlayerAiming.cs` | Schermo → mondo |
| 13 | `PlayerAiming.cs` | Orienta la torretta |
| 14 | `ProjectileLauncher.cs` | Prefab server vs dummy |
| 15-16 | `ProjectileLauncher.cs` | Iscrizione/disiscrizione `PrimaryFireEvent` |
| 17 | `ProjectileLauncher.cs` | Sparo su due binari (RPC + dummy) |
| 18 | `ProjectileLauncher.cs` | `PrimaryFireServerRpc` |
| 19 | `ProjectileLauncher.cs` | `SpawnDummyProjectileClientRpc` |
| 20 | `ProjectileLauncher.cs` | `HandlePrimaryFire` |
| 21 | `ProjectileLauncher.cs` | Helper `SpawnDummyProjectile` |
| 22 | `DestroySelfOnContact.cs` | Autodistruzione al contatto |
| 23 | `Lifetime.cs` | Autodistruzione a tempo |
| 24-24b | `PlayerMovement.cs` | Iscrizione `MoveEvent` + `turningRate` |
| 25 | `PlayerMovement.cs` | Disiscrizione |
| 26 | `PlayerMovement.cs` | Rotazione in `Update` |
| 27 | `PlayerMovement.cs` | Velocità in `FixedUpdate` |
| 28 | `PlayerMovement.cs` | `handleMove` |
| 29 | `ProjectileLauncher.cs` | `setOwnerClientId` |
| 30 | `DealDamageOnContact.cs` | Componente solo server |
| 31 | `DealDamageOnContact.cs` | `setOwnerClientId` |
| 32-33 | `DealDamageOnContact.cs` | Validazione bersaglio + no self-damage |
| 34 | `DealDamageOnContact.cs` | Applica danno |
| 35 | `HealthDisplay.cs` | Ruolo del componente |
| 36-37 | `HealthDisplay.cs` | Iscrizione/disiscrizione `OnValueChanged` |
| 38 | `HealthDisplay.cs` | Normalizza `fillAmount` |
| 39 | `Health.cs` | Solo server inizializza vita |
| 40 | `Health.cs` | `modifyHealth`, nessun `IsServer` esplicito necessario |
| 41 | `CoinWallet.cs` | `collect()` chiamato ovunque |
| 42 | `RespawningCoin.cs` | Ramo client: nasconde + ritorna 0 |
| 43 | `RespawningCoin.cs` | Ramo server: controllo autorevole |
| 44 | `CoinWallet.cs` | Solo server accredita `totalCoins` |
| 45 | `Coin.cs` | Contratto di `collect()` |
| 46 | `RespawningCoin.cs` | Evento `onCollected` |
| 47 | `RespawningCoin.cs` | Campo `previousPosition` |
| 48 | `CoinSpawner.cs` | Ruolo della classe |
| 49 | `CoinSpawner.cs` | `OnNetworkSpawn`, solo server spawna |
| 50 | `CoinSpawner.cs` | Iscrizione a `onCollected` |
| 51 | `RespawningCoin.cs` | Invoke `onCollected` |
| 52 | `CoinSpawner.cs` | `handleCoinCollected`, riposiziona |
| 53 | `RespawningCoin.cs` | `Reset()` |
| 54 | `RespawningCoin.cs` | `Update()`, ri-mostra la moneta |
| 55 | `SpawnOnDestroy.cs` | `OnDestroy`, effetto estetico locale |
| 56-56b | `ProjectileLauncher.cs` | Campi `wallet` e `costToFire` |
| 57 | `ProjectileLauncher.cs` | Campo `timer` (cooldown sparo) |
| 57b | `ProjectileLauncher.cs` | Controllo monete lato client (cosmetico) |
| 58 | `ProjectileLauncher.cs` | Controllo monete lato server (autorevole) |
| 59 | `CoinWallet.cs` | `spendCoins`, scala `totalCoins` |
| 60 | `ApplicationController.cs` | Campi `clientPrefab`/`hostPrefab` |
| 61 | `ApplicationController.cs` | `Start`: `DontDestroyOnLoad` + rilevamento dedicated server |
| 62 | `ApplicationController.cs` | `launchInMode`, ramo dedicated server (stub) |
| 63 | `ApplicationController.cs` | `launchInMode`, ramo client: crea `ClientSingleton` |
| 64 | `ApplicationController.cs` | Crea `HostSingleton`, sempre |
| 65 | `ApplicationController.cs` | Se autenticato, `goToMenu()` |
| 66 | `ClientSingleton.cs` | Pattern singleton `Instance` |
| 67 | `ClientSingleton.cs` | `Start`: `DontDestroyOnLoad` |
| 68 | `ClientSingleton.cs` | `createClient`: crea `ClientGameManager` |
| 69 | `ClientGameManager.cs` | `initAsync`: init UGS + `doAuth` |
| 70 | `ClientGameManager.cs` | `goToMenu`: carica scena "Menu" |
| 71 | `AuthenticationWrapper.cs` | Campo statico `authState` |
| 72 | `AuthenticationWrapper.cs` | `doAuth`, guardia "già autenticato" |
| 73 | `AuthenticationWrapper.cs` | `doAuth`, guardia "già in corso" |
| 74 | `AuthenticationWrapper.cs` | `doAuth`, delega a `SignInAnonimouslyAsync` (bug del `while` duplicato risolto) |
| 75 | `AuthenticationWrapper.cs` | `authenticating()`, attesa passiva |
| 76 | `AuthenticationWrapper.cs` | `SignInAnonimouslyAsync`, vera logica di login |
| 77 | `AuthenticationWrapper.cs` | `enum AuthState` |
| 78 | `HostSingleton.cs` | Pattern singleton `Instance` |
| 79 | `HostSingleton.cs` | `Start`: `DontDestroyOnLoad` |
| 80 | `HostSingleton.cs` | `createHost`: crea `HostGameManager` |
| 81 | `HostGameManager.cs` | `StartHostAsync` avvia davvero la sessione (Relay) |
| 82 | `HostSingleton.cs` | `GameManager` diventa proprietà pubblica, leggibile da `MainMenu` |
| 83 | `MainMenu.cs` | `StartHost()`, agganciato al bottone "Host" del Menu |
| 84 | `HostGameManager.cs` | Campi `allocation`/`joinCode`/`gameSceneName`/`maxConnections` |
| 85 | `HostGameManager.cs` | `StartHostAsync`: `Relay.Instance.CreateAllocationAsync` |
| 86 | `HostGameManager.cs` | `StartHostAsync`: `Relay.Instance.GetJoinCodeAsync` |
| 87 | `HostGameManager.cs` | `StartHostAsync`: configura `UnityTransport` con `RelayServerData` |
| 88 | `HostGameManager.cs` | `StartHostAsync`: `NetworkManager.Singleton.StartHost()` |
| 89 | `HostGameManager.cs` | `StartHostAsync`: `NetworkManager.Singleton.SceneManager.LoadScene("Game")` |

---

## 7. Ricette pronte da copiare in un gioco nuovo

Ogni pattern qui sotto è scritto in forma GENERICA, scollegata dai tank: puoi copiarlo in
qualsiasi altro progetto e riempirlo con la tua logica.

### 7.1 Stato autorevole con `NetworkVariable`

Quando un valore deve essere uguale per tutti e non falsificabile da un client (punteggio, vita,
oro...).

```csharp
public class ScorePlayer : NetworkBehaviour
{
    public NetworkVariable<int> score = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;   // solo il server decide il valore iniziale
        score.Value = 0;
    }

    [ServerRpc]
    public void AddPointServerRpc()
    {
        score.Value += 1;        // eseguito sul server: autorevole
    }
}
```

Un client chiama `AddPointServerRpc()`, ma è il server ad eseguire l'incremento vero. Tutti i
client vedono `score.Value` aggiornarsi da solo.

> **Esempio stupido**: è come segnare un gol e alzare le braccia (il client chiama la funzione),
> ma è l'arbitro (server) a fischiare e a far salire il numero sul tabellone. Se alzi le braccia
> ma l'arbitro non fischia, il tabellone non si muove.

### 7.2 Movimento client-authoritative

Zero lag sui controlli del proprio personaggio (implementazione completa in §4): sostituisci il
`NetworkTransform` standard con una variante che imposta `CanCommitToTransform = IsOwner` e
ritorna `false` da `OnIsServerAuthoritative()`.

### 7.3 Feedback immediato al client ("dummy pattern")

Quando un'azione deve SEMBRARE istantanea anche se la conferma richiede un giro di rete (§5.3).

```csharp
private void Fire()
{
    SpawnVisualEffectLocally();   // subito, nessuna attesa: solo estetica
    FireServerRpc();              // il vero "sparo" autorevole, arriva un attimo dopo
}

[ServerRpc]
private void FireServerRpc()
{
    // qui, e SOLO qui, la logica che conta davvero (danno, punteggio, ecc.)
}
```

Regola pratica: **tutto ciò che è "solo estetica" può girare ovunque; tutto ciò che "conta"
(danno, punteggio, stato) deve girare solo dove `IsServer` è vero.**

### 7.4 Cheat-sheet: quale controllo usare

| Proprietà | Vero quando | Usala per | Esempio stupido |
|---|---|---|---|
| `IsServer` | Questa istanza è il server (o l'host) | Logica autorevole: danno, punteggio, spawn di nemici/oggetti | Il maestro che corregge i compiti: solo lui mette il voto vero. |
| `IsClient` | Questa istanza è un client (o l'host, che è anche client) | UI, effetti visivi, suoni: cose che TUTTI devono vedere | Lo schermo che mostra i voti in classe: TUTTI lo leggono, anche chi non è il maestro. |
| `IsOwner` | Questa istanza possiede l'oggetto | Input e controlli del PROPRIO personaggio soltanto | Il tuo diario scolastico: solo tu ci scrivi dentro i tuoi compiti. |
| `IsListening` | Il server/host è attivo | Guard prima di inviare dati di rete | Controllare che il telefono abbia rete prima di provare a chiamare. |
| `IsConnectedClient` | Il client è connesso a un host | Guard prima di inviare dati di rete | Controllare di essere collegati al Wi-Fi prima di mandare un messaggio. |

Domanda da farsi sempre, prima di scrivere un `if`: *"questo codice deve girare per il
PROPRIETARIO, per TUTTI I CLIENT, o solo per IL SERVER?"*

### 7.5 Iscriviti/disiscriviti in `OnNetworkSpawn`/`OnNetworkDespawn`

Mai in `Start`/`OnDestroy` (troppo presto/tardi nel ciclo di vita di rete). Sempre a specchio,
per evitare eventi che richiamano componenti già distrutti.

```csharp
public override void OnNetworkSpawn()
{
    if (!IsOwner) return;
    inputReader.SomeEvent += HandleSomeEvent;
}

public override void OnNetworkDespawn()
{
    if (!IsOwner) return;
    inputReader.SomeEvent -= HandleSomeEvent;
}
```

> **Esempio stupido**: è come iscriversi e disiscriversi da un corso in palestra. Ti iscrivi
> quando arrivi (`OnNetworkSpawn`), ti disiscrivi quando te ne vai (`OnNetworkDespawn`). Se ti
> disiscrivi dal corso sbagliato o ti dimentichi di farlo, la palestra continua a chiamarti anche
> se non ci sei più: è un errore da evitare sempre.

### 7.6 Bus di eventi disaccoppiato (pattern `InputReader`)

Uno `ScriptableObject` condiviso che espone eventi C# invece di essere letto direttamente da
tutti. Utile ovunque serva scollegare "chi genera un dato" da "chi lo consuma".

```csharp
[CreateAssetMenu]
public class GameEvents : ScriptableObject
{
    public event Action OnMatchStarted;
    public void RaiseMatchStarted() => OnMatchStarted?.Invoke();
}
```

### 7.7 Controllo idempotente lato server ("già fatto?")

Quando un'azione potrebbe arrivare due volte nello stesso frame e va eseguita una volta sola
(vedi `alreadyCollected` in `RespawningCoin`).

```csharp
private bool alreadyHandled;

public void DoAuthoritativeThing()
{
    if (!IsServer) return;
    if (alreadyHandled) return;
    alreadyHandled = true;
    // ... logica vera, eseguita una sola volta
}
```

> **Esempio stupido**: è come timbrare il cartellino al lavoro. Se timbri due volte di fila per
> sbaglio, non vuoi che ti contino due giornate di lavoro: il flag `alreadyHandled` è il modo per
> dire "questa timbratura l'ho già registrata, la seconda non conta".

### 7.8 Respawn/riposiziona invece di distruggi/ricrea

Quando un oggetto "raccoglibile" deve ricomparire (moneta, powerup): spostalo e resettalo,
invece di distruggerlo e ricrearlo.

```csharp
public event Action<Collectible> OnCollected;

public void Collect()
{
    if (!IsServer) return;
    if (alreadyCollected) return;
    alreadyCollected = true;
    OnCollected?.Invoke(this);   // chi ha creato l'oggetto lo riposiziona
}

public void ResetState() => alreadyCollected = false;
```

### 7.9 Escludi il proprietario dal proprio effetto (no self-damage)

Salva l'`OwnerClientId` allo spawn e confrontalo al momento dell'impatto (§5.6).

```csharp
public void SetOwner(ulong clientId) => ownerClientId = clientId;

private void OnTriggerEnter2D(Collider2D other)
{
    if (other.attachedRigidbody == null) return;
    if (other.attachedRigidbody.TryGetComponent<NetworkObject>(out var netObj)
        && netObj.OwnerClientId == ownerClientId) return;

    // applica l'effetto...
}
```

### 7.10 Avvio sessione tramite Relay (niente IP diretto)

Vedi §2.5 per l'implementazione completa del lato Host.

```csharp
public async Task StartHostAsync(int maxConnections)
{
    Allocation allocation = await Relay.Instance.CreateAllocationAsync(maxConnections);
    string joinCode = await Relay.Instance.GetJoinCodeAsync(allocation.AllocationId);
    // ... mostra joinCode all'utente, cosi' possa condividerlo ...

    var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    transport.SetRelayServerData(new RelayServerData(allocation, "udp"));

    NetworkManager.Singleton.StartHost();
}

public async Task<bool> StartClientAsync(string joinCode)
{
    JoinAllocation allocation = await Relay.Instance.JoinAllocationAsync(joinCode);

    var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    transport.SetRelayServerData(new RelayServerData(allocation, "udp"));

    return NetworkManager.Singleton.StartClient();
}
```

Il ramo `StartClientAsync` (con `JoinAllocationAsync` al posto di `CreateAllocationAsync`) è
simmetrico ma **non ancora scritto** in questo progetto: è il prossimo passo naturale per
completare il flusso "Join" nel Menu.

> **Esempio stupido**: `CreateAllocationAsync` è come prenotare un tavolo al ristorante (crei tu
> la prenotazione). `JoinAllocationAsync` è come presentarti al ristorante dicendo "ho una
> prenotazione a nome Rossi" (ti unisci a una prenotazione già fatta da qualcun altro).

---

## 8. Checklist mentale per ogni nuovo componente di rete

Prima di scrivere un componente multiplayer nuovo, rispondi in ordine a queste domande:

1. **Questo valore/decisione deve essere uguale per tutti i giocatori?**
   → Sì: usa una `NetworkVariable` o validalo dentro una `[ServerRpc]`, scrivendo SOLO se
   `IsServer`.
2. **Questo comportamento riguarda solo il MIO personaggio (input, mira, movimento)?**
   → Metti `if (!IsOwner) return;` in cima al metodo.
3. **Serve che TUTTI (anche chi non è owner né server) vedano qualcosa (UI, barra vita, effetto)?**
   → Usa `IsClient`, e ascolta un cambiamento di `NetworkVariable` (`OnValueChanged`) invece di
   leggerla in `Update`.
4. **Voglio feedback istantaneo senza aspettare il giro di rete?**
   → Applica il "dummy pattern" (§7.3): mostra subito in locale, conferma dopo via `ServerRpc`.
5. **Questa azione potrebbe arrivare due volte per errore?**
   → Aggiungi un flag idempotente (§7.7), controllato solo lato server.
6. **Mi sto iscrivendo a un evento?**
   → Fallo in `OnNetworkSpawn`, disiscriviti a specchio in `OnNetworkDespawn`.
7. **Un oggetto raccoglibile deve "sparire e ricomparire"?**
   → Non distruggerlo: riposizionalo e resettane lo stato (§7.8).
8. **Un effetto/proiettile può colpire chi l'ha generato?**
   → Salva l'`OwnerClientId` e confrontalo prima di applicare l'effetto (§7.9).

**Esempio stupido riassuntivo di TUTTA la checklist**: immagina di gestire un piccolo negozio di
paese con un solo registratore di cassa vero (il server) e tanti clienti che entrano ed escono
(i client). Ogni cliente può GUARDARE il totale in cassa (`IsClient`), ma solo TU dietro il banco
(`IsServer`) puoi davvero cambiarlo. Ogni cliente ha il SUO carrello (`IsOwner`), nessuno tocca
il carrello degli altri. Se un cliente prende una mela dallo scaffale, la mela sparisce SUBITO ai
suoi occhi (feedback immediato), ma il registratore vero la conta solo quando la passi tu alla
cassa. E se un cliente prova a pagare due volte lo stesso scontrino per errore, il registratore
se ne accorge e non fa pagare due volte (controllo idempotente).

---

## 9. Glossario rapido

- **Host**: istanza che è contemporaneamente server e client. *Esempio stupido: l'arbitro che
  gioca anche lui la partita.*
- **Server**: istanza autorevole, senza rendering per il giocatore (a meno che non sia anche
  Host). *Esempio stupido: il regista di un film che non appare mai sullo schermo.*
- **Client**: istanza che si connette a un server/host, gioca ma non decide da sola l'esito delle
  azioni. *Esempio stupido: uno spettatore a teatro che può applaudire ma non cambiare il copione.*
- **Owner / Ownership**: il client "proprietario" di un `NetworkObject` (di solito il proprio
  personaggio). *Esempio stupido: le chiavi di casa tua, che hai solo tu.*
- **RPC (Remote Procedure Call)**: chiamata di metodo che attraversa la rete (`ServerRpc`
  client→server, `ClientRpc` server→client/i). *Esempio stupido: una lettera spedita per posta
  invece di parlare faccia a faccia.*
- **Interpolazione**: tecnica per rendere fluido il movimento di oggetti remoti, "riempiendo"
  visivamente lo spazio tra due posizioni ricevute dalla rete. *Esempio stupido: un cartone
  animato che, tra un disegno e l'altro, sembra muoversi morbido invece che a scatti.*
- **Prefab di rete**: prefab con un componente `NetworkObject`, registrabile nel `NetworkManager`
  per poter essere instanziato e sincronizzato in partita. *Esempio stupido: uno stampo per
  biscotti che, ovunque venga usato, produce sempre biscotti "riconoscibili" con lo stesso numero
  di serie.*
- **Autorevole (authoritative)**: chi ha l'ultima parola su un valore/decisione; nel progetto,
  quasi sempre il server. *Esempio stupido: il giudice in tribunale, non l'avvocato.*
- **NetworkManager**: il "regista" della sessione. *Vedi §1.*
- **NetworkObject**: il "codice a barre" di rete di un oggetto. *Vedi §1.*
- **NetworkBehaviour**: uno script "consapevole della rete". *Vedi §1.*
- **NetworkVariable\<T\>**: variabile auto-sincronizzata. *Vedi §1.*
- **Relay**: servizio di Unity che fa da "postino neutrale" tra host e client, senza bisogno di IP
  pubblici o port forwarding. *Vedi §2.5.*
- **Dedicated server**: un computer che ospita la partita ma su cui nessuno gioca davvero (niente
  scheda grafica, niente giocatore). *Esempio stupido: il server di posta elettronica — smista le
  email, ma nessuno "vive" dentro di lui.*
- **Dummy (proiettile finto)**: copia visiva istantanea di un'azione, mostrata prima che il
  server confermi quella vera. *Vedi §5.3.*
- **ScriptableObject**: un file/asset di Unity che contiene dati o logica condivisa, non
  agganciato a un singolo oggetto di scena. *Esempio stupido: un ricettario in cucina — non è
  "attaccato" a nessuna pentola in particolare, ma tutte le pentole possono usarlo.*

---

*Documento generato a partire da `GUIDA_MULTIPLAYER.md` e dai commenti `[FLUSSO N]` presenti nel
codice sorgente, con l'aggiunta del codice vero, spiegazioni più semplici ed esempi extra. Se
aggiungi nuove funzionalità al progetto, continua la numerazione `FLUSSO` da 90 in poi e
aggiorna sia `GUIDA_MULTIPLAYER.md` sia questo documento.*
