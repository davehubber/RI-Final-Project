using System.Collections.Generic;
using UnityEngine;

public class BalloonManager : MonoBehaviour
{
    private BalloonObject[] balloonSlots;

    private StateEventManager stateManager;

    public int MaxBalloons => balloonSlots.Length;
    private void Awake()
    {
        // gather all BalloonObject components in children
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

        // find StateEventManager
        stateManager = GetComponentInChildren<StateEventManager>();
        if (stateManager == null)
        {
            Debug.LogError("BalloonManager: No StateEventManager found in children!");
        }
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
                stateManager.balloonRestored(balloon);
                break; // on first found inactive balloon, restore and exit
            }
        }
    }

    // called by child balloon when popped
    public void PopBalloon(BalloonObject balloon)
    {
        stateManager.balloonPopped(balloon);
    }

}
