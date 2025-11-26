using UnityEngine;

public class RestoreBalloons : MonoBehaviour
{
    public float spawnerCooldwn = 10f;
    private float cooldownTimer = 0f;

    private Renderer spawnerRenderer;
    private Collider spawnerCollider;
    private bool isActive = true;

    private void Awake()
    {
        spawnerRenderer = GetComponent<Renderer>();
        spawnerCollider = GetComponent<Collider>();
    }

    private void Update()
    {
        if (!isActive)
        {
        
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                SpawnBalloon();
            }
        }
    }
    public void OnTriggerEnter(Collider other)
    {
        Debug.Log("RestoreBalloons collided with: " + other.name);
        if (other.CompareTag("ActiveBot"))
        {
            if (isActive)
            {
                DespawnBalloon();
                var balloonManager = other.GetComponent<BalloonManager>();
                Debug.Log("Found BalloonManager on ActiveBot: " + (balloonManager != null));
                if (balloonManager != null)
                {
                    balloonManager.AddBalloon();
                }
                else
                {
                    Debug.LogWarning("The collided ActiveBot does not have a BalloonManager component.");
                }
            }

        }
    }

    public void SpawnBalloon()
    {
        spawnerRenderer.enabled = true;
        spawnerCollider.enabled = true;
        isActive = true;
    }

    public void DespawnBalloon()
    {
        spawnerRenderer.enabled = false;
        spawnerCollider.enabled = false;
        cooldownTimer = spawnerCooldwn;
        isActive = false;
    }
}
