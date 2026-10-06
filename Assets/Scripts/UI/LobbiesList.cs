using System;
using System.Collections.Generic;
using Unity.Services.Multiplayer;
using UnityEngine;

// Pannello "lista lobby" nel Menu: chiede a Unity le partite pubbliche con
// posti liberi e crea un LobbyItem (una riga, con bottone "Join") per ognuna.
// Nel corso usa il pacchetto Unity.Services.Lobbies (deprecato); qui usa le
// Sessions: ogni Session creata dall'host con IsPrivate = false (vedi
// HostGameManager) E' una Lobby, quindi cercare sessioni = cercare lobby.
public class LobbiesList : MonoBehaviour
{
    [SerializeField] private Transform lobbyItemParent;
    [SerializeField] private LobbyItem lobbyItemPrefab;

    // isJoining/isRefreshing: guardie contro il doppio click. Sono chiamate di
    // rete asincrone: senza, premere due volte "Refresh" o "Join" mentre la prima
    // richiesta e' ancora in corso ne lancerebbe una seconda in parallelo.
    private bool isJoining;
    private bool isRefreshing;

    // OnEnable (non Start): il pannello viene acceso/spento dal Menu, e ad ogni
    // apertura la lista va ricaricata, non solo la prima volta.
    private void OnEnable()
    {
        refreshList();
    }

    // Agganciato anche al bottone "Refresh" del pannello (Inspector).
    // "async void" va bene solo perche' e' un event handler UI (vedi MainMenu).
    public async void refreshList()
    {
        if (isRefreshing) return;
        isRefreshing = true;

        try
        {
            // QuerySessionsOptions sostituisce QueryLobbiesOptions del corso. I filtri
            // sono gli stessi, cambia solo la sintassi: FilterOption(campo, valore,
            // operazione) invece di QueryFilter(field, op, value).
            // - AvailableSlots > 0: niente partite gia' piene;
            // - IsLocked == 0: niente partite chiuse dall'host a nuovi ingressi.
            // QuerySessionsAsync restituisce gia' solo le sessioni pubbliche.
            var options = new QuerySessionsOptions
            {
                Count = 25,
                FilterOptions = new List<FilterOption>
                {
                    new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater),
                    new FilterOption(FilterField.IsLocked, "0", FilterOperation.Equal),
                }
            };

            QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(options);

            // Svuotiamo la lista vecchia prima di ricostruirla da zero.
            foreach (Transform child in lobbyItemParent)
            {
                Destroy(child.gameObject);
            }

            // ISessionInfo prende il posto di Lobby del corso: e' un riassunto
            // "in sola lettura" della sessione (Id, Name, AvailableSlots...), quanto
            // basta per mostrarla in lista. Non sei ancora dentro: per quello serve
            // JoinAsync qui sotto.
            foreach (ISessionInfo sessionInfo in results.Sessions)
            {
                LobbyItem lobbyItem = Instantiate(lobbyItemPrefab, lobbyItemParent);
                lobbyItem.init(this, sessionInfo);
            }
        }
        // Exception generica invece di LobbyServiceException (stesso motivo di
        // HostGameManager: gli errori delle Sessions arrivano come SessionException).
        // Nel corso il catch era vuoto: qui almeno logghiamo, per vedere l'errore.
        catch (Exception e)
        {
            Debug.LogError(e);
        }

        isRefreshing = false;
    }

    // Chiamato da LobbyItem.join quando si preme "Join" su una riga.
    // Nel corso: JoinLobbyByIdAsync + lettura di Data["JoinCode"] + startClientAsync.
    // Qui tutto e' delegato a ClientGameManager.startClientByIdAsync, che fa i tre
    // passi in una sola chiamata (JoinSessionByIdAsync). Non serve try/catch qui:
    // gli errori li gestisce gia' startClientByIdAsync.
    public async void JoinAsync(ISessionInfo sessionInfo)
    {
        if (isJoining) return;
        isJoining = true;

        await ClientSingleton.Instance.gameManager.startClientByIdAsync(sessionInfo.Id);

        // Nel corso, in caso di errore il return dentro il catch saltava questa riga:
        // isJoining restava true e il bottone "Join" non funzionava piu'. Qui la
        // riga viene raggiunta sempre, anche se l'ingresso fallisce.
        isJoining = false;
    }
}
