using UnityEngine;

public class FreezablePlatform : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private Vector3 moveDirection = Vector3.right;
    [SerializeField] private float distance = 3f;
    [SerializeField] private float normalSpeed = 1f;

    private bool isFrozen = false;
    private float timeCounter = 0f;
    private Vector3 startPos;

    [SerializeField] string playerTag = "Player";
    [SerializeField] Transform platform;

    // Attaches player to platform
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag.Equals(playerTag))
        {
            other.gameObject.transform.parent = platform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag.Equals(playerTag))
        {
            other.gameObject.transform.parent = null;
        }
    }

    private void Start()
    {
        startPos = transform.position; // where the obstacle transforms from
    }

    private void Update()
    {
        if (isFrozen) return;

        timeCounter += Time.deltaTime * normalSpeed;

        float ping = Mathf.PingPong(timeCounter, distance); // moves obstacle back and forth again
        transform.position = startPos + moveDirection.normalized * ping;
    }

    public void ToggleFreeze()
    {
        isFrozen = !isFrozen; // when shot at
    }

}