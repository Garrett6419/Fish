using UnityEngine;

public class FishCaught : MonoBehaviour
{
    [SerializeField] private FishSpawner fishSpawner;
    [SerializeField] private GameObject bam;
    private GameObject fish;
    private Vector2 FAR = new(1000, 1000);

    public void SetUp()
    {
        bam.transform.position = Vector2.zero;
        fish = fishSpawner.GetFish();
    }

    public void TakeDown()
    {
        GameObject.FindAnyObjectByType<Player>().SetCanCast(true);
        bam.transform.position = FAR;
        fish.transform.position = FAR;
    }

}
