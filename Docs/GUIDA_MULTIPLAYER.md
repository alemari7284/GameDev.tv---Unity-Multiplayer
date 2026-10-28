# Guida alla Codebase Multiplayer

> Documentazione completa del progetto (tank 2D multiplayer, Unity + **Netcode for GameObjects** v1.12.2).
> Obiettivo: capire ogni riga di rete scritta finora e usarla come **base di partenza per costruire altri giochi multiplayer da zero**.

---

## 0. Come leggere questo documento

Il codice di **gameplay** (§4, §5) è raccontato in **ordine cronologico**, cioè nell'ordine in cui viene eseguito durante una partita, non nell'ordine dei file: ingresso in partita → input → mira → sparo → danno → vita → monete → morte e respawn. Ogni tabella indica il metodo o il campo in cui succede ogni passo, così puoi aprire il file e trovarlo subito.

Il codice di **bootstrap/rete** (§2 — avvio dell'app, autenticazione, avvio della sessione Host/Client) è invece organizzato per file/argomento, con il codice vero incollato dentro: quei file sono stati riscritti a settembre 2026 per una migrazione dell'ecosistema Unity Gaming Services avvenuta dopo la registrazione del corso (§2.6 spiega tutto nel dettaglio).

`ClientNetworkTransform.cs` descrive un meccanismo a sé stante (la sincronizzazione del transform) su cui si appoggia il resto del gameplay: per questo lo trattiamo come **fondamenta**, prima del flusso principale di gioco (§4).

Questo documento è organizzato così:

1. Concetti Netcode da sapere prima di leggere il codice (§1)
2. Bootstrap dell'app, autenticazione e avvio della sessione di rete — organizzato per file (§2 — il vero punto di partenza, prima ancora del flusso di gioco; include §2.6, la spiegazione dettagliata di come e perché questa parte diverge dal corso, §2.7 sul nome del giocatore e la connection approval, §2.8 su disconnessione e pulizia, §2.9 sull'avvio dall'Editor)
3. Il vecchio sistema di test locale con IP diretto (§3)
4. Le fondamenta: come si muove un oggetto in rete (§4 — `ClientNetworkTransform`)
5. Il flusso di gioco vero e proprio, in ordine cronologico (§5)
6. Catalogo di pattern riusabili, con mini-esempi "stupidi" scollegati dal progetto, pronti per essere copiati in un gioco nuovo (§6)
7. Checklist mentale da seguire ogni volta che scrivi un componente di rete nuovo (§7)
8. Glossario (§8)

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

Questo è il **vero** primo codice di rete eseguito all'avvio del gioco, prima ancora della scena con `ConnectionButtons` (§3): vive nella scena `NetBootstrap`. Prima di lei c'è solo la scena `Bootstrap` (build index 0), dove il giocatore sceglie il proprio nome (`NameSelector`, §2.7). L'ordine completo delle scene nelle Build Settings è:

```
Bootstrap (0)      NameSelector: scelta del nome, salvato in PlayerPrefs       (§2.7)
   |
NetBootstrap (1)   ApplicationController + NetworkManager: login UGS            (§2.1)
   |
Menu (2)           MainMenu + LobbiesList: Host / Join / lista lobby            (§2.5, §2.6.7)
   |
Game (3)           la partita vera, caricata dall'host via NetworkManager.SceneManager
```

Il suo scopo è distinguere un dedicated server da un giocatore normale e, nel secondo caso, autenticare il giocatore presso **Unity Gaming Services (UGS)** prima di lasciarlo entrare nel menu, per poi aprire/entrare in una partita.

> **Esempio stupido**: è l'inizio di Metal Gear Solid. Prima di arrivare nella base (il menu/la partita vera), Snake passa dal molo e deve farsi riconoscere al Codec dal Colonnello (autenticazione anonima): solo dopo si apre l'ascensore. Il personale della base (dedicated server) invece entra dall'ingresso di servizio, senza Codec e senza schermo.

**File coinvolti**:
- `Assets/Scripts/Networking/ApplicationController.cs` — punto di ingresso, decide dedicated server vs client.
- `Assets/Scripts/Networking/Client/ClientSingleton.cs` + `ClientGameManager.cs` — bootstrap lato client, autenticazione, avvio come client di una sessione.
- `Assets/Scripts/Networking/Client/AuthenticationWrapper.cs` — wrapper attorno a Unity Authentication Service.
- `Assets/Scripts/Networking/Host/HostSingleton.cs` + `HostGameManager.cs` — bootstrap lato host, avvio come host di una sessione.
- `Assets/Scripts/UI/MainMenu.cs` — bottoni "Host"/"Join" nella scena `Menu`.
- `Assets/Scripts/UI/LobbiesList.cs` + `LobbyItem.cs` — lista delle lobby pubbliche nella scena `Menu` (§2.6.7).
- `Assets/Scripts/UI/NameSelector.cs` + `Assets/Scripts/Networking/Shared/UserData.cs` + `Assets/Scripts/Networking/Server/NetworkServer.cs` — nome del giocatore e connection approval (§2.7).
- `Assets/Scripts/Networking/Client/NetworkClient.cs` — ritorno al Menu quando la connessione cade (§2.8).
- `Assets/Editor/StartupSceneLoader.cs` — script solo-Editor che fa partire il Play Mode sempre dalla scena 0 (§2.9).

> ⚠️ **Nota su questa sezione**: questa parte del progetto è stata riscritta a settembre 2026 per inseguire una migrazione dell'ecosistema Unity Gaming Services avvenuta *dopo* la registrazione del corso (gennaio 2026): i pacchetti che il corso usa direttamente (`com.unity.services.relay`, `.lobby`, `.matchmaker`, `.multiplay`) sono stati deprecati e sostituiti da un unico pacchetto unificato, `com.unity.services.multiplayer`. Tutto il §2.6 qui sotto spiega nel dettaglio **cosa dice il corso**, **cosa dice invece questo progetto**, e **perché**.

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

    private void OnDestroy()
    {
        gameManager?.Dispose();
    }
}
```

Pattern singleton "lazy" (`Instance`), identico nello spirito a `NetworkManager.Singleton`: cercato solo quando serve, non subito in `Awake`. `Start()` chiama `DontDestroyOnLoad` per lo stesso motivo di `ApplicationController`. `createClient()` crea il `ClientGameManager` e gli delega subito `initAsync()`, il cui `bool` risale fino ad `ApplicationController` (§2.1). `OnDestroy()` è l'unico aggancio al ciclo di vita Unity che una classe C# pura come `ClientGameManager` può avere: il `MonoBehaviour` contenitore, quando viene distrutto (in pratica alla chiusura del gioco, essendo `DontDestroyOnLoad`), chiama `Dispose()` sul manager (§2.8).

`ClientGameManager.cs` — parte **in buona parte invariata** dalla migrazione, tranne `startClientAsync`/`startClientByIdAsync` (spiegati in dettaglio in §2.6). `setConnectionData` e `networkClient` arrivano invece dalle lezioni successive (§2.7, §2.8):

```csharp
public class ClientGameManager : IDisposable
{
    private const string menuSceneName = "Menu";
    private ISession session;

    private NetworkClient networkClient;

    public async Task<bool> initAsync()
    {
        await UnityServices.InitializeAsync();

        networkClient = new NetworkClient(NetworkManager.Singleton);

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

    private void setConnectionData() { /* vedi §2.7 */ }

    public void Dispose()
    {
        networkClient?.Dispose();
    }
}
```

- **`initAsync`**: `UnityServices.InitializeAsync()` — va chiamato una volta per processo prima di qualunque servizio UGS (compresi Authentication e Multiplayer Services) — poi crea il `NetworkClient` (§2.8) e delega a `AuthenticationWrapper.doAuth()` (§2.3). **Invariato dal corso.**
- **`goToMenu`**: `SceneManager.LoadScene("Menu")`, chiamato solo se l'autenticazione è riuscita. **Invariato dal corso.**
- **`startClientAsync`**: **questo è il metodo migrato**. Nel corso, qui c'era `Relay.Instance.JoinAllocationAsync` + configurazione manuale di `UnityTransport` + `NetworkManager.Singleton.StartClient()`. Oggi c'è un'unica chiamata a `MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode)`, che entra nella sessione creata dall'host e avvia da sola questa istanza come client connesso via Relay. Il dettaglio completo, con codice prima/dopo, è in §2.6.
- **`startClientByIdAsync`**: gemello del precedente per entrare da una lobby scelta in lista (§2.6.7).
- **`setConnectionData`**: chiamato in entrambi i metodi **prima** del `JoinSessionBy*Async`, per allegare nome e id del giocatore alla richiesta di connessione (§2.7).
- **`Dispose`**: sgancia il `NetworkClient` dagli eventi del `NetworkManager` (§2.8).

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

    private void OnDestroy()
    {
        GameManager?.Dispose();
    }
}
```

Pattern singleton "lazy", identico a `ClientSingleton` (§2.2). `createHost` si limita a istanziare `HostGameManager`: a differenza di `createClient` non c'è qui nessuna logica asincrona da avviare — quella è tutta dentro `StartHostAsync` (§2.5), chiamata più tardi, quando l'utente preme davvero "Host". `GameManager` è una proprietà pubblica (non un campo privato) proprio per essere leggibile dall'esterno, da `MainMenu.StartHost` (§2.5) e da `TankPlayer` (che ci passa per raggiungere `networkServer`, §5.0). `OnDestroy` chiama `Dispose()` sul manager, che spegne il server e cancella la Session (§2.8).

**Non toccato dalla migrazione**: questo file non chiama nessuna API di rete direttamente, quindi non c'era nulla da migrare.

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

`StartHost` è agganciato all'`OnClick` del bottone "Host" nella scena `Menu` (Inspector, non codice); `startClient` è agganciato al bottone "Join", e legge il join code da un `TMP_InputField` in scena. `async void` va bene SOLO qui perché sono entrambi event handler UI: nessuno "aspetta" il completamento, eventuali eccezioni non catturate finirebbero solo in console. Entrambi funzionano solo se si è arrivati al Menu passando da `NetBootstrap` (dove `HostSingleton`/`ClientSingleton` vengono creati, §2.1): aprire la scena Menu direttamente lascia `Instance` a `null` e lancia una `NullReferenceException` — non un bug di questi metodi, ma dell'ordine di avvio delle scene. Nell'Editor questo problema è ora evitato da `StartupSceneLoader` (§2.9), che al Play riporta sempre alla scena 0.

