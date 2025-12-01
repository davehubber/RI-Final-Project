using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        BattleBotAgent agent = other.GetComponentInParent<BattleBotAgent>();

        if (agent != null)
        {
            bool wasHealed = agent.RestoreBalloon();

            if (wasHealed)
            {
                Destroy(gameObject);
            }
        }
    }
}