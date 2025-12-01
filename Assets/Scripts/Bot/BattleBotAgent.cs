using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;

public class BattleAgent : Agent
{
    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float turnSpeed = 200f;
    public float acceleration = 5f;

    [Header("Boost Settings")]
    public float boostMultiplier = 2f;
    public float boostDuration = 2f;
    public float boostCooldown = 5f;
    
    private bool isBoosting = false;
    private bool canBoost = true;

    Rigidbody rBody;

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

        // Random Spawn
        transform.localPosition = new Vector3(Random.Range(-5f, 5f), 0.5f, Random.Range(-5f, 5f));
        transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        var localVel = transform.InverseTransformDirection(rBody.linearVelocity);
        sensor.AddObservation(localVel.x);
        sensor.AddObservation(localVel.z);

        sensor.AddObservation(canBoost ? 1.0f : 0.0f);
        sensor.AddObservation(isBoosting ? 1.0f : 0.0f); 
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

        yield return new WaitForSeconds(boostDuration);

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