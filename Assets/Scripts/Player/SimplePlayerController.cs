using UnityEngine;

public class SimplePlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 720f;
    
    private Animator animator;
    private PlayerHealth playerHealth;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (playerHealth == null || !playerHealth.IsDead)
        {
            if (Input.GetKey(KeyCode.A) ||
                Input.GetKey(KeyCode.LeftArrow))
                horizontal = -1f;

            if (Input.GetKey(KeyCode.D) ||
                Input.GetKey(KeyCode.RightArrow))
                horizontal = 1f;

            if (Input.GetKey(KeyCode.W) ||
                Input.GetKey(KeyCode.UpArrow))
                vertical = 1f;

            if (Input.GetKey(KeyCode.S) ||
                Input.GetKey(KeyCode.DownArrow))
                vertical = -1f;
        }

        Vector3 direction =
            new Vector3(horizontal, 0f, vertical);

        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        transform.position +=
            direction * moveSpeed * Time.deltaTime;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation =
                Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime
                );
        }

        if (animator != null)
        {
            animator.SetFloat(
                SpeedHash,
                direction.magnitude * moveSpeed,
                0.1f,
                Time.deltaTime
            );
        }
    }
}