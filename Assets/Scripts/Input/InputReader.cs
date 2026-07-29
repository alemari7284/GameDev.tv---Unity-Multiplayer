using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
// [FLUSSO 0] "using static Controls;" ci permette di scrivere IPlayerActions e Controls
// senza doverli prefissare (es. Controls.IPlayerActions). "Controls" è la classe
// GENERATA AUTOMATICAMENTE dall'asset Controls.inputactions: non la scriviamo noi.
using static Controls;

/// <summary>
/// ScriptableObject che centralizza la lettura dell'input del giocatore tramite
/// il nuovo Input System ed espone gli input come eventi C#. In questo modo la
/// logica di gioco (movimento, fuoco) resta disaccoppiata dal dispositivo di
/// input e da come i tasti sono mappati.
///
/// Essendo un asset condiviso, puo' essere referenziato da piu' componenti
/// (es. movimento del corpo, rotazione della torretta, sparo) evitando che
/// ognuno debba istanziare la propria mappa di controlli.
/// </summary>
// [FLUSSO 1] Implementando "IPlayerActions" (interfaccia definita in Controls.cs)
// firmiamo un "contratto": ci impegniamo a fornire i metodi OnMove e OnPrimaryFire.
// Sara' l'Input System a chiamarli quando l'utente preme i tasti.
[CreateAssetMenu(fileName = "InputReader", menuName = "Input/Input Reader")]
public class InputReader : ScriptableObject, IPlayerActions
{
    // [FLUSSO 2] "controls" è la nostra istanza della classe generata: rappresenta
    // in codice l'intero asset Controls.inputactions (mappe, azioni e binding).
    private Controls controls;

    /// <summary>Sollevato quando il fuoco primario viene premuto (true) o rilasciato (false).</summary>
    // [FLUSSO 3] Questi due eventi sono il "megafono" verso il resto del gioco: noi
    // leggiamo l'input grezzo e lo ri-emettiamo come evento, cosi' chi ascolta
    // (es. PlayerMovement) non deve sapere nulla dell'Input System.
    public event Action<bool> PrimaryFireEvent;

    /// <summary>Sollevato ad ogni variazione dell'input di movimento, con il vettore direzione.</summary>
    public event Action<Vector2> MoveEvent;


    /// <summary>
    /// Crea (se necessario) la mappa di controlli, registra questo oggetto come
    /// gestore delle callback dell'action map "Player" e abilita l'input.
    /// </summary>
    private void OnEnable()
    {
        // [FLUSSO 4] Alla prima abilitazione istanziamo l'oggetto Controls.
        if (controls == null)
        {
            controls = new Controls();

            // [FLUSSO 5] Colleghiamo l'input al nostro codice.
            // "controls.Player" = la action map "Player" (contiene Move e PrimaryFire).
            // SetCallbacks(this) aggancia i nostri OnMove/OnPrimaryFire a TUTTE le fasi
            // (started/performed/canceled) di quelle azioni. Da qui in poi, quando premi
            // un tasto, l'Input System chiama i nostri metodi qui sotto.
            controls.Player.SetCallbacks(this);
        }

        // [FLUSSO 6] Attiviamo la lettura dell'input: senza Enable() le callback non scattano.
        controls.Enable();
    }

    /// <summary>
    /// Callback dell'Input System per l'azione di movimento: propaga il valore
    /// Vector2 letto tramite <see cref="MoveEvent"/>.
    /// </summary>
    public void OnMove(InputAction.CallbackContext context)
    {
        // [FLUSSO 7a] Chiamato dall'Input System a ogni cambio dell'azione "Move".
        // "context" è la "busta" con le info sull'evento: qui ci serve solo il valore,
        // quindi leggiamo la direzione (WASD -> Vector2) e la rilanciamo come evento.
        MoveEvent?.Invoke(context.ReadValue<Vector2>());
    }

    /// <summary>
    /// Callback dell'Input System per il fuoco primario: notifica l'inizio
    /// (performed) e la fine (canceled) dello sparo tramite <see cref="PrimaryFireEvent"/>.
    /// Per dare feedback immediato al giocatore, lo sparo potra' generare lato client
    /// un proiettile "dummy" istantaneo, mentre il server gestira' poco dopo il
    /// proiettile reale che infligge danno.
    /// </summary>
    public void OnPrimaryFire(InputAction.CallbackContext context)
    {
        // [FLUSSO 7b] Qui, a differenza del movimento, ci interessa la FASE dell'input.
        // context.performed = tasto premuto -> iniziamo a sparare (true).
        if (context.performed)
        {
            PrimaryFireEvent?.Invoke(true);
        }
        // context.canceled = tasto rilasciato -> smettiamo di sparare (false).
        else if (context.canceled)
        {
            PrimaryFireEvent?.Invoke(false);
        }
    }
}
