using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    [Header("Reward")]
    public float healReward = 0.075f;
    public bool zeroSum = true;

    private bool consumed = false;

    void OnTriggerEnter(Collider other)
    {
        if (consumed) return;

        BattleBotAgent agent = other.GetComponentInParent<BattleBotAgent>();
        if (agent == null) return;

        if (agent.arena != null && agent.arena.MatchIsEnding) return;

        bool wasHealed = agent.RestoreBalloon();
        if (!wasHealed) return;

        consumed = true;

        agent.AddReward(healReward);

        if (zeroSum && agent.arena != null)
        {
            var opp = (agent == agent.arena.agentA) ? agent.arena.agentB : agent.arena.agentA;
            if (opp != null) opp.AddReward(-healReward);
        }

        Destroy(gameObject);
    }
}