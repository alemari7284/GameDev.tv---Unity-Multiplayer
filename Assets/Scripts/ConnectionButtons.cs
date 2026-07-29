using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Espone i metodi di avvio della sessione di rete ai bottoni della UI temporanea.
/// Va collocato sul Canvas: i bottoni "Host" e "Join" richiamano rispettivamente
/// <see cref="StartHost"/> e <see cref="StartClient"/> tramite gli eventi OnClick.
/// Avviare due istanze del gioco (una come Host, una come Client nell'editor)
/// permette di testare la sincronizzazione in rete.
/// </summary>
public class ConnectionButtons : MonoBehaviour
{
    /// <summary>
    /// Avvia l'istanza come Host: agisce contemporaneamente da server e da client.
    /// L'Host possiede l'authority del server, quindi detta la posizione degli oggetti
    /// agli altri client connessi.
    /// </summary>
    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
    }

    /// <summary>
    /// Avvia l'istanza come Client puro: si connette a un Host/server esistente.
    /// Con la server authority di default il client puo' solo inviare input,
    /// mentre e' il server a decidere l'esito e a sincronizzare lo stato.
    /// </summary>
    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();

    }
}
