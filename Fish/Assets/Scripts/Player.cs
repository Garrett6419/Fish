using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    // --- Singleton Instance ---
    public static Player instance;

    [Header("Player Stats")]
    public float weightMult = 1;
    public float lengthMult = 1;
    public int weightLevel = 1;
    public int lengthLevel = 1;
    public int hookLevel = 1;
    public int points = 0;
    public float money = 0;
    [SerializeField] public float debt = 10000;

    // --- Casting Fields ---
    [SerializeField] private int lowCast;
    [SerializeField] private int highCast;
    private Rigidbody2D bobberRb;
    private GameObject bobber;
    private Transform bobberDefault;

    // --- Fishing Loop Fields ---
    private FishSpawner fishSpawner;
    private FishCaught fishCaughtPanel;
    private GameObject fishAlertUI;
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

    // --- NEW: Stats Tracking ---
    [Header("Overall Fish Stats")]
    private int numAllCaught = 0;
    private float heaviestAllCaught = 0;
    private float lightestAllCaught = float.MaxValue; // Start high to guarantee first fish is lighter
    private float longestAllCaught = 0;
    private float shortestAllCaught = float.MaxValue; // Start high to guarantee first fish is shorter

    [Header("Per-Fish-Type Stats")]
    private int[] numCaught;
    private float[] heaviestCaught;
    private float[] lightestCaught;
    private float[] longestCaught;
    private float[] shortestCaught;
    // ----------------------------


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
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // --- IMPORTANT: Change "BeachScene" to your exact scene name ---
        if (scene.name == "BeachScene")
        {
            Debug.Log("Beach scene loaded, re-linking references...");
            RelinkReferences();
        }
    }

    /// <summary>
    /// Finds all scene-specific objects and links them to this persistent Player.
    /// Also initializes stat arrays if this is the first time.
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
            bobber = refs.bobberObject;
            bobberDefault = refs.bobberStartPos;

            if (bobber != null)
                bobberRb = bobber.GetComponent<Rigidbody2D>();

            // Hide UI elements
            if (fishAlertUI != null) fishAlertUI.SetActive(false);
            if (fishCaughtPanel != null) fishCaughtPanel.gameObject.SetActive(false);
            if (dayTimeDebt != null) UpdateDayTimeDebtUI();

            // --- NEW: Initialize Stat Arrays ---
            // If this is the first time (arrays are null), initialize them
            // based on the number of fish types in the spawner.
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
            // ---------------------------------
        }
        else
        {
            // Only log an error if we are in the main scene and can't find refs
            if (SceneManager.GetActiveScene().name == "BeachScene")
            {
                Debug.LogError("Could not find 'SceneReferences' object in the Beach scene!");
            }
        }
    }

    #endregion

    #region Unity Methods (Start, Update)

    void Start()
    {
        // Set the starting time
        gameTimeInMinutes = dayStartMinutes;

        // This is a failsafe to link references if the game
        // starts directly in the Beach scene.
        RelinkReferences();
    }

    void Update()
    {
        // --- 1. Check for UI / Paused State ---
        // If catch panel is open, pause time and stop input
        if (fishCaughtPanel != null && fishCaughtPanel.gameObject.activeInHierarchy)
        {
            return; // Stop all processing
        }

        // --- 2. Advance Time ---
        gameTimeInMinutes += Time.deltaTime * timeScale;
        if (gameTimeInMinutes >= dayEndMinutes)
        {
            EndDay();
        }
        UpdateDayTimeDebtUI(); // Update UI clock

        // --- 3. Check for Scene Change Input ---
        // Right-click to go to shop (only if not casting)
        if (Input.GetMouseButtonDown(1) && !isCasting)
        {
            // --- IMPORTANT: Change "ShopScene" to your exact scene name ---
            SceneManager.LoadScene("Shop");
            return;
        }

        // --- 4. Check for Fishing Input ---
        // Left-click to Cast
        if (canCast && !isCasting && Input.GetMouseButtonDown(0))
        {
            Cast();
        }
        // Left-click to Reel
        else if (isFishOn && Input.GetMouseButtonDown(0))
        {
            Reel();
        }

        // --- 5. Update Reaction Timer ---
        if (isFishOn)
        {
            reactionTimer += Time.deltaTime;
        }
    }

    #endregion

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
            // --- 1. Waiting for a bite ---
            isFishOn = false;
            if (fishAlertUI != null) fishAlertUI.SetActive(false);

            // Get a random fish prefab AND its ID
            hookedFishPrefab = fishSpawner.GetFish(out hookedFishID);

            float waitTime = Random.Range(2.0f, 3.0f);
            yield return new WaitForSeconds(waitTime);

            if (!isCasting) yield break; // Check if we were interrupted

            // --- 2. Fish on the line! ---
            Debug.Log($"Fish on! (ID: {hookedFishID})");
            isFishOn = true;
            reactionTimer = 0f;
            if (fishAlertUI != null) fishAlertUI.SetActive(true);

            // --- 3. Wait for Player Reaction (1 second window) ---
            yield return new WaitForSeconds(1.0f);

            // --- 4. Check if player was too slow ---
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
        float timingMultiplier = 1.5f;
        if (reactionTimer <= 0.4f)
        {
            Debug.Log("Perfect catch! +20% bonus!");
            timingMultiplier = 1.2f;
        }
        else
        {
            Debug.Log("Good catch!\nTook " + reactionTimer + "Time");
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

            // --- NEW: Update all stats ---
            UpdateStats(hookedFishID, actualWeight, actualLength);
            // -----------------------------
        }

        // Calculate money and points
        float totalMoneyEarned = totalValueSum;
        // Your logic was: (hookLevel / 2f + 0.5f)
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
    /// Allows the FishCaught panel to re-enable casting.
    /// </summary>
    public void SetCanCast(bool cast)
    {
        canCast = cast;
        if (!cast)
        {
            isCasting = false;
            isFishOn = false;
            if (fishingCoroutine != null) StopCoroutine(fishingCoroutine);
            if (fishAlertUI != null) fishAlertUI.SetActive(false);
        }
    }

    #endregion

    #region Day/Time & UI

    private void EndDay()
    {
        day++;
        gameTimeInMinutes = dayStartMinutes;
        Debug.Log($"Day {day} has begun!");
        // TODO: Add mafia interest to debt?
        // debt *= 1.1f; 
    }

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

    #region Stats Tracking

    /// <summary>
    /// Updates all stat variables with a new fish's data.
    /// </summary>
    /// <param name="id">The fish type's ID (index).</param>
    /// <param name="weight">The final calculated weight of the fish.</param>
    /// <param name="length">The final calculated length of the fish.</param>
    private void UpdateStats(int id, float weight, float length)
    {
        // --- 1. Update Overall Stats ---
        numAllCaught++;

        if (weight > heaviestAllCaught) heaviestAllCaught = weight;
        if (weight < lightestAllCaught) lightestAllCaught = weight;
        if (length > longestAllCaught) longestAllCaught = length;
        if (length < shortestAllCaught) shortestAllCaught = length;

        // --- 2. Update Per-Fish Stats ---
        // Safety check in case something goes wrong
        if (id < 0 || id >= numCaught.Length)
        {
            Debug.LogError($"Invalid fish ID {id} passed to UpdateStats!");
            return;
        }

        numCaught[id]++;

        if (weight > heaviestCaught[id]) heaviestCaught[id] = weight;
        if (weight < lightestCaught[id]) lightestCaught[id] = weight;
        if (length > longestCaught[id]) longestCaught[id] = length;
        if (length < shortestCaught[id]) shortestCaught[id] = length;

        // You can log this to see it working:
        // Debug.Log($"Stats updated for {hookedFishPrefab.name} (ID {id}): " +
        //           $"Weight: {weight}, Length: {length}. " +
        //           $"Total caught (this type): {numCaught[id]}. " +
        //           $"Total caught (all): {numAllCaught}");
    }

    #endregion
}