# Guida alla Codebase Multiplayer

> Documentazione completa del progetto (tank 2D multiplayer, Unity + **Netcode for GameObjects** v1.12.2).
> Obiettivo: capire ogni riga di rete scritta finora e usarla come **base di partenza per costruire altri giochi multiplayer da zero**.

---

## 0. Come leggere questo documento

Nel codice ogni commento importante è taggato `// [FLUSSO N]`. Sono numeri progressivi che raccontano **l'ordine cronologico** in cui il codice viene eseguito durante una partita, non l'ordine dei file. Seguendo i numeri in salita si legge la partita come una storia: input → mira → sparo → danno → vita → monete.

Esistono però due eccezioni:

- `ClientNetworkTransform.cs` ha una propria numerazione interna (`FLUSSO 0`→`8`) che descrive un meccanismo a sé stante (la sincronizzazione del transform), richiamato dagli altri script come blocco unico ("vedi FLUSSO 0-8 in quel file"). Per questo lo trattiamo come **fondamenta**, prima del flusso principale di gioco.
- `FLUSSO 60`→`81` (bootstrap dell'app e autenticazione, §2) sono cronologicamente i **primi** a girare in assoluto — prima ancora di `FLUSSO 0` — ma sono numerati da 60 in su perché aggiunti dopo, seguendo la regola in fondo a questo documento ("continua la numerazione da N in poi"). Non farti ingannare dal numero alto: leggili come un prologo, non come un epilogo.

Questo documento è organizzato così:

1. Concetti Netcode da sapere prima di leggere il codice (§1)
2. Bootstrap dell'app e autenticazione, in ordine `FLUSSO 60 → 81` (§2 — il vero punto di partenza, prima ancora della sessione di rete)
3. Avvio della sessione di rete (§3)
4. Le fondamenta: come si muove un oggetto in rete (§4 — `ClientNetworkTransform`)
5. Il flusso di gioco vero e proprio, in ordine `FLUSSO 0 → 59` (§5)
6. Tabella riepilogativa di tutti i FLUSSO (§6)
7. Catalogo di pattern riusabili, con mini-esempi "stupidi" scollegati dal progetto, pronti per essere copiati in un gioco nuovo (§7)
8. Checklist mentale da seguire ogni volta che scrivi un componente di rete nuovo (§8)
9. Glossario (§9)

---

## 1. Concetti Netcode da sapere prima di leggere il codice

| Concetto | Cos'è | Esempio stupido |
|---|---|---|
| **NetworkManager** | Il "regista" della sessione: sa chi è connesso, avvia Host/Server/Client. | È il centralino di un call center: smista le chiamate, ma non parla lui con i clienti. |
| **NetworkObject** | Componente che rende un GameObject "spawnabile in rete": gli dà un ID univoco condiviso da tutti. | È il codice a barre su un pacco: server e client, guardando lo stesso codice, sanno che stanno parlando dello STESSO pacco. |
| **NetworkBehaviour** | Un `MonoBehaviour` "consapevole della rete": espone `IsServer`, `IsClient`, `IsOwner`, `OwnerClientId`, `OnNetworkSpawn`/`OnNetworkDespawn`. | È un dipendente che, oltre al suo lavoro normale, sa sempre rispondere a "lavoro qui come capo (server) o come impiegato (client)?". |
| **NetworkVariable\<T\>** | Variabile che si sincronizza da sola su tutti i client. Di default: scrivibile SOLO dal server, leggibile da tutti. | Una lavagna in classe: il maestro (server) è l'unico che può scriverci sopra, tutti gli alunni (client) la leggono. Se un alunno scrive sul SUO quaderno, la lavagna vera non cambia. |
| **ServerRpc** | Chiamata di metodo da client → eseguita sul server. | È come compilare una richiesta e imbucarla: tu (client) non esegui l'azione, chiedi che la esegua l'ufficio (server). |
| **ClientRpc** | Chiamata di metodo da server → eseguita su tutti i client (o su un sottoinsieme). | È un annuncio alla radio: lo trasmette solo l'emittente (server), ma lo sentono tutti gli ascoltatori (client). |
| **Server authority** | Il server è l'unica fonte di verità: decide se un'azione è valida. | L'arbitro di una partita a carte: un giocatore può *dire* "peschi", ma è l'arbitro a decidere se è il suo turno e a dirlo a tutti. |
| **Client authority** | Un client specifico ha il permesso di decidere lui stesso un valore (di solito per ridurre la latenza percepita). | Quando scrivi in chat, le lettere appaiono sul TUO schermo all'istante mentre le premi: nessuno aspetta il server per vedersele scrivere da sé. |
| **Ownership / IsOwner** | Ogni `NetworkObject` ha un proprietario (di solito il client che lo controlla, es. il proprio tank). | Le chiavi di un'auto a noleggio: solo chi le ha in mano può guidarla; gli altri la vedono muoversi ma non la guidano. |
| **Host** | Un'istanza che è CONTEMPORANEAMENTE server e client (gioca e allo stesso tempo arbitra). | Il tavolo dei giochi in casa tua: tu ospiti gli amici (sei il server) ma giochi anche tu (sei anche client). |

**Regola d'oro che attraversa tutto il progetto**: *"il client mostra, il server decide"*. Ogni volta che vedrai un client fare qualcosa visivamente in anticipo (nascondere una moneta, mostrare un proiettile finto), sappi che è solo estetica: la verità arriverà comunque dal server.

---

## 2. Bootstrap dell'app e autenticazione (`FLUSSO 60 → 81`)

Questo è il **vero** primo codice eseguito all'avvio del gioco, prima ancora della scena con `ConnectionButtons` (§3): vive nella scena `NetBootstrap`, caricata per prima. Il suo scopo è distinguere un dedicated server da un giocatore normale e, nel secondo caso, autenticare il giocatore presso **Unity Gaming Services (UGS)** prima di lasciarlo entrare nel menu.

> **Esempio stupido**: è come il controllo documenti all'ingresso di un evento, PRIMA della sala principale (il menu/la partita vera). Se sei un membro dello staff (dedicated server) passi da un'altra porta; se sei un ospite (giocatore) devi prima farti timbrare il biglietto (autenticazione anonima) — solo dopo ti aprono la porta della sala.

**File coinvolti**:
- `Assets/Scripts/Networking/ApplicationController.cs` — punto di ingresso, decide dedicated server vs client.
- `Assets/Scripts/Networking/Client/ClientSingleton.cs` + `ClientGameManager.cs` — bootstrap lato client, autenticazione, passaggio al menu.
- `Assets/Scripts/Networking/Client/AuthenticationWrapper.cs` — wrapper attorno a Unity Authentication Service.
- `Assets/Scripts/Networking/Host/HostSingleton.cs` + `HostGameManager.cs` — bootstrap lato host, per ora quasi vuoto.

### 2.1 `ApplicationController.cs` — punto di ingresso

| FLUSSO | Cosa succede |
|---|---|
| **60** | Campi Inspector `clientPrefab`/`hostPrefab`: i "capostipiti" delle due catene di singleton, istanziati dinamicamente più sotto. |
| **61** | `Start()`: `DontDestroyOnLoad(gameObject)` (l'oggetto deve sopravvivere al cambio scena verso il Menu, FLUSSO 70) e rilevamento dedicated server via `SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null` (un server headless non ha una GPU). |
| **62** | `launchInMode`, ramo dedicated server: ancora uno stub vuoto. |
| **64** | Subito dopo (nel codice viene PRIMA di FLUSSO 63, vedi nota sotto), **sempre** (anche per un client puro): `Instantiate(hostPrefab)` + `createHost()` (FLUSSO 80), per essere già pronti se questa istanza dovesse diventare host in seguito (es. premendo "Host" nel Menu, `MainMenu.StartHost`, FLUSSO 83). |
| **63** | `launchInMode`, ramo client: `Instantiate(clientPrefab)` poi `await clientSingleton.createClient()` (FLUSSO 68), che a catena esegue l'intera autenticazione. |
| **65** | Solo se `authenticated == true`: `clientSingleton.gameManager.goToMenu()` (FLUSSO 70). Se falso (es. `AuthState.Error`/`Timeout` dopo `maxTries` tentativi falliti, FLUSSO 76) l'app resta bloccata sulla scena di bootstrap: non c'è ancora un messaggio d'errore o un retry visibile all'utente. |

> **Nota sull'ordine 64 → 63**: i numeri `FLUSSO` restano legati al *significato* del passo (63 = ramo client, 64 = host creato sempre), non alla riga in cui compaiono — per questo qui il 64 precede il 63 nella tabella, rispecchiando l'ordine reale nel file. Il motivo dello scambio: `createHost()` è sincrono e non dipende in nulla dall'esito dell'autenticazione, quindi non ha senso lasciarlo "in mezzo" a un `await`. Spostandolo prima, `HostSingleton.Instance` è pronto il prima possibile invece che solo a fine autenticazione client.

### 2.2 `ClientSingleton.cs` + `ClientGameManager.cs` — bootstrap lato client

`ClientSingleton` è un `MonoBehaviour` "contenitore": esiste solo per dare un aggancio in scena a `ClientGameManager`, che è una classe C# pura (creata con `new`, non `Instantiate`) e quindi non potrebbe vivere da sola come componente.

| FLUSSO | File | Cosa succede |
|---|---|---|
| **66** | `ClientSingleton.cs` | Pattern singleton "lazy" (`Instance`), identico nello spirito a `NetworkManager.Singleton`. Al momento nessun altro script legge `ClientSingleton.Instance`: è pronto per essere usato da scene future (es. il Menu). |
| **67** | `ClientSingleton.Start` | `DontDestroyOnLoad`, stesso motivo del FLUSSO 61. |
| **68** | `ClientSingleton.createClient` | Crea il `ClientGameManager` e gli delega subito `initAsync()` (FLUSSO 69); il `bool` risultante risale fino ad `ApplicationController` (FLUSSO 63). |
| **69** | `ClientGameManager.initAsync` | `UnityServices.InitializeAsync()` — va chiamato una volta per processo prima di qualunque servizio UGS — poi `AuthenticationWrapper.doAuth()` (§2.3). |
| **70** | `ClientGameManager.goToMenu` | `SceneManager.LoadScene("Menu")`, chiamato solo se l'autenticazione è riuscita (FLUSSO 65). |

### 2.3 `AuthenticationWrapper.cs` — autenticazione anonima UGS

Wrapper `static` (un solo stato per tutto il processo, non per istanza) attorno a Unity Authentication Service, con una piccola macchina a stati (`AuthState`, FLUSSO 77) pensata per gestire chiamate concorrenti a `doAuth`.

| FLUSSO | Cosa succede |
|---|---|
| **71** | Campo statico `authState`: essendo la classe `static`, lo stato è condiviso da tutto il processo e sopravvive ai cambi scena come gli oggetti `DontDestroyOnLoad`. |
| **72** | `doAuth`, prima guardia: se già `Authenticated`, ritorna subito senza rifare l'handshake. |
| **73** | `doAuth`, seconda guardia: se un'altra chiamata ha già portato lo stato ad `Authenticating`, questa NON ne avvia una seconda in parallelo — aspetta passivamente l'esito con `authenticating()` (FLUSSO 75). |
| **74** | `doAuth`, terzo caso (nessuna autenticazione né in corso né già fatta): delega direttamente a `SignInAnonimouslyAsync(maxTries)` (FLUSSO 76) e ne ritorna l'esito. **Corretto un bug**: qui prima c'era un secondo ciclo, quasi duplicato di quello dentro `SignInAnonimouslyAsync`, che però non impostava `authState = AuthState.Authenticating;` prima del `while` — alla primissima chiamata `authState` valeva ancora `NonAuthenticated`, la condizione del `while` era quindi falsa fin da subito e il login non partiva mai. |
| **75** | `authenticating()` — helper di attesa passiva usato dal FLUSSO 73: polling ogni 200ms finché lo stato non esce da `Authenticating`/`NonAuthenticated`. |
| **76** | `SignInAnonimouslyAsync` (privato) — vera logica di login, ora richiamata da `doAuth` (FLUSSO 74): imposta subito `Authenticating` (cosa che prima mancava), ritenta fino a `maxRetries` volte, gestisce le eccezioni (`AuthenticationException`, `RequestFailedException`) impostando `Error`, e segna `Timeout` se i tentativi si esauriscono senza successo. |
| **77** | `enum AuthState` — i 5 stati possibili (`NonAuthenticated` è il default C#, valore 0, quindi anche lo stato iniziale). |

### 2.4 `HostSingleton.cs` + `HostGameManager.cs` — bootstrap lato host

Controparte simmetrica di §2.2, ma lato host: stesso identico pattern, dietro a un `HostGameManager` che ora (§2.5) sa davvero avviare una sessione.

| FLUSSO | File | Cosa succede |
|---|---|---|
| **78** | `HostSingleton.cs` | Pattern singleton "lazy", identico al FLUSSO 66. |
| **79** | `HostSingleton.Start` | `DontDestroyOnLoad`, stesso motivo del FLUSSO 67. |
| **80** | `HostSingleton.createHost` | Istanzia `HostGameManager` (FLUSSO 81); a differenza di `createClient` (FLUSSO 68) non c'è ancora nessuna logica asincrona da avviare qui: quella è tutta dentro `StartHostAsync` (FLUSSO 85-89), chiamata più tardi, non da `createHost`. |
| **81** | `HostGameManager.cs` | Non più un placeholder: `StartHostAsync` (§2.5) alloca una sessione su Unity Relay, configura il transport e avvia davvero l'host. |
| **82** | `HostSingleton.GameManager` | Proprietà pubblica (`{ get; private set; }`, prima era un campo privato illeggibile da fuori): si allinea al pattern già usato da `ClientSingleton.gameManager` (FLUSSO 66/68) e serve a `MainMenu.StartHost` (FLUSSO 83) per raggiungere `HostGameManager` dall'esterno tramite `HostSingleton.Instance.GameManager`. |

### 2.5 `MainMenu.cs` + `HostGameManager.StartHostAsync` — avvio reale della sessione Host (Relay)

File coinvolti:
- `Assets/Scripts/UI/MainMenu.cs` — bottone "Host" nella scena `Menu`.
- `Assets/Scripts/Networking/Host/HostGameManager.cs` — logica vera di avvio sessione.

Fino a qui `HostGameManager` era vuoto: `createHost()` (FLUSSO 80) si limitava a istanziarlo, pronto ma inerte. Da qui in poi, premendo "Host" nel Menu, la sessione viene davvero aperta, appoggiandosi a **Unity Relay** invece che a un IP diretto (vedi `com.unity.services.relay`, aggiunto in `Packages/manifest.json`).

> **Perché Relay e non un IP diretto**: un IP diretto (come fa ancora `ConnectionButtons`, §3, per i test in locale) richiede che l'host abbia un indirizzo raggiungibile dagli altri giocatori — quasi mai vero su Internet reale (NAT, router, firewall). Relay fa da "postino" neutrale: sia host che client si connettono AD ESSO, mai direttamente tra loro, ed è lui a girare i pacchetti. In cambio di un po' di latenza in più, funziona ovunque senza alcuna configurazione di rete lato utente.

| FLUSSO | File | Cosa succede |
|---|---|---|
| **83** | `MainMenu.StartHost` | Metodo agganciato all'`OnClick` del bottone "Host" nella scena `Menu` (Inspector, non codice). Chiama `HostSingleton.Instance.GameManager.StartHostAsync()`. Funziona solo se si è arrivati al Menu passando da `NetBootstrap` (dove `HostSingleton` viene creato, FLUSSO 64): aprire la scena Menu direttamente lascia `Instance` a `null` e lancia una `NullReferenceException` — non un bug del metodo, ma dell'ordine di avvio delle scene. |
| **84** | `HostGameManager` (campi) | `allocation`/`joinCode`: stato della sessione Relay corrente (servirà a mostrare il join code a schermo, quando esisterà un ramo "Join" lato client). `gameSceneName`/`maxConnections`: configurazione. |
| **85** | `HostGameManager.StartHostAsync` | `Relay.Instance.CreateAllocationAsync(maxConnections)`: riserva risorse sui server Relay di Unity per una partita fino a `maxConnections` giocatori. Chiamata di rete, quindi in `try/catch`. |
| **86** | `HostGameManager.StartHostAsync` | `Relay.Instance.GetJoinCodeAsync(allocation.AllocationId)`: trasforma l'allocation in un codice breve condivisibile. Per ora solo loggato (`Debug.Log`), non ancora mostrato in UI. |
| **87** | `HostGameManager.StartHostAsync` | Recupera lo `UnityTransport` dal `NetworkManager` e gli passa `new RelayServerData(allocation, "udp")`: da qui in poi Netcode instraderà tutto tramite Relay invece che con IP diretto. |
| **88** | `HostGameManager.StartHostAsync` | `NetworkManager.Singleton.StartHost()`: solo ORA, a transport configurato, questa istanza diventa davvero server+client (Host, vedi §1). |
| **89** | `HostGameManager.StartHostAsync` | `NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, ...)`: cambio scena "di rete" (diverso da `SceneManager.LoadScene` usato in FLUSSO 70), che porta con sé anche i client già connessi verso la scena `Game`. |

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

> **Esempio stupido**: prenotare un tavolo al ristorante tramite un centralino (Relay) invece di dare il proprio indirizzo di casa agli invitati. Il centralino (85) ti dà un numero di prenotazione da comunicare agli amici (86, il join code); da quel momento tutte le chiamate passano dal centralino, non serve che gli amici sappiano dove abiti davvero (87). Solo quando il tavolo è confermato apri davvero il ristorante (88) e fai accomodare tutti nella sala giusta (89).

**Cosa manca ancora, e sarà probabilmente il prossimo passo**: un bottone "Join" nel Menu che chieda il `joinCode` all'utente e lo passi a un `ClientGameManager.StartClientAsync` simmetrico (che oggi non esiste), usando `JoinAllocationAsync` invece di `CreateAllocationAsync`.

#### Diagramma del bootstrap

```
ApplicationController (scena NetBootstrap)
      |  Start(): DontDestroyOnLoad, rileva dedicated server (61)
      |
      |-- dedicated server (62): stub, nessuna auth
      |
      |-- (SEMPRE, PRIMA del ramo client) Instantiate(HostSingleton) + createHost() (64)
      |
      \-- client (63): Instantiate(ClientSingleton) -----> ClientSingleton.createClient (68)
                                                                    |
                                                                    v
                                                          ClientGameManager.initAsync (69)
                                                                    |
                                                       UnityServices.InitializeAsync()
                                                                    |
                                                                    v
                                                     AuthenticationWrapper.doAuth (72-74)
                                                                    |
                                                    SignInAnonimouslyAsync (76): login
                                                    + retry + gestione eccezioni
                                                                    |
                                                          risale come bool "authenticated"
                                                                    |
                            se authenticated == true (65): goToMenu() (70) -> scena "Menu"
                                                                    |
                                     utente preme "Host" -> MainMenu.StartHost (83, §2.5)
                                                                    |
                                     HostSingleton.Instance.GameManager.StartHostAsync()
                                                                    |
                                     Relay: CreateAllocationAsync + GetJoinCodeAsync (85-86)
                                                                    |
                                     StartHost() + LoadScene("Game") di rete (88-89)
```

---

## 3. Avvio della sessione — `ConnectionButtons.cs`

File: `Assets/Scripts/ConnectionButtons.cs`

Componente da mettere su un `Canvas` con due bottoni UI:

- **Host** → `NetworkManager.Singleton.StartHost()`: questa istanza fa contemporaneamente da server e da client (gioca e allo stesso tempo comanda la partita).
- **Join** → `NetworkManager.Singleton.StartClient()`: questa istanza si collega a un Host già avviato.

> **Esempio stupido**: per testare in locale, avvii due istanze del gioco (due finestre Editor/Build): una preme "Host" (apre la partita), l'altra preme "Join" (si siede al tavolo). Se ne avvii una terza e preme "Join", si aggiunge un terzo giocatore allo stesso tavolo.

Non c'è validazione, IP hardcoded o matchmaking: è la versione minima per testare la sincronizzazione in locale.

> **Stato attuale**: `ConnectionButtons` non è più agganciato a nessun bottone nelle scene attuali — il bottone "Host" del Menu ora chiama `MainMenu.StartHost` (FLUSSO 83, §2.5), che passa da Relay invece che da un IP diretto. Il file resta nel progetto come riferimento/rete di sicurezza per test locali rapidi (due istanze Editor sulla stessa macchina, senza bisogno di Relay), ma il percorso "di produzione" ora è quello descritto in §2.5.

---

## 4. Le fondamenta: sincronizzare un transform in rete — `ClientNetworkTransform.cs`

File: `Assets/Scripts/Utils/ClientNetworkTransform.cs`
Va assegnato ai prefab **Player**, **Treads** e **TurretPivot** al posto del `NetworkTransform` standard.

### Il problema che risolve

Il `NetworkTransform` di Netcode, di default, è **server-authoritative**: solo il server può spostare l'oggetto, ogni modifica locale del client viene ignorata. È sicuro contro i cheat (teleport, speed-hack) ma introduce **latenza**: ogni movimento deve fare un giro di andata e ritorno verso il server prima di essere visibile, e i controlli sembrano "gommosi".

`ClientNetworkTransform` capovolge la regola **solo per il movimento del proprio tank**: il proprietario (owner) scrive direttamente il proprio transform, il server lo riceve e lo ridistribuisce agli altri. Risultato: zero input-lag per chi guida, in cambio di una vulnerabilità accettabile (un client scorretto potrebbe teleportarsi).

> **Esempio stupido**: è la differenza tra scrivere su una lavagna condivisa passando sempre per il preside (server-authoritative, lento ma sicuro) e avere il permesso di scrivere direttamente sul TUO angolo di lavagna (client-authoritative, veloce, ma se sei disonesto puoi scrivere cose false).

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

> **Esempio stupido**: `InputReader` è il telecomando universale di casa. Preme un tasto una volta sola, e chiunque sia "sintonizzato" (PlayerMovement, PlayerAiming, ProjectileLauncher) riceve il segnale, senza che il telecomando sappia o si preoccupi di chi lo sta ascoltando.

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

> **Esempio stupido**: è come un girasole che gira sempre verso il sole (il mouse). Non importa dove sia il resto della pianta (il corpo del tank): la testa (la torretta) trova sempre il modo di puntare nella direzione giusta.

### 5.3 Sparo — `ProjectileLauncher.cs` (FLUSSO 14 → 21, + 29)

File: `Assets/Scripts/Core/Player/ProjectileLauncher.cs`

Qui si vede il pattern più importante del progetto: **due proiettili per ogni sparo**.

- `serverProjectilePrefab` → il proiettile **vero**, istanziato SOLO sul server, autorevole, infligge danno reale.
- `clientProjectilePrefab` → un proiettile **dummy**, solo visivo, mostrato subito in locale per dare feedback istantaneo senza aspettare il giro di rete.

> **Esempio stupido**: un attore che finge di sparare a salve per l'effetto scenico immediato (il dummy, che vedi subito), mentre il "colpo vero" viene autorizzato ed eseguito dal regista dietro le quinte (il server) un attimo dopo.

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

> **Esempio stupido**: un palloncino che scoppia se tocca uno spillo (FLUSSO 22), ma che comunque si sgonfia da solo dopo un minuto anche se non tocca niente (FLUSSO 23) — così non resta a fluttuare in scena per sempre, sprecando memoria.

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

> **Esempio stupido**: sterzare un'auto. Giri il volante (Update, ad ogni frame, effetto visivo immediato) e intanto l'auto avanza nella fisica del motore (FixedUpdate) sempre nella direzione in cui è puntato il muso — non magicamente verso nord.

### 5.6 Danno da contatto — `DealDamageOnContact.cs` (FLUSSO 29 → 34)

File: `Assets/Scripts/Core/Combat/DealDamageOnContact.cs` — presente **solo** sul `serverProjectilePrefab`, quindi la sua logica gira per forza solo sul server.

| FLUSSO | Cosa succede |
|---|---|
| **30** | Commento di classe: componente esclusivo del proiettile vero. |
| **31** | `setOwnerClientId` — chiamato dal server subito dopo lo spawn (FLUSSO 29, §5.3) per ricordare chi ha sparato. |
| **32** | Se l'oggetto colpito non ha `Rigidbody2D`, non è un bersaglio valido (es. muri/scenario): si esce subito. |
| **33** | Se il bersaglio ha un `NetworkObject` il cui `OwnerClientId` coincide con chi ha sparato, si esce: **non ci si può ferire da soli**. |
| **34** | Se il bersaglio ha un componente `Health`, gli si infligge danno. |

> **Esempio stupido**: appena esce dalla canna, un proiettile non può tornare indietro e colpire chi l'ha sparato — sarebbe come un boomerang impazzito che ferisce il lanciatore, che nessun gioco vorrebbe.

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

> **Esempio stupido**: l'indicatore di carburante in macchina. Solo il benzinaio (server) può davvero cambiare quanto carburante c'è nel serbatoio; la lancetta sul cruscotto (HealthDisplay) si limita a MOSTRARE il livello vero, non può decidere lei quanta benzina c'è.

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

> **Esempio stupido**: un gettone da sala giochi. Lo infili in una macchina e SPARISCE SUBITO ai tuoi occhi (feedback client immediato, FLUSSO 42) — non stai lì a fissare un gettone già usato. Solo il gestore della sala (server) sa davvero se il gettone era valido, lo conta nel cassetto (FLUSSO 44) e decide di farne ricomparire uno identico in un'altra macchina della sala (FLUSSO 52-54), senza doverne stampare uno nuovo da zero.

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

> **Esempio stupido**: come pagare un biglietto del bus convalidandolo alla macchinetta. Tu vedi il gesto (client, FLUSSO 57b: "ho abbastanza soldi? provo a salire"), ma è la macchinetta (server, FLUSSO 58) a controllare davvero il credito e a scalarlo (FLUSSO 59) — se provi a salire senza credito, il gesto non ha alcun effetto.

---

## 6. Tabella riepilogativa di tutti i FLUSSO

Riferimento rapido, in ordine numerico. "∞" = catena locale a `ClientNetworkTransform.cs` (numerazione indipendente).

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
| 76 | `AuthenticationWrapper.cs` | `SignInAnonimouslyAsync`, vera logica di login (ora richiamata da `doAuth`) |
| 77 | `AuthenticationWrapper.cs` | `enum AuthState` |
| 78 | `HostSingleton.cs` | Pattern singleton `Instance` |
| 79 | `HostSingleton.cs` | `Start`: `DontDestroyOnLoad` |
| 80 | `HostSingleton.cs` | `createHost`: crea `HostGameManager` |
| 81 | `HostGameManager.cs` | Non più placeholder: `StartHostAsync` avvia davvero la sessione (Relay) |
| 82 | `HostSingleton.cs` | `GameManager` diventa proprietà pubblica, leggibile da `MainMenu` |
| 83 | `MainMenu.cs` | `StartHost()`, agganciato al bottone "Host" del Menu |
| 84 | `HostGameManager.cs` | Campi `allocation`/`joinCode`/`gameSceneName`/`maxConnections` |
| 85 | `HostGameManager.cs` | `StartHostAsync`: `Relay.Instance.CreateAllocationAsync` |
| 86 | `HostGameManager.cs` | `StartHostAsync`: `Relay.Instance.GetJoinCodeAsync` |
| 87 | `HostGameManager.cs` | `StartHostAsync`: configura `UnityTransport` con `RelayServerData` |
| 88 | `HostGameManager.cs` | `StartHostAsync`: `NetworkManager.Singleton.StartHost()` |
| 89 | `HostGameManager.cs` | `StartHostAsync`: `NetworkManager.Singleton.SceneManager.LoadScene("Game")` |

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

### 7.10 Avvio sessione tramite Relay (niente IP diretto)

Quando l'host e i client non sono sulla stessa rete locale e non puoi contare su IP pubblici/port forwarding (praticamente sempre, fuori da un test in LAN). Vedi §2.5 per l'implementazione completa.

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

Il ramo `StartClientAsync` (con `JoinAllocationAsync` al posto di `CreateAllocationAsync`) è simmetrico ma **non ancora scritto** in questo progetto: è il prossimo passo naturale per completare il flusso "Join" nel Menu.

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

---

*Documento generato a partire dai commenti `[FLUSSO N]` presenti nel codice sorgente. Se aggiungi nuove funzionalità, continua la numerazione da 90 in poi e aggiorna la tabella in §6.*
