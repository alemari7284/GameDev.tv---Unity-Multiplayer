using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Genera all'avvio un numero fisso di RespawningCoin
// in punti casuali della mappa e, quando una di esse viene raccolta (evento onCollected),
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
        // Come altri setup autorevoli (es. Health.OnNetworkSpawn),
        // lo spawn delle monete lo decide solo il server: i client
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

        // Ci iscriviamo all'evento onCollected (dichiarato in RespawningCoin)
        // di questa specifica istanza, cosi' sapremo quando ricollocarla
        // (handleCoinCollected, qui sotto).
        coinInstance.onCollected += handleCoinCollected;
    }

    private void handleCoinCollected(RespawningCoin coin)
    {
        // Callback collegata a onCollected: gira solo sul server (e'
        // l'unico che riceve l'evento, dato che onCollected viene sollevato
        // esclusivamente nel ramo server di RespawningCoin.collect()). Spostiamo la
        // moneta invece di distruggerla: la nuova posizione si propaga ai client
        // da sola tramite NetworkTransform, dove RespawningCoin.Update
        // la riabilita a video. Il Reset() finale e' cio' che permette
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
