using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class PlayerStats
{
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

    public bool[] achievements = { false, false, false, false, false, false, false, false, false, false, false, false };

}