using UnityEngine;

public class FishCaught : MonoBehaviour
{
    // We don't need the spawner here anymore
    // [SerializeField] private FishSpawner fishSpawner; 
    [SerializeField] private GameObject bam;
    private GameObject fishInstance; // Renamed to 'fishInstance' for clarity
    private Vector2 FAR = new(1000, 1000);

    // SetUp now takes the prefab of the fish that was caught
    public void SetUp(GameObject caughtFishPrefab)
    {
        // Activate the "Bam!" effect
        bam.transform.position = Vector2.zero;

        // If we're already showing a fish, destroy it first
        if (fishInstance != null)
        {
            Destroy(fishInstance);
        }

        // Instantiate the *new* fish as a child of this panel
        // and position it (e.g., at the center)
        fishInstance = Instantiate(caughtFishPrefab, this.transform);
        fishInstance.transform.localPosition = Vector3.zero;
    }

    public void TakeDown()
    {
        // Find the player and allow them to cast again
        // Note: FindAnyObjectByType is a bit slow. If you have many objects,
        // it's better to have a direct reference to the Player.
        GameObject.FindAnyObjectByType<Player>().SetCanCast(true);

        // Hide the "Bam!" effect
        bam.transform.position = FAR;

        // Destroy the fish we were showing
        if (fishInstance != null)
        {
            Destroy(fishInstance);
        }

        // Hide this panel
        gameObject.SetActive(false);
    }
}