using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LeaderboardEntityDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text displayText;
    [SerializeField] private Color myColor;
    private FixedString32Bytes playerName;
    public ulong id { get; private set; }
    public int coins { get; private set; }

    public void init(ulong id, FixedString32Bytes playerName, int coins)
    {
        this.id = id;
        this.playerName = playerName;
        if (id == NetworkManager.Singleton.LocalClientId)
        {
            displayText.color = myColor;
        }
        updateCoins(coins);
    }

    public void updateCoins(int coins)
    {
        this.coins = coins;
        updateText();
    }

    public void updateText()
    {
        displayText.text = $"{transform.GetSiblingIndex() + 1}. {playerName} ({coins})";
    }
}
