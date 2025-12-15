using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 1v1 BattleBot agent.
/// Movement/turning is Rigidbody-driven and yaw-only to avoid tilt and floor-sticking.
/// </summary>
public class BattleBotAgent : Agent
{
    [Header("Game References")]
    public BattleArena arena;
    public List<BalloonUnit> myBalloons;

    [Header("Visual Settings")]
    public Material teamMaterial;
    public MeshRenderer bodyRenderer;

    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float turnSpeed = 200f;
    public float acceleration = 50f;

    [Header("Vertical Safety (if gravity is on)")]
    [Tooltip("Clamp vertical speed to prevent solver explosions from launching the bot.")]
    public float maxVerticalSpeed = 5f;

    [Header("Upright Safety")]
    [Tooltip("If bot ever gets tilted, snap back upright (yaw preserved).")]
    public bool snapUprightIfTilted = true;

    [Tooltip("Tilt threshold (degrees) above which we snap upright.")]
    public float tiltSnapDegrees = 2f;

    [Header("Boost Settings")]
    public float boostMultiplier = 2f;
    public float boostDuration = 2f;
    public float boostCooldown = 5f;

    [Header("Shaping Penalties (Capped)")]
    public float stepPenalty = -0.00005f;
    public float maxStepPenaltyPerEpisode = -0.2f;

    public float wallHitPenalty = -0.001f;
    public float maxWallPenaltyPerEpisode = -0.2f;

    private bool isBoosting = false;
    private bool canBoost = true;
    private Color originalColor;

    private Rigidbody rBody;

    private float stepPenaltyAcc = 0f; // negative
    private float wallPenaltyAcc = 0f; // negative

    void Start()
    {
        if (bodyRenderer != null && teamMaterial != null)
        {
            bodyRenderer.material = teamMaterial;
            originalColor = teamMaterial.color;
        }
    }

