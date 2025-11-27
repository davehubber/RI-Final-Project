using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using NUnit.Framework;

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

    [Header("Debug")]
    [SerializeField] private bool debugStartDead = false;

    private bool isDead = false;

    private float boostTimer = 0f;
    private float cooldownTimer = 0f;

    private StateEventManager stateManager;
    private BalloonManager balloonManager;
    private Rigidbody rb;

    // on intialize agent

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();

        balloonManager = GetComponent<BalloonManager>();
        if (balloonManager == null)
        {
            Debug.LogError("BattleBotAgent: No BalloonManager found on GameObject!");
            return;
        }

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
        stateManager.OnSelfBotDeath += handleSelfBotDeath;
    }
    // on destroy gameObject
    private void OnDestroy()
    {
        // unsubscribe to all state events
        stateManager.OnSelfBalloonPopped -= handleRewardOnSelfBalloonPopped;
        stateManager.OnSelfBalloonRestored -= handleRewardOnBalloonGained;
        stateManager.OnEnemyBalloonPopped -= handleRewardOnEnemyBalloonPopped;
        stateManager.OnSelfBotDeath -= handleSelfBotDeath;

    }

    public override void OnEpisodeBegin()
    {
        // isDead = false;
        isDead = debugStartDead;

        // Reset physics
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // assign alive tag
        gameObject.tag = "ActiveBot";

        // reset balloons
        balloonManager.ResetBalloons();

        // reset timers
        boostTimer = 0f;
        cooldownTimer = 0f;

        // TODO: Add logic to respawn at random spawn points
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Observe self stats
        sensor.AddObservation(transform.InverseTransformDirection(rb.linearVelocity));
        sensor.AddObservation(balloonManager.currentBalloons);
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

    private void handleSelfBotDeath(BalloonObject balloon)
    {
        Debug.Log("BattleBotAgent: Reward -5, bot died");
        
        isDead = true;

        //disable physics controls
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = false; // stay as dead weight

        //assign dead tag
        gameObject.tag = "DeadBot";

        AddReward(-5f); // Penalty for dying // makes sense ?? // maybe sacrifice for others is useful ?? // should we penalize it ???
    }

    //
    private void HandleTeamDeath()
    {
        // TODO: detect when full team dead
        EndEpisode();
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