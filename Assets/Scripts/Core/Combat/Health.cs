using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Health : NetworkBehaviour
{
    [field: SerializeField] public int MaxHealth { get; private set; } = 100;
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>();

    private bool isDead;

    public Action<Health> OnDie;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        currentHealth.Value = MaxHealth;
    }

    public void takeDamage(int damageValue)
    {
        modifyHealth(-damageValue);
    }

    public void restoreHealth(int healValue)
    {
        modifyHealth(healValue);
    }

    private void modifyHealth(int value)
    {
        if (isDead) return;

        int newHealth = currentHealth.Value + value;
        currentHealth.Value = Mathf.Clamp(newHealth, 0, MaxHealth);

        if (currentHealth.Value == 0)
        {
            OnDie?.Invoke(this);
            isDead = true;
        }
    }
}
