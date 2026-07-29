using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
[CreateAssetMenu(fileName = "InputReader", menuName = "Input/Input Reader")]
public class InputReader : ScriptableObject, IPlayerActions
{
    private Controls controls;

    /// <summary>Sollevato quando il fuoco primario viene premuto (true) o rilasciato (false).</summary>
    public event Action<bool> PrimaryFireEvent;

    /// <summary>Sollevato ad ogni variazione dell'input di movimento, con il vettore direzione.</summary>
    public event Action<Vector2> MoveEvent;


    /// <summary>
    /// Crea (se necessario) la mappa di controlli, registra questo oggetto come
    /// gestore delle callback dell'action map "Player" e abilita l'input.
    /// </summary>
    private void OnEnable()
    {
        if (controls == null)
        {
            controls = new Controls();
            controls.Player.SetCallbacks(this);
        }

        controls.Enable();
    }

    /// <summary>
    /// Callback dell'Input System per l'azione di movimento: propaga il valore
    /// Vector2 letto tramite <see cref="MoveEvent"/>.
    /// </summary>
    public void OnMove(InputAction.CallbackContext context)
    {
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
        if (context.performed)
        {
            PrimaryFireEvent?.Invoke(true);
        }
        else if (context.canceled)
        {
            PrimaryFireEvent?.Invoke(false);
        }
    }
}
