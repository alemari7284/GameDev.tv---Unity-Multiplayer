# Guida alla Codebase Multiplayer

> Documentazione completa del progetto (tank 2D multiplayer, Unity + **Netcode for GameObjects** v1.12.2).
> Obiettivo: capire ogni riga di rete scritta finora e usarla come **base di partenza per costruire altri giochi multiplayer da zero**.

---

## 0. Come leggere questo documento

Nel codice di **gameplay** (§5) ogni commento importante è ancora taggato `// [FLUSSO N]`. Sono numeri progressivi che raccontano **l'ordine cronologico** in cui il codice viene eseguito durante una partita, non l'ordine dei file. Seguendo i numeri in salita si legge la partita come una storia: input → mira → sparo → danno → vita → monete.

Il codice di **bootstrap/rete** (§2 — avvio dell'app, autenticazione, avvio della sessione Host/Client) invece **non usa più** questa numerazione: quei file sono stati riscritti a settembre 2026 per una migrazione dell'ecosistema Unity Gaming Services avvenuta dopo la registrazione del corso (§2.6 spiega tutto nel dettaglio), e mantenere dei numeri `FLUSSO` legati a un codice che nel frattempo è cambiato avrebbe solo confuso le idee. Quella sezione è quindi organizzata per file/argomento, con il codice vero incollato dentro.

Esiste ancora un'eccezione dentro il gameplay: `ClientNetworkTransform.cs` ha una propria numerazione interna (`FLUSSO 0`→`8`) che descrive un meccanismo a sé stante (la sincronizzazione del transform), richiamato dagli altri script come blocco unico ("vedi FLUSSO 0-8 in quel file"). Per questo lo trattiamo come **fondamenta**, prima del flusso principale di gioco (§4).

Questo documento è organizzato così:

1. Concetti Netcode da sapere prima di leggere il codice (§1)
2. Bootstrap dell'app, autenticazione e avvio della sessione di rete — organizzato per file, **senza numerazione FLUSSO** (§2 — il vero punto di partenza, prima ancora del flusso di gioco; include §2.6, la spiegazione dettagliata di come e perché questa parte diverge dal corso)
3. Il vecchio sistema di test locale con IP diretto (§3)
4. Le fondamenta: come si muove un oggetto in rete (§4 — `ClientNetworkTransform`)
5. Il flusso di gioco vero e proprio, in ordine `FLUSSO 0 → 59` (§5)
6. Tabella riepilogativa di tutti i FLUSSO di gameplay (§6)
7. Catalogo di pattern riusabili, con mini-esempi "stupidi" scollegati dal progetto, pronti per essere copiati in un gioco nuovo (§7)
8. Checklist mentale da seguire ogni volta che scrivi un componente di rete nuovo (§8)
9. Glossario (§9)

> 🎮 **Sugli "esempi stupidi"**: in tutto il documento i concetti sono spiegati con paragoni presi da tre giochi — **Metal Gear Solid** (Snake, il Codec, Shadow Moses), **Crash Bandicoot** (casse, frutti Wumpa, Crash Team Racing) e **Rocket League** (macchine, palla, boost, partite private). Non sono precisi al 100% su come quei giochi funzionano davvero dietro le quinte: servono solo a fissare l'idea.

---

## 1. Concetti Netcode da sapere prima di leggere il codice

| Concetto | Cos'è | Esempio stupido |
|---|---|---|
| **NetworkManager** | Il "regista" della sessione: sa chi è connesso, avvia Host/Server/Client. | È il Colonnello Campbell al Codec in Metal Gear Solid: sa chi è in missione, chi si collega e chi chiude la chiamata, ma non è lui a infilarsi a Shadow Moses. |
| **NetworkObject** | Componente che rende un GameObject "spawnabile in rete": gli dà un ID univoco condiviso da tutti. | È il numero sopra la macchina in Rocket League: tu vedi "Octane #3", il tuo amico pure, e tutti e due sanno che è LA STESSA macchina, anche se ognuno la vede sul proprio schermo. |
| **NetworkBehaviour** | Un `MonoBehaviour` "consapevole della rete": espone `IsServer`, `IsClient`, `IsOwner`, `OwnerClientId`, `OnNetworkSpawn`/`OnNetworkDespawn`. | È Snake con il Codec sempre in tasca: oltre a fare il suo lavoro (strisciare, nascondersi nella scatola di cartone), sa sempre rispondere a "sono io a dirigere la missione (server) o sto eseguendo ordini (client)? E questo corpo è il MIO (owner)?". |
| **NetworkVariable\<T\>** | Variabile che si sincronizza da sola su tutti i client. Di default: scrivibile SOLO dal server, leggibile da tutti. | Il tabellone del punteggio di Rocket League: lo aggiorna solo il server quando la palla entra in porta, e tutti lo vedono cambiare. Se attacchi un post-it con scritto "5-0" sul TUO monitor, il tabellone vero resta 0-3. |
| **ServerRpc** | Chiamata di metodo da client → eseguita sul server. | Snake che chiama Otacon al Codec: "apri questa porta!". Non è Snake ad aprirla: lui chiede, e la porta la apre chi ha davvero accesso al sistema (server). |
| **ClientRpc** | Chiamata di metodo da server → eseguita su tutti i client (o su un sottoinsieme). | L'allarme di Shadow Moses: lo fa scattare solo la base (server), ma tutte le guardie (client) lo sentono nello stesso momento e gli compare il "**!**" sopra la testa. |
| **Server authority** | Il server è l'unica fonte di verità: decide se un'azione è valida. | Il goal in Rocket League: puoi giurare di aver toccato la palla per ultimo, ma se il server dice che il goal l'ha segnato l'avversario, il goal è dell'avversario. Punto. |
| **Client authority** | Un client specifico ha il permesso di decidere lui stesso un valore (di solito per ridurre la latenza percepita). | Quando sterzi in Rocket League la TUA macchina gira subito sul tuo schermo, senza aspettare il server: se dovessi aspettare il giro di rete per ogni sterzata, un aerial sarebbe impossibile. |
| **Ownership / IsOwner** | Ogni `NetworkObject` ha un proprietario (di solito il client che lo controlla, es. il proprio tank). | In un 2v2 di Rocket League ognuno controlla solo la propria macchina: vedi quella del compagno muoversi, ma il pad di quella macchina ce l'ha in mano lui, non tu. |
| **Host** | Un'istanza che è CONTEMPORANEAMENTE server e client (gioca e allo stesso tempo arbitra). | Sei tu che crei la partita privata di Rocket League E ci giochi dentro: fai da arbitro (server) e intanto guidi la tua macchina (client). |

**Regola d'oro che attraversa tutto il progetto**: *"il client mostra, il server decide"*. Ogni volta che vedrai un client fare qualcosa visivamente in anticipo (nascondere una moneta, mostrare un proiettile finto), sappi che è solo estetica: la verità arriverà comunque dal server.

---

## 2. Bootstrap dell'app, autenticazione e avvio della sessione di rete

Questo è il **vero** primo codice eseguito all'avvio del gioco, prima ancora della scena con `ConnectionButtons` (§3): vive nella scena `NetBootstrap`, caricata per prima. Il suo scopo è distinguere un dedicated server da un giocatore normale e, nel secondo caso, autenticare il giocatore presso **Unity Gaming Services (UGS)** prima di lasciarlo entrare nel menu, per poi aprire/entrare in una partita.

> **Esempio stupido**: è l'inizio di Metal Gear Solid. Prima di arrivare nella base (il menu/la partita vera), Snake passa dal molo e deve farsi riconoscere al Codec dal Colonnello (autenticazione anonima): solo dopo si apre l'ascensore. Il personale della base (dedicated server) invece entra dall'ingresso di servizio, senza Codec e senza schermo.

**File coinvolti**:
- `Assets/Scripts/Networking/ApplicationController.cs` — punto di ingresso, decide dedicated server vs client.
- `Assets/Scripts/Networking/Client/ClientSingleton.cs` + `ClientGameManager.cs` — bootstrap lato client, autenticazione, avvio come client di una sessione.
- `Assets/Scripts/Networking/Client/AuthenticationWrapper.cs` — wrapper attorno a Unity Authentication Service.
- `Assets/Scripts/Networking/Host/HostSingleton.cs` + `HostGameManager.cs` — bootstrap lato host, avvio come host di una sessione.
- `Assets/Scripts/UI/MainMenu.cs` — bottoni "Host"/"Join" nella scena `Menu`.

> ⚠️ **Nota su questa sezione**: a differenza del resto del documento (§5), qui **non** troverai più tag `// [FLUSSO N]` nel codice né nella tabella. Questa parte del progetto è stata riscritta a settembre 2026 per inseguire una migrazione dell'ecosistema Unity Gaming Services avvenuta *dopo* la registrazione del corso (gennaio 2026): i pacchetti che il corso usa direttamente (`com.unity.services.relay`, `.lobby`, `.matchmaker`, `.multiplay`) sono stati deprecati e sostituiti da un unico pacchetto unificato, `com.unity.services.multiplayer`. Numerare questi file con `FLUSSO N` avrebbe legato per sempre la doc a una versione di codice che qui non esiste più. Tutto il §2.6 qui sotto spiega nel dettaglio **cosa dice il corso**, **cosa dice invece questo progetto**, e **perché**.

### 2.1 `ApplicationController.cs` — punto di ingresso

```csharp
public class ApplicationController : MonoBehaviour
{
    [SerializeField] private ClientSingleton clientPrefab;
    [SerializeField] private HostSingleton hostPrefab;

    private async Task Start()
    {
        DontDestroyOnLoad(gameObject);
        bool isDedicatedServer = SystemInfo.graphicsDeviceType ==
            UnityEngine.Rendering.GraphicsDeviceType.Null; //a ded. server has no graphics
        await launchInMode(isDedicatedServer);
    }

    private async Task launchInMode(bool isDedicatedServer)
    {
        if (isDedicatedServer)
        {
            // Ramo dedicated server: ancora uno stub vuoto.
        }
        else
        {
            HostSingleton hostSingleton = Instantiate(hostPrefab);
            hostSingleton.createHost();

            ClientSingleton clientSingleton = Instantiate(clientPrefab);
            bool authenticated = await clientSingleton.createClient();

            if (authenticated)
            {
                clientSingleton.gameManager.goToMenu();
            }
        }
    }
}
```

Cosa succede, in ordine:

1. **`Start()`**: `DontDestroyOnLoad(gameObject)` (l'oggetto deve sopravvivere al cambio scena verso il Menu) e rilevamento dedicated server via `SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null` (un server headless non ha una GPU).
2. **Ramo dedicated server** (`isDedicatedServer == true`): ancora uno stub vuoto — un dedicated server headless non deve autenticarsi come giocatore né mostrare un menu; qui andrà in futuro l'avvio diretto della sessione di rete lato server.
3. **Sempre, per un'istanza giocabile** (anche se poi resterà solo client): si crea subito `HostSingleton` (`Instantiate(hostPrefab)` + `createHost()`), per essere già pronti se questa istanza dovesse diventare host in seguito (es. premendo "Host" nel Menu). Questo viene fatto **prima** del login perché `createHost()` è sincrono, non dipende in nulla dall'esito dell'autenticazione, e non ha senso farlo aspettare "in mezzo" a un `await`.
4. Poi si crea `ClientSingleton` e si aspetta l'intera procedura di autenticazione (`await clientSingleton.createClient()`, §2.2).
5. Solo se `authenticated == true` si passa al Menu (`clientSingleton.gameManager.goToMenu()`, §2.2). Se falso (es. `AuthState.Error`/`Timeout` dopo i tentativi falliti, §2.3) l'app resta bloccata sulla scena di bootstrap: non c'è ancora un messaggio d'errore o un retry visibile all'utente — un limite noto, non ancora risolto.

**Non toccato dalla migrazione**: questo file non usa nessuna API di Lobby/Relay/Matchmaker/Sessions, quindi è identico a quello del corso.

### 2.2 `ClientSingleton.cs` + `ClientGameManager.cs` — bootstrap lato client

`ClientSingleton` è un `MonoBehaviour` "contenitore": esiste solo per dare un aggancio in scena a `ClientGameManager`, che è una classe C# pura (creata con `new`, non `Instantiate`) e quindi non potrebbe vivere da sola come componente.

```csharp
public class ClientSingleton : MonoBehaviour
{
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
        DontDestroyOnLoad(gameObject);
    }

    public async Task<bool> createClient()
    {
        gameManager = new ClientGameManager();
        return await gameManager.initAsync();
    }
}
```

Pattern singleton "lazy" (`Instance`), identico nello spirito a `NetworkManager.Singleton`: cercato solo quando serve, non subito in `Awake`. `Start()` chiama `DontDestroyOnLoad` per lo stesso motivo di `ApplicationController`. `createClient()` crea il `ClientGameManager` e gli delega subito `initAsync()`, il cui `bool` risale fino ad `ApplicationController` (§2.1).

`ClientGameManager.cs` — parte **in buona parte invariata** dalla migrazione, tranne il metodo `startClientAsync` (spiegato in dettaglio in §2.6):

```csharp
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
        try
        {
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
}
```

- **`initAsync`**: `UnityServices.InitializeAsync()` — va chiamato una volta per processo prima di qualunque servizio UGS (compresi Authentication e Multiplayer Services) — poi delega a `AuthenticationWrapper.doAuth()` (§2.3). **Invariato dal corso.**
- **`goToMenu`**: `SceneManager.LoadScene("Menu")`, chiamato solo se l'autenticazione è riuscita. **Invariato dal corso.**
- **`startClientAsync`**: **questo è il metodo migrato**. Nel corso, qui c'era `Relay.Instance.JoinAllocationAsync` + configurazione manuale di `UnityTransport` + `NetworkManager.Singleton.StartClient()`. Oggi c'è un'unica chiamata a `MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode)`, che entra nella sessione creata dall'host e avvia da sola questa istanza come client connesso via Relay. Il dettaglio completo, con codice prima/dopo, è in §2.6.

### 2.3 `AuthenticationWrapper.cs` — autenticazione anonima UGS

Wrapper `static` (un solo stato per tutto il processo, non per istanza) attorno a Unity Authentication Service, con una piccola macchina a stati (`AuthState`) pensata per gestire chiamate concorrenti a `doAuth`.

```csharp
public static class AuthenticationWrapper
{
    public static AuthState authState { get; private set; }

    public static async Task<AuthState> doAuth(int maxTries = 5)
    {
        if (authState == AuthState.Authenticated) return authState;

        if (authState == AuthState.Authenticating)
        {
            Debug.LogWarning("Already authenticating!");
            await authenticating();
            return authState;
        }

        await SignInAnonimouslyAsync(maxTries);
        return authState;
    }

    private static async Task<AuthState> authenticating()
    {
        while (authState == AuthState.Authenticating || authState == AuthState.NonAuthenticated)
        {
            await Task.Delay(200);
        }
        return authState;
    }

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

- Campo statico `authState`: essendo la classe `static`, lo stato è condiviso da tutto il processo e sopravvive ai cambi scena come gli oggetti `DontDestroyOnLoad`.
- `doAuth`, prima guardia: se già `Authenticated`, ritorna subito senza rifare l'handshake.
- `doAuth`, seconda guardia: se un'altra chiamata ha già portato lo stato ad `Authenticating`, questa NON ne avvia una seconda in parallelo — aspetta passivamente l'esito con `authenticating()`.
- `doAuth`, terzo caso (nessuna autenticazione né in corso né già fatta): delega direttamente a `SignInAnonimouslyAsync(maxTries)` e ne ritorna l'esito. **Bug storico corretto**: qui prima c'era un secondo ciclo, quasi duplicato di quello dentro `SignInAnonimouslyAsync`, che però non impostava `authState = AuthState.Authenticating;` prima del `while` — alla primissima chiamata `authState` valeva ancora `NonAuthenticated`, la condizione del `while` era quindi falsa fin da subito e il login non partiva mai.
- `authenticating()` — helper di attesa passiva: polling ogni 200ms finché lo stato non esce da `Authenticating`/`NonAuthenticated`.
- `SignInAnonimouslyAsync` (privato) — vera logica di login: imposta subito `Authenticating`, ritenta fino a `maxRetries` volte, gestisce le eccezioni (`AuthenticationException`, `RequestFailedException`) impostando `Error`, e segna `Timeout` se i tentativi si esauriscono senza successo.
- `enum AuthState` — i 5 stati possibili (`NonAuthenticated` è il default C#, valore 0, quindi anche lo stato iniziale).

> ✅ **Non toccato dalla migrazione**: `Unity.Services.Authentication` e `Unity.Services.Core` non fanno parte della fusione Lobby/Relay/Matchmaker/Multiplay → `com.unity.services.multiplayer`. Restano pacchetti a sé, e sono anzi una **dipendenza** dello stesso `com.unity.services.multiplayer` (che li richiede internamente per autenticare le sessioni). Questo file è quindi identico, riga per riga, a quello del corso.

### 2.4 `HostSingleton.cs` + `HostGameManager.cs` — bootstrap lato host

Controparte simmetrica di §2.2, ma lato host: stesso identico pattern, dietro a un `HostGameManager` che (§2.5) sa avviare davvero una sessione.

```csharp
public class HostSingleton : MonoBehaviour
{
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

    public HostGameManager GameManager { get; private set; }

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void createHost()
    {
        GameManager = new HostGameManager();
    }
}
```

Pattern singleton "lazy", identico a `ClientSingleton` (§2.2). `createHost` si limita a istanziare `HostGameManager`: a differenza di `createClient` non c'è qui nessuna logica asincrona da avviare — quella è tutta dentro `StartHostAsync` (§2.5), chiamata più tardi, quando l'utente preme davvero "Host". `GameManager` è una proprietà pubblica (non un campo privato) proprio per essere leggibile dall'esterno, da `MainMenu.StartHost` (§2.5).

**Non toccato dalla migrazione** (a parte la rimozione dei vecchi commenti `FLUSSO`): questo file non chiama nessuna API di rete direttamente, quindi non c'era nulla da migrare.

### 2.5 `MainMenu.cs` + `HostGameManager.StartHostAsync` / `ClientGameManager.startClientAsync` — avvio reale della sessione

File coinvolti:
- `Assets/Scripts/UI/MainMenu.cs` — bottoni "Host"/"Join" nella scena `Menu`.
- `Assets/Scripts/Networking/Host/HostGameManager.cs` — logica di avvio sessione lato host.
- `Assets/Scripts/Networking/Client/ClientGameManager.cs` — logica di ingresso in sessione lato client (§2.2).

```csharp
// Assets/Scripts/UI/MainMenu.cs
public class MainMenu : MonoBehaviour
{
    [SerializeField] private TMP_InputField joinCodeField;

    public async void StartHost()
    {
        await HostSingleton.Instance.GameManager.StartHostAsync();
    }

    public async void startClient()
    {
        await ClientSingleton.Instance.gameManager.startClientAsync(joinCodeField.text);
    }
}
```

`StartHost` è agganciato all'`OnClick` del bottone "Host" nella scena `Menu` (Inspector, non codice); `startClient` è agganciato al bottone "Join", e legge il join code da un `TMP_InputField` in scena. `async void` va bene SOLO qui perché sono entrambi event handler UI: nessuno "aspetta" il completamento, eventuali eccezioni non catturate finirebbero solo in console. Entrambi funzionano solo se si è arrivati al Menu passando da `NetBootstrap` (dove `HostSingleton`/`ClientSingleton` vengono creati, §2.1): aprire la scena Menu direttamente lascia `Instance` a `null` e lancia una `NullReferenceException` — non un bug di questi metodi, ma dell'ordine di avvio delle scene.

```csharp
// Assets/Scripts/Networking/Host/HostGameManager.cs
public class HostGameManager
{
    private ISession session;
    private const string gameSceneName = "Game";
    private const int maxConnections = 20;

    public async Task StartHostAsync()
    {
        try
        {
            var options = new SessionOptions
            {
                Name = "My Lobby",
                MaxPlayers = maxConnections,
                IsPrivate = false
            }.WithRelayNetwork();

            session = await MultiplayerService.Instance.CreateSessionAsync(options);

            Debug.Log($"Session created. Join code: {session.Code}");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}
```

- `CreateSessionAsync(options)` con `.WithRelayNetwork()` fa, **in un'unica chiamata**, tutto quello che nel corso richiedeva quattro passi separati (alloca il Relay, genera il join code, configura `UnityTransport`, avvia `NetworkManager.StartHost()`): vedi §2.6 per il confronto riga per riga con il codice del corso.
- La stessa chiamata crea anche la **Lobby**: `Name` è il nome con cui la partita comparirà nella lista lobby, `IsPrivate = false` la rende visibile nelle ricerche pubbliche. Nessuna chiamata separata a `CreateLobbyAsync`, nessun heartbeat: vedi §2.6.7.
- `session.Code` è il join code umano-leggibile, generato automaticamente da Unity — per ora solo loggato in console, non ancora mostrato in UI (stesso limite del corso: non esiste ancora un pannello che lo mostri al giocatore).
- Solo dopo che la sessione è stata creata (e quindi `NetworkManager` è già avviato come Host) si cambia scena con `NetworkManager.Singleton.SceneManager.LoadScene(...)` — la versione "di rete" del cambio scena (diversa da `SceneManager.LoadScene` usato in `ClientGameManager.goToMenu`), l'unica che porta con sé anche tutti i client già connessi.

```
Giocatore preme "Host" nel Menu
      |
MainMenu.StartHost
      |
HostSingleton.Instance.GameManager.StartHostAsync()
      |
      v
MultiplayerService.Instance.CreateSessionAsync(
    new SessionOptions{ Name = "My Lobby", MaxPlayers = 20, IsPrivate = false }
        .WithRelayNetwork()
)  ------------------------------------> [Unity Multiplayer Services]
      |  crea la Lobby (e la tiene viva con l'heartbeat),
      |  alloca il Relay, genera il join code,
      |  configura UnityTransport, avvia NetworkManager.StartHost()
      |  TUTTO IN UNA CHIAMATA
      v
session.Code  --------------------------> join code (per ora solo loggato)
      |
      v
NetworkManager.Singleton.SceneManager.LoadScene("Game")   <- tutti i client connessi seguono
```

> **Esempio stupido**: nel corso, aprire una partita era come un livello di Crash Bandicoot fatto tutto a mano: rompi la cassa per prendere la maschera Aku Aku (allocazione Relay), raccogli la gemma (join code), la porti tu stesso al warp (`UnityTransport`), e solo alla fine salti nel portale (`StartHost()`). Con le Sessions è come premere "Crea partita privata" in Rocket League: scegli nome e numero di giocatori, e il gioco si occupa da solo di server, codice e connessione.

**Cosa manca ancora**: mostrare `session.Code` a schermo (oggi solo loggato), e gestire `LeaveAsync()`/la disconnessione quando l'utente esce dalla partita.

### 2.6 La migrazione al Multiplayer Services SDK: cosa diverge dal corso, e perché

Questa è la parte più importante da capire se stai seguendo il corso GameDev.tv (registrato/aggiornato a **gennaio 2026**) mentre lavori su questo progetto **oggi (settembre 2026)**: la sezione "networking" del corso non compila più così com'è, e questo paragrafo spiega esattamente cosa è cambiato, perché, e come si traduce ogni pezzo di codice del corso in questo progetto.

#### 2.6.1 Cosa è cambiato nell'ecosistema Unity Gaming Services

Il corso costruisce l'intero multiplayer chiamando **direttamente** tre servizi separati di Unity Gaming Services (UGS), ciascuno con il proprio pacchetto Unity Package Manager e la propria API:

| Servizio | Pacchetto (usato dal corso) | A cosa serve nel corso |
|---|---|---|
| **Relay** | `com.unity.services.relay` | Far comunicare host e client senza IP pubblico/port forwarding (`Relay.Instance.CreateAllocationAsync`, `.GetJoinCodeAsync`, `.JoinAllocationAsync`) |
| **Lobby** | `com.unity.services.lobby` | Elenco di partite pubbliche a cui unirsi (`Lobbies.Instance.CreateLobbyAsync`, `SendHeartbeatPingAsync`, `QueryLobbiesAsync`): introdotto più avanti nel corso, la traduzione è in §2.6.7 |
| **Matchmaker** | `com.unity.services.matchmaker` | (idem) |
| **Multiplay** | `com.unity.services.multiplay` | (idem, per dedicated server hosting) |

Tra gennaio e settembre 2026, Unity ha **deprecato tutti e quattro questi pacchetti standalone** (su Unity 6 e successivi) e ha spostato le loro funzionalità dentro un **unico pacchetto unificato**: `com.unity.services.multiplayer`, che espone una nuova astrazione chiamata **Sessions** (namespace `Unity.Services.Multiplayer`, classi principali `MultiplayerService`, `ISession`, `SessionOptions`). Le funzionalità non sono sparite: Lobby, Relay e Matchmaker continuano a esistere *sotto* le Sessions, ma non si chiamano più direttamente — è la Session a orchestrarli per conto tuo.

I due pacchetti (quelli vecchi standalone e quello nuovo unificato) **non possono coesistere** nello stesso progetto: Unity Package Manager rifiuta la configurazione con un errore esplicito:

```
The following package has been added:
- Multiplayer Services (com.unity.services.multiplayer) version 2.2.3
However, it is incompatible with the Unity Multiplayer Service SDK.
Please remove the following package:
- Multiplayer Services (com.unity.services.multiplayer) version 2.2.3
If you wish to use the Unity Multiplayer Services SDK.
```

Questo è esattamente l'errore che ha reso necessaria questa migrazione: il progetto aveva sia i pacchetti vecchi (aggiunti seguendo il corso) sia `com.unity.services.multiplayer` (aggiunto, probabilmente, da un componente automatico dell'Editor come il Multiplayer Center), e i due non potevano stare insieme.

#### 2.6.2 Modifiche a `Packages/manifest.json`

**Prima** (come da corso):
```json
"com.unity.services.lobby": "1.3.0",
"com.unity.services.matchmaker": "1.2.0",
"com.unity.services.multiplay": "1.3.1",
"com.unity.services.multiplayer": "2.2.3",
"com.unity.services.relay": "1.2.0",
```

**Dopo** (in questo progetto):
```json
"com.unity.services.multiplayer": "2.2.3",
```

`com.unity.services.authentication` **non** compare in nessuna delle due liste perché non è mai stato un `dependency` diretto: era (ed è tuttora) una dipendenza *transitiva* — prima richiesta da `relay`/`lobby`/`matchmaker`, oggi richiesta direttamente da `com.unity.services.multiplayer` — quindi Unity Package Manager continua a risolverla da sé, senza bisogno di aggiungerla a mano.

#### 2.6.3 Modifiche a `HostGameManager.cs`

**Prima** (come da corso — Relay diretto):
```csharp
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class HostGameManager
{
    private Allocation allocation;
    private string joinCode;
    private const string gameSceneName = "Game";
    private const int maxConnections = 20;

    public async Task StartHostAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();
            allocation = await Relay.Instance.CreateAllocationAsync(maxConnections);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return;
        }

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

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
        transport.SetRelayServerData(relayServerData);

        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}
```

**Dopo** (in questo progetto — Sessions):
```csharp
using Unity.Netcode;
using Unity.Services.Multiplayer;

public class HostGameManager
{
    private ISession session;
    private const string gameSceneName = "Game";
    private const int maxConnections = 20;

    public async Task StartHostAsync()
    {
        try
        {
            var options = new SessionOptions
            {
                MaxPlayers = maxConnections
            }.WithRelayNetwork();

            session = await MultiplayerService.Instance.CreateSessionAsync(options);

            Debug.Log($"Session created. Join code: {session.Code}");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}
```

Confronto passo per passo:

| Passo del corso | Equivalente nelle Sessions |
|---|---|
| `Relay.Instance.CreateAllocationAsync(maxConnections)` | Sostituito da `MultiplayerService.Instance.CreateSessionAsync(options)`, dove `options` porta `MaxPlayers` e `.WithRelayNetwork()` |
| `Relay.Instance.GetJoinCodeAsync(allocation.AllocationId)` | Non serve più chiamarlo: `session.Code` è già il join code, disponibile subito dopo `CreateSessionAsync` |
| `NetworkManager.Singleton.GetComponent<UnityTransport>()` + `new RelayServerData(allocation, "dtls")` + `transport.SetRelayServerData(...)` | Non serve più: `.WithRelayNetwork()` dice alla Session di occuparsene da sola |
| `NetworkManager.Singleton.StartHost()` | Non serve più chiamarlo esplicitamente: `CreateSessionAsync` con `.WithRelayNetwork()` avvia da sola `NetworkManager` come Host, appena la sessione è pronta |
| `NetworkManager.Singleton.SceneManager.LoadScene(...)` | **Invariato**: il cambio scena di rete resta responsabilità nostra, le Sessions non lo fanno da sole |

Il campo `allocation`/`joinCode` (due variabili separate nel corso) è diventato un unico riferimento `session` (di tipo `ISession`), da cui si legge sia `session.Code` (il join code) sia, in futuro, `session.Id`, `session.PlayerCount`, `session.IsHost`, ecc.

> Fonte verificata sulla documentazione ufficiale Unity: con `.WithRelayNetwork()` "semplice" (senza opzioni di rete posticipate), `CreateSessionAsync`/`JoinSessionByCodeAsync` avviano la rete automaticamente; è solo omettendo `.WithRelayNetwork()` e configurando `SessionOptions` "a vuoto" che si passa al pattern manuale con `session.Network.StartDirectNetworkAsync(...)`, pensato per casi più avanzati (es. aspettare che si connettano tutti i giocatori prima di aprire la rete). Non è il caso di questo progetto.

#### 2.6.4 Modifiche a `ClientGameManager.cs`

**Prima** (come da corso):
```csharp
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public async Task startClientAsync(string joinCode)
{
    try
    {
        allocation = await Relay.Instance.JoinAllocationAsync(joinCode);
    }
    catch (Exception e)
    {
        Debug.Log(e);
        return;
    }

    UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
    transport.SetRelayServerData(relayServerData);

    NetworkManager.Singleton.StartClient();
}
```

**Dopo** (in questo progetto):
```csharp
using Unity.Services.Multiplayer;

public async Task startClientAsync(string joinCode)
{
    try
    {
        session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
    }
    catch (Exception e)
    {
        Debug.LogError(e);
    }
}
```

Stesso identico principio del lato host: `Relay.Instance.JoinAllocationAsync` + configurazione manuale del transport + `NetworkManager.Singleton.StartClient()` diventano un'unica chiamata a `JoinSessionByCodeAsync(joinCode)`, che entra nella sessione creata dall'host (che sa già, dal proprio lato, che userà il Relay) e avvia da sola questa istanza come Client connesso via Relay.

#### 2.6.5 Cosa NON è cambiato

Per chiarezza, un elenco esplicito di ciò che la migrazione **non** ha toccato, così da poter continuare a seguire il corso senza sorprese su queste parti:

- **`ApplicationController.cs`** (§2.1): identico al corso.
- **`ClientSingleton.cs` / `HostSingleton.cs`** (§2.2, §2.4): identici al corso (solo puliti dai vecchi riferimenti `FLUSSO`, che non avevano comunque effetto sul comportamento).
- **`AuthenticationWrapper.cs`** (§2.3): identico al corso, incluso il bugfix sul `while` di `SignInAnonimouslyAsync` già presente prima di questa migrazione.
- **`ClientGameManager.initAsync` / `.goToMenu`** (§2.2): identici al corso.
- **`MainMenu.cs`** (§2.5): identico al corso nella forma (stessi due metodi, stesso aggancio ai bottoni); cambia solo cosa succede *dentro* `HostGameManager`/`ClientGameManager` quando li chiama.
- Tutto il **gameplay** (§1, §4, §5): completamente estraneo a questa migrazione, non usa nessuna API UGS.

#### 2.6.6 Nuove classi/API da conoscere (Sessions)

| Elemento | Namespace | A cosa serve |
|---|---|---|
| `MultiplayerService.Instance` | `Unity.Services.Multiplayer` | Punto di ingresso singleton per creare/joinare sessioni (equivalente concettuale di `Relay.Instance` nel corso) |
| `SessionOptions` | `Unity.Services.Multiplayer` | Configurazione di una sessione da creare: `Name` (nome nella lista lobby), `MaxPlayers`, `IsPrivate` (visibile o no nelle ricerche), e i metodi di estensione `.WithRelayNetwork(...)` / `.WithDistributedAuthorityNetwork(...)` per scegliere come si connetteranno host e client |
| `CreateSessionAsync(options)` | `MultiplayerService.Instance` | Crea una sessione come host: alloca il Relay, genera il join code, e (con `.WithRelayNetwork()` semplice) avvia da sola `NetworkManager` come Host |
| `JoinSessionByCodeAsync(joinCode)` | `MultiplayerService.Instance` | Entra in una sessione esistente come client, usando il join code: avvia da sola `NetworkManager` come Client |
| `ISession` | `Unity.Services.Multiplayer` | Rappresenta la sessione attiva (sia per l'host che per i client): espone `Code` (join code), `Id`, `Host`, `IsHost`, `PlayerCount`, `MaxPlayers`, `CurrentPlayer`, e il metodo `LeaveAsync()` |
| `QuerySessionsAsync(options)` | `MultiplayerService.Instance` | Cerca le sessioni pubbliche (= le lobby) a cui unirsi; restituisce una lista di `ISessionInfo` (`Id`, `Name`, `AvailableSlots`, `MaxPlayers`…). Sostituisce `QueryLobbiesAsync` del corso |
| `JoinSessionByIdAsync(sessionId)` | `MultiplayerService.Instance` | Entra in una sessione scelta da una lista (invece che digitando il codice). Sostituisce `JoinLobbyByIdAsync` + lettura del join code + `StartClient` del corso |
| `session.LeaveAsync()` | `ISession` | Lascia la sessione: rimuove il giocatore dal backend e chiude in autonomia i moduli di rete associati (non serve chiamare `NetworkManager.Singleton.Shutdown()` a mano) |

#### 2.6.7 Lobby: la Session È già una Lobby

Più avanti il corso aggiunge le **Lobby**, cioè la lista di partite pubbliche a cui unirsi senza dover digitare il codice. Nel corso è un secondo servizio, chiamato a mano **dopo** aver creato il Relay. Nel Multiplayer Services SDK la Lobby è **inglobata nella Session**: `CreateSessionAsync` la crea già da sola. Per questo `HostGameManager` non ha un secondo blocco `try/catch` per la Lobby.

**Prima** (come da corso, aggiunto in fondo a `StartHostAsync`, dopo il Relay):
```csharp
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

private string lobbyId;

// ...dentro StartHostAsync, dopo aver ottenuto joinCode dal Relay:
try
{
    CreateLobbyOptions lobbyOptions = new CreateLobbyOptions();
    lobbyOptions.IsPrivate = false;
    lobbyOptions.Data = new Dictionary<string, DataObject>()
    {
        {
            "JoinCode", new DataObject(
                visibility: DataObject.VisibilityOptions.Member,
                value: joinCode
            )
        }
    };
    Lobby lobby = await Lobbies.Instance.CreateLobbyAsync("My Lobby", maxConnections, lobbyOptions);
    lobbyId = lobby.Id;
    HostSingleton.Instance.StartCoroutine(HeartbeatLobby(15));
}
catch (LobbyServiceException e)
{
    Debug.Log(e);
    return;
}

private IEnumerator HeartbeatLobby(float waitTimeSeconds)
{
    WaitForSecondsRealtime delay = new WaitForSecondsRealtime(waitTimeSeconds);
    while (true)
    {
        Lobbies.Instance.SendHeartbeatPingAsync(lobbyId);
        yield return delay;
    }
}
```

**Dopo** (in questo progetto): solo due proprietà in più nelle `SessionOptions` che c'erano già:
```csharp
var options = new SessionOptions
{
    Name = "My Lobby",          // nome nella lista lobby
    MaxPlayers = maxConnections,
    IsPrivate = false           // visibile in QuerySessionsAsync
}.WithRelayNetwork();
```

| Pezzo del corso | Cosa diventa con le Sessions | Perché |
|---|---|---|
| `Lobbies.Instance.CreateLobbyAsync(name, max, options)` | `Name` + `MaxPlayers` + `IsPrivate` dentro `SessionOptions` | `CreateSessionAsync` crea la Lobby da sé. Chiamarla anche a mano creerebbe **due** lobby per ogni partita |
| `lobbyOptions.Data["JoinCode"]` | Sparito | L'SDK salva da solo il join code del Relay dentro la Lobby e lo usa quando un client entra |
| Campo `lobbyId` | `session.Id` | La Session e la sua Lobby sono la stessa cosa, con lo stesso id |
| Coroutine `HeartbeatLobby(15)` avviata su `HostSingleton` | Sparita | L'SDK manda l'heartbeat automaticamente finché la sessione è viva |
| `catch (LobbyServiceException e)` | Il `catch (Exception e)` già esistente | Un'unica chiamata, un unico `try/catch`: gli errori di Lobby e Relay arrivano entrambi come `SessionException` |
| `using Unity.Services.Lobbies(.Models)` | Rimossi | Il pacchetto `com.unity.services.lobby` non è installato (e non può coesistere con `com.unity.services.multiplayer`, §2.6.1) |

> **Esempio stupido — l'heartbeat**: in Metal Gear Solid, se Snake smette di rispondere al Codec, dall'altra parte senti "Snake? Snake?! SNAAAAAKE!" e la missione finisce. La Lobby funziona uguale: se l'host non manda un "sono ancora qui" ogni tanto, Unity la considera morta e la chiude. Nel corso eri tu a dover rispondere al Codec ogni 15 secondi (la coroutine); con le Sessions il Codec risponde da solo.

> **Esempio stupido — `IsPrivate`**: una lobby pubblica è come una partita personalizzata di Rocket League che compare nella lista del server browser: chiunque la vede e ci entra. Una lobby privata (`IsPrivate = true`) è come la frequenza Codec di Meryl in MGS1 (140.15): non compare da nessuna parte, la conosce solo chi l'ha letta sul retro della custodia del CD, cioè chi ha ricevuto `session.Code`.

**Lato client (prossime lezioni)**: la lista lobby del corso (`QueryLobbiesAsync`, `LobbyItem`, `JoinLobbyByIdAsync` seguito dalla lettura di `Data["JoinCode"]` e da `StartClient`) si traduce così:
```csharp
// Lista lobby: ogni ISessionInfo ha Id, Name, AvailableSlots, MaxPlayers...
QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(
    new QuerySessionsOptions { Count = 25 });

foreach (ISessionInfo info in results.Sessions)
{
    // un LobbyItem per ogni info (nel corso riceveva un Lobby)
}

// Entrare in una lobby scelta dalla lista: fa anche il join Relay e avvia il Client
session = await MultiplayerService.Instance.JoinSessionByIdAsync(info.Id);
```

> **Esempio stupido — la lista lobby**: è la schermata di selezione livello di Crash Bandicoot, ma per le partite: vedi tutti i "portali" aperti, con quanti posti liberi ha ciascuno (`AvailableSlots`), e saltandoci dentro (`JoinSessionByIdAsync`) ti ritrovi direttamente nel livello, senza dover digitare nessun codice.

**Come adattiamo il codice del corso**: il codice copiato dalle lezioni viene marcato con un commento `// from udemy`; poi viene riscritto per le Sessions, il marker viene rimosso e le parti inutili (come l'heartbeat) vengono eliminate, con un commento che spiega a cosa corrispondono nel corso.

#### 2.6.8 Cosa manca ancora rispetto a un flusso completo

- Mostrare `session.Code` in una UI (oggi è solo loggato in console).
- La **lista lobby lato client** (`QuerySessionsAsync` + `JoinSessionByIdAsync`, vedi §2.6.7): il lato host è pronto (la sessione è già pubblica e ha un nome), manca la UI che la mostra e il metodo di join in `ClientGameManager`.
- Un nome di lobby vero: oggi è fisso a `"My Lobby"` per tutte le partite.
- Un bottone/flusso per lasciare la sessione (`session.LeaveAsync()`, non ancora richiamato da nessuna parte). Per l'host, chiudere la sessione chiude anche la Lobby: non c'è un `DeleteLobbyAsync` separato da chiamare.
- Gestione più ricca degli errori: la documentazione Unity consiglia di intercettare in modo specifico `SessionException` (sottoclasse di `Exception`) per distinguere gli errori delle Sessions da altri errori generici — oggi il codice cattura solo `Exception` generico, coerente con lo stile "minimale" tenuto finora dal corso.
- Se il corso introdurrà più avanti il Matchmaker, andrà anch'esso tradotto nelle API delle Sessions (`MultiplayerService.Instance.MatchmakeSessionAsync`), non nel pacchetto standalone ormai deprecato.

#### 2.6.9 Dove vedere i dati scambiati con Unity Gaming Services

Tutto quello che le Sessions creano dietro le quinte (Lobby, Relay, giocatori autenticati) si vede sulla **Unity Cloud Dashboard**: <https://cloud.unity.com>.

1. Apri il progetto collegato al gioco (lo stesso indicato in *Edit → Project Settings → Services* nell'Editor).
2. Controlla in alto di essere nell'ambiente **production**, quello di default.
3. Dal menu dei prodotti:
   - **Lobby**: le lobby attive, con nome (`Name`), giocatori, capienza (`MaxPlayers`) e dati. Qui comparirà "My Lobby".
   - **Relay**: allocazioni e statistiche di utilizzo (connessioni, traffico).
   - **Player Management / Authentication**: i giocatori autenticati in modo anonimo da `AuthenticationWrapper` (§2.3). Ogni istanza del gioco è un giocatore diverso.

I nomi delle voci nel menu cambiano spesso; se non le trovi, usa la ricerca prodotti della dashboard.

- **Le lobby si vedono solo mentre la partita è in corso**: quando l'host esce o si ferma il Play Mode, la sessione viene chiusa e la lobby sparisce dopo poco. Tieni il gioco in Play mentre guardi la dashboard.
- **I grafici di utilizzo non sono in tempo reale**: possono arrivare con qualche minuto di ritardo.
- **Il traffico Netcode** tra host e client (RPC, `NetworkVariable`) non passa dalla dashboard: per quello serve il pacchetto **Multiplayer Tools** (`com.unity.multiplayer.tools`), che aggiunge al Profiler di Unity i moduli di rete e un overlay con le statistiche runtime.

> **Esempio stupido**: la dashboard è il radar Soliton di Metal Gear Solid: vedi chi c'è nella base (lobby attive, giocatori collegati) mentre sei in missione. Ma se esci dalla missione (stop del Play Mode), la base viene sgomberata e il radar resta vuoto. E il radar non ti dice cosa si stanno dicendo le guardie tra loro (il traffico Netcode): per quello serve un altro strumento, la radio intercettata (Multiplayer Tools).

---

## 3. Avvio della sessione — `ConnectionButtons.cs`

File: `Assets/Scripts/ConnectionButtons.cs`

Componente da mettere su un `Canvas` con due bottoni UI:

- **Host** → `NetworkManager.Singleton.StartHost()`: questa istanza fa contemporaneamente da server e da client (gioca e allo stesso tempo comanda la partita).
- **Join** → `NetworkManager.Singleton.StartClient()`: questa istanza si collega a un Host già avviato.

> **Esempio stupido**: è lo split-screen di Crash Team Racing sulla stessa PlayStation: niente internet, tutti sulla stessa macchina. Avvii due istanze del gioco (due finestre Editor/Build): una preme "Host" (accende la console e sceglie la pista), l'altra preme "Join" (attacca il secondo pad). Una terza istanza che preme "Join" è il terzo pad: un altro kart sulla stessa pista.

Non c'è validazione, IP hardcoded o matchmaking: è la versione minima per testare la sincronizzazione in locale.

> **Stato attuale**: `ConnectionButtons` non è più agganciato a nessun bottone nelle scene attuali — il bottone "Host" del Menu ora chiama `MainMenu.StartHost` (§2.5), che passa dalle Sessions/Relay invece che da un IP diretto. Il file resta nel progetto come riferimento/rete di sicurezza per test locali rapidi (due istanze Editor sulla stessa macchina, senza bisogno di Relay), ma il percorso "di produzione" ora è quello descritto in §2.5.

---

## 4. Le fondamenta: sincronizzare un transform in rete — `ClientNetworkTransform.cs`

File: `Assets/Scripts/Utils/ClientNetworkTransform.cs`
Va assegnato ai prefab **Player**, **Treads** e **TurretPivot** al posto del `NetworkTransform` standard.

### Il problema che risolve

Il `NetworkTransform` di Netcode, di default, è **server-authoritative**: solo il server può spostare l'oggetto, ogni modifica locale del client viene ignorata. È sicuro contro i cheat (teleport, speed-hack) ma introduce **latenza**: ogni movimento deve fare un giro di andata e ritorno verso il server prima di essere visibile, e i controlli sembrano "gommosi".

`ClientNetworkTransform` capovolge la regola **solo per il movimento del proprio tank**: il proprietario (owner) scrive direttamente il proprio transform, il server lo riceve e lo ridistribuisce agli altri. Risultato: zero input-lag per chi guida, in cambio di una vulnerabilità accettabile (un client scorretto potrebbe teleportarsi).

> **Esempio stupido**: immagina Rocket League server-authoritative puro: tocchi lo stick, aspetti che il server ti risponda "ok, ora sei qui", e solo allora la macchina si muove. Sembra di guidare sul burro (lento ma sicuro). Con `ClientNetworkTransform` la TUA macchina si muove subito sul tuo schermo e il server si limita a inoltrare la posizione agli altri (veloce, ma un cheater potrebbe dire "sono in porta avversaria" e teletrasportarsi lì).

### Il meccanismo, passo per passo (FLUSSO 0→8, numerazione locale al file)

| FLUSSO | Cosa succede |
|---|---|
| **0** | `OnIsServerAuthoritative()` ritorna `false`: è la riga che dichiara "questo NON è più un NetworkTransform server-authoritative". |
| **1** | `OnNetworkSpawn()` chiama prima `base.OnNetworkSpawn()`, per non rompere il setup standard di Netcode. |
| **2** | Poi imposta `CanCommitToTransform = IsOwner`: solo il proprietario avrà il diritto di "spedire" il proprio transform. |
| **3** | In `Update()`, `CanCommitToTransform` viene ricalcolato **ogni frame** (non solo allo spawn), per restare corretto anche se la proprietà dell'oggetto cambiasse a runtime. |
| **4** | `base.Update()` fa il lavoro vero: se sei owner, "committa" (applica) lo stato locale; se non lo sei, **interpola** verso i valori ricevuti dalla rete (è quello che rende il movimento degli altri fluido e non a scatti). |
| **5** | Guard `NetworkManager != null`: prima dello spawn o fuori sessione potrebbe essere nullo. |
| **6** | Si invia il transform solo se si è davvero connessi (`IsConnectedClient`) o si è il server/host (`IsListening`). |
| **7** | Ultimo filtro: solo chi ha `CanCommitToTransform` (il proprietario, FLUSSO 3) prosegue. Le copie remote si fermano qui e restano in sola interpolazione. |
| **8** | `TryCommitTransformToServer(transform, NetworkManager.LocalTime.Time)`: il proprietario manda il SUO transform al server, insieme a un timestamp che serve agli altri client per interpolare correttamente nel tempo. |

```
Owner del tank                Server                     Altri client
     |  muove localmente        |                             |
     |  (nessun lag: è suo)     |                             |
     |------ transform+time --->|                             |
     |                          |------ sincronizza --------->|
     |                          |                              |  interpola
     |                          |                              |  (FLUSSO 4, ramo "else")
```

Questo stesso componente viene poi usato anche per **TurretPivot** (rotazione Z) e **Treads** (cingoli): stessa logica, assi diversi da sincronizzare.

---

## 5. Il flusso di gioco, in ordine (`FLUSSO 0 → 59`)

Da qui in poi seguiamo la numerazione **globale** del gameplay: dal momento in cui premi un tasto, fino a quando una moneta ricompare in un altro punto della mappa.

### 5.1 Input del giocatore — `InputReader.cs` (FLUSSO 0 → 7c)

File: `Assets/Scripts/Input/InputReader.cs` — è uno **ScriptableObject**, non un componente su un GameObject: è un asset condiviso che chiunque può referenziare (movimento, mira, sparo) senza dover ognuno gestire da sé l'Input System.

> **Esempio stupido**: `InputReader` è il Codec di Metal Gear Solid. Trasmette su una frequenza (`MoveEvent`, `PrimaryFireEvent`), e chiunque sia sintonizzato (PlayerMovement, ProjectileLauncher) riceve il messaggio, senza che il Codec sappia o si preoccupi di chi sta ascoltando. Se domani Snake cambia Codec (gamepad invece di tastiera), Otacon e Campbell non se ne accorgono nemmeno.

| FLUSSO | Cosa succede |
|---|---|
| **0** | `using static Controls;` — `Controls` è la classe generata automaticamente dall'asset `.inputactions`, non scritta a mano. |
| **1** | La classe implementa `IPlayerActions`: un "contratto" che obbliga a fornire `OnMove`, `OnPrimaryFire`, `OnAim`. Sarà l'Input System a chiamarli. |
| **2** | Il campo `controls` è l'istanza runtime di quella classe generata. |
| **3** | `PrimaryFireEvent` e `MoveEvent` sono il "megafono" verso il resto del gioco: l'input grezzo viene ri-emesso come evento C#, così chi ascolta non deve sapere nulla dell'Input System sottostante. |
| **3b** | `AimPosition` invece **non** è un evento ma una proprietà "sempre leggibile" (polling): la posizione del mouse cambia in continuazione e a chi mira serve sempre l'ultimo valore, non una notifica per ogni pixel. |
| **4-6** | `OnEnable()`: crea `Controls` se non esiste, registra questo oggetto come gestore delle callback (`SetCallbacks(this)`) e abilita la lettura (`controls.Enable()`). Senza quest'ultima riga, nessuna callback scatterebbe. |
| **7a** | `OnMove` — ad ogni cambio dell'azione "Move", rilancia il `Vector2` letto tramite `MoveEvent`. |
| **7b** | `OnPrimaryFire` — distingue `performed` (tasto premuto → evento `true`) da `canceled` (tasto rilasciato → evento `false`). |
| **7c** | `OnAim` — a differenza degli altri due, **non** solleva un evento: salva solo l'ultima posizione del mouse, che `PlayerAiming` leggerà da sé ogni frame. |

**Perché questo pattern conviene**: se domani cambi dispositivo di input (gamepad, touch), tocchi solo `InputReader`. Tutto il resto del gioco continua a funzionare perché dipende solo dagli eventi/proprietà astratti, non dai tasti fisici.

### 5.2 Mira della torretta — `PlayerAiming.cs` (FLUSSO 8 → 13)

File: `Assets/Scripts/Core/Player/PlayerAiming.cs`

| FLUSSO | Cosa succede |
|---|---|
| **8** | Riferimenti da Inspector: `inputReader` (da cui leggere `AimPosition`) e `turretTransform` (cosa far ruotare). |
| **9** | Il calcolo avviene in `LateUpdate`, **non** `Update`: la mira va calcolata DOPO che il corpo si è già mosso/ruotato, altrimenti la torretta punterebbe alla posizione del tank di un frame prima (uno "scatto" visivo). |
| **10** | `if (!IsOwner) return;` — solo il proprietario decide dove punta la propria torretta. Sulle copie remote, la rotazione arriva già pronta dalla rete (via `ClientNetworkTransform`, §4). |
| **11** | `AimPosition` è in coordinate **schermo** (pixel del mouse). |
| **12** | Si converte in coordinate **mondo** con `Camera.main.ScreenToWorldPoint`, per poterla confrontare con la posizione della torretta nella scena. |
| **13** | `turretTransform.up = aimWorldPos - (Vector2)turretTransform.position;` — si orienta l'asse "alto" della torretta (dove punta lo sprite del cannone) verso il mouse. |

> **Esempio stupido**: è la telecamera di sorveglianza di Shadow Moses che segue Snake. Il muro su cui è montata (il corpo del tank) può essere girato in qualsiasi modo, ma la telecamera (la torretta) ruota sempre verso il suo bersaglio (il mouse). E ruota DOPO che il muro si è sistemato (`LateUpdate`), altrimenti guarderebbe dove Snake era un frame fa.

### 5.3 Sparo — `ProjectileLauncher.cs` (FLUSSO 14 → 21, + 29)

File: `Assets/Scripts/Core/Player/ProjectileLauncher.cs`

Qui si vede il pattern più importante del progetto: **due proiettili per ogni sparo**.

- `serverProjectilePrefab` → il proiettile **vero**, istanziato SOLO sul server, autorevole, infligge danno reale.
- `clientProjectilePrefab` → un proiettile **dummy**, solo visivo, mostrato subito in locale per dare feedback istantaneo senza aspettare il giro di rete.

> **Esempio stupido**: la palla di Rocket League. Quando la colpisci, sul TUO schermo parte subito (previsione locale = il dummy), senza aspettare nessuno. Ma la traiettoria vera la decide il server, ed è quella che conta per il goal. Per questo a volte, con un ping alto, vedi la palla "scattare" di colpo in un'altra posizione: è il momento in cui la versione vera corregge quella finta.

| FLUSSO | Cosa succede |
|---|---|
| **14** | Dichiarazione dei due prefab (vedi sopra). |
| **15/16** | `OnNetworkSpawn`/`OnNetworkDespawn`: solo il proprietario si iscrive/disiscrive a `PrimaryFireEvent` (mirror del pattern già visto in §5.1 FLUSSO 3 e in `PlayerAiming` FLUSSO 10). |
| **17** | In `Update`, se `shouldFire` è vero e il cooldown (`fireRate`) è passato: si chiama **sia** `PrimaryFireServerRpc(...)` **sia** `SpawnDummyProjectile(...)` nello stesso frame. La ServerRpc non è bloccante: è solo l'invio di un messaggio, ritorna subito. Ecco perché il dummy appare "nello stesso istante" pur essendo scritto dopo nel codice. |
| **18** | `[ServerRpc] PrimaryFireServerRpc` — eseguita SOLO sul server: instanzia il proiettile vero, gli imposta velocità e direzione, ignora la collisione col proprio player, e poi chiama `SpawnDummyProjectileClientRpc` per far comparire il dummy anche sugli altri client. |
| **19** | `[ClientRpc] SpawnDummyProjectileClientRpc` — eseguita su TUTTI i client. Il proprietario, che ha già mostrato il proprio dummy al FLUSSO 17, si esclude con `if (IsOwner) return;` per non duplicarlo: questa callback serve solo agli ALTRI client. |
| **20** | `HandlePrimaryFire` — semplice callback che aggiorna il flag `shouldFire`, letto ogni frame dal FLUSSO 17. |
| **21** | `SpawnDummyProjectile` — helper condiviso tra proprietario (17) e altri client (19): istanzia solo l'effetto visivo, senza alcuna logica di danno (quella vive esclusivamente nel proiettile vero, FLUSSO 18). |
| **29** | Sempre dentro la ServerRpc (18): si chiama `dealDamage.setOwnerClientId(OwnerClientId)` sul proiettile vero, per sapere in seguito chi non deve poter colpire (vedi §5.6). |

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

File: `Assets/Scripts/Utils/DestroySelfOnContact.cs`, `Assets/Scripts/Utils/Lifetime.cs`

- **FLUSSO 22**: il proiettile vero (generato dal server) si autodistrugge al primo contatto. Essendo il server ad averlo istanziato, la distruzione è autorevole e si propaga a tutti.
- **FLUSSO 23**: `Lifetime` è una rete di sicurezza indipendente, su entrambi i tipi di proiettile (vero e dummy): se non colpiscono nulla entro N secondi, si autodistruggono comunque.

> **Esempio stupido**: le casse TNT di Crash Bandicoot. Esplodono se le tocchi (FLUSSO 22), ma partono anche da sole con il conto alla rovescia "3… 2… 1…" anche se non succede niente (FLUSSO 23) — così nessuna cassa resta nel livello per sempre a occupare memoria.

### 5.5 Movimento del corpo — `PlayerMovement.cs` (FLUSSO 24 → 28)

File: `Assets/Scripts/Core/Player/PlayerMovement.cs`

| FLUSSO | Cosa succede |
|---|---|
| **24** | `OnNetworkSpawn`/`OnNetworkDespawn` (invece di `Start`/`OnDestroy`, troppo presto/tardi nel ciclo di vita di rete): solo il proprietario si iscrive/disiscrive a `MoveEvent`. |
| **24b** | `turningRate`: velocità angolare MASSIMA in gradi/secondo. Con input parziale (es. joystick a metà corsa) la rotazione scala proporzionalmente (FLUSSO 26). |
| **25** | Disiscrizione simmetrica al FLUSSO 24. |
| **26** | In `Update()` (non `FixedUpdate`, perché è puramente visivo): `zRotation = input.x * -turningRate * Time.deltaTime`. Il `Time.deltaTime` serve perché `Update` non gira a intervalli fissi: senza, la velocità di rotazione dipenderebbe dal framerate. |
| **27** | In `FixedUpdate()` (fisica, intervallo fisso): `rb.velocity = bodyTransform.up * input.y * movementSpeed`. Si usa `bodyTransform.up` e non gli assi del mondo, così il tank avanza sempre "in avanti" rispetto a come è ruotato in quel momento. |
| **28** | `handleMove` — callback collegata al FLUSSO 24: aggiorna solo `previousMovementInput`, che 26 e 27 leggono ogni frame. |

> **Esempio stupido**: il kart di Crash Team Racing. Sterzi (Update, ogni frame, effetto immediato) e intanto il motore spinge (FixedUpdate, fisica) sempre nella direzione in cui punta il muso del kart — non magicamente verso nord, qualunque sia la curva.

### 5.6 Danno da contatto — `DealDamageOnContact.cs` (FLUSSO 29 → 34)

File: `Assets/Scripts/Core/Combat/DealDamageOnContact.cs` — presente **solo** sul `serverProjectilePrefab`, quindi la sua logica gira per forza solo sul server.

| FLUSSO | Cosa succede |
|---|---|
| **30** | Commento di classe: componente esclusivo del proiettile vero. |
| **31** | `setOwnerClientId` — chiamato dal server subito dopo lo spawn (FLUSSO 29, §5.3) per ricordare chi ha sparato. |
| **32** | Se l'oggetto colpito non ha `Rigidbody2D`, non è un bersaglio valido (es. muri/scenario): si esce subito. |
| **33** | Se il bersaglio ha un `NetworkObject` il cui `OwnerClientId` coincide con chi ha sparato, si esce: **non ci si può ferire da soli**. |
| **34** | Se il bersaglio ha un componente `Health`, gli si infligge danno. |

> **Esempio stupido**: il missile di Crash Team Racing. Appena lanciato, ti passa attraverso senza farti nulla: sa chi l'ha sparato (`OwnerClientId`) e ignora il proprio kart. Senza questo controllo, ogni missile esploderebbe in faccia a chi lo lancia nell'istante stesso in cui esce.

### 5.7 Vita e barra vita — `Health.cs` + `HealthDisplay.cs` (FLUSSO 35 → 40)

File: `Assets/Scripts/Core/Combat/Health.cs`, `Assets/Scripts/Core/Combat/HealthDisplay.cs`

`Health` tiene `currentHealth` in una `NetworkVariable<int>` (scrivibile solo dal server, sincronizzata automaticamente). `HealthDisplay` è puramente estetico (una UI Image "Filled") e vive lato client.

| FLUSSO | Cosa succede |
|---|---|
| **35** | Commento di classe di `HealthDisplay`: ascolta i cambiamenti di `Health.currentHealth` e aggiorna la barra a schermo. |
| **36** | `OnNetworkSpawn` — controllo `IsClient` (non `IsOwner`, non `IsServer`): OGNI client deve vedere la barra vita aggiornata, anche guardando i tank altrui. Ci si iscrive a `OnValueChanged` e si inizializza subito la barra. |
| **37** | Disiscrizione simmetrica in `OnNetworkDespawn`. |
| **38** | `handleHealthChanged` — normalizza la vita corrente sul massimo (`newHealth / MaxHealth`), perché `Image.fillAmount` va da 0 a 1. |
| **39** | `Health.OnNetworkSpawn` — solo il server inizializza `currentHealth.Value = MaxHealth`. Se lo facesse anche ogni client, ci sarebbero scritture concorrenti non autorizzate. |
| **40** | `modifyHealth` — nessun controllo `IsServer` esplicito: il metodo va chiamato solo da codice già server-side (es. `DealDamageOnContact`, FLUSSO 34). Anche se venisse chiamato per errore da un client, Netcode rifiuterebbe comunque la scrittura sulla `NetworkVariable`. |

> **Esempio stupido**: la barra LIFE di Snake in alto a sinistra in Metal Gear Solid. Si limita a MOSTRARE quanta vita hai (HealthDisplay). È il gioco (server) a decidere quanta vita ti toglie il proiettile di una guardia (Health). Colorare di verde la barra con Paint non ti cura.

### 5.8 Sistema monete — `Coin.cs`, `CoinWallet.cs`, `RespawningCoin.cs`, `CoinSpawner.cs` (FLUSSO 41 → 54)

Questa è la catena più lunga e riassume TUTTI i pattern precedenti insieme: server authority, feedback client immediato, eventi, respawn.

**File coinvolti**:
- `Assets/Scripts/Core/Coins/Coin.cs` — classe astratta base.
- `Assets/Scripts/Core/Coins/CoinWallet.cs` — sul player, conta le monete raccolte.
- `Assets/Scripts/Core/Coins/RespawningCoin.cs` — moneta concreta che, invece di sparire, ricompare altrove.
- `Assets/Scripts/Core/Coins/CoinSpawner.cs` — genera le monete all'avvio e le ricolloca quando vengono raccolte.

#### La catena completa, in ordine di esecuzione

| FLUSSO | File | Cosa succede |
|---|---|---|
| **48** | `CoinSpawner` (classe) | All'avvio genera un numero fisso di `RespawningCoin` in punti casuali della mappa; quando una viene raccolta, la ricolloca invece di distruggerla e ricrearla. |
| **49** | `CoinSpawner.OnNetworkSpawn` | Solo il server decide dove spawnare le monete (mirror di `Health.OnNetworkSpawn`, FLUSSO 39). |
| **50** | `CoinSpawner.spawnCoin` | Dopo aver instanziato e spawnato in rete la moneta, il spawner si iscrive al SUO evento `onCollected` (FLUSSO 46), per sapere quando quella specifica istanza viene raccolta. |
| **41** | `CoinWallet.OnTriggerEnter2D` | Il trigger 2D avviene in locale su OGNI client che possiede fisicamente il collider del wallet: si chiama `coin.collect()` **ovunque**, sia server che client. |
| **45** | `Coin.collect` (abstract) | Il valore di ritorno è significativo solo se chi lo calcola è il server: le sottoclassi devono restituire 0 se eseguite su un client. |
| **42** | `RespawningCoin.collect` (ramo client) | `if (!IsServer)`: nasconde la moneta localmente (`showCoin(false)`) per un feedback visivo immediato e ritorna sempre 0. Non è autorevole: non può decidere se la moneta è già stata raccolta né quanto valga. |
| **43** | `RespawningCoin.collect` (ramo server) | Controllo autorevole: se `alreadyCollected` è già vero (doppio trigger nello stesso frame, o due giocatori), ritorna 0 per non accreditare due volte. |
| **51** | `RespawningCoin.collect` (dopo aver marcato `alreadyCollected = true`) | Si invoca `onCollected?.Invoke(this)`: notifica il `CoinSpawner` che questa moneta va ricollocata. |
| **44** | `CoinWallet.OnTriggerEnter2D` | `if (!IsServer) return;` poi `totalCoins.Value += coinValue`: solo il server può scrivere sulla `NetworkVariable`. Sui client `coinValue` è comunque sempre 0 (FLUSSO 42), quindi qui non si tenterebbe comunque nulla di dannoso. |
| **52** | `CoinSpawner.handleCoinCollected` | Callback collegata al FLUSSO 51, gira solo sul server: sposta la moneta in un nuovo punto libero invece di distruggerla. |
| **53** | `RespawningCoin.Reset` | Chiamato subito dopo dal FLUSSO 52: azzera `alreadyCollected`, così il controllo autorevole (FLUSSO 43) torni a considerarla raccoglibile. |
| **54** | `RespawningCoin.Update` | Gira su OGNI client: la nuova `transform.position` decisa dal server (FLUSSO 52) arriva tramite la normale sincronizzazione di rete (`NetworkTransform`). Quando la posizione cambia rispetto al frame precedente, è il segnale che la moneta è stata rispawnata altrove: si annulla quindi il nascondimento locale fatto al FLUSSO 42 (`showCoin(true)`), perché la visibilità dello `SpriteRenderer` non è automaticamente sincronizzata dalla rete, solo il transform lo è. |
| **46** | `RespawningCoin.onCollected` (dichiarazione evento) | Sollevato solo quando `collect()` è eseguito lato server (FLUSSO 51): il modo con cui la singola istanza avvisa il `CoinSpawner` che l'ha creata. |
| **47** | `RespawningCoin.previousPosition` (campo) | Memorizza la posizione dell'ultimo frame, usata dal FLUSSO 54 per rilevare il teleport. |

#### Diagramma dell'intero ciclo di vita di una moneta

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

> **Esempio stupido**: i frutti Wumpa di Crash Bandicoot, in una versione multiplayer. Ci passi sopra e il frutto sparisce SUBITO dal tuo schermo con il suo "pop" (feedback client immediato, FLUSSO 42). Ma il contatore dei Wumpa lo aggiorna solo il server (FLUSSO 44). Se tu e un altro giocatore ci passate sopra nello stesso istante, il server ne dà uno solo (FLUSSO 43). Poi, invece di distruggere il frutto e crearne uno nuovo, il server lo teletrasporta in un altro punto del livello (FLUSSO 52-54).

### 5.9 Costo in monete per sparare, e polvere sui proiettili distrutti — `ProjectileLauncher.cs`, `CoinWallet.cs`, `SpawnOnDestroy.cs` (FLUSSO 55 → 59)

File coinvolti:
- `Assets/Scripts/Utils/SpawnOnDestroy.cs` — nuovo componente puramente estetico.
- `Assets/Scripts/Core/Player/ProjectileLauncher.cs` — aggiunta economica allo sparo.
- `Assets/Scripts/Core/Coins/CoinWallet.cs` — nuovo metodo `spendCoins`.

Prima esistevano solo le monete come *punteggio*; ora sparare **costa** monete, riusando esattamente lo stesso `CoinWallet` visto in §5.8 sia per accreditarle sia per scalarle. Segue la stessa "regola d'oro": il client fa un controllo rapido per non sprecare rete, ma solo il server decide davvero.

| FLUSSO | File | Cosa succede |
|---|---|---|
| **55** | `SpawnOnDestroy.cs` | Componente estetico agganciato a `OnDestroy` (ciclo di vita di Unity, non di rete): va sul proiettile dummy e, quando questo viene distrutto (per contatto o per `Lifetime`, FLUSSO 23), instanzia in locale un effetto (es. `DustCloud`), senza `Spawn()` di rete. |
| **56** | `ProjectileLauncher.cs` | Campo `wallet`: riferimento al `CoinWallet` del proprietario, usato dai controlli sotto. |
| **56b** | `ProjectileLauncher.cs` | Campo `costToFire`: costo in monete di ogni sparo. |
| **57** | `ProjectileLauncher.cs` | Campo `timer`: cooldown tra due spari, scalato ogni frame in `Update` e ricaricato a `1/fireRate` dopo uno sparo riuscito. |
| **57b** | `ProjectileLauncher.Update` | Controllo *cosmetico* lato client: se le monete non bastano, evita di inviare la ServerRpc e di mostrare un dummy che il server rifiuterebbe comunque. Non è autorevole. |
| **58** | `ProjectileLauncher.PrimaryFireServerRpc` | Controllo *autorevole* lato server: ripete la stessa verifica (perché il controllo client, FLUSSO 57b, è solo un'ottimizzazione aggirabile) e, solo se le monete bastano davvero, procede e scala il costo. |
| **59** | `CoinWallet.spendCoins` | Sottrae `costToFire` da `totalCoins`, la stessa `NetworkVariable` accreditata da `OnTriggerEnter2D` al FLUSSO 44. Chiamato solo dal server. |

> **Esempio stupido**: il boost di Rocket League. Le monete sono i cuscinetti di boost che raccogli sul campo, e sparare è premere il tasto boost. Il tuo gioco controlla per primo "ho ancora boost nella barra?" e, se è vuota, non prova neanche (client, FLUSSO 57b). Ma è il server a controllare davvero quanto boost hai e a scalarlo (FLUSSO 58-59): se modifichi il gioco per avere boost infinito, il server ti risponde comunque "barra vuota".

---

## 6. Tabella riepilogativa di tutti i FLUSSO (solo gameplay)

Riferimento rapido, in ordine numerico. "∞" = catena locale a `ClientNetworkTransform.cs` (numerazione indipendente). Il bootstrap/rete (`ApplicationController`, `ClientSingleton`/`HostSingleton`, `AuthenticationWrapper`, `ClientGameManager`/`HostGameManager`) **non è più numerato**: è documentato per file/argomento in §2.

| # | File | In breve |
|---|---|---|
| ∞0-8 | `ClientNetworkTransform.cs` | Meccanismo di sync client-authoritative del transform (vedi §4) |
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

**Bootstrap/rete (`ApplicationController`, `ClientSingleton`/`HostSingleton`, `AuthenticationWrapper`, `ClientGameManager`/`HostGameManager`, `MainMenu`)**: vedi §2, organizzato per file. In breve: `ApplicationController` decide dedicated server vs client e crea sempre entrambi i singleton (§2.1); `ClientSingleton`/`HostSingleton` sono contenitori "singleton lazy" per le rispettive classi C# pure (§2.2, §2.4); `AuthenticationWrapper` gestisce il login anonimo con una macchina a stati (§2.3, invariato dal corso); `HostGameManager.StartHostAsync`/`ClientGameManager.startClientAsync` creano/entrano in una `Session` tramite `MultiplayerService.Instance` invece di chiamare `Relay.Instance` direttamente (§2.5, §2.6 per il confronto dettagliato col corso).

---

## 7. Catalogo di pattern riusabili (per un gioco nuovo, da zero)

Qui sotto, ogni pattern è estratto dal progetto ma riscritto in forma **generica**, scollegata dai tank, pronta da adattare a qualsiasi altro gioco.

### 7.1 Stato autorevole con `NetworkVariable`

Quando un valore deve essere uguale per tutti e non falsificabile da un client (punteggio, vita, oro, ecc.).

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

Un client chiama `AddPointServerRpc()`, ma è il server ad eseguire l'incremento vero. Tutti i client vedono `score.Value` aggiornarsi da solo, senza scrivere altro codice di sincronizzazione.

### 7.2 Movimento client-authoritative

Quando serve zero lag sui controlli del proprio personaggio (vedi §4 per l'implementazione completa): sostituisci il `NetworkTransform` standard con una variante che, per il solo owner, imposta `CanCommitToTransform = IsOwner` e ritorna `false` da `OnIsServerAuthoritative()`.

### 7.3 Feedback immediato al client ("dummy pattern")

Quando un'azione deve SEMBRARE istantanea anche se la conferma autorevole richiede un giro di rete (vedi §5.3).

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

Regola pratica: **tutto ciò che è "solo estetica" può girare ovunque; tutto ciò che "conta" (danno, punteggio, stato) deve girare solo dove `IsServer` è vero.**

### 7.4 Cheat-sheet: quale controllo usare

| Proprietà | Vero quando | Usala per |
|---|---|---|
| `IsServer` | Questa istanza è il server (o l'host) | Logica autorevole: danno, punteggio, spawn di nemici/oggetti |
| `IsClient` | Questa istanza è un client (o l'host, che è anche client) | UI, effetti visivi, suoni: cose che TUTTI devono vedere |
| `IsOwner` | Questa istanza possiede l'oggetto | Input e controlli del PROPRIO personaggio soltanto |
| `IsListening` | Il server/host è attivo | Guard prima di inviare dati di rete |
| `IsConnectedClient` | Il client è connesso a un host | Guard prima di inviare dati di rete |

Domanda da farsi sempre: *"questo codice deve girare per il PROPRIETARIO, per TUTTI I CLIENT, o solo per IL SERVER?"* — è la prima cosa da decidere prima di scrivere un `if`.

### 7.5 Iscriviti/disiscriviti in `OnNetworkSpawn`/`OnNetworkDespawn`

Mai in `Start`/`OnDestroy` (troppo presto/tardi nel ciclo di vita di rete). Sempre a specchio, per evitare eventi che richiamano componenti già distrutti.

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

### 7.6 Bus di eventi disaccoppiato (pattern `InputReader`)

Uno `ScriptableObject` condiviso che espone eventi C# invece di essere letto direttamente da tutti. Utile ovunque serva scollegare "chi genera un dato" da "chi lo consuma" (non solo per l'input: es. un `GameEventChannel` per "partita iniziata", "partita finita", ecc.).

```csharp
[CreateAssetMenu]
public class GameEvents : ScriptableObject
{
    public event Action OnMatchStarted;
    public void RaiseMatchStarted() => OnMatchStarted?.Invoke();
}
```

### 7.7 Controllo idempotente lato server ("già fatto?")

Quando un'azione potrebbe arrivare due volte nello stesso frame (doppio trigger, due giocatori, pacchetti duplicati) e va eseguita una volta sola (vedi `alreadyCollected` in `RespawningCoin`, `isDead` in `Health`).

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

### 7.8 Respawn/riposiziona invece di distruggi/ricrea

Quando un oggetto "raccoglibile" deve ricomparire (moneta, powerup): non distruggerlo e ricrearlo, spostalo e resettalo. Risparmia una `Spawn()`/`Despawn()` di rete e mantiene stabile il suo `NetworkObjectId`.

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

Quando un proiettile/effetto non deve colpire chi lo ha generato: salva l'`OwnerClientId` allo spawn e confrontalo al momento dell'impatto (vedi §5.6).

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

### 7.10 Avvio sessione tramite Sessions/Relay (niente IP diretto)

Quando l'host e i client non sono sulla stessa rete locale e non puoi contare su IP pubblici/port forwarding (praticamente sempre, fuori da un test in LAN). Vedi §2.5 e §2.6 per l'implementazione completa e per il confronto con l'API diretta di Relay (deprecata, vedi nota sotto).

```csharp
using Unity.Services.Multiplayer;

public async Task<string> StartHostAsync(int maxConnections)
{
    var options = new SessionOptions
    {
        Name = "My Lobby",        // la Session e' anche una Lobby: questo e' il nome in lista
        MaxPlayers = maxConnections,
        IsPrivate = false         // true = raggiungibile solo con il codice
    }.WithRelayNetwork();
    ISession session = await MultiplayerService.Instance.CreateSessionAsync(options);
    // session.Code e' gia' il join code: mostralo all'utente, cosi' possa condividerlo.
    // NetworkManager e' gia' avviato come Host: nessuna configurazione manuale del transport.
    return session.Code;
}

public async Task JoinAsClientAsync(string joinCode)
{
    ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
    // NetworkManager e' gia' avviato come Client: nessuna configurazione manuale del transport.
}

public async Task JoinFromListAsync()
{
    // Lista lobby pubbliche, poi join della prima con posti liberi (vedi §2.6.7)
    QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());
    ISessionInfo first = results.Sessions.FirstOrDefault(s => s.AvailableSlots > 0);
    if (first != null)
    {
        await MultiplayerService.Instance.JoinSessionByIdAsync(first.Id);
    }
}
```

> **Esempio stupido**: due modi per entrare nella partita di un amico in Rocket League. Ti manda nome e password della partita privata (`JoinSessionByCodeAsync`), oppure la cerchi tu nella lista delle partite pubbliche (`QuerySessionsAsync` + `JoinSessionByIdAsync`). In entrambi i casi, una volta dentro, la connessione la gestisce il gioco.

> ⚠️ **Nota storica**: fino a gennaio 2026 questo pattern si scriveva chiamando `Relay.Instance.CreateAllocationAsync`/`GetJoinCodeAsync`/`JoinAllocationAsync` e configurando a mano `UnityTransport.SetRelayServerData` prima di `NetworkManager.Singleton.StartHost()`/`StartClient()` (è quello che mostra ancora il corso). Da settembre 2026 `com.unity.services.relay` standalone è deprecato: usa `com.unity.services.multiplayer` e le Sessions come sopra. Il dettaglio completo, con il codice vecchio a confronto riga per riga, è in §2.6.

---

## 8. Checklist mentale per ogni nuovo componente di rete

Prima di scrivere un componente multiplayer nuovo, rispondi in ordine a queste domande (ricalcano esattamente le decisioni prese in questo progetto):

1. **Questo valore/decisione deve essere uguale per tutti i giocatori?**
   → Sì: usa una `NetworkVariable` o validalo dentro una `[ServerRpc]`, scrivendo SOLO se `IsServer`.
2. **Questo comportamento riguarda solo il MIO personaggio (input, mira, movimento)?**
   → Metti `if (!IsOwner) return;` in cima al metodo.
3. **Serve che TUTTI (anche chi non è owner né server) vedano qualcosa (UI, barra vita, effetto)?**
   → Usa `IsClient`, e ascolta un cambiamento di `NetworkVariable` (`OnValueChanged`) invece di leggerla in `Update`.
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

---

## 9. Glossario rapido

- **Host**: istanza che è contemporaneamente server e client.
- **Server**: istanza autorevole, senza rendering per il giocatore (a meno che non sia anche Host).
- **Client**: istanza che si connette a un server/host, gioca ma non decide da sola l'esito delle azioni.
- **Owner / Ownership**: il client "proprietario" di un `NetworkObject` (di solito il proprio personaggio).
- **RPC (Remote Procedure Call)**: chiamata di metodo che attraversa la rete (`ServerRpc` client→server, `ClientRpc` server→client/i).
- **Interpolazione**: tecnica per rendere fluido il movimento di oggetti remoti, "riempiendo" visivamente lo spazio tra due posizioni ricevute dalla rete.
- **Prefab di rete**: prefab con un componente `NetworkObject`, registrabile nel `NetworkManager` per poter essere instanziato e sincronizzato in partita.
- **Autorevole (authoritative)**: chi ha l'ultima parola su un valore/decisione; nel progetto, quasi sempre il server.
- **Relay**: servizio Unity che fa da "postino neutrale" tra host e client, senza bisogno di IP pubblici o port forwarding. Nel corso si chiama direttamente (`com.unity.services.relay`, pacchetto oggi deprecato); in questo progetto è invocato indirettamente tramite le Sessions (§2.6).
- **Multiplayer Services SDK (MPS SDK)**: il pacchetto unificato `com.unity.services.multiplayer`, che da settembre 2026 sostituisce i pacchetti standalone Lobby/Relay/Matchmaker/Multiplay. Vedi §2.6.
- **Session / `ISession`**: l'astrazione centrale del Multiplayer Services SDK: rappresenta una partita in corso (o in fase di creazione), tenendo insieme allocazione Relay, join code e avvio di `NetworkManager`. Vedi §2.6.6.
- **`SessionOptions`**: la configurazione passata a `CreateSessionAsync` (nome, numero massimo di giocatori, pubblica/privata, tipo di rete via `.WithRelayNetwork()`/`.WithDistributedAuthorityNetwork()`). Vedi §2.6.6.
- **Lobby**: la "sala d'attesa" pubblica di una partita: ha un nome, una capienza e compare nelle ricerche degli altri giocatori. Nel corso si crea a mano (`com.unity.services.lobby`, deprecato); in questo progetto ogni Session è già una Lobby. Vedi §2.6.7.
- **Heartbeat**: il "sono ancora vivo" che l'host deve mandare periodicamente alla Lobby, altrimenti Unity la chiude. Con le Sessions lo manda l'SDK da solo. Vedi §2.6.7.
- **Unity Cloud Dashboard**: il sito (<https://cloud.unity.com>) dove vedere lobby attive, uso del Relay e giocatori autenticati. Vedi §2.6.9.

---

*Documento aggiornato a ottobre 2026 (aggiunte le Lobby, §2.6.7). Le sezioni di gameplay (§1, §4, §5, §6) restano generate a partire dai commenti `[FLUSSO N]` presenti nel codice sorgente: se aggiungi nuove funzionalità di gameplay, continua la numerazione da 90 in poi e aggiorna la tabella in §6. La sezione di bootstrap/rete (§2) non usa più questa numerazione: è stata riscritta per documentare la migrazione a Unity Multiplayer Services SDK (Sessions), resa necessaria dalla deprecazione dei pacchetti standalone Lobby/Relay/Matchmaker/Multiplay avvenuta dopo la registrazione del corso — vedi §2.6 per il dettaglio completo.*
