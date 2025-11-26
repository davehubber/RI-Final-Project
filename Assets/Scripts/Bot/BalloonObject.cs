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
        parentBalloonManager = GetComponentInParent<BalloonManager>();

    }

    // Update is called once per frame
    public void Pop()
    {
        if (!isActive) return;

        isActive = false; 
        balloonRenderer.enabled = false;
        balloonCollider.enabled = false;

        // parentBalloonManager.PopBalloon(this); // need to notify parent manager?
    }

    public void Restore()
    {
        isActive = true;
        balloonRenderer.enabled = true;
        balloonCollider.enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Spike"))
        {
            Pop();
        }
    }
}
