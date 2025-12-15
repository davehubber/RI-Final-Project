using UnityEngine;
using Unity.MLAgents;
using System.Collections;

/// <summary>
/// Controls the 1v1 arena, applies rewards for pops/win/loss, and handles scene resets.
/// Spawn/teleport logic is hardened against floor-penetration and overlap-induced tipping.
/// </summary>
public class BattleArena : MonoBehaviour
{
    [Header("Agents")]
    public BattleBotAgent agentA;
    public BattleBotAgent agentB;

    [Header("Arena Root (local space for spawns)")]
    [SerializeField] private Transform arenaRoot;

    [Header("Floor (for safe spawn height)")]
    [SerializeField] private Collider floorCollider; // assign your Floor collider here (recommended)
    [SerializeField] private LayerMask floorMask = ~0; // fallback raycast mask
    [SerializeField] private float spawnSkin = 0.02f;  // small lift above floor

    [Header("Arena Visuals")]
    public MeshRenderer floorRenderer;
    public Material defaultFloorMaterial;
    public float winFlashDuration = 1.0f;

    [Header("Episode Settings")]
    [Tooltip("Hard timeout in environment steps (physics steps). Set to 0 to disable.")]
    public int maxEnvironmentSteps = 5000;

    [Header("Rewards (Self-Play Friendly)")]
    public float winReward = 1.0f;
    public float loseReward = -1.0f;
    public float balloonPopReward = 0.1f;
    public float balloonPopPenalty = -0.1f;

    public bool enforceOutcomeSignForElo = true;
    public float minWinnerFinalReward = 0.1f;
    public float maxLoserFinalReward = -0.1f;

    [Header("Spawn Area")]
    [SerializeField] private float arenaHalfSize = 10f;
    [SerializeField] private float wallPadding = 0.5f;
    [SerializeField] private float spawnAreaFracDefault = 1.0f;
    [SerializeField] private int spawnTries = 80;

    [Header("Spawn Collision / Separation")]
    [Tooltip("LayerMask for things that should block spawns (e.g., walls, props). Prefer NOT to include agents themselves.")]
    [SerializeField] private LayerMask spawnBlockers;

    [Tooltip("If > 0, overrides auto radius for spawn overlap checks. If 0, auto-computed from agent collider bounds.")]
    [SerializeField] private float agentRadiusOverride = 0f;

    [Tooltip("Extra margin added on top of (radiusA + radiusB).")]
    [SerializeField] private float separationMargin = 0.15f;

    [Tooltip("After teleport, try to resolve small floor penetration by nudging up.")]
    [SerializeField] private int verticalResolveTries = 8;

    [Tooltip("Vertical nudge per resolve try.")]
    [SerializeField] private float verticalResolveStep = 0.02f;

    private SimpleMultiAgentGroup agentGroup;
    private bool matchIsEnding = false;
    private int envStepCount = 0;

    public bool MatchIsEnding => matchIsEnding;

