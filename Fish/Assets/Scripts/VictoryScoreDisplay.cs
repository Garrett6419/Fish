using System.Collections;
using UnityEngine;
using TMPro;

public class VictoryScoreDisplay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Animation")]
    [SerializeField] private float numberAnimationTime = 1.5f;

    void Start()
    {
        if (scoreText == null)
        {
            Debug.LogError("Score Text is not assigned in the Inspector!");
            return;
        }

        if (Player.instance != null)
        {
            // Get the final score that was just calculated
            long score = Player.instance.finalScore;
            if (score > Player.instance.PlayerStats.highScore)
            {
                Player.instance.PlayerStats.highScore = score;
            }

            // Start the animation
            StartCoroutine(AnimateScore(score));
        }
        else
        {
            // Failsafe in case the scene is run directly
            scoreText.text = "Final Score:\nError";
        }
    }

    /// <summary>
    /// Animates the score counting up.
    /// </summary>
    private IEnumerator AnimateScore(long targetScore)
    {
        string prefix = "Current Score:\n";
        float elapsedTime = 0f;

        while (elapsedTime < numberAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            // Use Sin for a nice ease-out effect
            float t = Mathf.Sin(elapsedTime / numberAnimationTime * Mathf.PI * 0.5f);

            // Lerp from 0 to the target score
            long currentValue = (long)Mathf.Lerp(0, targetScore, t);

            // Update text, using "N0" for comma formatting
            scoreText.text = prefix + currentValue.ToString("N0");
            yield return null; // Wait for the next frame
        }

        // After the loop, set the text to the exact final score
        scoreText.text = prefix + targetScore.ToString("N0");
    }
}