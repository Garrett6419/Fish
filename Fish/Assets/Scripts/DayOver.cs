using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class DayOver : MonoBehaviour
{
    [Header("Text Fields")]
    [SerializeField] private TextMeshProUGUI line1_DayComplete;
    [SerializeField] private TextMeshProUGUI line2_FishCaughtLabel;
    [SerializeField] private TextMeshProUGUI line3_FishCaughtValue;
    [SerializeField] private TextMeshProUGUI line4_DebtLabel;
    [SerializeField] private TextMeshProUGUI line5_DebtValue;
    [SerializeField] private TextMeshProUGUI line6_ContinuePrompt;

    [Header("Animation")]
    [SerializeField] private float lineDelay = 0.5f;
    [SerializeField] private float numberAnimationTime = 1.5f;

    private bool canContinue = false;

    void Start()
    {
        // Clear all text fields
        line1_DayComplete.text = "";
        line2_FishCaughtLabel.text = "";
        line3_FishCaughtValue.text = "";
        line4_DebtLabel.text = "";
        line5_DebtValue.text = "";
        line6_ContinuePrompt.text = "";

        // Find the Player and get the stats
        Player player = Player.instance;
        if (player == null)
        {
            Debug.LogError("DayOver scene can't find Player! Aborting.");
            return;
        }

        int day = player.day;
        int fishCaught = player.GetFishCaughtToday();
        float debt = player.debt;

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

    private IEnumerator AnimateSummary(int day, int fishCaught, float debt)
    {
        // Line 1
        line1_DayComplete.text = $"Day {day} Complete";
        yield return new WaitForSeconds(lineDelay);

        // Line 2
        line2_FishCaughtLabel.text = "Fish Caught Today:";
        yield return new WaitForSeconds(lineDelay * 0.5f);

        // Line 3 (Number Ticker)
        StartCoroutine(AnimateNumber(line3_FishCaughtValue, fishCaught, false));
        yield return new WaitForSeconds(numberAnimationTime); // Wait for ticker

        // Line 4
        line4_DebtLabel.text = "Total Debt Remaining:";
        yield return new WaitForSeconds(lineDelay * 0.5f);

        // Line 5 (Number Ticker)
        StartCoroutine(AnimateNumber(line5_DebtValue, debt, true));
        yield return new WaitForSeconds(numberAnimationTime); // Wait for ticker

        // Line 6
        line6_ContinuePrompt.text = "Press To Continue";

        // Animation is done, allow the player to continue
        canContinue = true;
    }

    // This is the number ticker logic, like from FishCaught.cs
    private IEnumerator AnimateNumber(TextMeshProUGUI textField, float targetValue, bool isCurrency)
    {
        float elapsedTime = 0f;

        while (elapsedTime < numberAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / numberAnimationTime;
            t = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease-out

            float currentValue = Mathf.Lerp(0, targetValue, t);

            if (isCurrency)
            {
                // Format as currency, e.g., $12345.67
                textField.text = "$" + currentValue.ToString("F2");
            }
            else
            {
                // Format as a whole number, e.g., 12
                textField.text = currentValue.ToString("F0");
            }

            yield return null; // Wait for the next frame
        }

        // After the loop, set the final, exact value
        if (isCurrency)
        {
            textField.text = "$" + targetValue.ToString("F2");
        }
        else
        {
            textField.text = targetValue.ToString("F0");
        }
    }
}