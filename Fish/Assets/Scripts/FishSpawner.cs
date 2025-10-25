using UnityEngine;

public class FishSpawner : MonoBehaviour
{
    [SerializeField] private Transform _landing;
    [SerializeField] private GameObject[] fish;

    public GameObject GetFish()
    {
        return fish[(int)Random.Range(0,fish.Length)];
    }

}
