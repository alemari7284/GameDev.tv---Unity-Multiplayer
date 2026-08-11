using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Lifetime : MonoBehaviour
{
    [SerializeField] private float lifetime;
    // Start is called before the first frame update
    void Start()
    {
        // [FLUSSO 23] Rete di sicurezza per i proiettili (reali e dummy): se non
        // colpiscono nulla entro "lifetime" secondi, si autodistruggono comunque,
        // evitando che restino in scena all'infinito sprecando memoria.
        Destroy(gameObject, lifetime);
    }
}
