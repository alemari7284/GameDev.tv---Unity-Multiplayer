using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Vero punto di ingresso "umano" del multiplayer: e' il metodo
// agganciato all'OnClick del bottone "Host" nella scena Menu (vedi Inspector).
// HostSingleton.Instance qui esiste gia' di sicuro, perche' e' stato
// creato da ApplicationController nella scena NetBootstrap PRIMA che
// si arrivasse al Menu. Se invece si prova ad aprire/lanciare la scena Menu da
// sola, saltando NetBootstrap, Instance e' null e questa riga lancia una
// NullReferenceException (e' esattamente il bug diagnosticato in precedenza,
// causato dal partire dalla scena sbagliata, non da un errore nel codice).
// "async void" (invece di "async Task") e' accettabile SOLO perche' questo e' un
// event handler UI, l'unico caso in cui Unity/C# lo tollera: nessuno "aspetta" il
// completamento, eventuali eccezioni non catturate finirebbero solo nella console.
public class MainMenu : MonoBehaviour
{
    [SerializeField] private TMP_InputField joinCodeField;
    public async void StartHost()
    {
        await HostSingleton.Instance.GameManager.StartHostAsync();
    }

    public async void startClient()
    {
        await ClientSingleton.Instance.gameManager.startClientAsync(joinCodeField.text);
    }
}
