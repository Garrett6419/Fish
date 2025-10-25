using UnityEngine;
using System.Collections;
using TMPro; // Add this for TextMeshPro
using UnityEngine.UI; // Add this if you are using legacy UI Text

public class FishCaught : MonoBehaviour
{
    [SerializeField] private GameObject bam;
    private GameObject fishInstance;
    [SerializeField] private Player player; // Make sure to assign this in the Inspector!

    [Header("UI Text Fields")]
    // Use TextMeshProUGUI if you're using TextMeshPro (recommended)
    // Use Text if you are using the legacy UI Text
    [SerializeField] private TextMeshProUGUI weightText;
    [SerializeField] private TextMeshProUGUI lengthText;
    [SerializeField] private TextMeshProUGUI caughtText;
    [SerializeField] private TextMeshProUGUI totalText;
    [SerializeField] private TextMeshProUGUI pointsText;

    [Header("Animation")]
    [SerializeField] private float numberAnimationTime = 1.5f;

    private bool canClose = false;

    // SetUp now takes all the calculated values from the Player
    public void SetUp(GameObject caughtFishPrefab, float weight, float length, int numCaught, float totalMoney, float totalPoints)
    {
        // Activate the "Bam!" effect

        gameObject.SetActive(true); // Make sure panel is visible
        bam.SetActive(true);

        // --- Instantiate Fish ---
        if (fishInstance != null)
        {
            Destroy(fishInstance);
        }
        fishInstance = Instantiate(caughtFishPrefab, this.transform);
        fishInstance.transform.localPosition = new(0, 1.0f);

        // --- Start UI Animations ---
        // Clear text fields first
        weightText.text = "";
        lengthText.text = "";
        caughtText.text = "";
        totalText.text = "";
        pointsText.text = "";

        // Start the number ticking
        StartCoroutine(AnimateNumbers(weight, length, numCaught, totalMoney, totalPoints));

        // Start the delay for closing
        canClose = false;
        StartCoroutine(EnableClosing());
    }

    private void Update()
    {
        // Check if the panel is ready to be closed and the player clicks
        if (canClose && Input.GetMouseButtonDown(0))
        {
            TakeDown();
        }
        if(player == null)
        {
            player = FindAnyObjectByType<Player>();
        }
    }

    // This coroutine creates the "count up" effect
    private IEnumerator AnimateNumbers(float targetWeight, float targetLength, int targetCaught, float targetTotal, float targetPoints)
    {
        float elapsedTime = 0f;

        while (elapsedTime < numberAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / numberAnimationTime;
            // Smooth the transition
            t = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease-out

            // Lerp (linear interpolate) from 0 to the target value
            float currentWeight = Mathf.Lerp(0, targetWeight, t);
            float currentLength = Mathf.Lerp(0, targetLength, t);
            float currentTotal = Mathf.Lerp(0, targetTotal, t);
            float currentPoints = Mathf.Lerp(0, targetPoints, t);
            // We can cast to int for 'caught' so it ticks 0 -> 1 -> 2
            int currentCaught = (int)Mathf.Lerp(0, targetCaught, t);

            // Update the text fields, using "F2" for 2 decimal places
            weightText.text = "Weight:\t" + currentWeight.ToString("F2");
            lengthText.text = "Length:\t" + currentLength.ToString("F2");
            caughtText.text = "Caught:\t*" + currentCaught.ToString();
            totalText.text = "Sum:\t$" + currentTotal.ToString("F2");
            pointsText.text = "Points:\t" + currentPoints.ToString("F0"); // "F0" for no decimals

            yield return null; // Wait for the next frame
        }

        // After the loop, set the text to the final, exact values
        weightText.text = "Weight:\t" + targetWeight.ToString("F2");
        lengthText.text = "Length:\t" + targetLength.ToString("F2");
        caughtText.text = "Caught:\t*" + targetCaught.ToString();
        totalText.text = "Sum:\t$" + targetTotal.ToString("F2");
        pointsText.text = "Points:\t" + targetPoints.ToString("F0");
    }

    // This coroutine adds a short delay before the panel can be closed
    private IEnumerator EnableClosing()
    {
        // Wait for a short time to prevent the "reel in" click
        // from immediately closing the results panel.
        yield return new WaitForSeconds(0.3f);
        canClose = true;
    }

    public void TakeDown()
    {
        // Stop any coroutines that are still running (like the number ticker)
        StopAllCoroutines();

        bam.SetActive(false);

        if (fishInstance != null)
        {
            Destroy(fishInstance);
        }

        gameObject.SetActive(false);

        // --- ADDED: Tell the player they can cast again ---
        if (player != null)
        {
            player.SetCanCast(true);
        }
        else
        {
            Debug.LogError("Player reference not set on FishCaught panel!");
        }
    }
}