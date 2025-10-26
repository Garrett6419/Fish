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
            return;
        }

        int day = player.day;
        int fishCaught = player.GetFishCaughtToday();
        long debt = player.currentDebt;

        // --- Get Interest Values ---
        long interestToShow = 0;
        if (debt > 0)
        {
            // Calculate debt interest for display
            interestToShow = (long)(debt * 0.05f);
        }

        // Get the bonus points earned
        long bonusPoints = player.pointsInterestEarned;
        // ---------------------------

        // Start the animation
        StartCoroutine(AnimateSummary(day, fishCaught, debt, interestToShow, bonusPoints));
    }

    void Update()
    {
        // Listen for a click ONLY if the animation is finished
        if (canContinue && Input.GetMouseButtonDown(0))
        {
            canContinue = false;
            Player.instance.StartNextDay();
        }
    }

    /// <summary>
    /// Animates the summary text line-by-line, including number tickers.
    /// </summary>
    private IEnumerator AnimateSummary(int day, int fishCaught, long debt, long interest, long bonusPoints)
    {
        // --- Build Static String Parts ---
        string line1 = $"Day {day} Complete\n\n";
        string line2 = "Fish Caught Today:\n";
        string line4 = "\n\nTotal Debt Remaining:\n";
        string line5 = "\n\nInterest Acquired:\n";
        // --- NEW LINE ---
        string lineBonusPoints = "\n\nBonus Interest Points:\n";
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
            float t = Mathf.Sin(elapsedTime / numberAnimationTime * Mathf.PI * 0.5f);
            long currentValue = (long)Mathf.Lerp(0, fishCaught, t);

            summaryText.text = line1 + line2 + currentValue.ToString("N0");
            yield return null;
        }
        summaryText.text = line1 + line2 + fishCaught.ToString("N0");
        yield return new WaitForSeconds(lineDelay);

        // --- Animate Line 4 ---
        summaryText.text += line4;
        yield return new WaitForSeconds(lineDelay * 0.5f);

        // --- Animate Debt Ticker ---
        elapsedTime = 0f;
        while (elapsedTime < numberAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Sin(elapsedTime / numberAnimationTime * Mathf.PI * 0.5f);
            long currentValue = (long)Mathf.Lerp(0, debt, t);

            summaryText.text = line1 + line2 + fishCaught.ToString("N0") +
                               line4 + "$" + currentValue.ToString("N0");
            yield return null;
        }
        summaryText.text = line1 + line2 + fishCaught.ToString("N0") +
                           line4 + "$" + debt.ToString("N0");
        yield return new WaitForSeconds(lineDelay);

        // --- Animate Interest Ticker ---
        summaryText.text += line5;
        yield return new WaitForSeconds(lineDelay * 0.5f);

        elapsedTime = 0f;
        while (elapsedTime < numberAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Sin(elapsedTime / numberAnimationTime * Mathf.PI * 0.5f);
            long currentValue = (long)Mathf.Lerp(0, interest, t);

            summaryText.text = line1 + line2 + fishCaught.ToString("N0") +
                               line4 + "$" + debt.ToString("N0") +
                               line5 + "$" + currentValue.ToString("N0");
            yield return null;
        }
        summaryText.text = line1 + line2 + fishCaught.ToString("N0") +
                           line4 + "$" + debt.ToString("N0") +
                           line5 + "$" + interest.ToString("N0");
        yield return new WaitForSeconds(lineDelay);

        // --- NEW: Animate Bonus Points Ticker ---
        summaryText.text += lineBonusPoints;
        yield return new WaitForSeconds(lineDelay * 0.5f);

        elapsedTime = 0f;
        while (elapsedTime < numberAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Sin(elapsedTime / numberAnimationTime * Mathf.PI * 0.5f);
            long currentValue = (long)Mathf.Lerp(0, bonusPoints, t);

            summaryText.text = line1 + line2 + fishCaught.ToString("N0") +
                               line4 + "$" + debt.ToString("N0") +
                               line5 + "$" + interest.ToString("N0") +
                               lineBonusPoints + currentValue.ToString("N0");
            yield return null;
        }
        summaryText.text = line1 + line2 + fishCaught.ToString("N0") +
                           line4 + "$" + debt.ToString("N0") +
                           line5 + "$" + interest.ToString("N0") +
                           lineBonusPoints + bonusPoints.ToString("N0");
        yield return new WaitForSeconds(lineDelay);
        // --- END OF NEW BLOCK ---

        // --- Animate Line 6 ---
        summaryText.text += line6;

        // Animation is done
        canContinue = true;
    }
}