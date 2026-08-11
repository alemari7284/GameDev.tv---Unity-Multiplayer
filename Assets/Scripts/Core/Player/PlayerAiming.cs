using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Orienta la torretta del tank verso il puntatore del giocatore.
/// Legge la posizione di mira dall'<see cref="InputReader"/> e ruota il
/// <see cref="turretTransform"/> in modo che punti sempre verso il mouse.
///
/// Essendo un NetworkBehaviour, agisce solo sul proprio tank (IsOwner);
/// la rotazione risultante viene poi replicata agli altri client dal
/// ClientNetworkTransform presente sul TurretPivot (sync rotazione Z).
/// </summary>
public class PlayerAiming : NetworkBehaviour
{
    // [FLUSSO 8] Riferimenti impostati nell'Inspector:
    // - inputReader: lo stesso ScriptableObject usato per il movimento, da cui leggiamo 
    // AimPosition.
    // - turretTransform: il transform del TurretPivot che vogliamo far ruotare.
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform turretTransform;

    // [FLUSSO 9] Usiamo LateUpdate (e non Update) di proposito: la mira va calcolata
    // DOPO che il corpo si e' mosso/ruotato in Update e FixedUpdate, cosi' la torretta
    // punta in base alla posizione piu' aggiornata del tank ed evita scatti di un frame.
    private void LateUpdate()
    {
        // [FLUSSO 10] Solo il proprietario decide dove punta la propria torretta.
        // Sulle copie remote IsOwner e' false: la loro rotazione arriva dalla rete.
        if (!IsOwner) return;

        // [FLUSSO 11] La mira letta dall'InputReader e' in coordinate SCHERMO (pixel).
        Vector2 aimScreenPos = inputReader.AimPosition;

        // [FLUSSO 12] Convertiamo quei pixel in coordinate MONDO, cosi' possiamo
        // confrontarli con la posizione della torretta nella scena.
        Vector2 aimWorldPos = Camera.main.ScreenToWorldPoint(aimScreenPos);

        // [FLUSSO 13] Orientiamo la torretta: impostando il suo asse "up" (l'alto locale)
        // sul vettore che va dalla torretta al punto mirato, la torretta "guarda" il mouse.
        // (turretTransform.up viene usato perche' lo sprite del cannone punta verso l'alto).
        turretTransform.up = aimWorldPos - (Vector2)turretTransform.position;
    }
}
