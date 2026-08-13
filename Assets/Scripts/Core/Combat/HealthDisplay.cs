using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// [FLUSSO 35] Componente lato UI: ascolta i cambiamenti di Health.currentHealth
// (NetworkVariable, sincronizzata automaticamente da Netcode) e aggiorna la barra
// vita a schermo. Va sull'oggetto UI, non sul giocatore.
public class HealthDisplay : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private Image healthBarImage;

    public override void OnNetworkSpawn()
    {
        // [FLUSSO 36] A differenza di ProjectileLauncher/PlayerAiming (che reagiscono
        // solo sul proprietario, IsOwner), qui ogni client deve vedere la barra vita
        // aggiornata: il controllo e' quindi IsClient, non IsOwner ne' IsServer, dato
        // che questo componente serve solo alla UI locale.
        if (!IsClient) return;  // Only clients need to update the health display

        health.currentHealth.OnValueChanged += handleHealthChanged;
        handleHealthChanged(0, health.currentHealth.Value); // Initialize the health bar display
    }

    public override void OnNetworkDespawn()
    {
        // [FLUSSO 37] Ci disiscriviamo alla dismissione dell'oggetto, a specchio del
        // FLUSSO 36: evita che l'evento continui a chiamare un componente ormai distrutto.
        if (!IsClient) return;
        health.currentHealth.OnValueChanged -= handleHealthChanged;
    }

    private void handleHealthChanged(int oldHealth, int newHealth)
    {
        // [FLUSSO 38] Callback collegata al FLUSSO 36: il fillAmount di un'Image di tipo
        // "Filled" va da 0 a 1, quindi normalizziamo la vita corrente sul massimo.
        healthBarImage.fillAmount = (float)newHealth / health.MaxHealth;
    }
}
