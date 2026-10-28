using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;

// Una riga della lista lobby (prefab istanziato da LobbiesList): mostra nome e
// giocatori di una sessione, e il suo bottone "Join" (OnClick -> join, Inspector)
// chiede a LobbiesList di entrarci.
public class LobbyItem : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private TMP_Text lobbyPlayersText;

    private LobbiesList lobbiesList;
    // ISessionInfo invece di Lobby (corso): il riassunto della sessione restituito
    // da QuerySessionsAsync, salvato per sapere in quale entrare al click.
    private ISessionInfo sessionInfo;

    public void init(LobbiesList lobbiesList, ISessionInfo sessionInfo)
    {
        this.lobbiesList = lobbiesList;
        this.sessionInfo = sessionInfo;
        lobbyNameText.text = sessionInfo.Name;
        // ISessionInfo non ha la lista dei giocatori (il corso usava
        // lobby.Players.Count): i giocatori presenti si ricavano dai posti
        // totali meno quelli ancora liberi.
        int playerCount = sessionInfo.MaxPlayers - sessionInfo.AvailableSlots;
        lobbyPlayersText.text = $"{playerCount}/{sessionInfo.MaxPlayers}";
    }

    public void join()
    {
        lobbiesList.JoinAsync(sessionInfo);
    }
}
