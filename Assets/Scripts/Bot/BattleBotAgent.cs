using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 1v1 BattleBot agent.
/// Rewards are kept small (shaping) and capped so they can't overwhelm terminal win/loss rewards in self-play.
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

    [Header("Boost Settings")]
    public float boostMultiplier = 2f;
    public float boostDuration = 2f;
    public float boostCooldown = 5f;

    [Header("Shaping Penalties (Capped)")]
    [Tooltip("Small per-step penalty to discourage stalling. Keep tiny and capped so it can't flip win/loss sign.")]
    public float stepPenalty = -0.00005f;

    [Tooltip("Maximum total step penalty per episode (negative). Example: -0.2f means the agent can't lose more than 0.2 from step penalties.")]
    public float maxStepPenaltyPerEpisode = -0.2f;

    [Tooltip("Penalty applied on each wall collision.")]
    public float wallHitPenalty = -0.005f;

    [Tooltip("Maximum total wall-hit penalty per episode (negative).")]
    public float maxWallPenaltyPerEpisode = -0.5f;

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
        }
    }

    public override void OnEpisodeBegin()
    {
        // Ensure we return to a clean state even if the arena forgets to reset something.
        ResetAgent();
    }

    /// <summary>
    /// Resets agent internal state + balloons for a new episode.
    /// Called by BattleArena.ResetScene() and also from OnEpisodeBegin().
    /// </summary>
    public void ResetAgent()
    {
        if (rBody != null)
        {
            rBody.linearVelocity = Vector3.zero;
            rBody.angularVelocity = Vector3.zero;
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
                if (balloon != null)
                {
                    balloon.ResetBalloon();
                }
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

                if (arena != null)
                {
                    arena.OnBalloonPopped(hitBalloon.owner, this);
                }
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (arena != null && arena.MatchIsEnding) return;

        if (collision.gameObject.CompareTag("Wall"))
        {
            ApplyCappedPenalty(wallHitPenalty, ref wallPenaltyAcc, maxWallPenaltyPerEpisode);
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

        // Small time penalty to discourage stalling (capped).
        ApplyCappedPenalty(stepPenalty, ref stepPenaltyAcc, maxStepPenaltyPerEpisode);

        float moveSignal = actionBuffers.ContinuousActions[0];
        float turnSignal = actionBuffers.ContinuousActions[1];
        int boostSignal = actionBuffers.DiscreteActions[0];

        transform.Rotate(0, turnSignal * turnSpeed * Time.fixedDeltaTime, 0);

        if (boostSignal == 1 && canBoost)
        {
            StartCoroutine(ActivateBoost());
        }

        float currentMaxSpeed = isBoosting ? moveSpeed * boostMultiplier : moveSpeed;
        Vector3 targetVelocity = transform.forward * moveSignal * currentMaxSpeed;

        if (rBody != null)
        {
            Vector3 newVelocity = Vector3.MoveTowards(rBody.linearVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
            newVelocity.y = rBody.linearVelocity.y;
            rBody.linearVelocity = newVelocity;
        }
    }

    private void ApplyCappedPenalty(float perEventPenalty, ref float accumulator, float cap)
    {
        if (perEventPenalty == 0f) return;
        if (cap == 0f) return;

        // If cap is positive by mistake, treat it as "no cap".
        if (cap > 0f)
        {
            AddReward(perEventPenalty);
            accumulator += perEventPenalty;
            return;
        }

        // accumulator is negative; cap is negative. We allow accumulator down to cap (e.g., -0.5).
        if (accumulator <= cap) return;

        float remaining = cap - accumulator;      // negative or zero
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
