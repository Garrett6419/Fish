using System.Collections;
using UnityEngine;

public class Bobber : MonoBehaviour
{
    // Define the states the bobber can be in
    public enum BobberState
    {
        Hidden,
        Waiting,
        AlertEarly,
        AlertLate
    }

    [Header("Bobber Sprites")]
    [SerializeField] private GameObject wait1;
    [SerializeField] private GameObject wait2;
    [SerializeField] private GameObject wait3;
    [SerializeField] private GameObject alert1;
    [SerializeField] private GameObject alert2;

    private Coroutine waitingAnimationCoroutine;

    void Start()
    {
        // Start hidden by default
        SetState(BobberState.Hidden);
    }

    /// <summary>
    /// Public method to change the bobber's visual state.
    /// Called by Player.cs.
    /// </summary>
    public void SetState(BobberState newState)
    {
        // Stop any running animations
        if (waitingAnimationCoroutine != null)
        {
            StopCoroutine(waitingAnimationCoroutine);
            waitingAnimationCoroutine = null;
        }

        // Hide all sprites before changing state
        HideAllSprites();

        // Set the new state
        switch (newState)
        {
            case BobberState.Hidden:
                // All sprites are already hidden
                break;
            case BobberState.Waiting:
                // Start the 1-2-3 animation loop
                waitingAnimationCoroutine = StartCoroutine(AnimateWaiting());
                break;
            case BobberState.AlertEarly:
                // Show the "perfect" alert
                alert1.SetActive(true);
                break;
            case BobberState.AlertLate:
                // Show the "normal" alert
                alert2.SetActive(true);
                break;
        }
    }

    /// <summary>
    /// Coroutine that handles the "1-2-3" bobbing animation.
    /// </summary>
    private IEnumerator AnimateWaiting()
    {
        while (true)
        {
            wait1.SetActive(true);
            wait2.SetActive(false);
            wait3.SetActive(false);
            yield return new WaitForSeconds(0.5f);

            wait1.SetActive(false);
            wait2.SetActive(true);
            wait3.SetActive(false);
            yield return new WaitForSeconds(0.5f);

            wait1.SetActive(false);
            wait2.SetActive(false);
            wait3.SetActive(true);
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>
    /// Helper method to turn off all sprites.
    /// </summary>
    private void HideAllSprites()
    {
        wait1.SetActive(false);
        wait2.SetActive(false);
        wait3.SetActive(false);
        alert1.SetActive(false);
        alert2.SetActive(false);
    }
}