using System.Collections;
using UnityEngine;
using TMPro; // For TextMeshPro
using UnityEngine.SceneManagement; // For scene management

/// <summary>
/// This is the main player controller script.
/// It is a persistent Singleton that handles all game state,
/// including time, money, stats, and the core fishing loop.
/// </summary>
public class Player : MonoBehaviour
{
    #region Fields

    // --- Singleton Instance ---
    public static Player instance;

    // --- State Bools ---
    private bool inputDisabled = false;
    private bool inFishingScene = false;

    [Header("Player Stats")]
    public float weightMult = 1;
    public float lengthMult = 1;
    public int weightLevel = 1;
    public int lengthLevel = 1;
    public int hookLevel = 1;
    public bool[] achievements = { false, false, false, false, false, false, false, false, false, false, false, false };

    [Header("Economy & Score")]
    public long money = 0;
    public long points = 0; // For spending in the shop
    public long finalScore = 0; // Your end-game score
    public long totalMoneyEarned = 0; // Tracks all money ever earned
    public int totalExtraDays = 0; // Tracks all days saved across all runs

    [Header("Debt")]
    [SerializeField] private long baseDebt = 1000000;
    public long currentDebt; // The active debt, including interest
    [SerializeField] private float interestRate = 1.05f; // 5% interest per day

    // --- Casting Fields ---
    [Header("Casting")]
    [SerializeField] private int lowCast = 100;
    [SerializeField] private int highCast = 200;
    [SerializeField] private Rigidbody2D bobberRb;
    [SerializeField] private Bobber bobber; // Using your Bobber script
    [SerializeField] private Transform bobberDefault;

    // --- Fishing Loop Fields ---
    // These are linked at runtime by RelinkReferences()
    private FishSpawner fishSpawner;
    private FishCaught fishCaughtPanel;
    private GameObject fishAlertUI;

    // State machine bools
    private bool canCast = true;
    private bool isCasting = false; // Is bobber in the water?
    private bool isFishOn = false;  // Is a fish on the line *right now*?
    private float reactionTimer;      // Timer to track player's reaction speed
    private Coroutine fishingCoroutine; // To store and stop the main coroutine
    private GameObject hookedFishPrefab;  // The type of fish on the line
    private int hookedFishID;         // The ID (index) of the fish on the line

    // --- Day/Time/Debt Fields ---
    [Header("Day & Time")]
    [SerializeField] public int day = 1;
    [SerializeField] private float timeScale = 10f; // 10 game minutes per 1 real second
    private TextMeshProUGUI dayTimeDebt;
    private float gameTimeInMinutes;
    private const float dayStartMinutes = 8 * 60; // 8:00 AM
    private const float dayEndMinutes = 20 * 60;  // 8:00 PM (20:00)

    // --- Stats Tracking Fields ---
    [Header("Overall Fish Stats")]
    public int numAllCaught = 0;
    public float heaviestAllCaught = 0;
    public float lightestAllCaught = float.MaxValue;
    public float longestAllCaught = 0;
    public float shortestAllCaught = float.MaxValue;

    [Header("Per-Fish-Type Stats")]
    public int[] numCaught;
    public float[] heaviestCaught;
    public float[] lightestCaught;
    public float[] longestCaught;
    public float[] shortestCaught;

    [Header("Daily Stats")]
    private int fishCaughtToday = 0;

    #endregion

    // -------------------------------------------------------------------

    #region Singleton & Scene Management

    void Awake()
    {
        // Singleton Pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        // Subscribe to the sceneLoaded event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        // Unsubscribe when disabled
        SceneManager.sceneLoaded -= OnSceneLoaded; // This was the typo fix
    }

