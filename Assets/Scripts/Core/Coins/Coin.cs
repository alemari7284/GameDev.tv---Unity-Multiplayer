using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Base astratta per le monete raccolte dal giocatore (vedi
/// CoinWallet.OnTriggerEnter2D). Non contiene controlli IsServer/IsClient propri: li lascia
/// alle sottoclassi (es. RespawningCoin), perche' il
/// comportamento autorevole di raccolta puo' cambiare a seconda del tipo di
/// moneta (es. monete che rispawnano vs monete singole che scompaiono
/// definitivamente).
/// </summary>
public abstract class Coin : NetworkBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    protected int coinValue = 10;
    protected bool alreadyCollected;

    // Il valore di ritorno e' significativo solo se chi lo calcola e'
    // il server (l'unico autorevole sulla raccolta, vedi
    // RespawningCoin.collect): CoinWallet lo usa per aggiornare totalCoins solo li'
    // (nel ramo server), quindi le implementazioni devono restituire 0 quando eseguite
    // su un client.
    public abstract int collect();

    public void setValue(int value) => coinValue = value;

    protected void showCoin(bool show) => spriteRenderer.enabled = show;

}
