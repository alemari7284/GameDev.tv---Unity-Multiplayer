using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Componente presente solo sul proiettile "vero" (serverProjectilePrefab),
// istanziato dal server in ProjectileLauncher.PrimaryFireServerRpc: gestisce il danno reale,
// quindi la sua logica deve girare esclusivamente li'.
public class DealDamageOnContact : MonoBehaviour
{
    [SerializeField] private int damage = 5;
    private ulong ownerClientId;

    // Chiamato dal server subito dopo lo spawn (ProjectileLauncher.PrimaryFireServerRpc) per memorizzare
    // chi ha sparato il proiettile, cosi' da poterlo escludere dal danno qui sotto.
    public void setOwnerClientId(ulong clientId)
    {
        ownerClientId = clientId;
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        // Se l'oggetto colpito non ha un Rigidbody2D collegato non e' un
        // bersaglio valido (es. muri/scenario): usciamo subito.
        if (collider.attachedRigidbody == null) return;

        if (collider.attachedRigidbody.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            // Non infliggiamo danno al proprietario del proiettile (l'id
            // salvato da setOwnerClientId), per evitare che un giocatore ferisca se stesso.
            if (ownerClientId == netObj.OwnerClientId) return;
        }

        // Se il bersaglio ha un componente Health (vedi Health.cs), gli
        // infliggiamo danno: questa logica gira solo sul server, perche' l'intero
        // proiettile "vero" esiste solo li' (vedi il commento della classe).
        if (collider.attachedRigidbody.TryGetComponent<Health>(out Health health))
        {
            health.takeDamage(damage);
        }
    }
}
