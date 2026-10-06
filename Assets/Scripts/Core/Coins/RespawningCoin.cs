using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawningCoin : Coin
{
    // Sollevato solo quando collect() e' stato eseguito lato server
    // (ramo server di collect), mai lato client: e' il modo con cui questa singola moneta
    // avvisa il CoinSpawner che l'ha creata di doverla riposizionare e
    // riabilitare (vedi CoinSpawner).
    public event Action<RespawningCoin> onCollected;

    // Memorizza la posizione dell'ultimo frame per poter rilevare,
    // in Update, un teleport della moneta deciso dal server.
    private Vector3 previousPosition;
    public override int collect()
    {
        // Chiamato anche dal client che tocca la moneta (vedi
        // CoinWallet.OnTriggerEnter2D), che pero' non e' autorevole:
        // non puo' decidere se la moneta e' gia' stata raccolta ne' quanto vale.
        // Si limita a nasconderla localmente per un feedback visivo immediato e
        // ritorna sempre 0, cosi' CoinWallet non accredita nulla lato client
        // (CoinWallet.OnTriggerEnter2D). Il vero conteggio arrivera' comunque a schermo tramite la
        // sincronizzazione automatica di totalCoins, decisa dal server qui sotto.
        if (!IsServer)
        {
            showCoin(false);
            return 0;
        }

        // Sul server invece il controllo e' autorevole: se la moneta
        // e' gia' stata raccolta (da questo o da un altro giocatore, in caso di
        // doppio trigger nello stesso frame) restituiamo 0 per evitare di
        // accreditare lo stesso valore due volte.
        if (alreadyCollected) return 0;
        else
        {
            alreadyCollected = true;
            // Notifichiamo il CoinSpawner (evento onCollected, dichiarato sopra)
            // solo qui, dopo aver accertato lato server che la raccolta e' valida:
            // reagira' spostando la moneta e riabilitandola (handleCoinCollected + Reset).
            onCollected?.Invoke(this);
            return coinValue;
        }
    }

    // Chiamato dal CoinSpawner subito dopo aver ricollocato la moneta
    // (handleCoinCollected): azzera alreadyCollected cosi' che il controllo autorevole in
    // collect() torni a considerarla raccoglibile.
    public void Reset()
    {
        alreadyCollected = false;
    }

    private void Update()
    {
        // Il riposizionamento della moneta e' deciso solo dal server
        // (CoinSpawner.handleCoinCollected) ma la nuova transform.position
        // arriva su ogni client tramite la normale sincronizzazione di rete: quando la
        // notiamo cambiata rispetto al frame precedente, e' il segnale che la moneta e'
        // stata rispawnata altrove, quindi annulliamo qui il nascondimento locale
        // fatto nel ramo client di collect(), che riguardava solo lo SpriteRenderer e non
        // e' automaticamente sincronizzato dalla rete.
        if (previousPosition != transform.position)
        {
            showCoin(true);
        }

        previousPosition = transform.position;
    }
}
