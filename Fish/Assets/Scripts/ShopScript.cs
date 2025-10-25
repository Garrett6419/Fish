using UnityEngine;
using System.Collections;
using System;

public class ShopScript : MonoBehaviour
{
    public Player MainPlayer;
    public float Mult = 1.5F;

    public void UpgradeWeight()
    {
        //Check for players points
        if (MainPlayer.points >= Math.Pow(2, MainPlayer.weightLevel))
        {
            Debug.Log(message: "Upgraded Weight");
            MainPlayer.weightMult *= Mult;
            MainPlayer.points -= (int)Math.Pow(2, MainPlayer.weightLevel);
            MainPlayer.weightLevel += 1;
        }
    }

    public void UpgradeLength()
    {
        if (MainPlayer.points >= Math.Pow(2, MainPlayer.lengthLevel))
        {
            Debug.Log(message: "Upgraded Length");
            MainPlayer.lengthMult *= Mult;
            MainPlayer.points -= (int)Math.Pow(2, MainPlayer.lengthLevel);
            MainPlayer.lengthLevel += 1;
        }
    }

    public void UpgradeHooks()
    {
        if (MainPlayer.points >= Math.Pow(2, MainPlayer.hookLevel))
        {
            Debug.Log(message: "Upgraded Hooks");
            MainPlayer.points -= (int)Math.Pow(2, MainPlayer.hookLevel);
            MainPlayer.hookLevel += 1;
        }
    }

    public void OnExit()
    {
        //Change Scene
        Debug.Log(message: "Exited");
        return;
    }

}
