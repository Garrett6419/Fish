using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class DayOver : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI summaryText; // The single text object

    [Header("Animation")]
    [SerializeField] private float lineDelay = 0.5f;
    [SerializeField] private float numberAnimationTime = 1.5f;

    private bool canContinue = false;

    void Start()
    {
        // Clear all text
        summaryText.text = "";

        // Find the Player and get the stats
        Player player = Player.instance;
        if (player == null)
        {
            Debug.LogError("DayOver scene can't find Player! Aborting.");
            // You might want to add a failsafe here, like loading the Beach scene
            // SceneManager.LoadScene("Beach");
            return;
        }

        int day = player.day;
        int fishCaught = player.GetFishCaughtToday();
        float debt = player.currentDebt;

        // Start the animation
        StartCoroutine(AnimateSummary(day, fishCaught, debt));
    }

    void Update()
    {
        // Listen for a click ONLY if the animation is finished
        if (canContinue && Input.GetMouseButtonDown(0))
        {
            // Prevent double-clicks
            canContinue = false;

            // Tell the Player to advance the day and load the next scene
            Player.instance.StartNextDay();
        }
    }

    /// <summary>
    /// Animates the summary text line-by-line, including number tickers.
    /// </summary>
    private IEnumerator AnimateSummary(int day, int fishCaught, float debt)
    {
        // --- Build Static String Parts ---
        string line1 = $"Day {day} Complete\n\n";
        string line2 = "Fish Caught Today:\n";
        string line4 = "\n\nTotal Debt Remaining:\n";
        string line6 = "\n\nPress To Continue";

        float elapsedTime = 0f;

        // --- Animate Line 1 ---
        summaryText.text = line1;
        yield return new WaitForSeconds(lineDelay);

        // --- Animate Line 2 ---
        summaryText.text += line2;
        yield return new WaitForSeconds(lineDelay * 0.5f);

        // --- Animate Line 3 (Fish Ticker) ---
        elapsedTime = 0f;
        while (elapsedTime < numberAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / numberAnimationTime;
            t = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease-out

            float currentValue = Mathf.Lerp(0, fishCaught, t);

            // Rebuild the text string every frame
            summaryText.text = line1 + line2 + currentValue.ToString("F0");
            yield return null; // Wait for next frame
        }

        // Set final value for fish
        summaryText.text = line1 + line2 + fishCaught.ToString("F0");
        yield return new WaitForSeconds(lineDelay);

        // --- Animate Line 4 ---
        summaryText.text += line4;
        yield return new WaitForSeconds(lineDelay * 0.5f);

        // --- Animate Line 5 (Debt Ticker) ---
        elapsedTime = 0f; // Reset timer
        while (elapsedTime < numberAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / numberAnimationTime;
            t = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease-out

            float currentValue = Mathf.Lerp(0, debt, t);

            // Rebuild text every frame, including the previous lines
            summaryText.text = line1 + line2 + fishCaught.ToString("F0") +
                               line4 + "$" + currentValue.ToString("F2");
            yield return null; // Wait for next frame
        }

        // Set final value for debt
        summaryText.text = line1 + line2 + fishCaught.ToString("F0") +
                           line4 + "$" + debt.ToString("F2");
        yield return new WaitForSeconds(lineDelay);

        // --- Animate Line 6 ---
        summaryText.text += line6;

        // Animation is done, allow the player to continue
        canContinue = true;
    }
}