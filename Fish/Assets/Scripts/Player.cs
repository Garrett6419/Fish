using System.Collections;
using UnityEngine;
using TMPro; // For TextMeshPro
using UnityEngine.SceneManagement; // For scene management
using System.IO; //For Save Data
using System; //For Save Data
using Newtonsoft.Json; //For Save Data

/// <summary>
/// This is the main player controller script.
/// It is a persistent Singleton that handles all game state,
/// including time, money, stats, and the core fishing loop.
/// </summary>
public class Player : MonoBehaviour
{
    #region Fields

    // Save Data Stuff
    private IDataService DataService = new JsonDataService();
    private bool EncryptionEnabled = false;
    public PlayerStats PlayerStats = new PlayerStats();

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

    [Header("Economy & Score")]
    public long money = 0;
    public long points = 0; // For spending in the shop
    public long finalScore = 0; // Your end-game score
    public long totalMoneyEarned = 0; // Tracks all money ever earned
    public int totalExtraDays = 0; // Tracks all days saved across all runs

    [Header("Debt")]
    [SerializeField] private long baseDebt = 1000000;
    // --- NEW ---
    // We use this const to calculate our difficulty multiplier
    private const long STARTING_BASE_DEBT = 1000000;
    // -----------
    public long currentDebt; // The active debt, including interest
    [SerializeField] private float interestRate = 1.05f; // 5% interest per day
    [SerializeField] private float debtAnimationDuration = 2.0f; // Animate over 2 seconds
    private double displayDebt; // Use double for smooth animation

    // --- Casting Fields ---
    [Header("Casting")]
    [SerializeField] private int lowCast = 100;
    [SerializeField] private int highCast = 200;
    [SerializeField] private Rigidbody2D bobberRb;
    [SerializeField] private Bobber bobber; // Using your Bobber script
    [SerializeField] private Transform bobberDefault;

    // --- Animation ---
    [Header("Animation")]
    [SerializeField] private Animator playerAnimator;

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

