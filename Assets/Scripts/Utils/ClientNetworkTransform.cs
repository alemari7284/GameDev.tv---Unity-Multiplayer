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
    /// Disattiva la server authority di base del NetworkTransform, rendendo
    /// questo componente client-authoritative.
    /// </summary>
    protected override bool OnIsServerAuthoritative()
    {
        // [FLUSSO 0] Netcode chiama questo metodo per sapere "chi comanda" sul transform.
        // Ritornando false dichiariamo che l'authority NON e' del server ma del client owner.
        // E' questa riga a trasformare un normale NetworkTransform in uno client-authoritative.
        return false;
    }

    /// <summary>
    /// Allo spawn in rete abilita la scrittura del transform solo se questa
    /// istanza e' il proprietario dell'oggetto, cosi' che ogni client controlli
    /// esclusivamente il proprio tank.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // [FLUSSO 1] base.OnNetworkSpawn() esegue la logica di setup del NetworkTransform
        // di Netcode: va chiamata per prima per non rompere il comportamento standard.
        base.OnNetworkSpawn();

        // [FLUSSO 2] CanCommitToTransform = "ho il diritto di scrivere/inviare il transform?".
        // Lo impostiamo pari a IsOwner: solo il proprietario dell'oggetto potra' farlo.
        // Sul tank degli altri giocatori IsOwner e' false -> restera' in sola ricezione.
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
        // [FLUSSO 3] Ricalcoliamo il diritto di scrittura a OGNI frame (non solo allo spawn):
        // e' robusto anche se la proprieta' dell'oggetto cambiasse a runtime.
        CanCommitToTransform = IsOwner;

        // [FLUSSO 4] base.Update() e' il motore del NetworkTransform:
        // - se siamo l'owner, applica/committa lo stato locale;
        // - se NON lo siamo, INTERPOLA verso i valori ricevuti dalla rete (movimento fluido).
        base.Update();

        // [FLUSSO 5] Guard di sicurezza: prima dello spawn o fuori da una sessione,
        // NetworkManager puo' essere null. Evita un NullReferenceException piu' sotto.
        if (NetworkManager != null)
        {
            // [FLUSSO 6] Inviamo dati solo se siamo davvero "in rete":
            // IsConnectedClient = client connesso a un host;
            // IsListening       = server/host attivo.
            if (NetworkManager.IsConnectedClient || NetworkManager.IsListening)
            {
                // [FLUSSO 7] Ultimo filtro: solo il proprietario prosegue (vedi FLUSSO 3).
                // Le copie remote si fermano qui e si limitano a interpolare (FLUSSO 4).
                if (CanCommitToTransform)
                {
                    // [FLUSSO 8] Cuore della client authority: mando il MIO transform al server
                    // insieme al tempo di rete locale (LocalTime.Time). Quel timestamp serve
                    // agli altri client per interpolare correttamente nel tempo cio' che ricevono.
                    TryCommitTransformToServer(transform, NetworkManager.LocalTime.Time);
                }
            }
        }
    }
}
