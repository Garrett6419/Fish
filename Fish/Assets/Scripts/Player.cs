using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    public float weightMult = 1;
    public float lengthMult = 1;
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
    [SerializeField] private Rigidbody2D bobber;
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
    }

    void Update()
    {
        // --- Input Handling ---

        // 1. Check for Cast
        // We can only cast if 'canCast' is true and we aren't already casting
        if (canCast && !isCasting && Input.GetMouseButtonDown(0))
        {
            Cast();
        }

        // 2. Check for Reel
        // We can only reel if a fish is currently on the line
        if (isFishOn && Input.GetMouseButtonDown(0))
        {
            Reel();
        }

        // 3. Update reaction timer
        // If a fish is on the line, start counting up
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
        bobber.AddForce(new(Random.Range(lowCast, highCast), Random.Range(lowCast, highCast)));

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

            // --- 2. Fish is on the line! ---
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
        Debug.Log("Reeling!");
        // Stop the fishing loop
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

        // --- Check Reaction Time ---
        float timingMultiplier = 1.0f;
        if (reactionTimer <= 0.2f)
        {
            Debug.Log("Perfect catch! +20% bonus!");
            timingMultiplier = 1.2f;
        }
        else
        {
            Debug.Log("Good catch!");
            // reactionTimer was > 0.2f but < 1.0f (since CastTime() didn't fire)
        }

        ProcessCatch(timingMultiplier);
    }

    private void ProcessCatch(float timingMultiplier)
    {
        float totalWeightAndLength = 0;
        Fish fishData = hookedFishPrefab.GetComponent<Fish>();

        if (fishData == null)
        {
            Debug.LogError("Hooked fish prefab is missing Fish component!");
            SetCanCast(true); // Failsafe
            return;
        }

        // Loop for each hook
        for (int i = 0; i < hookLevel; i++)
        {
            // Calculate randomized stats
            float actualWeight = fishData.weight * Random.Range(0.8f, 1.2f);
            float actualLength = fishData.length * Random.Range(0.8f, 1.2f);

            // Apply player multipliers and timing bonus
            actualWeight *= timingMultiplier * weightMult;
            actualLength *= timingMultiplier * lengthMult;

            // Add to the total "base value"
            totalWeightAndLength += (actualWeight + actualLength);

            // TODO: Update your stats arrays here
            // e.g., numAllCaught++, check heaviestAllCaught, etc.
        }

        // Final money calculation: (Sum of W+L) * Number of Fish
        float totalMoneyEarned = totalWeightAndLength * hookLevel;
        money += totalMoneyEarned;
        points += hookLevel; // Or however you want to calc points

        Debug.Log($"Caught {hookLevel} {hookedFishPrefab.name}(s) for ${totalMoneyEarned}!");

        // --- Show the results panel ---
        // We pass the *prefab* of the fish we caught so the panel can show it
        fishCaughtPanel.gameObject.SetActive(true);
        fishCaughtPanel.SetUp(hookedFishPrefab);
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