```csharp
// Assets/Scripts/Networking/Host/HostGameManager.cs
public class HostGameManager : IDisposable
{
    private IHostSession session;
    private const string gameSceneName = "Game";
    private const int maxConnections = 20;

    public NetworkServer networkServer { get; private set; }

    public async Task StartHostAsync()
    {
        string playerName = PlayerPrefs.GetString(NameSelector.playerNameKey, "Unknown");

        // Prima di CreateSessionAsync: vedi §2.7
        networkServer = new NetworkServer(NetworkManager.Singleton);
        // ... UserData -> JSON -> byte[] -> NetworkConfig.ConnectionData

        try
        {
            var options = new SessionOptions
            {
                Name = $"{playerName}'s Lobby",
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

    public void Dispose() { /* vedi §2.8 */ }
}
```

- **Prima** della sessione si creano il `NetworkServer` e il payload di connessione dell'host: `CreateSessionAsync` avvia già l'Host, e anche l'Host passa dalla connection approval con i propri dati (§2.7).
- `session` è di tipo `IHostSession` (non `ISession`): è quello che `CreateSessionAsync` restituisce davvero, ed espone le operazioni riservate all'host, come `DeleteAsync` usata in `Dispose` (§2.8).
- `CreateSessionAsync(options)` con `.WithRelayNetwork()` fa, **in un'unica chiamata**, tutto quello che nel corso richiedeva quattro passi separati (alloca il Relay, genera il join code, configura `UnityTransport`, avvia `NetworkManager.StartHost()`): vedi §2.6 per il confronto riga per riga con il codice del corso.
- La stessa chiamata crea anche la **Lobby**: `Name` è il nome con cui la partita comparirà nella lista lobby (costruito dal nome scelto dal giocatore in `NameSelector`, es. "Snake's Lobby"), `IsPrivate = false` la rende visibile nelle ricerche pubbliche. Nessuna chiamata separata a `CreateLobbyAsync`, nessun heartbeat: vedi §2.6.7.
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
new NetworkServer(...) + NetworkConfig.ConnectionData = {nome, authId}   (§2.7)
      |
