using UnityEngine;

public class BalloonObject : MonoBehaviour
{
    private Renderer balloonRenderer;
    private Collider balloonCollider;
    public bool isActive { get; private set; } = true;
    private BalloonManager parentBalloonManager;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        balloonRenderer = GetComponent<Renderer>();
        balloonCollider = GetComponent<Collider>();
        if (!balloonCollider.enabled && !balloonRenderer.enabled) isActive = false;
        parentBalloonManager = GetComponentInParent<BalloonManager>();

    }

    // hides balloon and notifies parent manager
    public void Pop()
    {
        if (!isActive) return;

        Hide();

        parentBalloonManager.PopBalloon(this); // notify parent manager
    }

    // restores balloon visual and collider
    public void Restore()
    {
        isActive = true;
        balloonRenderer.enabled = true;
        balloonCollider.enabled = true;
    }

    // hides balloon visuals and collider
    public void Hide()
    {
        isActive = false;
        balloonRenderer.enabled = false;
        balloonCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Spike"))
        {
            Pop();
        }
    }
}
