using System;
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
            // Il trigger 2D avviene in locale su OGNI client che
            // possiede fisicamente questo collider (non c'e' un controllo IsOwner
            // perche' qui non serve distinguere proprietario da altri: il wallet
            // che deve reagire e' sempre quello del giocatore che tocca la moneta).
            // Chiamiamo comunque collect() ovunque: sul server e' autorevole,
            // sui client serve solo a dare un feedback visivo immediato (vedi
            // RespawningCoin.collect).
            int coinValue = coin.collect();

            // Solo il server puo' aggiornare totalCoins (NetworkVariable
            // scrivibile di default solo lato server): usciamo qui sui client, dove
            // coinValue e' comunque sempre 0 (ramo client di RespawningCoin.collect), per non tentare una
            // scrittura non autorizzata.
            if (!IsServer) return;
            totalCoins.Value += coinValue;
        }

    }

    // Chiamato solo dal server (ProjectileLauncher.PrimaryFireServerRpc)
    // dopo aver gia' verificato che il giocatore abbia abbastanza
    // monete: sottrae il costo dello sparo dalla stessa NetworkVariable
    // incrementata qui sopra da OnTriggerEnter2D.
    public void spendCoins(int costToFire)
    {
        totalCoins.Value -= costToFire;
    }
}
