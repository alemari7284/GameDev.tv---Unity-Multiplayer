using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Muove e ruota il corpo del tank in base all'input di movimento.
/// Legge l'input dall'<see cref="InputReader"/> (MoveEvent) e lo applica in due
/// punti diversi: la rotazione in Update (visiva, ad ogni frame) e la velocita'
/// in FixedUpdate (fisica, a intervallo fisso).
///
/// Essendo un NetworkBehaviour, agisce solo sul proprio tank (IsOwner); il
/// transform risultante viene poi replicato agli altri client dal
/// ClientNetworkTransform presente sul corpo (vedi FLUSSO 0-8 in quel file).
/// </summary>
public class PlayerMovement : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private Rigidbody2D rb;

    [Header("Settings")]
    [SerializeField] private float movementSpeed;
    // [FLUSSO 24b] turningRate e' la velocita' angolare MASSIMA del tank, in gradi al
    // secondo: e' quanti gradi ruota il corpo in un secondo quando l'input orizzontale e'
    // al massimo (previousMovementInput.x = 1 o -1). Con input parziale (es. 0.5 su
    // joystick analogico) la rotazione effettiva scala proporzionalmente (FLUSSO 26).
    // Va tarato in base a movementSpeed: un tank veloce con turningRate basso "sterza"
    // come un camion, uno lento con turningRate alto "gira su se stesso".
    [SerializeField] private float turningRate;
    private Vector2 previousMovementInput;

    // [FLUSSO 24] Start e OnDestroy avvengono troppo presto o troppo tardi nel ciclo di
    // vita di rete (l'oggetto non e' ancora/piu' "spawnato"), quindi usiamo
    // OnNetworkSpawn/OnNetworkDespawn per (dis)iscriverci a MoveEvent (InputReader,
    // FLUSSO 3). Come PlayerAiming (FLUSSO 8), solo il proprietario reagisce al proprio
    // input: sulle copie remote IsOwner e' false, la loro posizione arriva dalla rete.
    public override void OnNetworkSpawn()
    {
        // You don't need to call base, since there is nothing below this
        // base.OnNetworkSpawn();
        if (!IsOwner) return;
        inputReader.MoveEvent += handleMove;
    }

    // [FLUSSO 25] Ci disiscriviamo alla dismissione dell'oggetto, a specchio del
    // FLUSSO 24: evita che l'evento continui a chiamare un componente ormai distrutto.
    public override void OnNetworkDespawn()
    {
        // You don't need to call base, since there is nothing below this
        // base.OnNetworkDespawn();
        if (!IsOwner) return;
        inputReader.MoveEvent -= handleMove;
    }

    // Update is called once per frame
    private void Update()
    {
        if (!IsOwner) return;

        // [FLUSSO 26] La rotazione va in Update (non FixedUpdate) perche' e' puramente
        // visiva: aggiornarla ad ogni frame renderizzato la rende piu' fluida.
        // La formula scompone in tre fattori:
        // - previousMovementInput.x: quanto e in che verso l'input orizzontale (-1..1),
        //   aggiornato dal FLUSSO 28;
        // - -turningRate: i gradi/secondo massimi (FLUSSO 24b), col segno invertito
        //   cosi' che input a destra (x positivo) produca rotazione oraria (Z negativo
        //   in Unity 2D, dove Z positivo e' antiorario);
        // - Time.deltaTime: converte i gradi/secondo in gradi-per-QUESTO-frame, perche'
        //   Update non gira a intervalli fissi: senza questo fattore la velocita' di
        //   rotazione dipenderebbe dal framerate (piu' fps = piu' rotazione al secondo).
        float zRotation = previousMovementInput.x * -turningRate * Time.deltaTime;
        bodyTransform.Rotate(0, 0, zRotation);
    }

    // FixedUpdate is called at a fixed interval and is independent of frame rate.
    // It's not called every frame of the normal engine, it's called every
    // frame of the physics engine.
    private void FixedUpdate()
    {
        if (!IsOwner) return;

        // [FLUSSO 27] Il movimento vero e proprio passa dal Rigidbody2D, quindi va nel
        // FixedUpdate (fisica). Usiamo bodyTransform.up come direzione anziche' gli assi
        // del mondo, cosi' il tank si muove sempre "in avanti" rispetto a come e' ruotato
        // in quel momento (FLUSSO 26), non lungo X/Y assoluti.
        // no need to multiply by Time.deltaTime here, since FixedUpdate is already called at a fixed interval
        rb.velocity = (Vector2)bodyTransform.up * previousMovementInput.y * movementSpeed;
    }


    // [FLUSSO 28] Callback collegata al FLUSSO 24: aggiorna solo l'input memorizzato, che
    // Update (FLUSSO 26) e FixedUpdate (FLUSSO 27) leggono ogni frame per calcolare
    // rispettivamente rotazione e velocita'.
    private void handleMove(Vector2 movementInput)
    {
        previousMovementInput = movementInput;
        Debug.Log("movement input: " + previousMovementInput);
    }
}
