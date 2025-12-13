using UnityEngine;
using Unity.MLAgents;
using System.Collections;

/// <summary>
/// Controls the 1v1 arena, applies rewards for pops/win/loss, and handles scene resets.
/// Designed for self-play: keeps the terminal outcome (win/loss/draw) dominant so TensorBoard ELO stays meaningful.
/// </summary>
public class BattleArena : MonoBehaviour
{
    [Header("Agents")]
    public BattleBotAgent agentA;
    public BattleBotAgent agentB;

    [Header("Arena Visuals")]
    public MeshRenderer floorRenderer;
    public Material defaultFloorMaterial;
    public float winFlashDuration = 1.0f;

    [Header("Episode Settings")]
    [Tooltip("Hard timeout in environment steps (physics steps). Set to 0 to disable.")]
    public int maxEnvironmentSteps = 2000;

    [Header("Rewards (Self-Play Friendly)")]
    [Tooltip("Terminal reward given to the winner when the match ends.")]
    public float winReward = 1.0f;

    [Tooltip("Terminal reward given to the loser when the match ends.")]
    public float loseReward = -1.0f;

    [Tooltip("Shaping reward for the attacker when a balloon is popped.")]
    public float balloonPopReward = 0.1f;

    [Tooltip("Shaping reward for the victim when a balloon is popped (usually -balloonPopReward for zero-sum shaping).")]
    public float balloonPopPenalty = -0.1f;

    [Tooltip("If enabled, guarantees winner finishes the episode with positive cumulative reward and loser with negative, so self-play ELO can't be inverted by penalties.")]
    public bool enforceOutcomeSignForElo = true;

    [Tooltip("Minimum positive cumulative reward for the winner at episode end when enforcing outcome sign.")]
    public float minWinnerFinalReward = 0.1f;

    [Tooltip("Maximum (closest to zero) cumulative reward for the loser at episode end when enforcing outcome sign (should be negative).")]
    public float maxLoserFinalReward = -0.1f;

    [SerializeField] float arenaHalfSize = 10f;
    [SerializeField] float wallPadding = 0.5f;
    [SerializeField] float agentRadius = 1.0f;
    [SerializeField] float minSeparation = 1.0f;
    [SerializeField] float spawnAreaFracDefault = 1f;
    [SerializeField] int spawnTries = 50;
    [SerializeField] LayerMask spawnBlockers;
    [SerializeField] private Transform arenaRoot;

    private SimpleMultiAgentGroup agentGroup;
    private bool matchIsEnding = false;
    private int envStepCount = 0;

    public bool MatchIsEnding => matchIsEnding;

    void Start()
    {
        agentGroup = new SimpleMultiAgentGroup();
        agentGroup.RegisterAgent(agentA);
        agentGroup.RegisterAgent(agentB);

        if (floorRenderer != null) floorRenderer.material = defaultFloorMaterial;

        ResetScene();
    }

    void FixedUpdate()
    {
        if (matchIsEnding) return;

        if (maxEnvironmentSteps > 0)
        {
            envStepCount++;
            if (envStepCount >= maxEnvironmentSteps)
            {
                // Draw / timeout: end with ~0 reward for both.
                EndMatchDraw();
            }
        }
    }

    public void OnBalloonPopped(BattleBotAgent victim, BattleBotAgent attacker)
    {
        if (matchIsEnding) return;

        // Small, outcome-aligned shaping.
        attacker.AddReward(balloonPopReward);
        victim.AddReward(balloonPopPenalty);

        if (victim.GetActiveBalloonCount() <= 0)
        {
            EndMatchWin(attacker, victim);
        }
    }

    private void EndMatchWin(BattleBotAgent winner, BattleBotAgent loser)
    {
        if (matchIsEnding) return;
        matchIsEnding = true;

        // Terminal outcome rewards (important for self-play ELO).
        winner.AddReward(winReward);
        loser.AddReward(loseReward);

        if (enforceOutcomeSignForElo)
        {
            EnforceOutcomeSigns(winner, loser);
        }

        // Flash the floor for feedback (does not delay the episode end/reset).
        if (floorRenderer != null && winner.teamMaterial != null)
        {
            StartCoroutine(FlashFloor(winner.teamMaterial));
        }

        // End the group episode, then reset the environment before the next Academy step.
        agentGroup.EndGroupEpisode();
        ResetScene();

        // Prevent any leftover physics callbacks from double-ending.
        StartCoroutine(ClearMatchEndingNextFrame());
    }

