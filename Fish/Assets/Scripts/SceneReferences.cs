using UnityEngine;
using TMPro;

// This script lives in the Beach scene and holds references
// to all the objects the persistent Player needs.
public class SceneReferences : MonoBehaviour
{
    [Header("Fishing Logic")]
    public FishSpawner spawner;
    public FishCaught caughtPanel;
    public GameObject alertUI;

    [Header("Player Bobber")]
    public GameObject bobberObject;
    public Transform bobberStartPos;

    [Header("UI")]
    public TextMeshProUGUI debtText;
}