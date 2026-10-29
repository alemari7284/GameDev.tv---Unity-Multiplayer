using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class Leaderboard : NetworkBehaviour
{
    [SerializeField] private Transform leaderboardEntityHolder;
    [SerializeField] private LeaderboardEntityDisplay leaderboardEntityPrefab;

    private NetworkList<LeaderboardEntityState> leaderboardEntities;
    private List<LeaderboardEntityDisplay> entityDisplays = new List<LeaderboardEntityDisplay>();

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
                if (!entityDisplays.Any(x => x.id == changeEvent.Value.clientId))
                {
                    LeaderboardEntityDisplay leaderboardEntity =
                        Instantiate(leaderboardEntityPrefab, leaderboardEntityHolder);
                    leaderboardEntity.init(
                        changeEvent.Value.clientId,
                        changeEvent.Value.playerName,
                        changeEvent.Value.coins);
                    entityDisplays.Add(leaderboardEntity);
                }
                break;
            case NetworkListEvent<LeaderboardEntityState>.EventType.Remove:
                LeaderboardEntityDisplay displayToRemove =
                    entityDisplays.FirstOrDefault(x => x.id == changeEvent.Value.clientId);
                if (displayToRemove != null)
                {
                    displayToRemove.transform.SetParent(null);
                    Destroy(displayToRemove.gameObject);
                    entityDisplays.Remove(displayToRemove);
                }
                break;
            case NetworkListEvent<LeaderboardEntityState>.EventType.Value:
                LeaderboardEntityDisplay displayToUpdate =
                    entityDisplays.FirstOrDefault(x => x.id == changeEvent.Value.clientId);
                if (displayToUpdate != null)
                {
                    displayToUpdate.updateCoins(changeEvent.Value.coins);
                }
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
        player.wallet.totalCoins.OnValueChanged +=
            (oldCoins, newCoins) => handleCoinsChanged(player.OwnerClientId, newCoins);
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

        player.wallet.totalCoins.OnValueChanged -=
        (oldCoins, newCoins) => handleCoinsChanged(player.OwnerClientId, newCoins);
    }

    private void handleCoinsChanged(ulong clientId, int newCoins)
    {
        for (int i = 0; i < leaderboardEntities.Count; i++)
        {
            if (leaderboardEntities[i].clientId != clientId) continue;

            leaderboardEntities[i] = new LeaderboardEntityState
            {
                clientId = leaderboardEntities[i].clientId,
                playerName = leaderboardEntities[i].playerName,
                coins = newCoins
            };
            return;
        }
    }
}
