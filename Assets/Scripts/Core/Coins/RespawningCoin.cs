using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawningCoin : Coin
{
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
            return coinValue;
        }
    }
}
