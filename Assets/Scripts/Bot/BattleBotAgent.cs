using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;
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

    [Header("Boost Settings")]
    public float boostMultiplier = 2f;
    public float boostDuration = 2f;
    public float boostCooldown = 5f;
    
    private bool isBoosting = false;
    private bool canBoost = true;
    private Color originalColor;

    Rigidbody rBody;

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
    }

    public override void OnEpisodeBegin()
    {
        rBody.linearVelocity = Vector3.zero;
        rBody.angularVelocity = Vector3.zero;
        StopAllCoroutines();
        canBoost = true;
        isBoosting = false;
    }

    public void ResetAgent()
    {
        foreach(var balloon in myBalloons)
        {
            balloon.ResetBalloon();
        }
    }

    public bool RestoreBalloon()
    {
        foreach (var balloon in myBalloons)
        {
            if (!balloon.gameObject.activeSelf)
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
        foreach(var b in myBalloons)
        {
            if(b.gameObject.activeSelf) count++;
        }
        return count;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Balloon"))
        {
            BalloonUnit hitBalloon = other.GetComponent<BalloonUnit>();

            if (hitBalloon != null && hitBalloon.owner != this)
            {
                hitBalloon.Pop();

                arena.OnBalloonPopped(hitBalloon.owner, this);
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            // Penalty for hitting a wall
            AddReward(-0.02f);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        var localVel = transform.InverseTransformDirection(rBody.linearVelocity);
        sensor.AddObservation(localVel.x);
        sensor.AddObservation(localVel.z);
        sensor.AddObservation(canBoost ? 1.0f : 0.0f);
        sensor.AddObservation(isBoosting ? 1.0f : 0.0f);
        sensor.AddObservation(GetActiveBalloonCount() / 3.0f);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
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
        Vector3 newVelocity = Vector3.MoveTowards(rBody.linearVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        newVelocity.y = rBody.linearVelocity.y;
        rBody.linearVelocity = newVelocity;
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