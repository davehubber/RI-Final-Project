using UnityEngine;
using System;

public class StateEventManager : MonoBehaviour
{
    public event Action<BalloonObject> OnSelfBalloonPopped;
    public event Action<BalloonObject> OnEnemyBalloonPopped;
    public event Action<BalloonObject> OnSelfBalloonRestored;

    public void InvokeSelfBalloonPopped(BalloonObject balloon)
    {
        OnSelfBalloonPopped?.Invoke(balloon);
    }

    public void InvokeSelfBalloonRestored(BalloonObject balloon)
    {
        OnSelfBalloonRestored?.Invoke(balloon);
    }

    public void InvokeEnemyBalloonPopped(BalloonObject balloon)
    {
        OnEnemyBalloonPopped?.Invoke(balloon);
    }

}
