using UnityEngine;
using Unity.MLAgents;
using System.Collections;

public class BattleArena : MonoBehaviour
{
    [Header("Agents")]
    public BattleBotAgent agentA;
    public BattleBotAgent agentB;

    [Header("Arena Visuals")]
    public MeshRenderer floorRenderer; 
    public Material defaultFloorMaterial; 
    public float winFlashDuration = 1.0f;
    
    private SimpleMultiAgentGroup agentGroup;
    private bool matchIsEnding = false;

    void Start()
    {
        agentGroup = new SimpleMultiAgentGroup();
        agentGroup.RegisterAgent(agentA);
        agentGroup.RegisterAgent(agentB);

        if(floorRenderer != null) floorRenderer.material = defaultFloorMaterial;
        
        ResetScene();
    }

    public void OnBalloonPopped(BattleBotAgent victim, BattleBotAgent attacker)
    {
        if (matchIsEnding) return;

        attacker.AddReward(0.5f);
        victim.AddReward(-0.2f);

        if (victim.GetActiveBalloonCount() <= 0)
        {
            attacker.AddReward(1.0f);
            victim.AddReward(-1.0f);
            
            agentGroup.EndGroupEpisode();
            
            StartCoroutine(EndMatchSequence(attacker.teamMaterial));
        }
    }

    IEnumerator EndMatchSequence(Material winnerMat)
    {
        if (floorRenderer != null && winnerMat != null)
        {
            floorRenderer.material = winnerMat;
        }

        yield return new WaitForSeconds(winFlashDuration);

        if (floorRenderer != null)
        {
            floorRenderer.material = defaultFloorMaterial;
        }

        ResetScene();
        matchIsEnding = false;
    }

    public void ResetScene()
    {
        agentA.ResetAgent();
        agentB.ResetAgent();

        agentA.transform.localPosition = new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
        agentB.transform.localPosition = new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));

        agentA.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        agentB.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
    }
}