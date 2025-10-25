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

    public void OpenTrophyCase()
    {
        SceneManager.LoadScene("Shop");
    }

    public void StartGame()
    {
        SceneManager.LoadScene("Beach");
    }
}
