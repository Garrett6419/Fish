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
    public int continueCount = 0; // Tracks NG+ level

    // --- NEW: For DayOver screen ---
    [HideInInspector]
    public long pointsInterestEarned = 0;

    [Header("Debt")]
    [SerializeField] private long baseDebt = 1000000;
    public long currentDebt; // The active debt, including interest
    [SerializeField] private float interestRate = 1.05f; // 5% interest per day
    [SerializeField] private float debtAnimationDuration = 2.0f; // Animate over 2 seconds
    [SerializeField] private float pointsAnimationDuration = 0.25f; // NEW: Animate points much faster

    // --- UI Animation Variables ---
    private double displayDebt; // Use double for smooth animation
    private double displayPoints; // For points animation

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

            // This coroutine disables FISHING input (left click to cast)
            inputDisabled = true;
            StartCoroutine(EnableInputCooldown());

            // --- NEW: Instantly sync all display values ---
            // This prevents numbers from "catching up" when entering the scene
            displayDebt = currentDebt;
            displayPoints = points;
            // ----------------------------------------------
        }
        else
        {
            // We are in the Shop, DayOver, Dialogue, etc.
            inFishingScene = false;
        }

        // This coroutine disables UI input (buttons) for all scenes
        StartCoroutine(EnableEventSystemCooldown());
    }

    /// <summary>
    /// Disables fishing input for a moment.
    /// </summary>
    private IEnumerator EnableInputCooldown()
    {
        // Wait for a very short time
        yield return new WaitForSeconds(0.1f);
        inputDisabled = false;
        Debug.Log("Fishing input enabled.");
    }

    /// <summary>
    /// Disables the UI EventSystem for a fraction of a second
    /// to "eat" the mouse click from the previous scene.
    /// </summary>
    private IEnumerator EnableEventSystemCooldown()
    {
        UnityEngine.EventSystems.EventSystem eventSystem = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();

        if (eventSystem != null)
        {
            eventSystem.enabled = false;
            yield return new WaitForSeconds(0.1f);
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

                for (int i = 0; i < fishTypeCount; i++)
                {
                    PlayerStats.lightestCaught[i] = float.MaxValue;
                    PlayerStats.shortestCaught[i] = float.MaxValue;
                }
            }
        }
        else
        {
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
        gameTimeInMinutes = dayStartMinutes;
        currentDebt = baseDebt;

        // Sync all display variables to their real counterparts on start
        displayDebt = currentDebt;
        displayPoints = points;

        PlayerStats = DataService.LoadData<PlayerStats>("/player-stats.json", EncryptionEnabled);

        RelinkReferences();
    }

    void Update()
    {
        if (fishCaughtPanel != null && fishCaughtPanel.gameObject.activeInHierarchy)
        {
            return;
        }

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

        // We now call this every frame to animate all UI values
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
                Cast();
            }
            else if (isFishOn)
            {
                Reel();
            }
            else if (isCasting && !isFishOn)
            {
                RetractEarly();
            }
        }

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

        StartCoroutine(LaunchBobberAfterDelay(0.5f));
    }

    private IEnumerator LaunchBobberAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!isCasting) yield break;

        Debug.Log("Launching bobber after delay.");
        bobber.gameObject.SetActive(true);
        bobber.transform.position = bobberDefault.position;
        bobberRb.AddForce(new(UnityEngine.Random.Range(lowCast, highCast), UnityEngine.Random.Range(lowCast, highCast)));

        bobber.SetState(Bobber.BobberState.Hidden);

        fishingCoroutine = StartCoroutine(CastTime());
    }

    private IEnumerator CastTime()
    {
        const float perfectWindow = 0.3f;
        const float totalWindow = 1.5f;

        while (isCasting)
        {
            isFishOn = false;
            if (fishAlertUI != null) fishAlertUI.SetActive(false);

            hookedFishPrefab = fishSpawner.GetFish(out hookedFishID);

            float waitTime = UnityEngine.Random.Range(2.0f, 3.0f);
            yield return new WaitForSeconds(waitTime);

            if (!isCasting) yield break;

            Debug.Log($"Fish on! (ID: {hookedFishID})");
            isFishOn = true;
            reactionTimer = 0f;
            if (fishAlertUI != null) fishAlertUI.SetActive(true);
            bobber.SetState(Bobber.BobberState.AlertEarly);

            yield return new WaitForSeconds(perfectWindow);

            if (isFishOn)
            {
                bobber.SetState(Bobber.BobberState.AlertLate);
            }

            yield return new WaitForSeconds(totalWindow - perfectWindow);

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

    private void RetractEarly()
    {
        Debug.Log("Reeled in too early! Resetting cast.");

        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Reset");
        }

        bobber.gameObject.SetActive(false);
        bobber.SetState(Bobber.BobberState.Hidden);

        bobberRb.linearVelocity = Vector2.zero;
        bobberRb.angularVelocity = 0f;

        if (fishingCoroutine != null) StopCoroutine(fishingCoroutine);

        isCasting = false;
        isFishOn = false;
        if (fishAlertUI != null) fishAlertUI.SetActive(false);

        canCast = true;
    }

    private void ProcessCatch(float timingMultiplier)
    {
        if (hookedFishPrefab == null)
        {
            Debug.LogError("ProcessCatch FAILED: hookedFishPrefab was null.");
            SetCanCast(true);
            if (playerAnimator != null) playerAnimator.SetTrigger("Reset");
            return;
        }

        Fish fishData = hookedFishPrefab.GetComponent<Fish>();
        if (fishData == null)
        {
            Debug.LogError($"Hooked fish prefab '{hookedFishPrefab.name}' is missing Fish component!");
            SetCanCast(true);
            if (playerAnimator != null) playerAnimator.SetTrigger("Reset");
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

        // --- THIS IS YOUR 5^N SCALING FACTOR ---
        long basePoints = (long)Mathf.Floor((hookLevel / 2f) + 0.5f);
        // This multiplier scales with 5^continueCount
        long difficultyMultiplier = (long)Math.Max(1, Mathf.Pow(5, continueCount));
        long pointsEarnedThisCatch = basePoints * difficultyMultiplier;
        // ----------------------------------------

        // Update player totals
        money += moneyEarnedThisCatch;
        totalMoneyEarned += moneyEarnedThisCatch;
        points += pointsEarnedThisCatch;
        currentDebt -= moneyEarnedThisCatch;

        Debug.Log($"Caught {hookLevel} {hookedFishPrefab.name}(s) for ${moneyEarnedThisCatch} and {pointsEarnedThisCatch} points! (Multiplier: 5^{continueCount} = {difficultyMultiplier}x)");

        if (fishCaughtPanel != null)
        {
            fishCaughtPanel.SetUp(hookedFishPrefab, displayWeight, displayLength, hookLevel, totalValueSum, pointsEarnedThisCatch);
        }
        else
        {
            Debug.LogError("ProcessCatch FAILED: fishCaughtPanel reference is null.");
            SetCanCast(true);
            if (playerAnimator != null) playerAnimator.SetTrigger("Reset");
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

        SetCanCast(false);

        if (day >= 7 || currentDebt <= 0)
        {
            Debug.Log("End of day. Checking win/loss condition...");

            if (currentDebt <= 0)
            {
                PlayerStats.achievements[7] = true;
                // --- THIS IS YOUR DIMINISHING BONUS LOGIC ---
                int daysRemaining = 7 - day;
                if (daysRemaining > 0)
                {
                    totalExtraDays += daysRemaining;

                    // 1. Calculate the base bonus (250, 125, 62, etc.)
                    long baseBonusPoints = 0;
                    double diminishingBonus = 250.0;
                    for (int i = 0; i < daysRemaining; i++)
                    {
                        baseBonusPoints += (long)Math.Floor(diminishingBonus);
                        diminishingBonus /= 2.0;
                    }

                    // 2. Calculate the scaling factor
                    long difficultyMultiplier = (long)Math.Max(1, Mathf.Pow(5, continueCount));

                    // 3. Calculate total bonus
                    long bonusShopPoints = baseBonusPoints * difficultyMultiplier;
                    points += bonusShopPoints;
                    Debug.Log($"Debt paid off {daysRemaining} days early! +{bonusShopPoints} shop points! (Base: {baseBonusPoints} * Multiplier: 5^{continueCount} = {difficultyMultiplier}x)");
                    // ---------------------------------------------
                }

                finalScore = totalMoneyEarned * (1 + totalExtraDays);
                Debug.Log($"Victory! Final Score: {finalScore} (Total Earned: {totalMoneyEarned} * (1 + {totalExtraDays} extra days))");

                SceneManager.LoadScene("VictoryDialogue");
            }
            else
            {
                Debug.Log("Defeat! Loading GameOverDialogue...");
                SceneManager.LoadScene("GameOverDialogue");
            }
        }
        else
        {
            // --- NEW: Calculate 1.25x (25%) "Interest" on points ---
            pointsInterestEarned = (long)(points * 0.25f);
            points += pointsInterestEarned;
            Debug.Log($"Added {pointsInterestEarned} 'Bonus Interest Points' (25% of {points - pointsInterestEarned})");
            // ---------------------------------------------------

            SceneManager.LoadScene("DayOver");
        }
        SerializeJson();
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
        pointsInterestEarned = 0; // Reset bonus points tracker

        if (currentDebt > 0)
        {
            long interestAdded = (long)(currentDebt * (interestRate - 1.0f));
            currentDebt += interestAdded;
            Debug.Log($"Added ${interestAdded} in interest.");
        }

        // Instantly snap the display debt to the new value (with interest)
        displayDebt = currentDebt;

        canCast = true;

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

        continueCount++; // Increment continue counter
        baseDebt *= 10;
        currentDebt = baseDebt;

        // Instantly sync display debt to new real debt
        displayDebt = currentDebt;

        day = 1;
        gameTimeInMinutes = dayStartMinutes;
        fishCaughtToday = 0;

        canCast = true;
        SceneManager.LoadScene("Beach");
    }

    /// <summary>
    /// Updates the Day/Time/Debt UI text.
    /// </summary>
    public void UpdateDayTimeDebtUI()
    {
        if (dayTimeDebt == null) return;

        // --- 1. SET ANIMATION TARGETS ---

        // Target for debt is the real debt (can be negative)
        long targetDebt = currentDebt;

        // Target for points is just the real points
        long targetPoints = points;


        // --- 2. ANIMATE ALL VALUES (DEBT & POINTS) ---
        AnimateDisplayValue(ref displayDebt, targetDebt, debtAnimationDuration);
        AnimateDisplayValue(ref displayPoints, targetPoints, pointsAnimationDuration);

        // --- 3. FORMAT THE FINAL STRING ---
        int hours = (int)(gameTimeInMinutes / 60);
        int minutes = (int)(gameTimeInMinutes % 60);
        string timeString = $"{hours:00}:{minutes:00}";

        // Format the debt string (will show negative)
        string debtString = $"DEBT: ${(long)displayDebt:N0}";

        // Format the points string
        string pointsString = $"\nPOINTS: {(long)Math.Round(displayPoints):N0}";

        // --- NEW: Format the prestige string ---
        string prestigeString = "";
        if (continueCount > 0)
        {
            prestigeString = $"\nPRESTIGE: {continueCount}";
        }
        // -------------------------------------

        // Combine all strings
        dayTimeDebt.text = $"DAY: {day}\tTIME: {timeString}\n{debtString}{pointsString}{prestigeString}";
    }

    /// <summary>
    /// Helper function to animate a UI value towards its target over a set duration.
    /// </summary>
    private void AnimateDisplayValue(ref double displayValue, long targetValue, float duration)
    {
        if (displayValue == targetValue) return;

        double difference = displayValue - targetValue;

        if (duration <= 0)
        {
            displayValue = targetValue;
            return;
        }

        double speed = difference / duration;
        double step = speed * Time.deltaTime;

        if (displayValue > targetValue)
        {
            displayValue = Math.Max(targetValue, displayValue - step);
        }
        else if (displayValue < targetValue)
        {
            displayValue = Math.Min(targetValue, displayValue - step);
        }
    }

    #endregion

    // -------------------------------------------------------------------

    #region Stats Tracking & Getters

    /// <summary>
    /// Updates all stat variables with a new fish's data.
    /// </summary>
    private void UpdateStats(int id, float weight, float length)
    {
        fishCaughtToday++;
        PlayerStats.numAllCaught++;

        if (weight > PlayerStats.heaviestAllCaught) PlayerStats.heaviestAllCaught = weight;
        if (weight < PlayerStats.lightestAllCaught) PlayerStats.lightestAllCaught = weight;
        if (length > PlayerStats.longestAllCaught) PlayerStats.longestAllCaught = length;
        if (length < PlayerStats.shortestAllCaught) PlayerStats.shortestAllCaught = length;

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
}
