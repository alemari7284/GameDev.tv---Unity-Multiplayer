using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// [FLUSSO 48] Genera all'avvio un numero fisso di RespawningCoin (FLUSSO 42-43)
// in punti casuali della mappa e, quando una di esse viene raccolta (FLUSSO 51),
// la ricolloca e la riabilita invece di distruggerla e ricrearla.
public class CoinSpawner : NetworkBehaviour
{
    [SerializeField] private RespawningCoin coinPrefab;
    [SerializeField] private int maxCoins = 50;
    [SerializeField] private int coinValue = 10;
    [SerializeField] private Vector2 xSpawnRange;
    [SerializeField] private Vector2 ySpawnRange;
    [SerializeField] private LayerMask layerMask;
    private Collider2D[] coinBuffer = new Collider2D[1];
    private float coinRadius;

    public override void OnNetworkSpawn()
    {
        // [FLUSSO 49] Come altri setup autorevoli (es. Health.OnNetworkSpawn,
        // FLUSSO 39), lo spawn delle monete lo decide solo il server: i client
        // le vedranno comparire tramite la normale sincronizzazione dei
        // NetworkObject.
        if (!IsServer) return;

        coinRadius = coinPrefab.GetComponent<CircleCollider2D>().radius;

        for (int i = 0; i < maxCoins; i++)
        {
            spawnCoin();
        }
    }

    private void spawnCoin()
    {
        RespawningCoin coinInstance = Instantiate(
            coinPrefab,
            getSpawnPoint(),
            Quaternion.identity);

        coinInstance.setValue(coinValue);
        coinInstance.GetComponent<NetworkObject>().Spawn();

        // [FLUSSO 50] Ci iscriviamo all'evento onCollected (dichiarato al FLUSSO 46)
        // di questa specifica istanza, cosi' sapremo quando ricollocarla
        // (FLUSSO 51-52).
        coinInstance.onCollected += handleCoinCollected;
    }

    private void handleCoinCollected(RespawningCoin coin)
    {
        // [FLUSSO 52] Callback collegata al FLUSSO 51: gira solo sul server (e'
        // l'unico che riceve l'evento, dato che onCollected viene sollevato
        // esclusivamente nel ramo server di collect(), FLUSSO 43). Spostiamo la
        // moneta invece di distruggerla: la nuova posizione si propaga ai client
        // da sola tramite NetworkTransform, dove RespawningCoin.Update (FLUSSO 54)
        // la riabilita a video. Il Reset() finale (FLUSSO 53) e' cio' che permette
        // al controllo autorevole in collect() di considerarla di nuovo raccoglibile.
        coin.transform.position = getSpawnPoint();
        coin.Reset();
    }

    private Vector2 getSpawnPoint()
    {
        float x = 0;
        float y = 0;
        while (true)
        {
            x = Random.Range(xSpawnRange.x, xSpawnRange.y);
            y = Random.Range(ySpawnRange.x, ySpawnRange.y);
            Vector2 spawnPoint = new Vector2(x, y);
            int numColliders = Physics2D.OverlapCircleNonAlloc(spawnPoint, coinRadius, coinBuffer, layerMask);
            if (numColliders == 0)
                return spawnPoint;
        }
    }

}
