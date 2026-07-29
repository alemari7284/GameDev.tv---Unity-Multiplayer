using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerAim : NetworkBehaviour
{
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform turretTransform;

    private void LateUpdate()
    {
        if (!IsOwner) return;
        Vector2 aimScreenPos = inputReader.AimPosition;
        Vector2 aimWorldPos = Camera.main.ScreenToWorldPoint(aimScreenPos);

        turretTransform.up = aimWorldPos - (Vector2)turretTransform.position;
    }
}
