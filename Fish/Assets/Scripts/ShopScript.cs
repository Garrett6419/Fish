using UnityEngine;
using System.Collections;
using System;
using TMPro;
using UnityEngine.SceneManagement;

public class ShopScript : MonoBehaviour
{
    private GameObject PlayerObj;
    private Player MainPlayer;
    public float Mult = 1.5F;

    public TMP_Text playerPoints;
    public TMP_Text weightButton;
    public TMP_Text lengthButton;
    public TMP_Text hookButton;

    private Scene shopScene;
    
    public void Start()
    {
        //Initialize the shop scene on start
        shopScene = SceneManager.GetSceneByName("Shop");

        PlayerObj = GameObject.FindWithTag("Player");
        MainPlayer = PlayerObj.GetComponent<Player>();
    }

    //Runs every tick
    //UpdateTexts if the scene is open
    public void Update()
    {
        if (shopScene.isLoaded)
        {
            UpdateTexts();
        }
    }


    public void UpgradeWeight()
    {
        //Check for players points and then upgrade the weight mult
        if (MainPlayer.points >= (int)Math.Pow(2, (MainPlayer.weightLevel-1)))
        {
            MainPlayer.weightMult *= Mult;
            MainPlayer.points -= (int)Math.Pow(2, (MainPlayer.weightLevel-1));
            MainPlayer.weightLevel += 1;
        }
    }

    public void UpgradeLength()
    {
        //Check for player points and then upgrade the player's mult
        if (MainPlayer.points >= (int)Math.Pow(2, (MainPlayer.lengthLevel-1)))
        {
            MainPlayer.lengthMult *= Mult;
            MainPlayer.points -= (int)Math.Pow(2, (MainPlayer.lengthLevel - 1));
            MainPlayer.lengthLevel += 1;
        }
    }

    public void UpgradeHooks()
    {
        //Check if the player has the necessary points and then increment the hookLevel
        if (MainPlayer.points >= (int)Math.Pow(2, (MainPlayer.hookLevel - 1)))
        {
            MainPlayer.points -= (int)Math.Pow(2, (MainPlayer.hookLevel - 1));
            MainPlayer.hookLevel += 1;
        }
    }

    public void UpdateTexts()
    {
        //Update each TMP every tick
        playerPoints.text = "" + MainPlayer.points + " Points";
        weightButton.text = "Upgrade Fish Weight\n" + (int)Math.Pow(2, (MainPlayer.weightLevel - 1)) + " Points";
        lengthButton.text = "Upgrade Fish Length\n" + (int)Math.Pow(2, (MainPlayer.lengthLevel - 1)) + " Points";
        hookButton.text = "Upgrade # Of Hooks\n" + (int)Math.Pow(2, (MainPlayer.hookLevel - 1)) + " Points";
    }

    public void OnExit()
    {
        //Change Scene back to the beach
        SceneManager.LoadScene("Beach");
    }

}
