using System.Collections.Generic;
using UnityEngine;

public class BalloonManager : MonoBehaviour
{
    private BalloonObject[] balloonSlots;

    [Header("SharedState")]
    public SharedState sharedState;

    public int MaxBalloons => balloonSlots.Length;
    private void Awake()
    {
        List<BalloonObject> balloons = new List<BalloonObject>();
        for (int i = 0; i < transform.childCount; i++)
        {
            BalloonObject balloon = transform.GetChild(i).GetComponent<BalloonObject>();
            if (balloon != null)
            {
                balloons.Add(balloon);
            }
        }
        balloonSlots = balloons.ToArray();
    }


    public void getCurrentBalloons()
    {
        int count = 0;
        foreach (var balloon in balloonSlots)
        {
            if (balloon.isActive)
                count++;
        }
    }

    //called by spawner to add balloons
    public void AddBalloon()
    {
        Debug.Log("BalloonManager: Adding balloon");
        foreach (BalloonObject balloon in balloonSlots)
        {
            if (!balloon.isActive)
            {
                Debug.Log("BalloonManager: Restoring balloon");
                balloon.Restore();
                sharedState.balloonGained = true;
                break; // on first found inactive balloon, restore and exit
            }
        }
    }

}
