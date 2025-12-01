using UnityEngine;

public class BalloonUnit : MonoBehaviour
{
    public BattleBotAgent owner;

    public void Pop()
    {
        gameObject.SetActive(false);
    }

    public void ResetBalloon()
    {
        gameObject.SetActive(true);
    }
}