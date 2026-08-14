using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawningCoin : Coin
{
    public override int collect()
    {
        if (!IsServer)
        {
            showCoin(false);
            return 0;
        }
        if (alreadyCollected) return 0;
        else
        {
            alreadyCollected = true;
            return coinValue;
        }
    }
}