    void Awake()
    {
        if (arenaRoot == null) arenaRoot = transform;

        // Best-effort auto-find the floor collider if not assigned.
        if (floorCollider == null && arenaRoot != null)
        {
            var floorT = arenaRoot.Find("Floor");
            if (floorT != null) floorCollider = floorT.GetComponent<Collider>();
        }
    }

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
                EndMatchDraw();
            }
        }
    }

    public void OnBalloonPopped(BattleBotAgent victim, BattleBotAgent attacker)
    {
        if (matchIsEnding) return;

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

        winner.AddReward(winReward);
        loser.AddReward(loseReward);

        if (enforceOutcomeSignForElo)
        {
            EnforceOutcomeSigns(winner, loser);
        }

        if (floorRenderer != null && winner.teamMaterial != null)
        {
            StartCoroutine(FlashFloor(winner.teamMaterial));
        }

        agentGroup.EndGroupEpisode();
        ResetScene();

        StartCoroutine(ClearMatchEndingNextFrame());
    }

    private void EndMatchDraw()
    {
        if (matchIsEnding) return;
        matchIsEnding = true;

        agentA.SetReward(0f);
        agentB.SetReward(0f);

        agentGroup.EndGroupEpisode();
        ResetScene();
        StartCoroutine(ClearMatchEndingNextFrame());
    }

    private void EnforceOutcomeSigns(BattleBotAgent winner, BattleBotAgent loser)
    {
        float winnerCum = winner.GetCumulativeReward();
        if (winnerCum <= 0f)
            winner.AddReward(minWinnerFinalReward - winnerCum);

        float loserCum = loser.GetCumulativeReward();
        if (loserCum >= 0f)
            loser.AddReward(maxLoserFinalReward - loserCum);
    }

    IEnumerator FlashFloor(Material winnerMat)
    {
        floorRenderer.material = winnerMat;
        yield return new WaitForSeconds(winFlashDuration);

        if (floorRenderer != null && defaultFloorMaterial != null)
            floorRenderer.material = defaultFloorMaterial;
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

    // -------------------------
    // Spawn / Teleport Utilities
    // -------------------------

    Vector3 SampleSpawnLocal()
    {
        float spawnAreaFrac = Academy.Instance.EnvironmentParameters.GetWithDefault("spawn_area_frac", spawnAreaFracDefault);
        float limit = (arenaHalfSize - wallPadding) * spawnAreaFrac;

        float x = Random.Range(-limit, limit);
        float z = Random.Range(-limit, limit);

        // y is set later using floor + collider size
        return new Vector3(x, 0f, z);
    }

    Vector3 LocalToWorld(Vector3 localPos) => arenaRoot.TransformPoint(localPos);

    Quaternion YawLocalToWorld(float yawDeg) => arenaRoot.rotation * Quaternion.Euler(0f, yawDeg, 0f);

    Collider GetPrimaryNonTriggerCollider(BattleBotAgent a)
    {
        if (a == null) return null;
        var cols = a.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            if (c != null && !c.isTrigger) return c;
        }
        return null;
    }

    float GetAgentSpawnRadius(BattleBotAgent a)
    {
        if (agentRadiusOverride > 0f) return agentRadiusOverride;

        var col = GetPrimaryNonTriggerCollider(a);
        if (col == null) return 0.5f;

        // Use horizontal extents as radius estimate
        var e = col.bounds.extents;
        return Mathf.Max(e.x, e.z);
    }

    float GetAgentHalfHeight(BattleBotAgent a)
    {
        var col = GetPrimaryNonTriggerCollider(a);
        if (col == null) return 0.5f;
        return col.bounds.extents.y;
    }

    float GetFloorTopYAtXZ(Vector3 worldXZ)
    {
        if (floorCollider != null)
            return floorCollider.bounds.max.y;

        // Fallback: raycast down from above arena
        float rayStartY = arenaRoot.position.y + 10f;
        var origin = new Vector3(worldXZ.x, rayStartY, worldXZ.z);

        if (Physics.Raycast(origin, Vector3.down, out var hit, 50f, floorMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return arenaRoot.position.y;
    }

    bool IsFreeWorld(Vector3 worldPos, float radius)
    {
        // Use OverlapSphere so we can safely ignore our own agents even if they are in the mask.
        var hits = Physics.OverlapSphere(worldPos, radius, spawnBlockers, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h == null) continue;

            // Ignore our two agents' colliders (old positions shouldn't block spawn sampling)
            if (agentA != null && h.transform.IsChildOf(agentA.transform)) continue;
            if (agentB != null && h.transform.IsChildOf(agentB.transform)) continue;

            return false;
        }
        return true;
    }

    Vector3 FindSpawnLocal(float selfRadius, Vector3? otherLocal, float requiredSeparation)
    {
        for (int t = 0; t < spawnTries; t++)
        {
            var p = SampleSpawnLocal();

            if (otherLocal.HasValue && Vector3.Distance(p, otherLocal.Value) < requiredSeparation)
                continue;

            // Check blockers at the intended world position (with a safe y just above floor).
            var world = LocalToWorld(p);
            float floorTop = GetFloorTopYAtXZ(world);
            float halfH = Mathf.Max(0.05f, GetAgentHalfHeight(selfRadius == GetAgentSpawnRadius(agentA) ? agentA : agentB)); // best-effort
            world.y = floorTop + halfH + spawnSkin;

            if (!IsFreeWorld(world, selfRadius))
                continue;

            return p;
        }

        // fallback corners
        if (!otherLocal.HasValue) return new Vector3(-arenaHalfSize + wallPadding, 0f, -arenaHalfSize + wallPadding);
        return new Vector3(arenaHalfSize - wallPadding, 0f, arenaHalfSize - wallPadding);
    }

    void PlaceAgentsNonOverlapping()
    {
        float rA = GetAgentSpawnRadius(agentA);
        float rB = GetAgentSpawnRadius(agentB);
        float required = rA + rB + separationMargin;

        var pALocal = FindSpawnLocal(rA, null, 0f);
        var pBLocal = FindSpawnLocal(rB, pALocal, required);

        TeleportAgent(agentA, LocalToWorld(pALocal), YawLocalToWorld(Random.Range(0, 360)), rA);
        TeleportAgent(agentB, LocalToWorld(pBLocal), YawLocalToWorld(Random.Range(0, 360)), rB);

        Physics.SyncTransforms();
    }

    void TeleportAgent(BattleBotAgent a, Vector3 worldPos, Quaternion worldRot, float spawnRadius)
    {
        if (a == null) return;

        // Force yaw-only rotation (no pitch/roll)
        float yaw = worldRot.eulerAngles.y;
        worldRot = Quaternion.Euler(0f, yaw, 0f);

        // Safe y above floor using collider half-height
        float halfH = Mathf.Max(0.05f, GetAgentHalfHeight(a));
        float floorTop = GetFloorTopYAtXZ(worldPos);
        worldPos.y = floorTop + halfH + spawnSkin;

        var rb = a.GetComponent<Rigidbody>();
        if (rb == null)
        {
            a.transform.SetPositionAndRotation(worldPos, worldRot);
            return;
        }

        // Hard-teleport: avoid solver keeping old contacts and torques
        rb.detectCollisions = false;
        rb.isKinematic = true;

        rb.position = worldPos;
        rb.rotation = worldRot;

        Physics.SyncTransforms();

        // If we still overlap due to floor/edge, nudge upward a few times
        for (int i = 0; i < verticalResolveTries; i++)
        {
            if (IsFreeWorld(rb.position, spawnRadius))
                break;

            rb.position += Vector3.up * verticalResolveStep;
            Physics.SyncTransforms();
        }

        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.detectCollisions = true;
        rb.WakeUp();
    }
}
