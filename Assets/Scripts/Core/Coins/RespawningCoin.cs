using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawningCoin : Coin
{
    // [FLUSSO 46] Sollevato solo quando collect() e' stato eseguito lato server
    // (FLUSSO 43), mai lato client: e' il modo con cui questa singola moneta
    // avvisa il CoinSpawner che l'ha creata di doverla riposizionare e
    // riabilitare (vedi CoinSpawner, FLUSSO 48-52).
    public event Action<RespawningCoin> onCollected;

    // [FLUSSO 47] Memorizza la posizione dell'ultimo frame per poter rilevare,
    // in Update, un teleport della moneta deciso dal server (FLUSSO 54).
    private Vector3 previousPosition;
    public override int collect()
    {
        // [FLUSSO 42] Chiamato anche dal client che tocca la moneta (vedi
        // CoinWallet.OnTriggerEnter2D, FLUSSO 41), che pero' non e' autorevole:
        // non puo' decidere se la moneta e' gia' stata raccolta ne' quanto vale.
        // Si limita a nasconderla localmente per un feedback visivo immediato e
        // ritorna sempre 0, cosi' CoinWallet non accredita nulla lato client
        // (FLUSSO 44). Il vero conteggio arrivera' comunque a schermo tramite la
        // sincronizzazione automatica di totalCoins, decisa dal server qui sotto.
        if (!IsServer)
        {
            showCoin(false);
            return 0;
        }

        // [FLUSSO 43] Sul server invece il controllo e' autorevole: se la moneta
        // e' gia' stata raccolta (da questo o da un altro giocatore, in caso di
        // doppio trigger nello stesso frame) restituiamo 0 per evitare di
        // accreditare lo stesso valore due volte.
        if (alreadyCollected) return 0;
        else
        {
            alreadyCollected = true;
            // [FLUSSO 51] Notifichiamo il CoinSpawner (evento dichiarato al FLUSSO 46)
            // solo qui, dopo aver accertato lato server che la raccolta e' valida:
            // reagira' spostando la moneta e riabilitandola (FLUSSO 52-53).
            onCollected?.Invoke(this);
            return coinValue;
        }
    }

    private void Update()
    {
        // [FLUSSO 54] Il riposizionamento della moneta e' deciso solo dal server
        // (CoinSpawner.handleCoinCollected, FLUSSO 52) ma la nuova transform.position
        // arriva su ogni client tramite la normale sincronizzazione di rete: quando la
        // notiamo cambiata rispetto al frame precedente, e' il segnale che la moneta e'
        // stata rispawnata altrove, quindi annulliamo qui il nascondimento locale
        // fatto in collect() (FLUSSO 42), che riguardava solo lo SpriteRenderer e non
        // e' automaticamente sincronizzato dalla rete.
        if (previousPosition != transform.position)
        {
            showCoin(true);
        }

        previousPosition = transform.position;
    }

    // [FLUSSO 53] Chiamato dal CoinSpawner subito dopo aver ricollocato la moneta
    // (FLUSSO 52): azzera alreadyCollected cosi' che il controllo autorevole in
    // collect() (FLUSSO 43) torni a considerarla raccoglibile.
    public void Reset()
    {
        alreadyCollected = false;
    }
}
