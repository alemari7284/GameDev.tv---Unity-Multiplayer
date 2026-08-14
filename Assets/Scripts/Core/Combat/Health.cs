using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Gestisce i punti vita di un'entita' (player, dummy, ecc.) tramite una
/// NetworkVariable, sincronizzata automaticamente da Netcode a tutti i client.
///
/// Il valore e' autorevole solo sul server: i client la leggono soltanto (vedi
/// HealthDisplay, FLUSSO 35-38, che ne ascolta i cambiamenti per aggiornare la
/// barra vita a schermo).
/// </summary>
public class Health : NetworkBehaviour
{
    [field: SerializeField] public int MaxHealth { get; private set; } = 100;
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>();

    private bool isDead;

    public Action<Health> OnDie;

    public override void OnNetworkSpawn()
    {
        // [FLUSSO 39] Solo il server inizializza la vita: e' lui l'unico
        // autorevole su "quanto vale" la vita di partenza. Se lo facesse anche
        // ogni client, la NetworkVariable riceverebbe scritture concorrenti e non
        // autorizzate (di default scrivibile solo dal server) invece che un
        // singolo valore coerente propagato dalla sync automatica.
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
        // [FLUSSO 40] Non c'e' un controllo IsServer qui perche' non serve: il
        // metodo va chiamato solo da codice che gira gia' sul server (es.
        // DealDamageOnContact, FLUSSO 34, eseguito esclusivamente li'). Se venisse
        // invocato per errore da un client, la scrittura su currentHealth.Value
        // qui sotto verrebbe comunque rifiutata da Netcode (permesso di scrittura
        // server-only di default).
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
