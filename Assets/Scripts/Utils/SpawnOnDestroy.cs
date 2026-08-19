using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnOnDestroy : MonoBehaviour
{
    [SerializeField] private GameObject prefab;

    private void OnDestroy()
    {
        // [FLUSSO 55] Componente puramente estetico: si aggancia al ciclo di vita
        // standard di Unity (OnDestroy), non a quello di rete, quindi non servono
        // controlli IsServer/IsOwner. Va sul proiettile "dummy"
        // (clientProjectilePrefab, vedi ProjectileLauncher FLUSSO 14): quando il
        // dummy viene distrutto - per contatto o per timeout (Lifetime, FLUSSO 23) -
        // lascia al suo posto un effetto locale (es. una nuvola di polvere), creato
        // in modo indipendente da ogni client, senza alcuno Spawn() di rete.
        Instantiate(prefab, transform.position, Quaternion.identity);
    }
}