    private void EndMatchDraw()
    {
        if (matchIsEnding) return;
        matchIsEnding = true;

        // Make a timeout behave like a true draw: final reward should be ~0 for both,
        // otherwise self-play ELO may treat it as a win/loss depending on the sign.
        NeutralizeReward(agentA);
        NeutralizeReward(agentB);

        agentGroup.EndGroupEpisode();
        ResetScene();

        StartCoroutine(ClearMatchEndingNextFrame());
    }

    private void NeutralizeReward(BattleBotAgent agent)
    {
        if (agent == null) return;
        agent.AddReward(-agent.GetCumulativeReward());
    }

    private void EnforceOutcomeSigns(BattleBotAgent winner, BattleBotAgent loser)
    {
        // Ensures ELO calculation can't be inverted by shaping penalties.
        float winnerCum = winner.GetCumulativeReward();
        if (winnerCum <= 0f)
        {
            winner.AddReward(minWinnerFinalReward - winnerCum);
        }

        float loserCum = loser.GetCumulativeReward();
        if (loserCum >= 0f)
        {
            // maxLoserFinalReward should be negative (e.g. -0.1). This pushes the loser below 0.
            loser.AddReward(maxLoserFinalReward - loserCum);
        }
    }

    IEnumerator FlashFloor(Material winnerMat)
    {
        floorRenderer.material = winnerMat;

        yield return new WaitForSeconds(winFlashDuration);

        if (floorRenderer != null && defaultFloorMaterial != null)
        {
            floorRenderer.material = defaultFloorMaterial;
        }
    }

    IEnumerator ClearMatchEndingNextFrame()
    {
        yield return null;
        matchIsEnding = false;
    }

    public void ResetScene()
    {
        envStepCount = 0;

        agentA.ResetAgent();
        agentB.ResetAgent();

        PlaceAgentsNonOverlapping();
    }

    Vector3 SampleSpawnLocal()
    {
        float spawnAreaFrac = Academy.Instance.EnvironmentParameters.GetWithDefault("spawn_area_frac", spawnAreaFracDefault);
        float limit = (arenaHalfSize - wallPadding) * spawnAreaFrac;
        float x = Random.Range(-arenaHalfSize + wallPadding, arenaHalfSize - wallPadding);
        float z = Random.Range(-arenaHalfSize + wallPadding, arenaHalfSize - wallPadding);
        return new Vector3(x, 0f, z); // local to arenaRoot
    }

    Vector3 LocalToWorld(Vector3 localPos) => arenaRoot.TransformPoint(localPos);

    Quaternion YawLocalToWorld(float yawDeg) =>
        arenaRoot.rotation * Quaternion.Euler(0f, yawDeg, 0f);

    bool IsFreeLocal(Vector3 localPos)
    {
        Vector3 world = LocalToWorld(localPos);
        return !Physics.CheckSphere(world, agentRadius, spawnBlockers, QueryTriggerInteraction.Ignore);
    }

    Vector3 FindSpawnLocal(Vector3? otherLocal)
    {
        for (int t = 0; t < spawnTries; t++)
        {
            var p = SampleSpawnLocal();
            if (!IsFreeLocal(p)) continue;
            if (otherLocal.HasValue && Vector3.Distance(p, otherLocal.Value) < minSeparation) continue;
            return p;
        }

        // fallback
        if (!otherLocal.HasValue) return new Vector3(-arenaHalfSize + wallPadding, 0f, -arenaHalfSize + wallPadding);
        return new Vector3( arenaHalfSize - wallPadding, 0f,  arenaHalfSize - wallPadding);
    }

    void PlaceAgentsNonOverlapping()
    {
        var pALocal = FindSpawnLocal(null);
        var pBLocal = FindSpawnLocal(pALocal);

        TeleportAgent(agentA, LocalToWorld(pALocal), YawLocalToWorld(Random.Range(0, 360)));
        TeleportAgent(agentB, LocalToWorld(pBLocal), YawLocalToWorld(Random.Range(0, 360)));

        Physics.SyncTransforms();
    }

    void TeleportAgent(BattleBotAgent a, Vector3 worldPos, Quaternion worldRot)
    {
        var rb = a.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = worldPos;
            rb.rotation = worldRot;
            rb.WakeUp();
        }
        else
        {
            a.transform.SetPositionAndRotation(worldPos, worldRot);
        }
    }
}
