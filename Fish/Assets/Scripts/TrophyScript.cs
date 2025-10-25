using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class TrophyScript : MonoBehaviour
{
    public Image[] blackouts;
    private GameObject PlayerObj;
    private static Player MainPlayer;

    private Scene trophyScene;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        PlayerObj = GameObject.FindWithTag("Player");
        MainPlayer = PlayerObj.GetComponent<Player>();

        trophyScene = SceneManager.GetSceneByName("Achievements");
    }

    // Update is called once per frame
    void Update()
    {
        if (trophyScene.isLoaded)
        {
            UpdateTrophyCase();
        }
    }

    public static void UpdateTrophyCase()
    {
        for (int i = 0; i < 12; i++)
        {
            if (MainPlayer.achievements[i])
            {
                blackouts[i].enabled = false;
            }
        }
    }
}
