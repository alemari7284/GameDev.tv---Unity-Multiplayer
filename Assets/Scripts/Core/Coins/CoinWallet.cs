using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Tiene il conteggio delle monete raccolte da un giocatore in una
/// NetworkVariable, sincronizzata automaticamente da Netcode a tutti i client.
/// </summary>
public class CoinWallet : NetworkBehaviour
{
    public NetworkVariable<int> totalCoins = new NetworkVariable<int>();

    private void OnTriggerEnter2D(Collider2D collision)
    {

        if (collision.TryGetComponent<Coin>(out Coin coin))
        {
            // [FLUSSO 41] Il trigger 2D avviene in locale su OGNI client che
            // possiede fisicamente questo collider (non c'e' un controllo IsOwner
            // perche' qui non serve distinguere proprietario da altri: il wallet
            // che deve reagire e' sempre quello del giocatore che tocca la moneta).
            // Chiamiamo comunque collect() ovunque: sul server e' autorevole,
            // sui client serve solo a dare un feedback visivo immediato (vedi
            // RespawningCoin.collect, FLUSSO 42-43).
            int coinValue = coin.collect();

            // [FLUSSO 44] Solo il server puo' aggiornare totalCoins (NetworkVariable
            // scrivibile di default solo lato server): usciamo qui sui client, dove
            // coinValue e' comunque sempre 0 (FLUSSO 42), per non tentare una
            // scrittura non autorizzata.
            if (!IsServer) return;
            totalCoins.Value += coinValue;
        }

    }
}
