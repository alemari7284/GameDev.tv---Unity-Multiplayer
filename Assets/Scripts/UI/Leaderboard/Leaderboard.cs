using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class Leaderboard : NetworkBehaviour
{
    [SerializeField] private Transform leaderboardEntityHolder;
    [SerializeField] private LeaderboardEntityDisplay leaderboardEntityPrefab;

    private NetworkList<LeaderboardEntityState> leaderboardEntities;

    private void Awake()
    {
        leaderboardEntities = new NetworkList<LeaderboardEntityState>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            leaderboardEntities.OnListChanged += handleLeaderboardEntitiesChanged;
            foreach (LeaderboardEntityState entity in leaderboardEntities)
            {
                handleLeaderboardEntitiesChanged(new NetworkListEvent<LeaderboardEntityState>
                {
                    Type = NetworkListEvent<LeaderboardEntityState>.EventType.Add,
                    Value = entity
                });
            }
        }

        if (IsServer)
        {
            TankPlayer[] players = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
            foreach (TankPlayer player in players)
            {
                handlePlayerSpawned(player);
            }
            TankPlayer.OnPlayerSpawned += handlePlayerSpawned;
            TankPlayer.OnPlayerDespawned += handlePlayerDespawned;
        }

    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            leaderboardEntities.OnListChanged -= handleLeaderboardEntitiesChanged;
        }

        if (IsServer)
        {
            TankPlayer[] players = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);
            foreach (TankPlayer player in players)
            {
                handlePlayerDespawned(player);
            }
            TankPlayer.OnPlayerSpawned -= handlePlayerSpawned;
            TankPlayer.OnPlayerDespawned -= handlePlayerDespawned;
        }
    }

    private void handleLeaderboardEntitiesChanged(NetworkListEvent<LeaderboardEntityState> changeEvent)
    {
        switch (changeEvent.Type)
        {
            case NetworkListEvent<LeaderboardEntityState>.EventType.Add:
                Instantiate(leaderboardEntityPrefab, leaderboardEntityHolder);
                break;
            case NetworkListEvent<LeaderboardEntityState>.EventType.Remove:
                break;
        }
    }

    private void handlePlayerSpawned(TankPlayer player)
    {
        leaderboardEntities.Add(new LeaderboardEntityState
        {
            clientId = player.OwnerClientId,
            playerName = player.playerName.Value,
            coins = 0
        });
    }

    private void handlePlayerDespawned(TankPlayer player)
    {
        if (leaderboardEntities == null) return;

        foreach (LeaderboardEntityState entity in leaderboardEntities)
        {
            if (entity.clientId != player.OwnerClientId) continue;

            leaderboardEntities.Remove(entity);
            break;
        }
    }
}
