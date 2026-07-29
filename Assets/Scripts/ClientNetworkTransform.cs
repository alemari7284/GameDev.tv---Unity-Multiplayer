using System.Collections;
using System.Collections.Generic;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Variante client-authoritative del <see cref="NetworkTransform"/> di Netcode.
///
/// Il NetworkTransform standard e' server-authoritative: solo il server puo'
/// modificare la posizione degli oggetti e i cambiamenti locali di un client
/// vengono ignorati. Questo garantisce sicurezza contro i cheat, ma introduce
/// latenza (ogni azione richiede un round-trip di rete), rendendo il movimento
/// poco reattivo.
///
/// Per mantenere i controlli fluidi e senza input delay, diamo al client
/// l'authority sul proprio movimento: il proprietario (owner) invia direttamente
/// il proprio transform al server, che poi lo sincronizza agli altri client.
/// Lo svantaggio e' la vulnerabilita' a cheat come il teleport, accettabile in
/// cambio di zero lag sui controlli per i giocatori legittimi.
///
/// Va assegnato ai prefab Player, Treads e TurretPivot al posto del
/// NetworkTransform di default, configurando gli assi da sincronizzare
/// (posizione per il Player, rotazione su Z per torretta e cingoli).
/// </summary>
public class ClientNetworkTransform : NetworkTransform
{
    /// <summary>
    /// Allo spawn in rete abilita la scrittura del transform solo se questa
    /// istanza e' il proprietario dell'oggetto, cosi' che ogni client controlli
    /// esclusivamente il proprio tank.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CanCommitToTransform = IsOwner;
    }

    /// <summary>
    /// Ogni frame ri-verifica la proprieta' e, se siamo connessi e possediamo
    /// l'oggetto, invia al server la nostra posizione corrente insieme al tempo
    /// locale. Il timestamp e' necessario al server per interpolare il movimento
    /// in modo fluido sugli altri client.
    /// </summary>
    protected override void Update()
    {
        CanCommitToTransform = IsOwner;
        base.Update();
        if (NetworkManager != null)
        {
            if (NetworkManager.IsConnectedClient || NetworkManager.IsListening)
            {
                if (CanCommitToTransform)
                {
                    TryCommitTransformToServer(transform, NetworkManager.LocalTime.Time);
                }
            }
        }
    }

    /// <summary>
    /// Disattiva la server authority di base del NetworkTransform, rendendo
    /// questo componente client-authoritative.
    /// </summary>
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
