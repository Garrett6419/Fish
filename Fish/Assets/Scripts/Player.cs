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
    public int points = 0;
    public float money = 0;
    public bool[] achievements = { false, false, false, false, false, false, false, false, false, false, false, false };

    [Header("Debt")]
    [SerializeField] private float baseDebt = 1000000;
    public float currentDebt;
    [SerializeField] private float interestRate = 1.05f; // 5% interest per day

    // --- Casting Fields ---
    [Header("Casting")]
    [SerializeField] private int lowCast = 100;
    [SerializeField] private int highCast = 200;
    [SerializeField] private Rigidbody2D bobberRb;
    [SerializeField] private GameObject bobber;
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
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// This method is called every time a new scene is loaded.
    /// </summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Check for the main fishing scene
        if (scene.name == "Beach")
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
            inFishingScene = false;
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
        // If catch panel is open, pause time and stop all input
        if (fishCaughtPanel != null && fishCaughtPanel.gameObject.activeInHierarchy)
        {
            return;
        }

        // 2. Check for other scenes
        // If we are not in the fishing scene (e.g., Shop), pause time
        if (!inFishingScene)
        {
            return;
        }

        // 3. Advance Time
        // Time will now run while casting and reeling
        gameTimeInMinutes += Time.deltaTime * timeScale;

        // Check for end of day
        if (gameTimeInMinutes >= dayEndMinutes)
        {
            EndDay();
            return;
        }

        // Update UI clock
        if (dayTimeDebt != null)
            UpdateDayTimeDebtUI();

        // 4. Check for Scene Change Input
        if (Input.GetMouseButtonDown(1) && !isCasting)
        {
            SceneManager.LoadScene("Shop");
            return;
        }

        // 5. Check for Fishing Input
        if (canCast && !isCasting && !inputDisabled && Input.GetMouseButtonDown(0))
        {
            Cast();
        }
        else if (isFishOn && !inputDisabled && Input.GetMouseButtonDown(0))
        {
            Reel();
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
        bobber.SetActive(true);
        bobber.transform.position = bobberDefault.position;
        bobberRb.AddForce(new(Random.Range(lowCast, highCast), Random.Range(lowCast, highCast)));

        fishingCoroutine = StartCoroutine(CastTime());
    }

    private IEnumerator CastTime()
    {
        while (isCasting)
        {
            // 1. Waiting for a bite
            isFishOn = false;
            if (fishAlertUI != null) fishAlertUI.SetActive(false);

            // Get a random fish prefab AND its ID
            hookedFishPrefab = fishSpawner.GetFish(out hookedFishID);

            float waitTime = Random.Range(2.0f, 3.0f);
            yield return new WaitForSeconds(waitTime);

            if (!isCasting) yield break; // Check if interrupted

            // 2. Fish on the line!
            Debug.Log($"Fish on! (ID: {hookedFishID})");
            isFishOn = true;
            reactionTimer = 0f;
            if (fishAlertUI != null) fishAlertUI.SetActive(true);

            // 3. Wait for Player Reaction (1 second window)
            yield return new WaitForSeconds(1.0f);

            // 4. Check if player was too slow
            if (isFishOn)
            {
                Debug.Log("Fish got away! Too slow.");
                // Loop restarts automatically
            }
        }
    }

    public void Reel()
    {
        // Hide and reset the bobber
        bobber.SetActive(false);
        bobberRb.linearVelocity = Vector2.zero; // Use .velocity, not .linearVelocity
        bobberRb.angularVelocity = 0f;

        Debug.Log("Reeling!");
        if (fishingCoroutine != null) StopCoroutine(fishingCoroutine);

        isCasting = false;
        isFishOn = false;
        if (fishAlertUI != null) fishAlertUI.SetActive(false);

        // Check reaction time for bonus
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
        // Safety check for the prefab
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
            // Calculate randomized stats
            float actualWeight = fishData.weight * Random.Range(0.8f, 1.2f);
            float actualLength = fishData.length * Random.Range(0.8f, 1.2f);

            // Apply player multipliers and timing bonus
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

            // Update all stat trackers
            UpdateStats(hookedFishID, actualWeight, actualLength);
        }

        // Calculate money and points
        float totalMoneyEarned = totalValueSum;
        float totalPointsEarned = (hookLevel / 2f) + 0.5f;

        // Update player totals
        money += totalMoneyEarned;
        currentDebt -= totalMoneyEarned;
        points += (int)totalPointsEarned;

        Debug.Log($"Caught {hookLevel} {hookedFishPrefab.name}(s) for ${totalMoneyEarned}!");

        // Safety check for the UI panel
        if (fishCaughtPanel != null)
        {
            fishCaughtPanel.SetUp(hookedFishPrefab, displayWeight, displayLength, hookLevel, totalMoneyEarned, totalPointsEarned);
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
            if (bobber != null) bobber.SetActive(false);
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

            // Check win condition (money >= debt OR debt is negative)
            if (money >= currentDebt || currentDebt <= 0)
            {
                // --- THIS IS THE BONUS CALCULATION ---
                // It now only runs at the end of the day.
                int daysRemaining = 7 - day; // Calculate remaining days
                if (daysRemaining > 0)
                {
                    int earlyBonus = daysRemaining * 250;
                    points += earlyBonus;
                    Debug.Log($"Debt paid off {daysRemaining} days early! +{earlyBonus} points!");
                }
                else
                {
                    Debug.Log("Debt paid off!");
                }
                // ---------------------------------

                // Player wins
                Debug.Log("Victory! Loading VictoryDialogue...");
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

        // --- ADD INTEREST ---
        // Only add interest if the debt hasn't been paid off
        if (currentDebt > 0)
        {
            currentDebt *= interestRate;
        }
        // --------------------

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

        // Increase BASE debt for the new cycle
        baseDebt *= 10;

        // Set the new current debt to the new base debt
        currentDebt = baseDebt;

        // Reset day to 1
        day = 1;
        gameTimeInMinutes = dayStartMinutes;
        fishCaughtToday = 0;

        // Player keeps their money, stats, and achievements
        canCast = true; // Enable casting

        // Load the Beach scene to start the new cycle
        SceneManager.LoadScene("Beach");
    }

    /// <summary>
    /// Updates the Day/Time/Debt UI text.
    /// </summary>
    public void UpdateDayTimeDebtUI()
    {
        // Don't try to update if the text object isn't linked yet
        if (dayTimeDebt == null) return;

        int hours = (int)(gameTimeInMinutes / 60);
        int minutes = (int)(gameTimeInMinutes % 60);

        string timeString = $"{hours:00}:{minutes:00}";
        // Display current debt
        string debtString = $"${currentDebt:F2}";

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