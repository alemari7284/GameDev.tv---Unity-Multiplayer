using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine;

public class LeaderboardEntityDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text displayText;
    private FixedString32Bytes playerName;
    public ulong id { get; private set; }
    public int coins { get; private set; }

    public void init(ulong id, FixedString32Bytes playerName, int coins)
    {
        this.id = id;
        this.playerName = playerName;
        updateCoins(coins);
    }

    public void updateCoins(int coins)
    {
        this.coins = coins;
        updateText();
    }

    private void updateText()
    {
        displayText.text = $"1. {playerName} ({coins})";
    }
}
