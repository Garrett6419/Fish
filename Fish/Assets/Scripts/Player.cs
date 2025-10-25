using System.Collections;
using UnityEngine;
using TMPro; // For TextMeshPro
using UnityEngine.SceneManagement; // For scene management
using UnityEngine.EventSystems;

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
    [SerializeField] public float debt = 10000;

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
        // --- THIS IS THE CRITICAL FIX ---
        // Change "BeachScene" to "Beach"
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

        if(!inFishingScene)
        {
            return;
        }

        gameTimeInMinutes += Time.deltaTime * timeScale;

        if (gameTimeInMinutes >= dayEndMinutes)
        {
            EndDay();
            return;
        }
        if (dayTimeDebt != null)
            UpdateDayTimeDebtUI();

        // 3. Check for Scene Change Input
        if (Input.GetMouseButtonDown(1) && !isCasting)
        {
            SceneManager.LoadScene("Shop");
            return;
        }

        // 4. Check for Fishing Input
        // This check will now only pass if the click was NOT on a UI element
        if (canCast && !isCasting && !inputDisabled && Input.GetMouseButtonDown(0))
        {
            Cast();
        }
        else if (isFishOn && !inputDisabled && Input.GetMouseButtonDown(0))
        {
            Reel();
        }

        // 5. Update Reaction Timer
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
        bobberRb.linearVelocity = Vector2.zero;
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
        Fish fishData = hookedFishPrefab.GetComponent<Fish>();
        if (fishData == null)
        {
            Debug.LogError("Hooked fish prefab is missing Fish component!");
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
        debt -= totalMoneyEarned;
        points += (int)totalPointsEarned;

        Debug.Log($"Caught {hookLevel} {hookedFishPrefab.name}(s) for ${totalMoneyEarned}!");

        // Show the results panel
        fishCaughtPanel.SetUp(hookedFishPrefab, displayWeight, displayLength, hookLevel, totalMoneyEarned, totalPointsEarned);
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
        Debug.Log("Day has ended. Loading DayOver scene.");

        // Stop all fishing activity
        SetCanCast(false);

        // Load the summary scene
        // *** IMPORTANT: Change "DayOver" to your exact scene name ***
        SceneManager.LoadScene("DayOver");
    }

    /// <summary>
    /// This is called by the DayOver scene to reset the clock
    /// and load the next fishing day.
    /// </summary>
    public void StartNextDay()
    {
        day++;
        gameTimeInMinutes = dayStartMinutes;
        fishCaughtToday = 0; // Reset daily fish count
        canCast = true;

        Debug.Log($"Day {day} has begun!");

        // TODO: Add mafia interest to debt?
        // debt *= 1.1f; 

        // *** IMPORTANT: Change "BeachScene" to your exact scene name ***
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
        string debtString = $"${debt:F2}";

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