        StartCoroutine(EnableEventSystemCooldown());
    }

    private IEnumerator EnableInputCooldown()
    {
        // Wait for a very short time
        yield return new WaitForSeconds(0.1f);
        inputDisabled = false;
        Debug.Log("Input enabled.");
    }

    /// <summary>
    /// Disables the UI EventSystem for a fraction of a second
    /// to "eat" the mouse click from the previous scene.
    /// </summary>
    private IEnumerator EnableEventSystemCooldown()
    {
        // Find the EventSystem in the newly loaded scene
        UnityEngine.EventSystems.EventSystem eventSystem = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();

        if (eventSystem != null)
        {
            // Disable it immediately
            eventSystem.enabled = false;

            // Wait for a very short time
            yield return new WaitForSeconds(0.1f);

            // Re-enable it (if it still exists)
            if (eventSystem != null)
            {
                eventSystem.enabled = true;
            }
        }
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
            if (PlayerStats.numCaught == null && fishSpawner != null)
            {
                int fishTypeCount = fishSpawner.GetFishTypeCount();
                Debug.Log($"Initializing stats for {fishTypeCount} fish types.");

                PlayerStats.numCaught = new int[fishTypeCount];
                PlayerStats.heaviestCaught = new float[fishTypeCount];
                PlayerStats.lightestCaught = new float[fishTypeCount];
                PlayerStats.longestCaught = new float[fishTypeCount];
                PlayerStats.shortestCaught = new float[fishTypeCount];

                // Initialize lightest/shortest arrays to MaxValue
                for (int i = 0; i < fishTypeCount; i++)
                {
                    PlayerStats.lightestCaught[i] = float.MaxValue;
                    PlayerStats.shortestCaught[i] = float.MaxValue;
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

        // Sync the display debt to the real debt on start
        displayDebt = currentDebt;

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

        // We now call this every frame to animate the debt
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

        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Cast");
        }

        // Start the new delay coroutine
        StartCoroutine(LaunchBobberAfterDelay(0.5f)); //
    }

    /// <summary>
    /// This new coroutine waits for the cast animation
    /// before launching the bobber and starting the fish timer.
    /// </summary>
    private IEnumerator LaunchBobberAfterDelay(float delay)
    {
        // 1. Wait for the animation to reach its peak
        yield return new WaitForSeconds(delay);

        // 2. Check if we were interrupted (e.g., scene change, end day)
        if (!isCasting) yield break;

        // 3. Now, launch the bobber
        Debug.Log("Launching bobber after delay.");
        bobber.gameObject.SetActive(true);
        bobber.transform.position = bobberDefault.position;
        bobberRb.AddForce(new(UnityEngine.Random.Range(lowCast, highCast), UnityEngine.Random.Range(lowCast, highCast)));

        // Set state to Hidden. The bobber's own collision
        // will set it to "Waiting" when it hits the water.
        bobber.SetState(Bobber.BobberState.Hidden);

        // 4. Start the main fishing loop coroutine
        fishingCoroutine = StartCoroutine(CastTime());
    }

    private IEnumerator CastTime()
    {
        const float perfectWindow = 0.3f;
        const float totalWindow = 1.5f;

        while (isCasting)
        {
            // 1. Waiting for a bite
            isFishOn = false;
            if (fishAlertUI != null) fishAlertUI.SetActive(false);

            hookedFishPrefab = fishSpawner.GetFish(out hookedFishID);

            float waitTime = UnityEngine.Random.Range(2.0f, 3.0f);
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
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Reel");
        }

        // Hide and reset the bobber
        bobber.gameObject.SetActive(false);
        bobber.SetState(Bobber.BobberState.Hidden);

        // --- FIXED ---
        bobberRb.linearVelocity = Vector2.zero; //
        // -------------

        bobberRb.angularVelocity = 0f;

        Debug.Log("Reeling!");
        if (fishingCoroutine != null) StopCoroutine(fishingCoroutine);

        isCasting = false;
        isFishOn = false;
        if (fishAlertUI != null) fishAlertUI.SetActive(false);

        float timingMultiplier = 1.0f;
        if (reactionTimer <= 0.3f)
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

    /// <summary>
    /// Called when the player clicks to reel in, but no fish is on the line.
    /// </summary>
    private void RetractEarly()
    {
        Debug.Log("Reeled in too early! Resetting cast.");

        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Reset");
        }

        bobber.gameObject.SetActive(false);
        bobber.SetState(Bobber.BobberState.Hidden);

        // --- FIXED ---
        bobberRb.linearVelocity = Vector2.zero; //
        // -------------

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
            if (playerAnimator != null) playerAnimator.SetTrigger("Reset"); // Error handling
            return;
        }

        Fish fishData = hookedFishPrefab.GetComponent<Fish>();
        if (fishData == null)
        {
            Debug.LogError($"Hooked fish prefab '{hookedFishPrefab.name}' is missing Fish component!");
            SetCanCast(true);
            if (playerAnimator != null) playerAnimator.SetTrigger("Reset"); // Error handling
            return;
        }

        float totalValueSum = 0;
        float displayWeight = 0;
        float displayLength = 0;

        for (int i = 0; i < hookLevel; i++)
        {
            float actualWeight = fishData.weight * UnityEngine.Random.Range(0.8f, 1.2f);
            float actualLength = fishData.length * UnityEngine.Random.Range(0.8f, 1.2f);
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

        // --- NEW SCALING POINT LOGIC ---
        long basePoints = (long)Mathf.Floor((hookLevel / 2f) + 0.5f);
        // Ensure multiplier is at least 1
        long difficultyMultiplier = Math.Max(1, (baseDebt / STARTING_BASE_DEBT));
        long pointsEarnedThisCatch = basePoints * difficultyMultiplier;
        // -------------------------------

        // Update player totals
        money += moneyEarnedThisCatch;
        totalMoneyEarned += moneyEarnedThisCatch;
        points += pointsEarnedThisCatch;
        currentDebt -= moneyEarnedThisCatch;

        Debug.Log($"Caught {hookLevel} {hookedFishPrefab.name}(s) for ${moneyEarnedThisCatch} and {pointsEarnedThisCatch} points! (Multiplier: {difficultyMultiplier}x)");

        if (fishCaughtPanel != null)
        {
            // Pass the float value for display, and the new points value
            fishCaughtPanel.SetUp(hookedFishPrefab, displayWeight, displayLength, hookLevel, totalValueSum, pointsEarnedThisCatch);
        }
        else
        {
            Debug.LogError("ProcessCatch FAILED: fishCaughtPanel reference is null.");
            SetCanCast(true);
            if (playerAnimator != null) playerAnimator.SetTrigger("Reset"); // Error handling
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

        // We must reset the animator in BOTH cases.
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Reset");
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

        SerializeJson();

        // Stop all fishing activity
        SetCanCast(false);

        // Check for end of 7-day cycle OR if debt is paid off
        if (day >= 7 || currentDebt <= 0)
        {
            Debug.Log("End of day. Checking win/loss condition...");

            // Win condition (debt is 0 or less)
            if (currentDebt <= 0)
            {
                // --- MODIFIED: ADD SCALING SHOP POINT BONUS ---
                int daysRemaining = 7 - day;
                if (daysRemaining > 0)
                {
                    totalExtraDays += daysRemaining;

                    // --- NEW SCALING BONUS ---
                    long difficultyMultiplier = Math.Max(1, (baseDebt / STARTING_BASE_DEBT));
                    long bonusShopPoints = daysRemaining * 250 * difficultyMultiplier;
                    points += bonusShopPoints;
                    Debug.Log($"Debt paid off {daysRemaining} days early! +{bonusShopPoints} shop points! (Multiplier: {difficultyMultiplier}x)");
                    // -------------------------------
                }

                finalScore = totalMoneyEarned * (1 + totalExtraDays);
                Debug.Log($"Victory! Final Score: {finalScore} (Total Earned: {totalMoneyEarned} * (1 + {totalExtraDays} extra days))");

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
        if (currentDebt > 0)
        {
            long interestAdded = (long)(currentDebt * (interestRate - 1.0f));
            currentDebt += interestAdded;
            Debug.Log($"Added ${interestAdded} in interest.");
        }

        // Instantly snap the display debt to the new value (with interest)
        displayDebt = currentDebt;

        canCast = true; // Re-enable casting

        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Reset");
        }

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
        currentDebt = baseDebt;

        // Instantly sync display debt to new real debt
        displayDebt = currentDebt;

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

        // --- DEBT ANIMATION LOGIC ---
        if (displayDebt != currentDebt)
        {
            double difference = displayDebt - currentDebt;
            double speed = difference / debtAnimationDuration;

            if (debtAnimationDuration <= 0)
            {
                displayDebt = currentDebt;
            }
            else
            {
                double step = speed * Time.deltaTime;

                if (displayDebt > currentDebt)
                {
                    // Count down
                    displayDebt = Math.Max(currentDebt, displayDebt - step);
                }
                else if (displayDebt < currentDebt)
                {
                    // Count up (for interest)
                    displayDebt = Math.Min(currentDebt, displayDebt - step);
                }
            }
        }

        int hours = (int)(gameTimeInMinutes / 60);
        int minutes = (int)(gameTimeInMinutes % 60);

        string timeString = $"{hours:00}:{minutes:00}";

        string debtString = $"${(long)displayDebt:N0}";

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
        PlayerStats.numAllCaught++;

        if (weight > PlayerStats.heaviestAllCaught) PlayerStats.heaviestAllCaught = weight;
        if (weight < PlayerStats.lightestAllCaught) PlayerStats.lightestAllCaught = weight;
        if (length > PlayerStats.longestAllCaught) PlayerStats.longestAllCaught = length;
        if (length < PlayerStats.shortestAllCaught) PlayerStats.shortestAllCaught = length;

        // --- 3. Update Per-Fish Stats ---
        if (id < 0 || PlayerStats.numCaught == null || id >= PlayerStats.numCaught.Length)
        {
            Debug.LogError($"Invalid fish ID {id} or stats array not initialized!");
            return;
        }

        PlayerStats.numCaught[id]++;

        if (weight > PlayerStats.heaviestCaught[id]) PlayerStats.heaviestCaught[id] = weight;
        if (weight < PlayerStats.lightestCaught[id]) PlayerStats.lightestCaught[id] = weight;
        if (length > PlayerStats.longestCaught[id]) PlayerStats.longestCaught[id] = length;
        if (length < PlayerStats.shortestCaught[id]) PlayerStats.shortestCaught[id] = length;
    }

    /// <summary>
    /// Public getter for the DayOver scene.
    /// </summary>
    public int GetFishCaughtToday()
    {
        return fishCaughtToday;
    }

    #endregion

    // -------------------------------------------------------------------

    #region Sava Data Methods

    public void ToggleEncryption(bool EncryptionEnabled)
    {
        this.EncryptionEnabled = EncryptionEnabled;
    }

    public void SerializeJson()
    {
        if (DataService.SaveData("/player-stats.json", PlayerStats, EncryptionEnabled))
        {
            try
            {
                PlayerStats data = DataService.LoadData<PlayerStats>("/player-stats.json", EncryptionEnabled);
            }
            catch (Exception e)
            {
                Debug.Log($"Error {e.Message}");
            }
        }
        else
        {
            Debug.Log("Couldn't Save File");
        }
    }

    #endregion

    // -------------------------------------------------------------------
}