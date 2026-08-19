using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class ProjectileLauncher : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;
    // [FLUSSO 56] Riferimento al CoinWallet del proprietario: serve per il costo in
    // monete di ogni sparo (costToFire, dichiarato sotto), controllato sia lato
    // client (FLUSSO 57b, solo per ottimizzazione) sia lato server (FLUSSO 58,
    // l'unico controllo che conta davvero).
    [SerializeField] private CoinWallet wallet;
    [SerializeField] private Transform projectileSpawnPoint;
    // [FLUSSO 14] Due prefab diversi per lo stesso sparo: "serverProjectilePrefab" e' il
    // proiettile VERO, istanziato solo sul server, che infligge danno reale (autorevole).
    // "clientProjectilePrefab" e' invece un proiettile "dummy" (solo visivo, senza danno),
    // mostrato subito in locale per dare un feedback istantaneo, senza aspettare la rete.
    [SerializeField] private GameObject serverProjectilePrefab;
    [SerializeField] private GameObject clientProjectilePrefab;
    [SerializeField] private GameObject muzzleFlash;
    [SerializeField] private Collider2D playerCollider;


    [Header("Settings")]
    [SerializeField] private float projectileSpeed;
    [SerializeField] private float fireRate;
    [SerializeField] private float muzzleFlashDuration;
    // [FLUSSO 56b] Costo in monete di ogni sparo: viene scalato dal wallet (vedi
    // "wallet" qui sopra, FLUSSO 56) tramite CoinWallet.spendCoins (FLUSSO 59),
    // solo quando il server autorizza effettivamente lo sparo (FLUSSO 58).
    [SerializeField] private int costToFire;

    private bool shouldFire;
    // [FLUSSO 57] Timer di cooldown tra due spari: conta alla rovescia ad ogni
    // frame in Update e si ricarica a 1/fireRate solo dopo uno sparo riuscito.
    // "Posso sparare se timer <= 0" e' piu' semplice da leggere rispetto a
    // confrontare due timestamp assoluti (Time.time).
    private float timer;
    private float muzzleFlashTimer;

    public override void OnNetworkSpawn()
    {
        // [FLUSSO 15] Come PlayerAiming (FLUSSO 8), solo il proprietario reagisce al
        // proprio input: ci iscriviamo a PrimaryFireEvent per sapere quando il tasto
        // di fuoco viene premuto o rilasciato (vedi InputReader, FLUSSO 3).
        if (!IsOwner) return;
        inputReader.PrimaryFireEvent += HandlePrimaryFire;
    }


    override public void OnNetworkDespawn()
    {
        // [FLUSSO 16] Ci disiscriviamo alla dismissione dell'oggetto, a specchio del
        // FLUSSO 15: evita che l'evento continui a chiamare un componente ormai distrutto.
        if (!IsOwner) return;
        inputReader.PrimaryFireEvent -= HandlePrimaryFire;
    }
    // Update is called once per frame
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

        // [FLUSSO 57b] Controllo "cosmetico" lato client: se non ci sono abbastanza
        // monete evitiamo di inviare inutilmente la ServerRpc e di mostrare un
        // dummy che il server rifiuterebbe comunque. Non e' autorevole: la vera
        // verifica, quella che decide se lo sparo conta davvero, e' lato server
        // (FLUSSO 58) e va rifatta li' per intero.
        if (wallet.totalCoins.Value < costToFire) return;

        // [FLUSSO 17] Se il tasto di fuoco e' premuto (shouldFire, aggiornato dal
        // FLUSSO 20), avviamo lo sparo su due binari: chiediamo al server di generare
        // il proiettile reale (autorevole, infligge danno) e mostriamo SUBITO in locale
        // un proiettile "dummy" per dare un feedback immediato, senza aspettare il
        // round-trip di rete.
        // PrimaryFireServerRpc non è bloccante: è semplicemente l'invio di un 
        // messaggio di rete, ritorna quasi istantaneamente senza aspettare che il server 
        // lo riceva o processi. Quindi anche se il dummy è spawnato "dopo" nel testo del 
        // metodo, entrambe le istruzioni vengono eseguite nello stesso frame lato client.

        PrimaryFireServerRpc(projectileSpawnPoint.position, projectileSpawnPoint.up);

        SpawnDummyProjectile(projectileSpawnPoint.position, projectileSpawnPoint.up);
        timer = 1 / fireRate;

    }

    [ServerRpc]
    private void PrimaryFireServerRpc(Vector2 spawnPos, Vector2 direction)
    {
        // [FLUSSO 18] Eseguito solo sul server: istanzia il proiettile VERO (quello che
        // infliggera' danno) e poi avvisa tutti i client, tramite ClientRpc, di mostrare
        // anche loro un proiettile dummy nello stesso punto e direzione.
        // [FLUSSO 58] Controllo autorevole: rifa' la stessa verifica del client
        // (FLUSSO 57b), perche' quel controllo e' solo un'ottimizzazione, non una
        // garanzia - un client modificato potrebbe inviare comunque la ServerRpc.
        // Solo se qui le monete bastano davvero si procede e si scala il costo
        // (CoinWallet.spendCoins, FLUSSO 59).
        if (wallet.totalCoins.Value < costToFire) return;

        wallet.spendCoins(costToFire);

        GameObject projectileInstance = Instantiate(
            serverProjectilePrefab,
            spawnPos,
            Quaternion.identity);

        projectileInstance.transform.up = direction;
        Physics2D.IgnoreCollision(playerCollider, projectileInstance.GetComponent<Collider2D>());

        // [FLUSSO 29] Il proiettile deve sapere chi lo ha sparato, cosi' da non poter
        // colpire il proprio proprietario (vedi DealDamageOnContact, FLUSSO 30-33):
        // eseguito solo qui, lato server (FLUSSO 18), dove OwnerClientId del
        // ProjectileLauncher e' gia' noto e autorevole.
        if (projectileInstance.TryGetComponent<DealDamageOnContact>(out DealDamageOnContact dealDamage))
        {
            dealDamage.setOwnerClientId(OwnerClientId);
        }

        if (projectileInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            Debug.Log("projectileSpeed " + projectileSpeed);
            rb.velocity = rb.transform.up * projectileSpeed;
        }
        SpawnDummyProjectileClientRpc(spawnPos, direction);
    }

    [ClientRpc]
    private void SpawnDummyProjectileClientRpc(Vector2 spawnPos, Vector2 direction)
    {
        // [FLUSSO 19] Eseguito su TUTTI i client. Il proprietario ha gia' mostrato il
        // suo dummy al FLUSSO 17, quindi qui lo saltiamo (IsOwner) per non duplicarlo:
        // questa callback serve solo agli altri client, che vedono cosi' lo sparo senza
        // dover aspettare il proiettile reale del server.
        if (IsOwner) return;
        SpawnDummyProjectile(spawnPos, direction);
    }

    private void HandlePrimaryFire(bool shouldFire)
    {
        // [FLUSSO 20] Callback collegata al FLUSSO 15: aggiorna solo il flag
        // "shouldFire", che l'Update (FLUSSO 17) legge ad ogni frame per decidere
        // se sparare.
        this.shouldFire = shouldFire;
    }

    private void SpawnDummyProjectile(Vector2 spawnPos, Vector2 direction)
    {
        muzzleFlash.SetActive(true);
        muzzleFlashTimer = muzzleFlashDuration;

        // [FLUSSO 21] Helper condiviso da proprietario (FLUSSO 17) e altri client
        // (FLUSSO 19): istanzia solo l'effetto visivo del proiettile, senza logica di
        // danno (quella e' gestita esclusivamente dal server in FLUSSO 18).
        GameObject projectileInstance = Instantiate(
            clientProjectilePrefab,
            spawnPos,
            Quaternion.identity);

        projectileInstance.transform.up = direction;
        Physics2D.IgnoreCollision(playerCollider, projectileInstance.GetComponent<Collider2D>());

        if (projectileInstance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            Debug.Log("projectileSpeed " + projectileSpeed);
            rb.velocity = rb.transform.up * projectileSpeed;
        }

    }

}
