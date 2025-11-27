using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class BattleBotAgent : Agent
{
    [Header("Stats")]
    public float moveSpeed = 10f;
    public float turnSpeed = 200f;
    public float boostMultiplier = 2f;
    public float boostDuration = 2f;
    public float boostCooldown = 5f;
    
    [Header("State")]
    public int teamID; // 0 for Team A, 1 for Team B
    public int balloons = 2;
    private bool isDead = false;
    private float boostTimer = 0f;
    private float cooldownTimer = 0f;

    private StateEventManager stateManager;
    private Rigidbody rb;

    // on intialize agent

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();

        // subscribe to all state events
        stateManager = GetComponentInChildren<StateEventManager>();
        if (stateManager == null)
        {
            Debug.LogError("BattleBotAgent: No StateEventManager found in children!");
            return;
        }
        stateManager.OnSelfBalloonPopped += handleRewardOnSelfBalloonPopped;
        stateManager.OnSelfBalloonRestored += handleRewardOnBalloonGained;
        stateManager.OnEnemyBalloonPopped += handleRewardOnEnemyBalloonPopped;

    }
    // on destroy gameObject
    private void OnDestroy()
    {
        // unsubscribe to all state events
        stateManager.OnSelfBalloonPopped -= handleRewardOnSelfBalloonPopped;
        stateManager.OnSelfBalloonRestored -= handleRewardOnBalloonGained;
        stateManager.OnEnemyBalloonPopped -= handleRewardOnEnemyBalloonPopped;

    }

    public override void OnEpisodeBegin()
    {
        // Reset Health, Position, and Physics
        balloons = 2;
        isDead = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        // TODO: Add logic to respawn at random spawn points
        // TODO: Reactivate balloon visuals
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (isDead) return;

        // Observe self stats
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));
        sensor.AddObservation(balloons);
        sensor.AddObservation(cooldownTimer > 0 ? 1 : 0); // Is boost available?
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isDead) return;

        // 1. Movement Actions
        float moveSignal = actions.ContinuousActions[0];
        float turnSignal = actions.ContinuousActions[1];

        // 2. Ability Actions
        int boostSignal = actions.DiscreteActions[0]; 
        
        // Boost Logic
        float currentSpeed = moveSpeed;
        if (boostSignal == 1 && cooldownTimer <= 0)
        {
            boostTimer = boostDuration;
            cooldownTimer = boostCooldown;
        }

        if (boostTimer > 0)
        {
            currentSpeed *= boostMultiplier;
            boostTimer -= Time.fixedDeltaTime;
        }
        if (cooldownTimer > 0) cooldownTimer -= Time.fixedDeltaTime;

        // Physics Application
        rb.AddForce(transform.forward * moveSignal * currentSpeed, ForceMode.Force);
        transform.Rotate(Vector3.up, turnSignal * turnSpeed * Time.fixedDeltaTime);

        // Rewards (Existential penalty to encourage finishing quickly)
        AddReward(-0.001f); 
    }

    // Basic Heuristic for manual testing
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Vertical");
        continuousActions[1] = Input.GetAxis("Horizontal");
        
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Logic: If collision.gameObject is "Spike" and opposing team -> Pop Balloon
        // If balloons == 0 -> Die()
    }

    private void Die()
    {
        isDead = true;
        // Disable Agent Script logic but keep Rigidbody/Collider active
        // Change tag to "DeadBot"
        EndEpisode(); // Or stay in scene depending on training mode
    }


    // reward functions
    private void handleRewardOnBalloonGained(BalloonObject balloon)
    {
        Debug.Log("BattleBotAgent: Reward +1, balloon gained");
        AddReward(+1f);
    }

    private void handleRewardOnSelfBalloonPopped(BalloonObject balloon)
    {
        Debug.Log("BattleBotAgent: Reward -1, balloon popped");
        AddReward(-1f);
    }

    private void handleRewardOnEnemyBalloonPopped(BalloonObject balloon)
    {
        Debug.Log("BattleBotAgent: Reward +3, enemy balloon popped");
        AddReward(+3f);
    }
}