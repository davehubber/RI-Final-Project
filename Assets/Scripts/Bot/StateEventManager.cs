using UnityEngine;
using System;

public class StateEventManager : MonoBehaviour
{
    public event Action<BalloonObject> OnBalloonPopped;
    public event Action<BalloonObject> OnBalloonRestored;

    public void balloonPopped(BalloonObject balloon)
    {
        Debug.Log("StateEventManager: Balloon popped event invoked.");
        OnBalloonPopped?.Invoke(balloon);
    }

    public void balloonRestored(BalloonObject balloon)
    {
        Debug.Log("StateEventManager: Balloon restored event invoked.");
        OnBalloonRestored?.Invoke(balloon);
    }

}
