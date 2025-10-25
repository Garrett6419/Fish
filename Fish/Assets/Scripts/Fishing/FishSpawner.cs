using UnityEngine;

public class FishSpawner : MonoBehaviour
{
    [SerializeField] private Transform _landing;
    [SerializeField] private GameObject[] fish; // This array holds all your fish prefabs

    /// <summary>
    /// Gets a random fish prefab and also returns its
    /// index (ID) from the fish array.
    /// </summary>
    /// <param name="fishID">The index of the fish in the array.</param>
    /// <returns>The fish GameObject prefab.</returns>
    public GameObject GetFish(out int fishID)
    {
        // Get a random index
        int id = (int)Random.Range(0, fish.Length);

        // Pass that ID back to the Player
        fishID = id;

        // Return the prefab at that index
        return fish[id];
    }

    /// <summary>
    /// Returns the total number of different fish types in the spawner.
    /// </summary>
    public int GetFishTypeCount()
    {
        return fish.Length;
    }
}