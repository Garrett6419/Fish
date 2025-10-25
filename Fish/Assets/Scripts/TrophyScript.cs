using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class TrophyScript : MonoBehaviour
{
    public Image[] blackouts= new Image[12];
    private GameObject PlayerObj;
    private Player MainPlayer;

    private Scene trophyScene;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        PlayerObj = GameObject.FindWithTag("Player");
        MainPlayer = PlayerObj.GetComponent<Player>();

        trophyScene = SceneManager.GetSceneByName("Achievements");

        UpdateAchievements();
    }

    // Update is called once per frame
    void Update()
    {
        if (trophyScene.isLoaded)
        {
            UpdateTrophyCase();
        }
    }

    public void UpdateTrophyCase()
    {
        for (int i = 0; i < 12; i++)
        {
            if (MainPlayer.achievements[i])
            {
                blackouts[i].enabled = false;
            }
        }
    }

    public void UpdateAchievements()
    {
        //10 Lb Fish
        if (MainPlayer.heaviestAllCaught >= 10)
        {
            MainPlayer.achievements[0] = true;

            //100 Lb Fish
            if (MainPlayer.heaviestAllCaught >= 100)
            {
                MainPlayer.achievements[1] = true;

                //1k Lb Fish
                if (MainPlayer.heaviestAllCaught >= 1000)
                {
                    MainPlayer.achievements[2] = true;
                }
            }
           
        }

        //10 Meter Fish
        if (MainPlayer.longestAllCaught >= 10)
        {
            MainPlayer.achievements[4] = true;

            //100 Meter Fish
            if (MainPlayer.longestAllCaught >= 100)
            {
                MainPlayer.achievements[5] = true;

                //1000 Meter Fish
                if (MainPlayer.longestAllCaught >= 1000)
                {
                    MainPlayer.achievements[6] = true;
                }
            }
        }

        //10 Fish Caught
        if (MainPlayer.numAllCaught >= 10)
        {
            MainPlayer.achievements[8] = true;

            //100 Fish Caught
            if (MainPlayer.numAllCaught >= 100)
            {
                MainPlayer.achievements[9] = true;

                //1000 Fish Caught
                if (MainPlayer.numAllCaught >= 1000)
                {
                    MainPlayer.achievements[10] = true;
                }
            }
        }

        //Checking if all fish have been caught
        bool allFishCaught = true;
        foreach (int num in MainPlayer.numCaught)
        {
            if (num < 1)
            {
                allFishCaught = false;
            }
        }

        if (allFishCaught)
        {
            MainPlayer.achievements[3] = true;
        }

        //Check for game ended
        if (MainPlayer.debt <= 0)
        {
            MainPlayer.achievements[7] = true;
        }

        
    }

    public void OnExit()
    {
        SceneManager.LoadScene("Start");
    }
}
