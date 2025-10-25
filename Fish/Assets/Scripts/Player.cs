using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    public float weightMult = 1;
    public float lengthMult = 1;
    public int weightLevel = 1;
    public int lengthLevel = 1;
    public int hookLevel = 1;
    public int points = 0;
    public float money = 0;
    public float debt = 0;

    // --- Stats Tracking (from your code) ---
    private int numAllCaught = 0;
    private float heaviestAllCaught = 0;
    private float lightestAllCaught = 0;
    private float longestAllCaught;
    private float shortesAlltCaught;

    private int[] numCaught;
    private float[] heaviestCaught;
    private float[] lightestCaught;
    private float[] longestCaught;
    private float[] shortestCaught;

    // --- Casting Fields (from your code) ---
    [SerializeField] private Rigidbody2D bobberRb;
    [SerializeField] private GameObject bobber;
    [SerializeField] private Transform bobberDefault;
    [SerializeField] private int lowCast;
    [SerializeField] private int highCast;

    // --- NEW Fields for Fishing Loop ---
    [Header("Fishing Loop")]
    [SerializeField] private FishSpawner fishSpawner;
    [SerializeField] private FishCaught fishCaughtPanel;
    [SerializeField] private GameObject fishAlertUI; // A "!" icon or text to show a bite

    private bool canCast = true;
    private bool isCasting = false; // Is bobber in the water?
    private bool isFishOn = false;  // Is a fish on the line *right now*?
    private float reactionTimer;      // Timer to track player's reaction speed
    private Coroutine fishingCoroutine; // To store and stop the main coroutine
    private GameObject hookedFishPrefab;  // The type of fish on the line

    void Start()
    {
        // Ensure UI is hidden on start and we can cast
        if (fishAlertUI != null)
        {
            fishAlertUI.SetActive(false);
        }
        fishCaughtPanel.gameObject.SetActive(false);
        canCast = true;
        bobberRb = bobber.GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // --- Input Handling ---
        // NEW: Check if the fish caught panel is active. If it is, don't allow casting/reeling.
        if (fishCaughtPanel != null && fishCaughtPanel.gameObject.activeInHierarchy)
        {
            return; // Stop processing input
        }

        // 1. Check for Cast
        if (canCast && !isCasting && Input.GetMouseButtonDown(0))
        {
            Cast();
        }

        // 2. Check for Reel
        if (isFishOn && Input.GetMouseButtonDown(0))
        {
            Reel();
        }

        // 3. Update reaction timer
        if (isFishOn)
        {
            reactionTimer += Time.deltaTime;
        }
    }

    public void Cast()
    {
        Debug.Log("Casting!");
        canCast = false;
        isCasting = true;
        isFishOn = false; // Just in case
        bobber.SetActive(true);
        bobber.transform.position = bobberDefault.position;
        bobberRb.AddForce(new(Random.Range(lowCast, highCast), Random.Range(lowCast, highCast)));

        // Start the fishing loop coroutine
        fishingCoroutine = StartCoroutine(CastTime());
    }

    // This is the main fishing loop
    private IEnumerator CastTime()
    {
        // This loop continues as long as we are in the 'isCasting' state
        // It will be stopped by the Reel() or SetCanCast() methods
        while (isCasting)
        {
            // --- 1. Waiting for a bite ---
            isFishOn = false;
            if (fishAlertUI != null)
            {
                fishAlertUI.SetActive(false);
            }

            // Decide what fish we *might* catch
            hookedFishPrefab = fishSpawner.GetFish();

            // Wait 2-3 seconds for a bite
            float waitTime = Random.Range(2.0f, 3.0f);
            yield return new WaitForSeconds(waitTime);

            // Check if we were interrupted (e.g., player quit)
            if (!isCasting)
            {
                yield break;
            }

            // --- 2. Fish on the line! ---
            Debug.Log("Fish on!");
            isFishOn = true;
            reactionTimer = 0f; // Reset reaction timer
            if (fishAlertUI != null)
            {
                fishAlertUI.SetActive(true);
            }

            // --- 3. Wait for Player Reaction (1 second window) ---
            yield return new WaitForSeconds(1.0f);

            // --- 4. Check if player was too slow ---
            // If 'isFishOn' is still true after 1 second, the fish got away
            if (isFishOn)
            {
                Debug.Log("Fish got away! Too slow.");
                // Loop restarts to wait for another fish
            }

            // If the player reeled, 'isFishOn' will be false,
            // and the 'while' loop will restart, but Reel() will have
            // also set 'isCasting' to false, breaking the loop.
        }
    }

    public void Reel()
    {
        // --- ADDED: Hide and reset the bobber ---
        bobber.SetActive(false);
        bobberRb.linearVelocity = Vector2.zero;
        bobberRb.angularVelocity = 0f;
        // ----------------------------------------

        Debug.Log("Reeling!");
        if (fishingCoroutine != null)
        {
            StopCoroutine(fishingCoroutine);
        }

        isCasting = false;
        isFishOn = false;
        if (fishAlertUI != null)
        {
            fishAlertUI.SetActive(false);
        }

        float timingMultiplier = 1.0f;
        if (reactionTimer <= 0.2f)
        {
            Debug.Log("Perfect catch! +20% bonus!");
            timingMultiplier = 1.2f;
        }
        else
        {
            Debug.Log("Good catch!");
        }

        ProcessCatch(timingMultiplier);
    }

    private void ProcessCatch(float timingMultiplier)
    {
        Fish fishData = hookedFishPrefab.GetComponent<Fish>();
        if (fishData == null)
        {
            Debug.LogError("Hooked fish prefab is missing Fish component!");
            SetCanCast(true);
            return;
        }

        float totalValueSum = 0; // This will be the Sum of (W+L) for all fish
        float displayWeight = 0; // Stats for the *first* fish to show on panel
        float displayLength = 0; // Stats for the *first* fish to show on panel

        for (int i = 0; i < hookLevel; i++)
        {
            float actualWeight = fishData.weight * Random.Range(0.8f, 1.2f);
            float actualLength = fishData.length * Random.Range(0.8f, 1.2f);

            actualWeight *= timingMultiplier * weightMult;
            actualLength *= timingMultiplier * lengthMult;

            // Store the first fish's stats for display
            if (i == 0)
            {
                displayWeight = actualWeight;
                displayLength = actualLength;
            }

            // Add this fish's value to the total pot
            totalValueSum += (actualWeight + actualLength);
        }

        // --- NEW Calculation based on screenshot ---
        // Total money is the sum of all fish values
        float totalMoneyEarned = totalValueSum;
        // Points are Total / 2 (as per "Points: Caught/2" -> I'm assuming it means $Sum/2)
        float totalPointsEarned = hookLevel / 2f  + 0.5f;

        money += totalMoneyEarned;
        debt -= totalMoneyEarned;
        points += (int)totalPointsEarned; // Add to total points

        Debug.Log($"Caught {hookLevel} {hookedFishPrefab.name}(s) for ${totalMoneyEarned}!");

        // --- Show the results panel with all the new info ---
        fishCaughtPanel.SetUp(hookedFishPrefab, displayWeight, displayLength, hookLevel, totalMoneyEarned, totalPointsEarned);
    }


    public void SetCanCast(bool cast)
    {
        canCast = cast;
        if (!cast)
        {
            // If we are forced to stop, kill the fishing coroutine
            isCasting = false;
            isFishOn = false;
            if (fishingCoroutine != null)
            {
                StopCoroutine(fishingCoroutine);
            }
            if (fishAlertUI != null)
            {
                fishAlertUI.SetActive(false);
            }
        }
    }
}