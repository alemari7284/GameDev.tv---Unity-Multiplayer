using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroySelfOnContact : MonoBehaviour
{

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // [FLUSSO 22] Il proiettile "vero" generato dal server (FLUSSO 18) si
        // autodistrugge al primo contatto (es. con un tank o un ostacolo): essendo il
        // server ad averlo istanziato, questa distruzione e' autorevole e si propaga
        // a tutti i client.
        if (collision.gameObject.layer == LayerMask.NameToLayer("Projectile")) return; // ignora altri proiettili
        Destroy(gameObject);
    }
}