    /// <summary>
    /// This method is called every time a new scene is loaded.
    /// </summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Check for the main fishing scene
        if (scene.name == "Beach") //
        {
            Debug.Log("Beach scene loaded, re-linking references...");
            RelinkReferences();
            inFishingScene = true;

            // This will now run correctly and solve the accidental cast
            inputDisabled = true;
            StartCoroutine(EnableInputCooldown());
        }
        else
        {
            // We are in the Shop, DayOver, Dialogue, etc.
            inFishingScene = false; //
        }
    }

    private IEnumerator EnableInputCooldown()
    {
        // Wait for a very short time
        yield return new WaitForSeconds(0.1f);
        inputDisabled = false;
        Debug.Log("Input enabled.");
    }

    /// <summary>
    /// Finds all scene-specific objects via the SceneReferences script
    /// and links them to this persistent Player.
    /// </summary>
    void RelinkReferences()
    {
        SceneReferences refs = FindFirstObjectByType<SceneReferences>();
        if (refs != null)
        {
            // Link all scene objects
            fishSpawner = refs.spawner;
            fishCaughtPanel = refs.caughtPanel;
            fishAlertUI = refs.alertUI;
            dayTimeDebt = refs.debtText;

            // Hide UI elements and set initial state
            if (fishAlertUI != null) fishAlertUI.SetActive(false);
            if (fishCaughtPanel != null) fishCaughtPanel.gameObject.SetActive(false);
            if (dayTimeDebt != null) UpdateDayTimeDebtUI();

            // Initialize stat arrays if this is the first time
            if (numCaught == null && fishSpawner != null)
            {
                int fishTypeCount = fishSpawner.GetFishTypeCount();
                Debug.Log($"Initializing stats for {fishTypeCount} fish types.");

                numCaught = new int[fishTypeCount];
                heaviestCaught = new float[fishTypeCount];
                lightestCaught = new float[fishTypeCount];
                longestCaught = new float[fishTypeCount];
                shortestCaught = new float[fishTypeCount];

                // Initialize lightest/shortest arrays to MaxValue
                for (int i = 0; i < fishTypeCount; i++)
                {
                    lightestCaught[i] = float.MaxValue;
                    shortestCaught[i] = float.MaxValue;
                }
            }
        }
        else
        {
            // Only log an error if we are in the main scene
            if (SceneManager.GetActiveScene().name == "Beach")
            {
                Debug.LogError("Could not find 'SceneReferences' object in the Beach scene!");
            }
        }
    }

    #endregion

    // -------------------------------------------------------------------

    #region Unity Methods (Start, Update)

    void Start()
    {
        // Set the starting time
        gameTimeInMinutes = dayStartMinutes;
        // Set the current debt to the base debt on first start
        currentDebt = baseDebt;

        // Failsafe to link references if the game starts in the Beach scene
        RelinkReferences();
    }

    void Update()
    {
        // 1. Check for UI / Paused State
        if (fishCaughtPanel != null && fishCaughtPanel.gameObject.activeInHierarchy)
        {
            return;
        }

        // 2. Check for other scenes
        if (!inFishingScene)
        {
            return;
        }

        // 3. Advance Time
        gameTimeInMinutes += Time.deltaTime * timeScale;
        if (gameTimeInMinutes >= dayEndMinutes)
        {
            EndDay();
            return;
        }
        if (dayTimeDebt != null)
            UpdateDayTimeDebtUI();

        // 4. Check for Scene Change Input
        if (Input.GetMouseButtonDown(1) && !isCasting)
        {
            SceneManager.LoadScene("Shop");
            return;
        }

        // 5. Check for Fishing Input
        if (inputDisabled) return; // Wait for cooldown

        if (Input.GetMouseButtonDown(0))
        {
            if (canCast && !isCasting)
            {
                // 1. Can cast and not casting: CAST
                Cast();
            }
            else if (isFishOn)
            {
                // 2. Fish is on the line: REEL (success)
                Reel();
            }
            else if (isCasting && !isFishOn)
            {
                // 3. Casting but no fish on line: REEL (early)
                RetractEarly();
            }
        }

        // 6. Update Reaction Timer
        if (isFishOn)
        {
            reactionTimer += Time.deltaTime;
        }
    }

    #endregion

    // -------------------------------------------------------------------

    #region Fishing Core Loop

    public void Cast()
    {
        Debug.Log("Casting!");
        canCast = false;
        isCasting = true;
        isFishOn = false;

        // Position and launch bobber
        bobber.gameObject.SetActive(true);
        bobber.transform.position = bobberDefault.position;
        bobberRb.AddForce(new(Random.Range(lowCast, highCast), Random.Range(lowCast, highCast)));
        bobber.SetState(Bobber.BobberState.Waiting);

        fishingCoroutine = StartCoroutine(CastTime());
    }

    private IEnumerator CastTime()
    {
        // --- TIMING CHANGED ---
        const float perfectWindow = 0.3f;
        const float totalWindow = 1.5f;
        // ----------------------

        while (isCasting)
        {
            // 1. Waiting for a bite
            isFishOn = false;
            if (fishAlertUI != null) fishAlertUI.SetActive(false);

            hookedFishPrefab = fishSpawner.GetFish(out hookedFishID);

            float waitTime = Random.Range(2.0f, 3.0f);
            yield return new WaitForSeconds(waitTime);

            if (!isCasting) yield break;

            // 2. Fish on the line!
            Debug.Log($"Fish on! (ID: {hookedFishID})");
            isFishOn = true;
            reactionTimer = 0f;
            if (fishAlertUI != null) fishAlertUI.SetActive(true);
            bobber.SetState(Bobber.BobberState.AlertEarly);

            // 3. Wait for the "perfect" window to end
            yield return new WaitForSeconds(perfectWindow);

            if (isFishOn)
            {
                bobber.SetState(Bobber.BobberState.AlertLate);
            }

            // 4. Wait for the rest of the total window
            yield return new WaitForSeconds(totalWindow - perfectWindow);

            // 5. Check if player was too slow
            if (isFishOn)
            {
                Debug.Log("Fish got away! Too slow.");
                bobber.SetState(Bobber.BobberState.Waiting);
            }
        }
    }

    public void Reel()
    {
        // Hide and reset the bobber
        bobber.gameObject.SetActive(false);
        bobber.SetState(Bobber.BobberState.Hidden);
        bobberRb.linearVelocity = Vector2.zero;
        bobberRb.angularVelocity = 0f;

        Debug.Log("Reeling!");
        if (fishingCoroutine != null) StopCoroutine(fishingCoroutine);

        isCasting = false;
        isFishOn = false;
        if (fishAlertUI != null) fishAlertUI.SetActive(false);

        float timingMultiplier = 1.0f;
        // --- TIMING CHANGED ---
        if (reactionTimer <= 0.3f)
        {
            Debug.Log("Perfect catch! +20% bonus!");
            timingMultiplier = 1.2f;
        }
        else
        {
            Debug.Log("Good catch!");
        }
        // ----------------------

        ProcessCatch(timingMultiplier);
    }

    /// <summary>
    /// Called when the player clicks to reel in, but no fish is on the line.
    /// </summary>
    private void RetractEarly()
    {
        Debug.Log("Reeled in too early! Resetting cast.");

        bobber.gameObject.SetActive(false);
        bobber.SetState(Bobber.BobberState.Hidden);
        bobberRb.linearVelocity = Vector2.zero;
        bobberRb.angularVelocity = 0f;

        if (fishingCoroutine != null) StopCoroutine(fishingCoroutine);

        isCasting = false;
        isFishOn = false;
        if (fishAlertUI != null) fishAlertUI.SetActive(false);

        canCast = true; // Allow the player to cast again
    }

    private void ProcessCatch(float timingMultiplier)
    {
        if (hookedFishPrefab == null)
        {
            Debug.LogError("ProcessCatch FAILED: hookedFishPrefab was null.");
            SetCanCast(true);
            return;
        }

        Fish fishData = hookedFishPrefab.GetComponent<Fish>();
        if (fishData == null)
        {
            Debug.LogError($"Hooked fish prefab '{hookedFishPrefab.name}' is missing Fish component!");
            SetCanCast(true);
            return;
        }

        float totalValueSum = 0;
        float displayWeight = 0;
        float displayLength = 0;

        for (int i = 0; i < hookLevel; i++)
        {
            float actualWeight = fishData.weight * Random.Range(0.8f, 1.2f);
            float actualLength = fishData.length * Random.Range(0.8f, 1.2f);
            actualWeight *= timingMultiplier * weightMult;
            actualLength *= timingMultiplier * lengthMult;

            if (i == 0)
            {
                displayWeight = actualWeight;
                displayLength = actualLength;
            }

            totalValueSum += (actualWeight + actualLength);
            UpdateStats(hookedFishID, actualWeight, actualLength);
        }

        long moneyEarnedThisCatch = (long)Mathf.Floor(totalValueSum);
        long pointsEarnedThisCatch = (long)Mathf.Floor((hookLevel / 2f) + 0.5f);

        // Update player totals
        money += moneyEarnedThisCatch;
        totalMoneyEarned += moneyEarnedThisCatch;
        points += pointsEarnedThisCatch;

        // --- DEBT DECREMENT RE-ADDED ---
        currentDebt -= moneyEarnedThisCatch;
        // -------------------------------

        Debug.Log($"Caught {hookLevel} {hookedFishPrefab.name}(s) for ${moneyEarnedThisCatch} and {pointsEarnedThisCatch} points!");

        if (fishCaughtPanel != null)
        {
            // Pass the float value for display, and the new points value
            fishCaughtPanel.SetUp(hookedFishPrefab, displayWeight, displayLength, hookLevel, totalValueSum, pointsEarnedThisCatch);
        }
        else
        {
            Debug.LogError("ProcessCatch FAILED: fishCaughtPanel reference is null.");
            SetCanCast(true);
        }
    }

    /// <summary>
    /// Public method to allow external scripts (like FishCaught) 
    /// to re-enable casting.
    /// </summary>
    public void SetCanCast(bool cast)
    {
        canCast = cast;
        if (!cast)
        {
            // Force-stop all fishing activity
            isCasting = false;
            isFishOn = false;
            if (fishingCoroutine != null) StopCoroutine(fishingCoroutine);
            if (fishAlertUI != null) fishAlertUI.SetActive(false);
            if (bobber != null)
            {
                bobber.gameObject.SetActive(false);
                bobber.SetState(Bobber.BobberState.Hidden);
            }
        }
    }

    #endregion

    // -------------------------------------------------------------------

    #region Day/Time & UI

    /// <summary>
    /// Called when the game timer reaches the end of the day.
    /// </summary>
    private void EndDay()
    {
        Debug.Log($"Day {day} has ended.");

        // Stop all fishing activity
        SetCanCast(false);

        // Check for end of 7-day cycle OR if debt is paid off
        if (day >= 7 || currentDebt <= 0)
        {
            Debug.Log("End of day. Checking win/loss condition...");

            // Win condition (debt is 0 or less)
            if (currentDebt <= 0)
            {
                // --- POINT CALCULATION ---
                int daysRemaining = 7 - day;
                if (daysRemaining > 0)
                {
                    totalExtraDays += daysRemaining;
                    Debug.Log($"Debt paid off {daysRemaining} days early!");
                }

                finalScore = totalMoneyEarned * (1 + totalExtraDays);
                Debug.Log($"Victory! Final Score: {finalScore} (Total Earned: {totalMoneyEarned} * (1 + {totalExtraDays} extra days))");
                // ----------------------

                // Player wins
                SceneManager.LoadScene("VictoryDialogue");
            }
            else
            {
                // Player loses
                Debug.Log("Defeat! Loading GameOverDialogue...");
                SceneManager.LoadScene("GameOverDialogue");
            }
        }
        else
        {
            // If it's not the end of Day 7 and debt isn't paid, proceed to normal DayOver scene
            SceneManager.LoadScene("DayOver");
        }
    }

    /// <summary>
    /// This is called by the DayOver scene (on days 1-6) to reset the clock
    /// and load the next fishing day.
    /// </summary>
    public void StartNextDay()
    {
        day++;
        gameTimeInMinutes = dayStartMinutes;
        fishCaughtToday = 0;

        // --- INTEREST CALCULATION ---
        // Interest is only applied if debt is still positive
        if (currentDebt > 0)
        {
            // (interestRate - 1.0f) gets the interest percent (e.g., 0.05f)
            // We apply it to the full outstanding debt
            long interestAdded = (long)(currentDebt * (interestRate - 1.0f));
            currentDebt += interestAdded;
            Debug.Log($"Added ${interestAdded} in interest.");
        }
        // ---------------------------------

        canCast = true; // Re-enable casting
        Debug.Log($"Day {day} has begun!");
        SceneManager.LoadScene("Beach");
    }

    /// <summary>
    /// Called from the Victory Screen to start a new 7-day cycle
    /// with 10x increased debt.
    /// </summary>
    public void ContinueGame()
    {
        Debug.Log("Continuing game! Base debt will be 10x.");

        baseDebt *= 10;

        // The new debt is the new base debt *plus* any leftover money
        // This is a "roll-over" mechanic. If you overpaid, you start with a head start.
        // If you had $500k and debt was -$100k, your new debt is 10M - 600k = 9.4M
        currentDebt = baseDebt;

        // Reset day to 1
        day = 1;
        gameTimeInMinutes = dayStartMinutes;
        fishCaughtToday = 0;

        // Player keeps all other stats (money, points, totalMoneyEarned, totalExtraDays)
        canCast = true;
        SceneManager.LoadScene("Beach");
    }

    /// <summary>
    /// Updates the Day/Time/Debt UI text.
    /// </summary>
    public void UpdateDayTimeDebtUI()
    {
        if (dayTimeDebt == null) return;

        int hours = (int)(gameTimeInMinutes / 60);
        int minutes = (int)(gameTimeInMinutes % 60);

        string timeString = $"{hours:00}:{minutes:00}";
        // Format as "N0" to add commas (e.g., $1,000,000)
        string debtString = $"${currentDebt:N0}";

        dayTimeDebt.text = $"DAY: {day}\tTIME: {timeString}\nDEBT: {debtString}";
    }

    #endregion

    // -------------------------------------------------------------------

    #region Stats Tracking & Getters

    /// <summary>
    /// Updates all stat variables with a new fish's data.
    /// </summary>
    private void UpdateStats(int id, float weight, float length)
    {
        // --- 1. Update Daily Stats ---
        fishCaughtToday++;

        // --- 2. Update Overall Stats ---
        numAllCaught++;

        if (weight > heaviestAllCaught) heaviestAllCaught = weight;
        if (weight < lightestAllCaught) lightestAllCaught = weight;
        if (length > longestAllCaught) longestAllCaught = length;
        if (length < shortestAllCaught) shortestAllCaught = length;

        // --- 3. Update Per-Fish Stats ---
        if (id < 0 || numCaught == null || id >= numCaught.Length)
        {
            Debug.LogError($"Invalid fish ID {id} or stats array not initialized!");
            return;
        }

        numCaught[id]++;

        if (weight > heaviestCaught[id]) heaviestCaught[id] = weight;
        if (weight < lightestCaught[id]) lightestCaught[id] = weight;
        if (length > longestCaught[id]) longestCaught[id] = length;
        if (length < shortestCaught[id]) shortestCaught[id] = length;
    }

    /// <summary>
    /// Public getter for the DayOver scene.
    /// </summary>
    public int GetFishCaughtToday()
    {
        return fishCaughtToday;
    }

    #endregion
}