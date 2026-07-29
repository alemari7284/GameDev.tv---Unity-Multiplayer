using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private Rigidbody2D rb;

    [Header("Settings")]
    [SerializeField] private float movementSpeed;
    [SerializeField] private float turningRate;
    private Vector2 previousMovementInput;

    // Start and OnDestroy happen too early or too late in the network lifecycle
    // so we use OnNetworkSpawn and OnNetworkDespawn instead to register/unregister events
    public override void OnNetworkSpawn()
    {
        // You don't need to call base, since there is nothing below this
        // base.OnNetworkSpawn();
        if (!IsOwner) return;
        inputReader.MoveEvent += handleMove;
    }

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

        float zRotation = previousMovementInput.x * -turningRate * Time.deltaTime;
        bodyTransform.Rotate(0, 0, zRotation);
    }

    // FixedUpdate is called at a fixed interval and is independent of frame rate. 
    // It's not called every frame of the normal engine, it's called every
    // frame of the physics engine.
    private void FixedUpdate()
    {
        if (!IsOwner) return;

        // no need to multiply by Time.deltaTime here, since FixedUpdate is already called at a fixed interval
        rb.velocity = (Vector2)bodyTransform.up * previousMovementInput.y * movementSpeed;
    }


    private void handleMove(Vector2 movementInput)
    {
        previousMovementInput = movementInput;
        Debug.Log("movement input: " + previousMovementInput);
    }
}
