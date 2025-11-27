using System.Collections.Generic;
using UnityEngine;

public class BalloonManager : MonoBehaviour
{
    private BalloonObject[] balloonSlots;

    private StateEventManager stateManager;

    // Stores the starting active/inactive configuration of baloons
    private bool[] initialActiveState;

    public int currentBalloons {get; private set; } = 0;

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

                if (balloon.isActive) currentBalloons++; // count how many balloons are active
            }
        }
        balloonSlots = balloons.ToArray();

        // Save starting state for resets
        initialActiveState = new bool[balloonSlots.Length];
        for (int i = 0; i < balloonSlots.Length; i++)
            initialActiveState[i] = balloonSlots[i].isActive;

        // find StateEventManager
        stateManager = GetComponentInChildren<StateEventManager>();
        if (stateManager == null)
        {
            Debug.LogError("BalloonManager: No StateEventManager found in children!");
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
                stateManager.InvokeSelfBalloonRestored(balloon);
                currentBalloons++;
                break; // on first found inactive balloon, restore and exit
            }
        }
    }

    // called by child balloon when popped
    public void PopBalloon(BalloonObject balloon)
    {
        stateManager.InvokeSelfBalloonPopped(balloon);
        currentBalloons--;
        if (currentBalloons == 0)
        {
            stateManager.InvokeSelfBotDeath(balloon);
        }
    }

    public void ResetBalloons()
    {
        currentBalloons = 0;

        for (int i = 0; i < balloonSlots.Length; i++)
        {
            BalloonObject balloon = balloonSlots[i];
            bool shouldBeActive = initialActiveState[i];

            if (shouldBeActive)
            {
                balloon.Restore();
                currentBalloons++;
            }
            else
            {
                balloon.Hide();
            }
        }

        Debug.Log("BalloonManager: Balloons reset to original configuration.");
    }
}