    public override void Initialize()
    {
        rBody = GetComponent<Rigidbody>();
        if (rBody == null)
        {
            Debug.LogError($"{nameof(BattleBotAgent)} on {name} requires a Rigidbody.");
            return;
        }

        // Enforce "no tipping" in code too (matches your plan: constrain rotations, not y position).
        rBody.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Recommended for stable contact with the floor during ML training.
        rBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rBody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public override void OnEpisodeBegin()
    {
        ResetAgent();
    }

    public void ResetAgent()
    {
        if (rBody != null)
        {
            rBody.linearVelocity = Vector3.zero;
            rBody.angularVelocity = Vector3.zero;

            // Ensure we start upright even if something went wrong last episode
            float yaw = rBody.rotation.eulerAngles.y;
            rBody.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        StopAllCoroutines();
        canBoost = true;
        isBoosting = false;

        stepPenaltyAcc = 0f;
        wallPenaltyAcc = 0f;

        if (bodyRenderer != null && teamMaterial != null)
        {
            bodyRenderer.material = teamMaterial;
            originalColor = teamMaterial.color;
        }

        if (myBalloons != null)
        {
            foreach (var balloon in myBalloons)
            {
                if (balloon != null) balloon.ResetBalloon();
            }
        }
    }

    public bool RestoreBalloon()
    {
        if (myBalloons == null) return false;

        foreach (var balloon in myBalloons)
        {
            if (balloon != null && !balloon.gameObject.activeSelf)
            {
                balloon.ResetBalloon();
                return true;
            }
        }
        return false;
    }

    public int GetActiveBalloonCount()
    {
        int count = 0;
        if (myBalloons == null) return count;

        foreach (var b in myBalloons)
        {
            if (b != null && b.gameObject.activeSelf) count++;
        }
        return count;
    }

    void OnTriggerEnter(Collider other)
    {
        if (arena != null && arena.MatchIsEnding) return;

        if (other.CompareTag("Balloon"))
        {
            BalloonUnit hitBalloon = other.GetComponent<BalloonUnit>();

            if (hitBalloon != null && hitBalloon.owner != null && hitBalloon.owner != this)
            {
                hitBalloon.Pop();
                if (arena != null) arena.OnBalloonPopped(hitBalloon.owner, this);
            }
        }
    }

    public void ApplyWallHitPenalty()
    {
        if (arena != null && arena.MatchIsEnding) return;
        ApplyCappedPenalty(wallHitPenalty, ref wallPenaltyAcc, maxWallPenaltyPerEpisode);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (arena != null && arena.MatchIsEnding) return;

        if (collision.gameObject.CompareTag("Wall"))
        {
            ApplyWallHitPenalty();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        var localVel = transform.InverseTransformDirection(rBody != null ? rBody.linearVelocity : Vector3.zero);
        sensor.AddObservation(localVel.x);
        sensor.AddObservation(localVel.z);
        sensor.AddObservation(canBoost ? 1.0f : 0.0f);
        sensor.AddObservation(isBoosting ? 1.0f : 0.0f);
        sensor.AddObservation(GetActiveBalloonCount() / 3.0f);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (arena != null && arena.MatchIsEnding) return;
        if (rBody == null) return;

        ApplyCappedPenalty(stepPenalty, ref stepPenaltyAcc, maxStepPenaltyPerEpisode);

        float moveSignal = actionBuffers.ContinuousActions[0];
        float turnSignal = actionBuffers.ContinuousActions[1];
        int boostSignal = actionBuffers.DiscreteActions[0];

        if (boostSignal == 1 && canBoost)
        {
            StartCoroutine(ActivateBoost());
        }

        // --- Yaw-only turning via Rigidbody (no Transform.Rotate) ---
        float yawDelta = turnSignal * turnSpeed * Time.fixedDeltaTime;
        Quaternion yawRot = Quaternion.AngleAxis(yawDelta, Vector3.up) * rBody.rotation;

        // Force pitch/roll to zero (keeps yaw)
        float yaw = yawRot.eulerAngles.y;
        rBody.MoveRotation(Quaternion.Euler(0f, yaw, 0f));

        // --- Planar movement (XZ driven), Y preserved + clamped ---
        float currentMaxSpeed = isBoosting ? moveSpeed * boostMultiplier : moveSpeed;
        Vector3 planarTarget = transform.forward * (moveSignal * currentMaxSpeed);

        Vector3 v = rBody.linearVelocity;
        float yVel = Mathf.Clamp(v.y, -maxVerticalSpeed, maxVerticalSpeed);

        Vector3 desired = new Vector3(planarTarget.x, yVel, planarTarget.z);
        Vector3 newVel = Vector3.MoveTowards(v, desired, acceleration * Time.fixedDeltaTime);
        rBody.linearVelocity = newVel;

        // --- Last-resort upright snap (should almost never trigger once spawns are fixed) ---
        if (snapUprightIfTilted)
        {
            float tilt = Vector3.Angle(transform.up, Vector3.up);
            if (tilt > tiltSnapDegrees)
            {
                float currentYaw = rBody.rotation.eulerAngles.y;
                rBody.rotation = Quaternion.Euler(0f, currentYaw, 0f);

                Vector3 av = rBody.angularVelocity;
                rBody.angularVelocity = new Vector3(0f, av.y, 0f);
            }
        }
    }

    private void ApplyCappedPenalty(float perEventPenalty, ref float accumulator, float cap)
    {
        if (perEventPenalty == 0f) return;
        if (cap == 0f) return;

        if (cap > 0f)
        {
            AddReward(perEventPenalty);
            accumulator += perEventPenalty;
            return;
        }

        if (accumulator <= cap) return;

        float remaining = cap - accumulator;             // negative or zero
        float delta = Mathf.Max(perEventPenalty, remaining); // clamp to not exceed cap

        AddReward(delta);
        accumulator += delta;
    }

    IEnumerator ActivateBoost()
    {
        canBoost = false;
        isBoosting = true;

        if (bodyRenderer != null) bodyRenderer.material.color = Color.white;

        yield return new WaitForSeconds(boostDuration);

        if (bodyRenderer != null) bodyRenderer.material.color = originalColor;
        isBoosting = false;

        yield return new WaitForSeconds(boostCooldown);
        canBoost = true;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        var discreteActionsOut = actionsOut.DiscreteActions;

        continuousActionsOut[0] = Input.GetAxis("Vertical");
        continuousActionsOut[1] = Input.GetAxis("Horizontal");
        discreteActionsOut[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }
}
