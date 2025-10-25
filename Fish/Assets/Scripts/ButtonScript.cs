using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGameScript : MonoBehaviour
{
    private GameObject PlayerObj;
    private Player MainPlayer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        PlayerObj = GameObject.FindWithTag("Player");
        MainPlayer = PlayerObj.GetComponent<Player>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void AchievementScene()
    {
        SceneManager.LoadScene("Achievements");
    }

    public void BeachScene()
    {
        SceneManager.LoadScene("Beach");
    }

    public void TitleScene()
    {
        SceneManager.LoadScene("Start");
    }

    public void IntroDialogue()
    {
        SceneManager.LoadScene("IntroDialogue");
    }

    public void ContinueDialogue()
    {
        PlayerObj.GetComponent<Player>().ContinueGame();
        SceneManager.LoadScene("ContinueDialogue");
    }



    
}
