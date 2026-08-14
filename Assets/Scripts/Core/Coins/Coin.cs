using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Base astratta per le monete raccolte dal giocatore (vedi CoinWallet,
/// FLUSSO 41-44). Non contiene controlli IsServer/IsClient propri: li lascia
/// alle sottoclassi (es. RespawningCoin, FLUSSO 42-43), perche' il
/// comportamento autorevole di raccolta puo' cambiare a seconda del tipo di
/// moneta (es. monete che rispawnano vs monete singole che scompaiono
/// definitivamente).
/// </summary>
public abstract class Coin : NetworkBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    protected int coinValue = 10;
    protected bool alreadyCollected;

    // [FLUSSO 45] Il valore di ritorno e' significativo solo se chi lo calcola e'
    // il server (l'unico autorevole sulla raccolta, vedi RespawningCoin.collect,
    // FLUSSO 42-43): CoinWallet lo usa per aggiornare totalCoins solo li'
    // (FLUSSO 44), quindi le implementazioni devono restituire 0 quando eseguite
    // su un client.
    public abstract int collect();

    public void setValue(int value) => coinValue = value;

    protected void showCoin(bool show) => spriteRenderer.enabled = show;

}
