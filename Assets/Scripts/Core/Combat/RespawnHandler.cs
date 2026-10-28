using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RespawnHandler : NetworkBehaviour
{
    [SerializeField] private NetworkObject playerPrefab;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        TankPlayer[] players = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
        foreach (TankPlayer player in players)
        {
            handlePlayerSpawned(player);
        }

        TankPlayer.OnPlayerSpawned += handlePlayerSpawned;
        TankPlayer.OnPlayerDespawned += handlePlayerDespawned;

    }

    private void handlePlayerSpawned(TankPlayer player)
    {
        player.Health.OnDie += (health) => handlePlayerDie(player);
    }


    private void handlePlayerDespawned(TankPlayer player)
    {
        player.Health.OnDie -= (health) => handlePlayerDie(player);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        TankPlayer.OnPlayerSpawned -= handlePlayerSpawned;
        TankPlayer.OnPlayerDespawned -= handlePlayerDespawned;
    }

    private void handlePlayerDie(TankPlayer player)
    {
        Destroy(player.gameObject);

        StartCoroutine(respawnPlayer(player.OwnerClientId));
    }

    private IEnumerator respawnPlayer(ulong ownerClientId)
    {
        yield return null;

        NetworkObject playerInstance = Instantiate
            (playerPrefab, SpawnPoint.getRandomSpawnPos(), Quaternion.identity);

        playerInstance.SpawnAsPlayerObject(ownerClientId);


    }
}
