using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    public float weightMult = 1;
    public float lengthMult = 1;
    public int hookLevel = 1;
    public int points = 0;
    public float money = 0;
    public float debt = 0;

    private int numAllCaught = 0 ;
    private float heaviestAllCaught = 0;
    private float lightestAllCaught = 0;
    private float longestAllCaught;
    private float shortesAlltCaught;

    private int[] numCaught;
    private float[] heaviestCaught;
    private float[] lightestCaught;
    private float[] longestCaught;
    private float[] shortestCaught;

    private Rigidbody2D bobber;

    private bool canCast;

    public void Cast()
    {
        canCast = false;
        bobber.AddForce(new(Random.Range(5,8), Random.Range(5,8)));
    }

    private IEnumerable CastTime()
    {
        yield return null;
    }

    public void Reel()
    {

    }

    public void SetCanCast(bool cast)
    {
        canCast = cast;
    }





}
