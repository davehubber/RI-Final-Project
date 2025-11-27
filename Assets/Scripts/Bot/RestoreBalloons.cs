using UnityEngine;

public class RestoreBalloons : MonoBehaviour
{
    public Material materialTeam0;
    public Material materialTeam1;

    public float spawnerCooldwn = 10f;
    private float cooldownTimer = 0f;

    private Renderer spawnerRenderer;
    private Collider spawnerCollider;
    private bool isActive = false;

    //Limits of the plain
    public Vector2 xRange = new Vector2(-5f, 5f);
    public Vector2 zRange = new Vector2(-5f, 5f);

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
        if (other.CompareTag("ActiveBot"))
        {
            if (isActive)
            {
                DespawnBalloon();
                var balloonManager = other.GetComponent<BalloonManager>();
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
        //Translaction of the ballon
        float randomX = Random.Range(xRange.x, xRange.y);
        float randomZ = Random.Range(zRange.x, zRange.y);
        float y = transform.position.y;
        transform.position = new Vector3(randomX, y, randomZ);


        //What material it will use
        int randomChoice = Random.Range(0, 2); // 0 or 1
        if (randomChoice == 0)
            spawnerRenderer.material = materialTeam0;
        else
            spawnerRenderer.material = materialTeam1;


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
