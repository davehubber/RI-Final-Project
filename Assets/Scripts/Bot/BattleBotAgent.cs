using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

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

    [Header("Vertical Safety")]
    public float maxVerticalSpeed = 5f;

    [Header("Upright Safety")]
    public bool snapUprightIfTilted = true;
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

    // State Variables
    private Rigidbody rBody;
    private Color originalColor;
    
    // Physics/Input Caching
    private float m_MoveInput;
    private float m_TurnInput;
    private bool m_BoostInput;

    // Boost Logic
    private bool isBoosting = false;
    private bool canBoost = true;
    private float m_BoostTimer = 0f;

    // Reward Accumulators
    private float stepPenaltyAcc = 0f;
    private float wallPenaltyAcc = 0f;

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
            Debug.LogError($"{nameof(BattleBotAgent)} requires a Rigidbody.");
            return;
        }

        rBody.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
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

            float yaw = rBody.rotation.eulerAngles.y;
            rBody.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // Reset Inputs
        m_MoveInput = 0f;
        m_TurnInput = 0f;
        m_BoostInput = false;

        // Reset Boost
        canBoost = true;
        isBoosting = false;
        m_BoostTimer = 0f;

        // Reset Penalties
        stepPenaltyAcc = 0f;
        wallPenaltyAcc = 0f;

        if (bodyRenderer != null && teamMaterial != null)
        {
            bodyRenderer.material = teamMaterial;
        }

        if (myBalloons != null)
        {
            foreach (var balloon in myBalloons)
            {
                if (balloon != null) balloon.ResetBalloon();
            }
        }
    }

    void FixedUpdate()
    {
        if (arena != null && arena.MatchIsEnding) return;
        if (rBody == null) return;

        HandleBoostLogic();
        HandleMovementPhysics();
        HandleUprightSnap();
    }

    private void HandleBoostLogic()
    {
        if (m_BoostTimer > 0f)
        {
            m_BoostTimer -= Time.fixedDeltaTime;
        }
        else if (isBoosting)
        {
            // Boost finished
            isBoosting = false;
            m_BoostTimer = boostCooldown;
            if (bodyRenderer != null) bodyRenderer.material.color = originalColor;
        }
        else if (!canBoost && m_BoostTimer <= 0f)
        {
            // Cooldown finished
            canBoost = true;
        }

        // Activate boost if requested
        if (m_BoostInput && canBoost)
        {
            isBoosting = true;
            canBoost = false;
            m_BoostTimer = boostDuration;
            m_BoostInput = false; // Consume input
            if (bodyRenderer != null) bodyRenderer.material.color = Color.white;
        }
    }

    private void HandleMovementPhysics()
    {
        // Rotation
        float yawDelta = m_TurnInput * turnSpeed * Time.fixedDeltaTime;
        Quaternion yawRot = Quaternion.AngleAxis(yawDelta, Vector3.up) * rBody.rotation;
        
        float yaw = yawRot.eulerAngles.y;
        rBody.MoveRotation(Quaternion.Euler(0f, yaw, 0f));

        // Translation
        float currentMaxSpeed = isBoosting ? moveSpeed * boostMultiplier : moveSpeed;
        Vector3 planarTarget = transform.forward * (m_MoveInput * currentMaxSpeed);

        Vector3 v = rBody.linearVelocity;
        float yVel = Mathf.Clamp(v.y, -maxVerticalSpeed, maxVerticalSpeed);

        Vector3 desired = new Vector3(planarTarget.x, yVel, planarTarget.z);
        Vector3 newVel = Vector3.MoveTowards(v, desired, acceleration * Time.fixedDeltaTime);
        
        rBody.linearVelocity = newVel;
    }

    private void HandleUprightSnap()
    {
        if (!snapUprightIfTilted) return;

        float tilt = Vector3.Angle(transform.up, Vector3.up);
        if (tilt > tiltSnapDegrees)
        {
            float currentYaw = rBody.rotation.eulerAngles.y;
            rBody.rotation = Quaternion.Euler(0f, currentYaw, 0f);

            Vector3 av = rBody.angularVelocity;
            rBody.angularVelocity = new Vector3(0f, av.y, 0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (arena != null && arena.MatchIsEnding) return;

        ApplyCappedPenalty(stepPenalty, ref stepPenaltyAcc, maxStepPenaltyPerEpisode);

        // Cache inputs for FixedUpdate
        m_MoveInput = actionBuffers.ContinuousActions[0];
        m_TurnInput = actionBuffers.ContinuousActions[1];
        
        if (actionBuffers.DiscreteActions[0] == 1)
        {
            m_BoostInput = true;
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

    // Helper Methods

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

    void OnCollisionStay(Collision collision)
    {
        if (arena != null && arena.MatchIsEnding) return;
        if (collision.gameObject.CompareTag("Wall"))
        {
            ApplyWallHitPenalty();
        }
    }

    private void ApplyCappedPenalty(float perEventPenalty, ref float accumulator, float cap)
    {
        if (perEventPenalty == 0f || cap == 0f) return;

        if (cap > 0f) // Positive cap means we are capping a reward, not penalty (rare but handled)
        {
            AddReward(perEventPenalty);
            accumulator += perEventPenalty;
            return;
        }

        if (accumulator <= cap) return; // Cap reached

        float remaining = cap - accumulator;
        float delta = Mathf.Max(perEventPenalty, remaining);

        AddReward(delta);
        accumulator += delta;
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