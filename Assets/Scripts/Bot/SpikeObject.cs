using UnityEngine;

public class SpikeObject : MonoBehaviour
{
    private StateEventManager stateManager;
    
    private void Awake()
    {
        // find StateEventManager in parent object
        stateManager = transform.parent.GetComponentInChildren<StateEventManager>();
        if (stateManager == null)
        {
            Debug.LogError("SpikeObject: No StateEventManager found in siblings!");
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Balloon")){
            Debug.Log("SpikeObject: " + gameObject.name + " hit " + other.name + ", invoking event.");
            stateManager.InvokeEnemyBalloonPopped(other.GetComponent<BalloonObject>());
        }
    }
}