MultiplayerService.Instance.CreateSessionAsync(
    new SessionOptions{ Name = "<nome>'s Lobby", MaxPlayers = 20, IsPrivate = false }
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

**Cosa manca ancora**: mostrare `session.Code` a schermo (oggi solo loggato), e un bottone per uscire dalla partita (`LeaveAsync()`). La disconnessione *subita* (host che chiude, connessione persa) è invece già gestita da `NetworkClient` (§2.8).

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
- **`ClientSingleton.cs` / `HostSingleton.cs`** (§2.2, §2.4): identici al corso.
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
| `IHostSession` | `Unity.Services.Multiplayer` | Sottotipo di `ISession` restituito da `CreateSessionAsync`: aggiunge le operazioni riservate all'host (modificare proprietà, espellere giocatori, cancellare la sessione) |
| `session.DeleteAsync()` | `IHostSession` | Chiude la sessione per tutti e cancella la Lobby lato servizio. Sostituisce `Lobbies.Instance.DeleteLobbyAsync(lobbyId)` del corso (§2.8) |

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
    Name = $"{playerName}'s Lobby", // nome nella lista lobby (era "My Lobby" prima di §2.7)
    MaxPlayers = maxConnections,
    IsPrivate = false               // visibile in QuerySessionsAsync
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

**Lato client: la lista lobby** — file `Assets/Scripts/UI/LobbiesList.cs` (il pannello con la lista e il bottone "Refresh"), `Assets/Scripts/UI/LobbyItem.cs` (una riga della lista, con il bottone "Join") e il nuovo metodo `ClientGameManager.startClientByIdAsync`.

**Prima** (come da corso):
```csharp
// LobbiesList.refreshList
QueryLobbiesOptions options = new QueryLobbiesOptions();
options.Count = 25;
options.Filters = new List<QueryFilter>()
{
    new QueryFilter(field: QueryFilter.FieldOptions.AvailableSlots, op: QueryFilter.OpOptions.GT, value: "0"),
    new QueryFilter(field: QueryFilter.FieldOptions.IsLocked, op: QueryFilter.OpOptions.EQ, value: "0"),
};
QueryResponse lobbies = await Lobbies.Instance.QueryLobbiesAsync(options);
foreach (Lobby lobby in lobbies.Results) { /* Instantiate LobbyItem, init(this, lobby) */ }

// LobbiesList.JoinAsync
Lobby joiningLobby = await Lobbies.Instance.JoinLobbyByIdAsync(lobby.Id);
string joinCode = joiningLobby.Data["JoinCode"].Value;
await ClientSingleton.Instance.gameManager.startClientAsync(joinCode);

// LobbyItem.init
lobbyPlayersText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";
```

**Dopo** (in questo progetto):
```csharp
// LobbiesList.refreshList
var options = new QuerySessionsOptions
{
    Count = 25,
    FilterOptions = new List<FilterOption>
    {
        new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater),
        new FilterOption(FilterField.IsLocked, "0", FilterOperation.Equal),
    }
};
QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(options);
foreach (ISessionInfo sessionInfo in results.Sessions) { /* Instantiate LobbyItem, init(this, sessionInfo) */ }

// LobbiesList.JoinAsync
await ClientSingleton.Instance.gameManager.startClientByIdAsync(sessionInfo.Id);

// ClientGameManager.startClientByIdAsync
session = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);

// LobbyItem.init
int playerCount = sessionInfo.MaxPlayers - sessionInfo.AvailableSlots;
lobbyPlayersText.text = $"{playerCount}/{sessionInfo.MaxPlayers}";
```

| Pezzo del corso | Cosa diventa con le Sessions | Note |
|---|---|---|
| `QueryLobbiesOptions` + `QueryFilter(field, op, value)` | `QuerySessionsOptions` + `FilterOption(field, value, operation)` | Stessi filtri, solo l'ordine dei parametri cambia |
| `Lobbies.Instance.QueryLobbiesAsync` → `QueryResponse.Results` | `MultiplayerService.Instance.QuerySessionsAsync` → `QuerySessionsResults.Sessions` | |
| `Lobby` (in `LobbyItem` e `JoinAsync`) | `ISessionInfo` | Riassunto in sola lettura della sessione: `Id`, `Name`, `MaxPlayers`, `AvailableSlots`… Dietro le quinte è proprio un wrapper attorno alla `Lobby` |
| `lobby.Players.Count` | `MaxPlayers - AvailableSlots` | `ISessionInfo` non espone la lista dei giocatori |
| `JoinLobbyByIdAsync` + `Data["JoinCode"]` + `startClientAsync(joinCode)` | `startClientByIdAsync(sessionInfo.Id)` → `JoinSessionByIdAsync` | Il join code del Relay lo legge e lo usa l'SDK da solo |
| `catch (LobbyServiceException e) { }` vuoto | `catch (Exception e) { Debug.LogError(e); }` | Un catch vuoto nasconde gli errori: almeno li vediamo in console |

Due bug del codice copiato, corretti durante l'adattamento:
- **`Onable` invece di `OnEnable`**: Unity chiama i metodi "magici" solo se il nome è esatto. Con il refuso la lista non si caricava mai da sola all'apertura del pannello: andava premuto "Refresh" a mano.
- **`isJoining` bloccato a `true`**: nel corso, in caso di errore il `return` dentro il `catch` saltava `isJoining = false`, e da quel momento ogni click su "Join" veniva ignorato. Ora gli errori li gestisce `startClientByIdAsync` e `isJoining = false` viene raggiunto sempre.

> **Esempio stupido — la lista lobby**: è la schermata di selezione livello di Crash Bandicoot, ma per le partite: vedi tutti i "portali" aperti, con quanti posti liberi ha ciascuno (`AvailableSlots`), e saltandoci dentro (`JoinSessionByIdAsync`) ti ritrovi direttamente nel livello, senza dover digitare nessun codice.

**Come adattiamo il codice del corso**: il codice copiato dalle lezioni viene marcato con un commento `// from udemy`; poi viene riscritto per le Sessions, il marker viene rimosso e le parti inutili (come l'heartbeat) vengono eliminate, con un commento che spiega a cosa corrispondono nel corso.

#### 2.6.8 Cosa manca ancora rispetto a un flusso completo

- Mostrare `session.Code` in una UI (oggi è solo loggato in console).
- Un bottone/flusso per lasciare la sessione (`session.LeaveAsync()`, non ancora richiamato da nessuna parte). Lato host la chiusura esiste già, ma solo alla chiusura del gioco: `HostGameManager.Dispose` chiama `session.DeleteAsync()` (§2.8).
- Lato client, quando la connessione cade `NetworkClient` riporta al Menu (§2.8) ma il campo `ClientGameManager.session` resta valorizzato: non viene chiamato `LeaveAsync()`, quindi il giocatore può restare nella Lobby lato servizio finché questa non lo rimuove da sola.
- Gestione più ricca degli errori: la documentazione Unity consiglia di intercettare in modo specifico `SessionException` (sottoclasse di `Exception`) per distinguere gli errori delle Sessions da altri errori generici — oggi il codice cattura solo `Exception` generico, coerente con lo stile "minimale" tenuto finora dal corso.
- Se il corso introdurrà più avanti il Matchmaker, andrà anch'esso tradotto nelle API delle Sessions (`MultiplayerService.Instance.MatchmakeSessionAsync`), non nel pacchetto standalone ormai deprecato.

#### 2.6.9 Dove vedere i dati scambiati con Unity Gaming Services

Tutto quello che le Sessions creano dietro le quinte (Lobby, Relay, giocatori autenticati) si vede sulla **Unity Cloud Dashboard**: <https://cloud.unity.com>.

1. Apri il progetto collegato al gioco (lo stesso indicato in *Edit → Project Settings → Services* nell'Editor).
2. Controlla in alto di essere nell'ambiente **production**, quello di default.
3. Dal menu dei prodotti:
   - **Lobby**: le lobby attive, con nome (`Name`), giocatori, capienza (`MaxPlayers`) e dati. Qui comparirà "<nome del giocatore>'s Lobby".
   - **Relay**: allocazioni e statistiche di utilizzo (connessioni, traffico).
   - **Player Management / Authentication**: i giocatori autenticati in modo anonimo da `AuthenticationWrapper` (§2.3). Ogni istanza del gioco è un giocatore diverso.

I nomi delle voci nel menu cambiano spesso; se non le trovi, usa la ricerca prodotti della dashboard.

- **Le lobby si vedono solo mentre la partita è in corso**: quando l'host esce o si ferma il Play Mode, la sessione viene chiusa e la lobby sparisce dopo poco. Tieni il gioco in Play mentre guardi la dashboard.
- **I grafici di utilizzo non sono in tempo reale**: possono arrivare con qualche minuto di ritardo.
- **Il traffico Netcode** tra host e client (RPC, `NetworkVariable`) non passa dalla dashboard: per quello serve il pacchetto **Multiplayer Tools** (`com.unity.multiplayer.tools`), che aggiunge al Profiler di Unity i moduli di rete e un overlay con le statistiche runtime.

> **Esempio stupido**: la dashboard è il radar Soliton di Metal Gear Solid: vedi chi c'è nella base (lobby attive, giocatori collegati) mentre sei in missione. Ma se esci dalla missione (stop del Play Mode), la base viene sgomberata e il radar resta vuoto. E il radar non ti dice cosa si stanno dicendo le guardie tra loro (il traffico Netcode): per quello serve un altro strumento, la radio intercettata (Multiplayer Tools).

### 2.7 Nome del giocatore e connection approval — `NameSelector.cs`, `UserData.cs`, `NetworkServer.cs`

Finora il server accettava chiunque senza sapere nulla di lui. Qui il giocatore sceglie un nome, il nome viaggia insieme alla richiesta di connessione, e il server lo registra prima di far entrare il giocatore.

**File coinvolti**:
- `Assets/Scripts/UI/NameSelector.cs` — scena `Bootstrap`, la prima del gioco.
- `Assets/Scripts/Networking/Shared/UserData.cs` — il "pacchetto" con i dati del giocatore.
- `ClientGameManager.setConnectionData` e l'inizio di `HostGameManager.StartHostAsync` — preparano il pacchetto.
- `Assets/Scripts/Networking/Server/NetworkServer.cs` — classe C# pura, lato server, che riceve e registra il pacchetto.
- Nel `NetworkManager` della scena `NetBootstrap` è attiva l'opzione **Connection Approval**: senza, Netcode non chiamerebbe mai `ApprovalCheck`.

> **Esempio stupido**: è l'ingresso di Shadow Moses con la tessera. Al molo Snake scrive il proprio nome sulla tessera (`NameSelector`). Quando arriva al cancello consegna la tessera insieme alla richiesta di entrare (`ConnectionData`). La guardia (il server, in `ApprovalCheck`) legge la tessera, si segna nome e matricola sul registro, e solo allora apre il cancello e indica a Snake dove presentarsi (`res.Position`).

#### Passo 1 — scelta del nome (`NameSelector`)

```csharp
public class NameSelector : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameField;
    [SerializeField] private Button connectButton;
    [SerializeField] private int minNameLength = 1;
    [SerializeField] private int maxNameLength = 12;

    public const string playerNameKey = "PlayerName";

    private void Start()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
            return;
        }
        nameField.text = PlayerPrefs.GetString(playerNameKey, string.Empty);
        handleNameChanged();
    }

    public void handleNameChanged()
    {
        connectButton.interactable =
            nameField.text.Length >= minNameLength &&
            nameField.text.Length <= maxNameLength;
    }

    public void connect()
    {
        PlayerPrefs.SetString(playerNameKey, nameField.text);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
```

| Dove | Cosa succede |
|---|---|
| `Start`, controllo `GraphicsDeviceType.Null` | Stesso test di `ApplicationController` (§2.1): un dedicated server non ha tastiera né schermo, quindi salta subito alla scena successiva (`NetBootstrap`). |
| `Start`, `PlayerPrefs.GetString` | Ripropone l'ultimo nome usato: `PlayerPrefs` è un piccolo archivio chiave/valore salvato sul disco, che sopravvive alla chiusura del gioco. |
| `handleNameChanged` | Agganciato all'evento "On Value Changed" dell'input field (Inspector). Abilita "Connect" solo se il nome è lungo tra 1 e 12 caratteri. |
| `connect` | Agganciato al bottone "Connect". Salva il nome e carica la scena successiva per indice (`buildIndex + 1`), non per nome. |
| `playerNameKey` | `public const`: è la stessa chiave che `HostGameManager` e `ClientGameManager` usano per rileggere il nome. Un'unica costante evita refusi tra file diversi. |

#### Passo 2 — il pacchetto `UserData` allegato alla connessione

```csharp
[Serializable]
public class UserData
{
    public string username;
    public string userAuthId;
}
```

```csharp
// ClientGameManager.setConnectionData (lato host lo stesso codice è in testa a StartHostAsync)
UserData userData = new UserData
{
    username = PlayerPrefs.GetString(NameSelector.playerNameKey, "Missing name"),
    userAuthId = AuthenticationService.Instance.PlayerId
};
string payload = JsonUtility.ToJson(userData);
byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
```

- `[Serializable]` serve a `JsonUtility`: senza, la classe verrebbe trasformata in un JSON vuoto `{}`.
- `userAuthId` è il `PlayerId` dato da Unity Authentication (§2.3): resta lo stesso anche se il giocatore cambia nome, quindi è la chiave giusta per riconoscerlo.
- `ConnectionData` è un `byte[]` che Netcode spedisce insieme alla richiesta di connessione. La catena è: oggetto → JSON (`string`) → byte UTF-8.
- **Ordine obbligatorio rispetto al corso**: nel corso `ConnectionData` si imposta subito prima di `NetworkManager.StartClient()`/`StartHost()`. Con le Sessions quelle chiamate non le facciamo più noi: le fanno `JoinSessionBy*Async` e `CreateSessionAsync` (§2.6). Per questo il pacchetto va preparato **prima** di quelle chiamate. Se lo si impostasse dopo, la richiesta di connessione sarebbe già partita e il server riceverebbe un payload vuoto.
- **L'host passa anch'esso dall'approval**: è sia server sia client, e il suo "client" si connette al suo "server" con il proprio payload. Per questo anche `StartHostAsync` imposta `ConnectionData`, e crea il `NetworkServer` prima di `CreateSessionAsync`.

#### Passo 3 — il server legge e registra (`NetworkServer`)

```csharp
public class NetworkServer : IDisposable
{
    private NetworkManager networkManager;
    private Dictionary<ulong, string> clientIdToAuth = new Dictionary<ulong, string>();
    private Dictionary<string, UserData> authIdToUserData = new Dictionary<string, UserData>();

    public NetworkServer(NetworkManager networkManager)
    {
        this.networkManager = networkManager;

        networkManager.ConnectionApprovalCallback += ApprovalCheck;
        networkManager.OnServerStarted += OnNetworkReady;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
    {
        string payload = System.Text.Encoding.UTF8.GetString(req.Payload);
        UserData userData = JsonUtility.FromJson<UserData>(payload);

        clientIdToAuth[req.ClientNetworkId] = userData.userAuthId;
        authIdToUserData[userData.userAuthId] = userData;

        res.Approved = true;
        res.Position = SpawnPoint.getRandomSpawnPos();
        res.Rotation = Quaternion.identity;
        res.CreatePlayerObject = true;
    }

    public UserData getUserDataByClientId(ulong clientId) { /* due lookup, null se assente */ }

    // OnNetworkReady / OnClientDisconnect / Dispose: vedi §2.8
}
```

| Dove | Cosa succede |
|---|---|
| Costruttore | Classe C# pura, quindi niente `OnEnable`: si iscrive agli eventi del `NetworkManager` ricevuto come parametro. Viene creata da `HostGameManager.StartHostAsync` e resta raggiungibile da `HostSingleton.Instance.GameManager.networkServer`. |
| `ApprovalCheck`, decodifica | Il percorso inverso del passo 2: byte → JSON → `UserData`. |
| `clientIdToAuth` | `ClientNetworkId` (l'id **di Netcode**, assegnato a ogni connessione e diverso a ogni partita) → `userAuthId` (l'id **di UGS**, stabile). |
| `authIdToUserData` | `userAuthId` → `UserData` completo. Due dizionari invece di uno perché i due id hanno vite diverse: un giocatore che si riconnette riceve un nuovo `ClientNetworkId`, ma ha lo stesso `userAuthId`. |
| `res.Approved = true` | Per ora tutti vengono accettati: qui in futuro si potrebbe rifiutare (partita piena, nome già usato, ban…). |
| `res.Position` / `res.Rotation` | Dove nasce il Player Object di chi si connette: un punto casuale tra gli `SpawnPoint` della scena (§5.0). |
| `res.CreatePlayerObject = true` | Netcode istanzia e spawna automaticamente il **Player Prefab** registrato sul `NetworkManager`, con il nuovo client come owner. |
| `getUserDataByClientId` | Il modo con cui il resto del gioco, dato un `OwnerClientId`, risale al nome del giocatore (es. `TankPlayer`, §5.0). |

```
NameSelector (scena Bootstrap)
   | PlayerPrefs["PlayerName"] = "Snake"
   v
... Menu, il giocatore preme Host o Join ...
   |
   v
HostGameManager.StartHostAsync / ClientGameManager.setConnectionData
   | UserData{ "Snake", PlayerId } -> JSON -> byte[] -> NetworkConfig.ConnectionData
   v
CreateSessionAsync / JoinSessionBy*Async  ---- richiesta di connessione + payload ---->  Server
                                                                                          |
                                                          NetworkServer.ApprovalCheck <---+
                                                            - registra clientId -> authId -> UserData
                                                            - Approved, Position casuale, CreatePlayerObject
                                                                                          |
                                                          spawn del Player Prefab --------+--> TankPlayer (§5.0)
```

### 2.8 Disconnessione e pulizia — `NetworkClient.cs` e la catena di `Dispose`

Due problemi diversi, risolti nello stesso giro di lezioni:
1. **Lato client**: se l'host chiude la partita o la connessione cade, il client restava fermo nella scena `Game` vuota.
2. **Lato processo**: `ClientGameManager`, `HostGameManager` e `NetworkServer` sono classi C# pure iscritte a eventi del `NetworkManager`. Nessuno le "spegneva", e la Lobby dell'host restava aperta finché il servizio non la chiudeva da solo per mancanza di heartbeat.

#### `NetworkClient` — ritorno al Menu

```csharp
public class NetworkClient : IDisposable
{
    private NetworkManager networkManager;
    private const string menuSceneName = "Menu";

    public NetworkClient(NetworkManager networkManager)
    {
        this.networkManager = networkManager;
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (clientId != 0 && clientId != networkManager.LocalClientId) return;

        if (SceneManager.GetActiveScene().name != menuSceneName)
        {
            SceneManager.LoadScene(menuSceneName);
        }

        if (networkManager.IsConnectedClient)
        {
            networkManager.Shutdown();
        }
    }

    public void Dispose()
    {
        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        }
    }
}
```

| Dove | Cosa succede |
|---|---|
| Costruttore | Creato in `ClientGameManager.initAsync` (§2.2), quindi esiste in **ogni** istanza giocabile, anche in quella che poi farà da host. |
| Filtro `clientId != 0 && clientId != LocalClientId` | `OnClientDisconnectCallback` scatta per eventi diversi a seconda di chi lo riceve. Questa riga lascia passare solo i due casi che ci interessano: l'id `0` (l'host, cioè il server è sparito) o il nostro stesso id (siamo stati disconnessi noi). Sull'host, la disconnessione di un *altro* giocatore viene ignorata: la partita continua. |
| `SceneManager.LoadScene(menuSceneName)` | Il cambio scena **locale** (non quello di rete): a connessione chiusa non c'è più nessun server da seguire. Il controllo sul nome evita di ricaricare il Menu se ci siamo già. |
| `Shutdown()` se `IsConnectedClient` | Se il `NetworkManager` crede ancora di essere connesso, lo si spegne, così è pronto per un nuovo Host/Join. |

> **Esempio stupido**: in Crash Team Racing, se l'amico che ha la console stacca la spina, il gioco non ti lascia sulla pista vuota a guidare da solo per sempre: ti riporta al menu principale. Se invece è un altro giocatore a staccare il pad, la gara continua senza di lui.

#### La catena di `Dispose`

```
Chiusura del gioco (o distruzione dei singleton DontDestroyOnLoad)
   |
   +-- ClientSingleton.OnDestroy --> ClientGameManager.Dispose --> NetworkClient.Dispose
   |                                                                  (si sgancia da OnClientDisconnectCallback)
   |
   +-- HostSingleton.OnDestroy ----> HostGameManager.Dispose
                                        |
                                        +-- NetworkServer.Dispose
                                        |     - si sgancia da ConnectionApprovalCallback,
                                        |       OnClientDisconnectCallback, OnServerStarted
                                        |     - networkManager.Shutdown() se IsListening
                                        |
                                        +-- _ = DeleteSessionAsync()   (fire-and-forget)
                                              - session = null subito (niente doppia cancellazione)
                                              - await session.DeleteAsync()  -> la Lobby sparisce
```

`NetworkServer` completa anche il lavoro di §2.7: in `OnNetworkReady` (chiamato da `OnServerStarted`) si iscrive a `OnClientDisconnectCallback`, e in `OnClientDisconnect` toglie il giocatore uscito da entrambi i dizionari, così il registro non contiene giocatori fantasma.

**Cosa diverge dal corso** (stesso schema di §2.6):

| Pezzo del corso | In questo progetto | Perché |
|---|---|---|
| `public async void Dispose()` in `HostGameManager` | `public void Dispose()` + `_ = DeleteSessionAsync()` | `IDisposable.Dispose` è sincrono. `async void` compila, ma un'eccezione non catturata farebbe cadere il processo. Il metodo `async Task` separato, lanciato senza `await` (`_ =`), cattura i propri errori da sé |
| `HostSingleton.Instance.StopCoroutine(nameof(HeartbeatLobby))` | Sparito | Con le Sessions non c'è nessuna coroutine di heartbeat da fermare (§2.6.7) |
| `await Lobbies.Instance.DeleteLobbyAsync(lobbyId)` | `await session.DeleteAsync()` | La Session è la Lobby: cancellandola si cancella anche la Lobby. Per questo `session` è ora un `IHostSession` (§2.6.6) |
| `catch (LobbyServiceException e)` | `catch (Exception e)` | Stesso motivo di `StartHostAsync`: l'SDK lancia `SessionException` |

**Limiti noti**:
- `Dispose` parte solo alla distruzione dei singleton, cioè in pratica alla chiusura del gioco. Una chiamata di rete lanciata "fire-and-forget" mentre il processo si chiude non ha garanzia di arrivare a destinazione: se non arriva, la Lobby viene comunque chiusa dal servizio dopo un po', per mancanza di heartbeat.
- Tornare al Menu **non** chiama `Dispose`: il `NetworkServer` creato da una partita precedente resta iscritto agli eventi, e un secondo "Host" ne crea un altro. Andrà sistemato quando ci sarà un flusso "esci dalla partita" (§2.6.8).

### 2.9 Avvio dall'Editor sempre dalla prima scena — `StartupSceneLoader.cs`

File: `Assets/Editor/StartupSceneLoader.cs`. Stando nella cartella speciale `Editor`, non finisce nella build: serve solo mentre si lavora in Unity.

```csharp
[InitializeOnLoad]
public class StartupSceneLoader
{
    static StartupSceneLoader()
    {
        EditorApplication.playModeStateChanged += LoadStartupScene;
    }

    private static void LoadStartupScene(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            if (EditorSceneManager.GetActiveScene().buildIndex != 0)
            {
                EditorSceneManager.LoadScene(0);
            }
        }
    }
}
```

- `[InitializeOnLoad]` + costruttore `static`: Unity esegue il costruttore ogni volta che ricompila gli script o apre il progetto. È il punto in cui ci si iscrive a `playModeStateChanged`.
- `ExitingEditMode` (hai appena premuto Play): propone di salvare le scene modificate, perché il cambio scena successivo farebbe perdere le modifiche non salvate.
- `EnteredPlayMode`: se la scena aperta non è la 0 (`Bootstrap`), carica la 0.

Risolve il problema descritto in §2.5: si può lavorare con la scena `Game` aperta, premere Play, e il gioco parte comunque dal flusso completo Bootstrap → NetBootstrap → Menu, con i singleton creati. Senza, premere Play su `Game` darebbe subito `NullReferenceException` su `HostSingleton.Instance`.

> **Esempio stupido**: è come ricaricare un salvataggio in Metal Gear Solid. Puoi metterti a guardare la mappa di qualsiasi area, ma quando premi "Start" la missione riparte sempre dal molo: è lì che Snake riceve Codec ed equipaggiamento, e senza quelli nelle altre aree non funzionerebbe niente.

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

### Il meccanismo, passo per passo

| Dove | Cosa succede |
|---|---|
| `OnIsServerAuthoritative()` | Ritorna `false`: è la riga che dichiara "questo NON è più un NetworkTransform server-authoritative". |
| `OnNetworkSpawn()` | Chiama prima `base.OnNetworkSpawn()`, per non rompere il setup standard di Netcode, poi imposta `CanCommitToTransform = IsOwner`: solo il proprietario avrà il diritto di "spedire" il proprio transform. |
| `Update()`, inizio | `CanCommitToTransform` viene ricalcolato **ogni frame** (non solo allo spawn), per restare corretto anche se la proprietà dell'oggetto cambiasse a runtime. |
| `base.Update()` | Fa il lavoro vero: se sei owner, "committa" (applica) lo stato locale; se non lo sei, **interpola** verso i valori ricevuti dalla rete (è quello che rende il movimento degli altri fluido e non a scatti). |
| Guard `NetworkManager != null` | Prima dello spawn o fuori sessione potrebbe essere nullo. |
| Guard `IsConnectedClient` / `IsListening` | Si invia il transform solo se si è davvero connessi o si è il server/host. |
| Guard `CanCommitToTransform` | Ultimo filtro: solo il proprietario prosegue. Le copie remote si fermano qui e restano in sola interpolazione. |
| `TryCommitTransformToServer(transform, NetworkManager.LocalTime.Time)` | Il proprietario manda il SUO transform al server, insieme a un timestamp che serve agli altri client per interpolare correttamente nel tempo. |

```
Owner del tank                Server                     Altri client
     |  muove localmente        |                             |
     |  (nessun lag: è suo)     |                             |
     |------ transform+time --->|                             |
     |                          |------ sincronizza --------->|
     |                          |                              |  interpola
     |                          |                              |  (base.Update, ramo non-owner)
```

Questo stesso componente viene poi usato anche per **TurretPivot** (rotazione Z) e **Treads** (cingoli): stessa logica, assi diversi da sincronizzare.

---

## 5. Il flusso di gioco, in ordine

Da qui in poi seguiamo una partita in ordine cronologico: dal momento in cui il tuo tank compare sulla mappa, fino a quando viene distrutto e rinasce altrove. Le tabelle indicano, per ogni passo, il metodo o il campo in cui succede, così puoi aprire il file e trovarlo subito.

### 5.0 Ingresso in partita: punto di spawn, nome e camera — `SpawnPoint.cs`, `TankPlayer.cs`, `PlayerNameDisplay.cs`

File coinvolti:
- `Assets/Scripts/Core/SpawnPoint.cs` — segnaposto in scena (nella scena `Game` ce ne sono 14, sparsi per la mappa).
- `Assets/Scripts/Core/Player/TankPlayer.cs` — componente "radice" del Player Prefab: tiene insieme camera, nome, `Health` e gli eventi di spawn/despawn.
- `Assets/Scripts/Core/Player/PlayerNameDisplay.cs` — la scritta con il nome sopra il tank.

È il primo codice di gameplay che gira, subito dopo la connection approval (§2.7).

| # | Dove | Cosa succede |
|---|---|---|
| 1 | `SpawnPoint.OnEnable` / `OnDisable` | Ogni segnaposto si aggiunge/toglie da sé in una lista **statica** condivisa (`spawnPoints`). Nessuno deve trascinarli a mano in un array nell'Inspector: basta metterne uno nuovo in scena. |
| 2 | `SpawnPoint.OnDrawGizmos` | Disegna una sfera blu nella Scene view: i segnaposto non hanno grafica e altrimenti sarebbero invisibili mentre li posizioni. |
| 3 | `SpawnPoint.getRandomSpawnPos` | Metodo `static`: restituisce la posizione di un segnaposto a caso, o `Vector3.zero` se la lista è vuota (es. scena senza segnaposti). |
| 4 | `NetworkServer.ApprovalCheck` (§2.7) | Il server usa `getRandomSpawnPos()` come `res.Position`: il Player Object nasce già nel punto giusto, senza essere spostato dopo. |
| 5 | `TankPlayer.OnNetworkSpawn`, ramo `IsServer` | Il server recupera il `UserData` del proprietario con `networkServer.getUserDataByClientId(OwnerClientId)` e scrive il nome in `playerName`. Poi lancia l'evento statico `OnPlayerSpawned` (usato da `RespawnHandler`, §5.10). |
| 6 | `TankPlayer.playerName` | `NetworkVariable<FixedString32Bytes>`, non `NetworkVariable<string>`: le `NetworkVariable` vogliono tipi a dimensione fissa, e `string` non lo è. 32 byte bastano per i 12 caratteri di `NameSelector`. |
| 7 | `TankPlayer.OnNetworkSpawn`, ramo `IsOwner` | Alza la `Priority` della `CinemachineVirtualCamera` del proprio tank (`ownerPriority = 15`). Ogni tank ha la sua camera virtuale, e Cinemachine segue sempre quella con priorità più alta: così ogni giocatore vede il proprio tank, non quello di un altro. |
| 8 | `PlayerNameDisplay.Start` | Su **ogni** istanza scrive subito il nome già sincronizzato nel `TMP_Text`, poi si iscrive a `playerName.OnValueChanged` per gli aggiornamenti successivi. |
| 9 | `PlayerNameDisplay.OnDestroy` / `TankPlayer.OnNetworkDespawn` | Disiscrizione dal `OnValueChanged`; il server lancia `OnPlayerDespawned`. |

**Due dettagli da notare**:
- `PlayerNameDisplay` è un semplice `MonoBehaviour` e usa `Start`/`OnDestroy`, non `OnNetworkSpawn`/`OnNetworkDespawn` (§6.5). Funziona perché non scrive niente in rete: legge soltanto una `NetworkVariable` di un altro componente, e quando un oggetto di rete viene istanziato da Netcode, `Start` arriva dopo lo spawn, a valore già sincronizzato.
- `TankPlayer` raggiunge il registro dei giocatori passando per `HostSingleton.Instance.GameManager.networkServer`: funziona solo perché oggi il server è sempre anche l'host. Con un dedicated server (§2.1, ancora uno stub) andrà trovato un altro percorso.

> **Esempio stupido**: è il calcio d'inizio di Rocket League. Il server ti assegna una delle posizioni di partenza (`SpawnPoint`), scrive il tuo nome sopra la macchina leggendolo dal tuo profilo (`playerName`, che tutti vedono), e la TUA telecamera inquadra la TUA macchina (priorità Cinemachine), non quella dell'avversario.

### 5.1 Input del giocatore — `InputReader.cs`

File: `Assets/Scripts/Input/InputReader.cs` — è uno **ScriptableObject**, non un componente su un GameObject: è un asset condiviso che chiunque può referenziare (movimento, mira, sparo) senza dover ognuno gestire da sé l'Input System.

> **Esempio stupido**: `InputReader` è il Codec di Metal Gear Solid. Trasmette su una frequenza (`MoveEvent`, `PrimaryFireEvent`), e chiunque sia sintonizzato (PlayerMovement, ProjectileLauncher) riceve il messaggio, senza che il Codec sappia o si preoccupi di chi sta ascoltando. Se domani Snake cambia Codec (gamepad invece di tastiera), Otacon e Campbell non se ne accorgono nemmeno.

| Dove | Cosa succede |
|---|---|
| `using static Controls;` | `Controls` è la classe generata automaticamente dall'asset `.inputactions`, non scritta a mano. |
| `: IPlayerActions` | Un "contratto" che obbliga a fornire `OnMove`, `OnPrimaryFire`, `OnAim`. Sarà l'Input System a chiamarli. |
| Campo `controls` | L'istanza runtime di quella classe generata. |
| Eventi `PrimaryFireEvent` / `MoveEvent` | Il "megafono" verso il resto del gioco: l'input grezzo viene ri-emesso come evento C#, così chi ascolta non deve sapere nulla dell'Input System sottostante. |
| Proprietà `AimPosition` | **Non** è un evento ma una proprietà "sempre leggibile" (polling): la posizione del mouse cambia in continuazione e a chi mira serve sempre l'ultimo valore, non una notifica per ogni pixel. |
| `OnEnable()` | Crea `Controls` se non esiste, registra questo oggetto come gestore delle callback (`SetCallbacks(this)`) e abilita la lettura (`controls.Enable()`). Senza quest'ultima riga, nessuna callback scatterebbe. |
| `OnMove` | Ad ogni cambio dell'azione "Move", rilancia il `Vector2` letto tramite `MoveEvent`. |
| `OnPrimaryFire` | Distingue `performed` (tasto premuto → evento `true`) da `canceled` (tasto rilasciato → evento `false`). |
| `OnAim` | A differenza degli altri due, **non** solleva un evento: salva solo l'ultima posizione del mouse, che `PlayerAiming` leggerà da sé ogni frame. |

**Perché questo pattern conviene**: se domani cambi dispositivo di input (gamepad, touch), tocchi solo `InputReader`. Tutto il resto del gioco continua a funzionare perché dipende solo dagli eventi/proprietà astratti, non dai tasti fisici.

### 5.2 Mira della torretta — `PlayerAiming.cs`

File: `Assets/Scripts/Core/Player/PlayerAiming.cs`

| Dove | Cosa succede |
|---|---|
| Campi `inputReader`, `turretTransform` | Riferimenti da Inspector: da dove leggere `AimPosition` e cosa far ruotare. |
| `LateUpdate()` | Il calcolo avviene qui, **non** in `Update`: la mira va calcolata DOPO che il corpo si è già mosso/ruotato, altrimenti la torretta punterebbe alla posizione del tank di un frame prima (uno "scatto" visivo). |
| `if (!IsOwner) return;` | Solo il proprietario decide dove punta la propria torretta. Sulle copie remote, la rotazione arriva già pronta dalla rete (via `ClientNetworkTransform`, §4). |
| `AimPosition` → `Camera.main.ScreenToWorldPoint` | `AimPosition` è in coordinate **schermo** (pixel del mouse): si converte in coordinate **mondo** per poterla confrontare con la posizione della torretta nella scena. |
| `turretTransform.up = aimWorldPos - (Vector2)turretTransform.position;` | Si orienta l'asse "alto" della torretta (dove punta lo sprite del cannone) verso il mouse. |

> **Esempio stupido**: è la telecamera di sorveglianza di Shadow Moses che segue Snake. Il muro su cui è montata (il corpo del tank) può essere girato in qualsiasi modo, ma la telecamera (la torretta) ruota sempre verso il suo bersaglio (il mouse). E ruota DOPO che il muro si è sistemato (`LateUpdate`), altrimenti guarderebbe dove Snake era un frame fa.

### 5.3 Sparo — `ProjectileLauncher.cs`

File: `Assets/Scripts/Core/Player/ProjectileLauncher.cs`

Qui si vede il pattern più importante del progetto: **due proiettili per ogni sparo**.

- `serverProjectilePrefab` → il proiettile **vero**, istanziato SOLO sul server, autorevole, infligge danno reale.
- `clientProjectilePrefab` → un proiettile **dummy**, solo visivo, mostrato subito in locale per dare feedback istantaneo senza aspettare il giro di rete.

> **Esempio stupido**: la palla di Rocket League. Quando la colpisci, sul TUO schermo parte subito (previsione locale = il dummy), senza aspettare nessuno. Ma la traiettoria vera la decide il server, ed è quella che conta per il goal. Per questo a volte, con un ping alto, vedi la palla "scattare" di colpo in un'altra posizione: è il momento in cui la versione vera corregge quella finta.

| Dove | Cosa succede |
|---|---|
| Campi `serverProjectilePrefab` / `clientProjectilePrefab` | I due prefab descritti sopra. |
| `OnNetworkSpawn` / `OnNetworkDespawn` | Solo il proprietario si iscrive/disiscrive a `PrimaryFireEvent` (stesso pattern di `PlayerAiming`, che esce subito se `!IsOwner`). |
| `Update()` | Se `shouldFire` è vero e il cooldown (`fireRate`) è passato: si chiama **sia** `PrimaryFireServerRpc(...)` **sia** `SpawnDummyProjectile(...)` nello stesso frame. La ServerRpc non è bloccante: è solo l'invio di un messaggio, ritorna subito. Ecco perché il dummy appare "nello stesso istante" pur essendo scritto dopo nel codice. |
| `[ServerRpc] PrimaryFireServerRpc` | Eseguita SOLO sul server: istanzia il proiettile vero, gli imposta velocità e direzione, ignora la collisione col proprio player, chiama `dealDamage.setOwnerClientId(OwnerClientId)` (per sapere in seguito chi non deve poter colpire, §5.6) e poi chiama `SpawnDummyProjectileClientRpc` per far comparire il dummy anche sugli altri client. |
| `[ClientRpc] SpawnDummyProjectileClientRpc` | Eseguita su TUTTI i client. Il proprietario, che ha già mostrato il proprio dummy in `Update`, si esclude con `if (IsOwner) return;` per non duplicarlo: questa callback serve solo agli ALTRI client. |
| `HandlePrimaryFire` | Semplice callback che aggiorna il flag `shouldFire`, letto ogni frame da `Update`. |
| `SpawnDummyProjectile` | Helper condiviso tra proprietario (`Update`) e altri client (`ClientRpc`): istanzia solo l'effetto visivo, senza alcuna logica di danno (quella vive esclusivamente nel proiettile vero). |

```
Client (owner)                          Server                       Altri client
   | preme fuoco                          |                              |
   | mostra dummy locale -----------------+------------------------------|  (istantaneo, nessuna attesa)
   | invia PrimaryFireServerRpc --------->|                              |
   |                                      | spawna proiettile VERO       |
   |                                      | invia ClientRpc ------------>|
   |                                      |                              | mostra il proprio dummy
```

### 5.4 Fine vita dei proiettili — `DestroySelfOnContact.cs` + `Lifetime.cs`

File: `Assets/Scripts/Utils/DestroySelfOnContact.cs`, `Assets/Scripts/Utils/Lifetime.cs`

- **`DestroySelfOnContact`**: il proiettile vero (generato dal server) si autodistrugge al primo contatto. Essendo il server ad averlo istanziato, la distruzione è autorevole e si propaga a tutti.
- **`Lifetime`**: una rete di sicurezza indipendente, su entrambi i tipi di proiettile (vero e dummy): se non colpiscono nulla entro N secondi, si autodistruggono comunque.

> **Esempio stupido**: le casse TNT di Crash Bandicoot. Esplodono se le tocchi (`DestroySelfOnContact`), ma partono anche da sole con il conto alla rovescia "3… 2… 1…" anche se non succede niente (`Lifetime`) — così nessuna cassa resta nel livello per sempre a occupare memoria.

### 5.5 Movimento del corpo — `PlayerMovement.cs`

File: `Assets/Scripts/Core/Player/PlayerMovement.cs`

| Dove | Cosa succede |
|---|---|
| `OnNetworkSpawn` / `OnNetworkDespawn` | Usati invece di `Start`/`OnDestroy` (troppo presto/tardi nel ciclo di vita di rete): solo il proprietario si iscrive/disiscrive a `MoveEvent`, in modo simmetrico. |
| Campo `turningRate` | Velocità angolare MASSIMA in gradi/secondo. Con input parziale (es. joystick a metà corsa) la rotazione scala proporzionalmente. |
| `Update()` | Non `FixedUpdate`, perché è puramente visivo: `zRotation = input.x * -turningRate * Time.deltaTime`. Il `Time.deltaTime` serve perché `Update` non gira a intervalli fissi: senza, la velocità di rotazione dipenderebbe dal framerate. |
| `FixedUpdate()` | Fisica, intervallo fisso: `rb.velocity = bodyTransform.up * input.y * movementSpeed`. Si usa `bodyTransform.up` e non gli assi del mondo, così il tank avanza sempre "in avanti" rispetto a come è ruotato in quel momento. |
| `handleMove` | Callback collegata a `MoveEvent`: aggiorna solo `previousMovementInput`, che `Update` e `FixedUpdate` leggono ogni frame. |

**Collisioni con i bordi**: nella matrice di collisione 2D (*Edit → Project Settings → Physics 2D → Layer Collision Matrix*) il layer `Player` collide con `Default`, il layer dei bordi della mappa. Senza questa spunta il tank attraverserebbe i muri. Nello stesso commit è stata corretta l'altezza dei collider dei bordi.

> **Esempio stupido**: il kart di Crash Team Racing. Sterzi (Update, ogni frame, effetto immediato) e intanto il motore spinge (FixedUpdate, fisica) sempre nella direzione in cui punta il muso del kart — non magicamente verso nord, qualunque sia la curva.

### 5.6 Danno da contatto — `DealDamageOnContact.cs`

File: `Assets/Scripts/Core/Combat/DealDamageOnContact.cs` — presente **solo** sul `serverProjectilePrefab`, quindi la sua logica gira per forza solo sul server.

| Dove | Cosa succede |
|---|---|
| `setOwnerClientId` | Chiamato dal server subito dopo lo spawn (in `PrimaryFireServerRpc`, §5.3) per ricordare chi ha sparato. |
| `OnTriggerEnter2D`, controllo `Rigidbody2D` | Se l'oggetto colpito non ha `Rigidbody2D`, non è un bersaglio valido (es. muri/scenario): si esce subito. |
| `OnTriggerEnter2D`, controllo `OwnerClientId` | Se il bersaglio ha un `NetworkObject` il cui `OwnerClientId` coincide con chi ha sparato, si esce: **non ci si può ferire da soli**. |
| `OnTriggerEnter2D`, `Health` | Se il bersaglio ha un componente `Health`, gli si infligge danno. |

> **Esempio stupido**: il missile di Crash Team Racing. Appena lanciato, ti passa attraverso senza farti nulla: sa chi l'ha sparato (`OwnerClientId`) e ignora il proprio kart. Senza questo controllo, ogni missile esploderebbe in faccia a chi lo lancia nell'istante stesso in cui esce.

### 5.7 Vita e barra vita — `Health.cs` + `HealthDisplay.cs`

File: `Assets/Scripts/Core/Combat/Health.cs`, `Assets/Scripts/Core/Combat/HealthDisplay.cs`

`Health` tiene `currentHealth` in una `NetworkVariable<int>` (scrivibile solo dal server, sincronizzata automaticamente). `HealthDisplay` è puramente estetico (una UI Image "Filled") e vive lato client: ascolta i cambiamenti di `Health.currentHealth` e aggiorna la barra a schermo.

| Dove | Cosa succede |
|---|---|
| `HealthDisplay.OnNetworkSpawn` | Controllo `IsClient` (non `IsOwner`, non `IsServer`): OGNI client deve vedere la barra vita aggiornata, anche guardando i tank altrui. Ci si iscrive a `OnValueChanged` e si inizializza subito la barra. |
| `HealthDisplay.OnNetworkDespawn` | Disiscrizione simmetrica. |
| `HealthDisplay.handleHealthChanged` | Normalizza la vita corrente sul massimo (`newHealth / MaxHealth`), perché `Image.fillAmount` va da 0 a 1. |
| `Health.OnNetworkSpawn` | Solo il server inizializza `currentHealth.Value = MaxHealth`. Se lo facesse anche ogni client, ci sarebbero scritture concorrenti non autorizzate. |
| `Health.modifyHealth` | Nessun controllo `IsServer` esplicito: il metodo va chiamato solo da codice già server-side (es. `DealDamageOnContact`, §5.6). Anche se venisse chiamato per errore da un client, Netcode rifiuterebbe comunque la scrittura sulla `NetworkVariable`. |
| `Health.modifyHealth`, vita a 0 | Lancia `OnDie?.Invoke(this)` e imposta `isDead = true`: da quel momento ogni altro danno o cura viene ignorato (controllo idempotente, §6.7). Chi ascolta `OnDie` è `RespawnHandler` (§5.10). |

> **Esempio stupido**: la barra LIFE di Snake in alto a sinistra in Metal Gear Solid. Si limita a MOSTRARE quanta vita hai (HealthDisplay). È il gioco (server) a decidere quanta vita ti toglie il proiettile di una guardia (Health). Colorare di verde la barra con Paint non ti cura.

### 5.8 Sistema monete — `Coin.cs`, `CoinWallet.cs`, `RespawningCoin.cs`, `CoinSpawner.cs`

Questa è la catena più lunga e riassume TUTTI i pattern precedenti insieme: server authority, feedback client immediato, eventi, respawn.

**File coinvolti**:
- `Assets/Scripts/Core/Coins/Coin.cs` — classe astratta base.
- `Assets/Scripts/Core/Coins/CoinWallet.cs` — sul player, conta le monete raccolte.
- `Assets/Scripts/Core/Coins/RespawningCoin.cs` — moneta concreta che, invece di sparire, ricompare altrove.
- `Assets/Scripts/Core/Coins/CoinSpawner.cs` — genera le monete all'avvio e le ricolloca quando vengono raccolte.

#### Gli elementi in gioco

| Dove | Cosa fa |
|---|---|
| `Coin.collect` (abstract) | Il valore di ritorno è significativo solo se chi lo calcola è il server: le sottoclassi devono restituire 0 se eseguite su un client. |
| `RespawningCoin.onCollected` (evento) | Sollevato solo quando `collect()` è eseguito lato server: il modo con cui la singola moneta avvisa il `CoinSpawner` che l'ha creata. |
| `RespawningCoin.previousPosition` (campo) | Memorizza la posizione dell'ultimo frame, usata da `Update` per accorgersi che la moneta è stata spostata. |

#### La catena completa, in ordine di esecuzione

| # | Dove | Cosa succede |
|---|---|---|
| 1 | `CoinSpawner` (classe) | All'avvio genera un numero fisso di `RespawningCoin` in punti casuali della mappa; quando una viene raccolta, la ricolloca invece di distruggerla e ricrearla. |
| 2 | `CoinSpawner.OnNetworkSpawn` | Solo il server decide dove spawnare le monete (come in `Health.OnNetworkSpawn`). |
| 3 | `CoinSpawner.spawnCoin` | Dopo aver istanziato e spawnato in rete la moneta, lo spawner si iscrive al SUO evento `onCollected`, per sapere quando quella specifica istanza viene raccolta. |
| 4 | `CoinWallet.OnTriggerEnter2D` | Il trigger 2D avviene in locale su OGNI client che possiede fisicamente il collider del wallet: si chiama `coin.collect()` **ovunque**, sia server che client. |
| 5 | `RespawningCoin.collect` (ramo client) | `if (!IsServer)`: nasconde la moneta localmente (`showCoin(false)`) per un feedback visivo immediato e ritorna sempre 0. Non è autorevole: non può decidere se la moneta è già stata raccolta né quanto valga. |
| 6 | `RespawningCoin.collect` (ramo server) | Controllo autorevole: se `alreadyCollected` è già vero (doppio trigger nello stesso frame, o due giocatori), ritorna 0 per non accreditare due volte. |
| 7 | `RespawningCoin.collect` (dopo `alreadyCollected = true`) | Si invoca `onCollected?.Invoke(this)`: notifica il `CoinSpawner` che questa moneta va ricollocata. |
| 8 | `CoinWallet.OnTriggerEnter2D` | `if (!IsServer) return;` poi `totalCoins.Value += coinValue`: solo il server può scrivere sulla `NetworkVariable`. Sui client `coinValue` è comunque sempre 0 (passo 5), quindi qui non si tenterebbe comunque nulla di dannoso. |
| 9 | `CoinSpawner.handleCoinCollected` | Callback collegata a `onCollected`, gira solo sul server: sposta la moneta in un nuovo punto libero invece di distruggerla. |
| 10 | `RespawningCoin.Reset` | Chiamato subito dopo da `handleCoinCollected`: azzera `alreadyCollected`, così il controllo autorevole (passo 6) torna a considerarla raccoglibile. |
| 11 | `RespawningCoin.Update` | Gira su OGNI client: la nuova `transform.position` decisa dal server arriva tramite la normale sincronizzazione di rete (`NetworkTransform`). Quando la posizione cambia rispetto a `previousPosition`, è il segnale che la moneta è stata rispawnata altrove: si annulla quindi il nascondimento locale del passo 5 (`showCoin(true)`), perché la visibilità dello `SpriteRenderer` non è automaticamente sincronizzata dalla rete, solo il transform lo è. |

#### Diagramma dell'intero ciclo di vita di una moneta

```
CoinSpawner (server)                RespawningCoin                     CoinWallet (ogni client)
      |  spawna N monete (1-3)            |                                    |
      |  si iscrive a onCollected  ------>|                                    |
      |                                   |            <---- trigger 2D -------|  (4, su OGNI client)
      |                                   |  ramo CLIENT (5): nasconde,        |
      |                                   |  ritorna 0                         |
      |                                   |  ramo SERVER (6): valida,          |
      |                                   |  ritorna coinValue                 |
      |                                   |  invoca onCollected (7) ---------->|
      |  <----------------------------- evento                                 |
      |                                                                        |  se server: totalCoins += (8)
      |  handleCoinCollected (9):                                              |
      |    - sposta la moneta                                                  |
      |    - coin.Reset() (10)                                                 |
      |                                   |                                    |
      |         nuova posizione si propaga via NetworkTransform                |
      |                                   |  Update rileva il cambio (11):     |
      |                                   |  showCoin(true) — la fa ricomparire|
```

> **Esempio stupido**: i frutti Wumpa di Crash Bandicoot, in una versione multiplayer. Ci passi sopra e il frutto sparisce SUBITO dal tuo schermo con il suo "pop" (feedback client immediato, passo 5). Ma il contatore dei Wumpa lo aggiorna solo il server (passo 8). Se tu e un altro giocatore ci passate sopra nello stesso istante, il server ne dà uno solo (passo 6). Poi, invece di distruggere il frutto e crearne uno nuovo, il server lo teletrasporta in un altro punto del livello (passi 9-11).

### 5.9 Costo in monete per sparare, e polvere sui proiettili distrutti — `ProjectileLauncher.cs`, `CoinWallet.cs`, `SpawnOnDestroy.cs`

File coinvolti:
- `Assets/Scripts/Utils/SpawnOnDestroy.cs` — nuovo componente puramente estetico.
- `Assets/Scripts/Core/Player/ProjectileLauncher.cs` — aggiunta economica allo sparo.
- `Assets/Scripts/Core/Coins/CoinWallet.cs` — nuovo metodo `spendCoins`.

Prima esistevano solo le monete come *punteggio*; ora sparare **costa** monete, riusando esattamente lo stesso `CoinWallet` visto in §5.8 sia per accreditarle sia per scalarle. Segue la stessa "regola d'oro": il client fa un controllo rapido per non sprecare rete, ma solo il server decide davvero.

| Dove | Cosa succede |
|---|---|
| `SpawnOnDestroy.OnDestroy` | Componente estetico agganciato a `OnDestroy` (ciclo di vita di Unity, non di rete): va sul proiettile dummy e, quando questo viene distrutto (per contatto o per `Lifetime`, §5.4), istanzia in locale un effetto (es. `DustCloud`), senza `Spawn()` di rete. |
| `ProjectileLauncher`, campi `wallet` e `costToFire` | Riferimento al `CoinWallet` del proprietario e costo in monete di ogni sparo. |
| `ProjectileLauncher`, campo `timer` | Cooldown tra due spari, scalato ogni frame in `Update` e ricaricato a `1/fireRate` dopo uno sparo riuscito. |
| `ProjectileLauncher.Update` | Controllo *cosmetico* lato client: se le monete non bastano, evita di inviare la ServerRpc e di mostrare un dummy che il server rifiuterebbe comunque. Non è autorevole. |
| `ProjectileLauncher.PrimaryFireServerRpc` | Controllo *autorevole* lato server: ripete la stessa verifica (perché il controllo client è solo un'ottimizzazione aggirabile) e, solo se le monete bastano davvero, procede e scala il costo. |
| `CoinWallet.spendCoins` | Sottrae `costToFire` da `totalCoins`, la stessa `NetworkVariable` accreditata da `OnTriggerEnter2D` (§5.8). Chiamato solo dal server. |

> **Esempio stupido**: il boost di Rocket League. Le monete sono i cuscinetti di boost che raccogli sul campo, e sparare è premere il tasto boost. Il tuo gioco controlla per primo "ho ancora boost nella barra?" e, se è vuota, non prova neanche (client, `Update`). Ma è il server a controllare davvero quanto boost hai e a scalarlo (`PrimaryFireServerRpc` + `spendCoins`): se modifichi il gioco per avere boost infinito, il server ti risponde comunque "barra vuota".

### 5.10 Morte e respawn — `RespawnHandler.cs`

File: `Assets/Scripts/Core/Combat/RespawnHandler.cs` — un `NetworkBehaviour` su un GameObject `RespawnHandler` nella scena `Game`, con un riferimento al Player Prefab. Tutta la sua logica gira **solo sul server**.

```csharp
public class RespawnHandler : NetworkBehaviour
{
    [SerializeField] private NetworkObject playerPrefab;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        TankPlayer[] players = FindObjectsOfType<TankPlayer>();
        foreach (TankPlayer player in players)
        {
            handlePlayerSpawned(player);
        }

        TankPlayer.OnPlayerSpawned += handlePlayerSpawned;
        TankPlayer.OnPlayerDespawned += handlePlayerDespawned;
    }

    private void handlePlayerSpawned(TankPlayer player)
    {
        player.Health.OnDie += (health) => handlePlayerDie(player);
    }

    private void handlePlayerDespawned(TankPlayer player)
    {
        player.Health.OnDie -= (health) => handlePlayerDie(player);
    }

    private void handlePlayerDie(TankPlayer player)
    {
        Destroy(player.gameObject);
        StartCoroutine(respawnPlayer(player.OwnerClientId));
    }

    private IEnumerator respawnPlayer(ulong ownerClientId)
    {
        yield return null;

        NetworkObject playerInstance = Instantiate
            (playerPrefab, SpawnPoint.getRandomSpawnPos(), Quaternion.identity);

        playerInstance.SpawnAsPlayerObject(ownerClientId);
    }
}
```

| # | Dove | Cosa succede |
|---|---|---|
| 1 | `OnNetworkSpawn`, `FindObjectsOfType<TankPlayer>()` | I tank già in scena **prima** che il `RespawnHandler` esistesse (es. quello dell'host) hanno già lanciato il loro `OnPlayerSpawned`, quando nessuno ascoltava: li si recupera a mano, una volta sola. |
| 2 | `OnNetworkSpawn`, `+= OnPlayerSpawned/OnPlayerDespawned` | Da qui in poi i nuovi tank si annunciano da soli tramite gli eventi statici di `TankPlayer` (§5.0). Disiscrizione a specchio in `OnNetworkDespawn`. |
| 3 | `handlePlayerSpawned` | Si iscrive a `Health.OnDie` di quel tank con una lambda che "ricorda" quale `TankPlayer` è (l'evento passa solo l'`Health`). |
| 4 | `Health.modifyHealth` (§5.7) | La vita arriva a 0: `OnDie` scatta, una volta sola grazie a `isDead`. |
| 5 | `handlePlayerDie`, `Destroy(player.gameObject)` | Il server distrugge il tank. Distruggere lato server un `NetworkObject` spawnato lo despawna anche su tutti i client. |
| 6 | `handlePlayerDie`, `StartCoroutine(...)` | Si salva `OwnerClientId` (il tank sta per sparire) e si rimanda la ricreazione. |
| 7 | `respawnPlayer`, `yield return null` | Si aspetta un frame: `Destroy` non è immediato, e un client può avere **un solo** Player Object alla volta. Spawnarne uno nuovo nello stesso frame si scontrerebbe con quello vecchio non ancora rimosso. |
| 8 | `respawnPlayer`, `Instantiate` + `SpawnAsPlayerObject(ownerClientId)` | Nuovo tank in un punto casuale (§5.0), spawnato in rete **come Player Object** di quel client: diventa owner, e su quel client `IsOwner` torna vero (camera, input, mira). |
| 9 | `TankPlayer.OnNetworkSpawn` del nuovo tank | Riparte la catena di §5.0: nome riletto da `NetworkServer`, `OnPlayerSpawned`, il `RespawnHandler` si iscrive al nuovo `Health.OnDie`. |

Essendo un oggetto **nuovo**, il tank respawnato ha tutto azzerato: vita piena (`Health.OnNetworkSpawn`), `isDead = false`, e anche le monete di `CoinWallet` ripartono da 0.

**Perché qui distruggi/ricrea e non "riposiziona" come le monete (§6.8)?** La moneta è un oggetto senza proprietario e con poco stato: spostarla e azzerare un flag basta. Il tank ha owner, camera, input, vita, monete e componenti con stato interno (cooldown, `isDead`…): ricrearlo da zero garantisce che ogni pezzo riparta pulito, senza dover scrivere un `Reset()` per ciascuno.

> ⚠️ **Bug noto (anche nel codice del corso)**: `handlePlayerDespawned` prova a togliere l'iscrizione con una **nuova** lambda: `OnDie -= (health) => handlePlayerDie(player)`. Due lambda scritte uguali sono comunque due oggetti diversi, quindi `-=` non trova niente da togliere e l'iscrizione resta. In pratica non fa danni, perché l'`Health` del tank viene distrutto insieme al tank e l'iscrizione sparisce con lui. Ma la riga non fa quello che sembra. La forma corretta è salvare il delegato (es. in un `Dictionary<TankPlayer, Action<Health>>`) e togliere proprio quello. Vedi §6.12.

> **Esempio stupido**: è la vita persa in Crash Bandicoot. Crash finisce nel burrone (`OnDie`), sparisce (`Destroy`), un attimo di pausa (`yield return null`), e ricompare a un checkpoint (`SpawnPoint`) con la salute piena. Non è lo stesso Crash "rimesso in piedi": è un Crash nuovo, che riparte pulito. Solo che qui, a differenza del gioco, perdi anche tutti i frutti Wumpa raccolti.

---


## 6. Catalogo di pattern riusabili (per un gioco nuovo, da zero)

Qui sotto, ogni pattern è estratto dal progetto ma riscritto in forma **generica**, scollegata dai tank, pronta da adattare a qualsiasi altro gioco.

### 6.1 Stato autorevole con `NetworkVariable`

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

### 6.2 Movimento client-authoritative

Quando serve zero lag sui controlli del proprio personaggio (vedi §4 per l'implementazione completa): sostituisci il `NetworkTransform` standard con una variante che, per il solo owner, imposta `CanCommitToTransform = IsOwner` e ritorna `false` da `OnIsServerAuthoritative()`.

### 6.3 Feedback immediato al client ("dummy pattern")

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

### 6.4 Cheat-sheet: quale controllo usare

| Proprietà | Vero quando | Usala per |
|---|---|---|
| `IsServer` | Questa istanza è il server (o l'host) | Logica autorevole: danno, punteggio, spawn di nemici/oggetti |
| `IsClient` | Questa istanza è un client (o l'host, che è anche client) | UI, effetti visivi, suoni: cose che TUTTI devono vedere |
| `IsOwner` | Questa istanza possiede l'oggetto | Input e controlli del PROPRIO personaggio soltanto |
| `IsListening` | Il server/host è attivo | Guard prima di inviare dati di rete |
| `IsConnectedClient` | Il client è connesso a un host | Guard prima di inviare dati di rete |

Domanda da farsi sempre: *"questo codice deve girare per il PROPRIETARIO, per TUTTI I CLIENT, o solo per IL SERVER?"* — è la prima cosa da decidere prima di scrivere un `if`.

### 6.5 Iscriviti/disiscriviti in `OnNetworkSpawn`/`OnNetworkDespawn`

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

### 6.6 Bus di eventi disaccoppiato (pattern `InputReader`)

Uno `ScriptableObject` condiviso che espone eventi C# invece di essere letto direttamente da tutti. Utile ovunque serva scollegare "chi genera un dato" da "chi lo consuma" (non solo per l'input: es. un `GameEventChannel` per "partita iniziata", "partita finita", ecc.).

```csharp
[CreateAssetMenu]
public class GameEvents : ScriptableObject
{
    public event Action OnMatchStarted;
    public void RaiseMatchStarted() => OnMatchStarted?.Invoke();
}
```

### 6.7 Controllo idempotente lato server ("già fatto?")

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

### 6.8 Respawn/riposiziona invece di distruggi/ricrea

Quando un oggetto "raccoglibile" deve ricomparire (moneta, powerup): non distruggerlo e ricrearlo, spostalo e resettalo. Risparmia una `Spawn()`/`Despawn()` di rete e mantiene stabile il suo `NetworkObjectId`. Per oggetti con owner e molto stato, come il personaggio del giocatore, conviene invece il contrario: vedi §5.10 e §6.13.

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

### 6.9 Escludi il proprietario dal proprio effetto (no self-damage)

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

### 6.10 Avvio sessione tramite Sessions/Relay (niente IP diretto)

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

### 6.11 Dati del giocatore nella richiesta di connessione (connection approval)

Quando il server deve sapere qualcosa di un giocatore (nome, skin, squadra…) **prima** che il suo personaggio esista. Vedi §2.7.

```csharp
// Lato client, PRIMA di avviare la connessione (StartClient o JoinSession*)
[Serializable] public class PlayerInfo { public string name; public int team; }

NetworkManager.Singleton.NetworkConfig.ConnectionData =
    Encoding.UTF8.GetBytes(JsonUtility.ToJson(new PlayerInfo { name = "Crash", team = 1 }));

// Lato server (con "Connection Approval" attivo sul NetworkManager)
NetworkManager.Singleton.ConnectionApprovalCallback += (req, res) =>
{
    PlayerInfo info = JsonUtility.FromJson<PlayerInfo>(Encoding.UTF8.GetString(req.Payload));
    playersById[req.ClientNetworkId] = info;   // registro consultabile più tardi

    res.Approved = info.name.Length > 0;       // qui si può anche rifiutare
    res.CreatePlayerObject = true;
    res.Position = PickSpawnFor(info.team);
};
```

Il registro (`playersById`) va ripulito in `OnClientDisconnectCallback`, altrimenti cresce a ogni connessione.

### 6.12 Eventi statici di spawn/despawn per "censire" gli oggetti di rete

Quando un sistema del server deve reagire a ogni giocatore (respawn, classifica, minimappa) senza che giocatore e sistema si conoscano. Vedi `TankPlayer` + `RespawnHandler` (§5.0, §5.10).

```csharp
public class Player : NetworkBehaviour
{
    public static event Action<Player> OnSpawned;
    public static event Action<Player> OnDespawned;

    public override void OnNetworkSpawn()   { if (IsServer) OnSpawned?.Invoke(this); }
    public override void OnNetworkDespawn() { if (IsServer) OnDespawned?.Invoke(this); }
}

public class Scoreboard : NetworkBehaviour
{
    private readonly Dictionary<Player, Action<Health>> deathHandlers = new();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        foreach (Player p in FindObjectsOfType<Player>()) Track(p);   // chi c'era già prima di me
        Player.OnSpawned += Track;
        Player.OnDespawned += Untrack;
    }

    private void Track(Player p)
    {
        Action<Health> handler = h => AddDeath(p);
        deathHandlers[p] = handler;                 // salvo il delegato...
        p.GetComponent<Health>().OnDie += handler;
    }

    private void Untrack(Player p)
    {
        if (deathHandlers.Remove(p, out Action<Health> handler))
            p.GetComponent<Health>().OnDie -= handler;   // ...per poter togliere PROPRIO quello
    }
}
```

Due trappole:
- **Chi è arrivato prima**: un evento lanciato quando nessuno ascoltava è perso. Per questo, oltre a iscriversi, si recuperano a mano gli oggetti già presenti (`FindObjectsOfType`).
- **Lambda e `-=`**: `evento -= x => Foo(x)` crea una lambda nuova e non toglie nulla. Per disiscriversi serve lo stesso delegato usato con `+=`: un metodo con nome, oppure la lambda salvata (come sopra). È proprio il bug segnalato in §5.10.

Gli eventi `static` vanno sempre tolti a specchio (`OnNetworkDespawn`): sopravvivono a scene e oggetti, e un'iscrizione dimenticata tiene vivo un oggetto già distrutto.

### 6.13 Respawn del personaggio: distruggi e ricrea come Player Object

Quando il personaggio di un giocatore muore e deve rinascere pulito (vedi §5.10). Lato server:

```csharp
private void OnPlayerDied(NetworkObject player)
{
    ulong owner = player.OwnerClientId;   // salvalo PRIMA di distruggere
    Destroy(player.gameObject);           // lato server = despawn per tutti
    StartCoroutine(Respawn(owner));
}

private IEnumerator Respawn(ulong owner)
{
    yield return null;                    // un frame: il vecchio Player Object deve sparire davvero
    NetworkObject fresh = Instantiate(playerPrefab, PickSpawnPoint(), Quaternion.identity);
    fresh.SpawnAsPlayerObject(owner);     // NON Spawn(): così torna il "personaggio" di quel client
}
```

`Spawn()` normale creerebbe un oggetto del server; `SpawnWithOwnership(owner)` lo darebbe al client ma non come suo personaggio "ufficiale" (`NetworkManager.ConnectedClients[owner].PlayerObject`). `SpawnAsPlayerObject` fa entrambe le cose. Per aspettare qualche secondo prima di rinascere basta sostituire `yield return null` con `yield return new WaitForSeconds(3)`.

### 6.14 Registro statico che si compila da solo (`OnEnable`/`OnDisable`)

Quando in scena ci sono N oggetti dello stesso tipo (punti di spawn, checkpoint, porte) e vuoi trovarli senza trascinarli uno per uno in un array dell'Inspector. Vedi `SpawnPoint` (§5.0).

```csharp
public class Checkpoint : MonoBehaviour
{
    private static readonly List<Checkpoint> all = new();

    private void OnEnable()  => all.Add(this);
    private void OnDisable() => all.Remove(this);

    public static Vector3 Random() =>
        all.Count == 0 ? Vector3.zero : all[UnityEngine.Random.Range(0, all.Count)].transform.position;

    private void OnDrawGizmos() { Gizmos.color = Color.blue; Gizmos.DrawSphere(transform.position, 1); }
}
```

Non è un oggetto di rete: ogni istanza ha la propria lista locale, ma conta solo quella del server, l'unico che sceglie dove spawnare.

### 6.15 Classi C# pure iscritte a eventi: `IDisposable` + `OnDestroy` del contenitore

Quando una classe non-`MonoBehaviour` (creata con `new`) si iscrive a eventi di oggetti più longevi, come il `NetworkManager`. Non ha `OnDestroy`, quindi nessuno la sgancerebbe mai. Vedi la catena di §2.8.

```csharp
public class MatchTracker : IDisposable
{
    private readonly NetworkManager nm;

    public MatchTracker(NetworkManager nm)
    {
        this.nm = nm;
        nm.OnClientConnectedCallback += OnConnected;
    }

    private void OnConnected(ulong id) { /* ... */ }

    public void Dispose()
    {
        if (nm == null) return;               // alla chiusura il NetworkManager può essere già distrutto
        nm.OnClientConnectedCallback -= OnConnected;
    }
}

public class MatchTrackerHolder : MonoBehaviour   // il "contenitore" in scena
{
    private MatchTracker tracker;
    private void Start()     => tracker = new MatchTracker(NetworkManager.Singleton);
    private void OnDestroy() => tracker?.Dispose();
}
```

Se `Dispose` deve fare lavoro asincrono (es. cancellare una Session), non renderlo `async void`: lancia un metodo `async Task` separato con `_ = DoCleanupAsync();` e gestisci le eccezioni lì dentro.

---

## 7. Checklist mentale per ogni nuovo componente di rete

Prima di scrivere un componente multiplayer nuovo, rispondi in ordine a queste domande (ricalcano esattamente le decisioni prese in questo progetto):

1. **Questo valore/decisione deve essere uguale per tutti i giocatori?**
   → Sì: usa una `NetworkVariable` o validalo dentro una `[ServerRpc]`, scrivendo SOLO se `IsServer`.
2. **Questo comportamento riguarda solo il MIO personaggio (input, mira, movimento)?**
   → Metti `if (!IsOwner) return;` in cima al metodo.
3. **Serve che TUTTI (anche chi non è owner né server) vedano qualcosa (UI, barra vita, effetto)?**
   → Usa `IsClient`, e ascolta un cambiamento di `NetworkVariable` (`OnValueChanged`) invece di leggerla in `Update`.
4. **Voglio feedback istantaneo senza aspettare il giro di rete?**
   → Applica il "dummy pattern" (§6.3): mostra subito in locale, conferma dopo via `ServerRpc`.
5. **Questa azione potrebbe arrivare due volte per errore?**
   → Aggiungi un flag idempotente (§6.7), controllato solo lato server.
6. **Mi sto iscrivendo a un evento?**
   → Fallo in `OnNetworkSpawn`, disiscriviti a specchio in `OnNetworkDespawn`.
7. **Un oggetto raccoglibile deve "sparire e ricomparire"?**
   → Non distruggerlo: riposizionalo e resettane lo stato (§6.8).
8. **Un effetto/proiettile può colpire chi l'ha generato?**
   → Salva l'`OwnerClientId` e confrontalo prima di applicare l'effetto (§6.9).
9. **Il server deve sapere qualcosa del giocatore prima di creare il suo personaggio?**
   → Mettilo in `ConnectionData` prima di avviare la connessione e leggilo in `ConnectionApprovalCallback` (§6.11).
10. **Un sistema deve reagire a ogni giocatore che entra/esce?**
    → Eventi statici lanciati in `OnNetworkSpawn`/`OnNetworkDespawn`, più un recupero di chi c'era già. Per disiscriverti usa lo stesso delegato, mai una lambda nuova (§6.12).
11. **Il personaggio di un giocatore deve rinascere?**
    → Distruggi lato server, aspetta un frame, `SpawnAsPlayerObject(ownerClientId)` (§6.13).
12. **Una classe C# pura si iscrive a eventi?**
    → Falla `IDisposable` e chiama `Dispose` dall'`OnDestroy` del `MonoBehaviour` che la contiene (§6.15).

---

## 8. Glossario rapido

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
- **Connection approval**: il controllo che il server fa su ogni richiesta di connessione prima di accettarla (`ConnectionApprovalCallback`). Decide se accettare, dove far nascere il giocatore e se creargli il Player Object. Va attivata sul `NetworkManager`. Vedi §2.7.
- **`ConnectionData` / payload**: i byte che il client allega alla richiesta di connessione, letti dal server come `req.Payload`. In questo progetto contengono un `UserData` (nome + id di autenticazione) in JSON. Vedi §2.7.
- **Player Object / Player Prefab**: il `NetworkObject` che rappresenta "il personaggio" di un client (qui il tank). Viene creato automaticamente all'approval (`CreatePlayerObject = true`) o a mano con `SpawnAsPlayerObject`. Ogni client ne ha al massimo uno. Vedi §5.0, §5.10.
- **`ClientNetworkId` / `OwnerClientId` vs `userAuthId`**: il primo è l'id di Netcode, diverso a ogni connessione; il secondo è l'id di Unity Authentication, stabile per lo stesso giocatore. Vedi §2.7.
- **`IDisposable` / `Dispose`**: l'interfaccia C# per "fare pulizia" in una classe che non è un `MonoBehaviour` (sganciarsi da eventi, chiudere connessioni). Vedi §2.8 e §6.15.
- **`[InitializeOnLoad]`**: attributo Editor che fa eseguire il costruttore statico di una classe a ogni ricompilazione o apertura del progetto. Vedi §2.9.

---

*Documento aggiornato a ottobre 2026 (aggiunte le Lobby, §2.6.7; nome giocatore e connection approval, §2.7; disconnessione e `Dispose`, §2.8; avvio dall'Editor, §2.9; ingresso in partita, §5.0; morte e respawn, §5.10; pattern §6.11–§6.15). Le sezioni di gameplay (§4, §5) seguono l'ordine di esecuzione durante una partita: se aggiungi nuove funzionalità di gameplay, aggiungi una sottosezione in §5 nel punto della catena in cui il codice viene eseguito. La sezione di bootstrap/rete (§2) è organizzata per file e documenta la migrazione a Unity Multiplayer Services SDK (Sessions) — vedi §2.6 per il dettaglio completo.*
