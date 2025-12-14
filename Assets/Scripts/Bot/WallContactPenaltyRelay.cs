using UnityEngine;

public class WallContactPenaltyRelay : MonoBehaviour
{
    private BattleBotAgent agent;

    private void Awake()
    {
        agent = GetComponentInParent<BattleBotAgent>();
        if (agent == null)
            Debug.LogError($"WallContactPenaltyRelay on {name} couldn't find BattleBotAgent in parents.");
    }

    private void OnTriggerStay(Collider other)
    {
        if (agent == null) return;
        if (other.CompareTag("Wall"))
            agent.ApplyWallHitPenalty();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (agent == null) return;
        if (collision.gameObject.CompareTag("Wall"))
            agent.ApplyWallHitPenalty();
    }
}
