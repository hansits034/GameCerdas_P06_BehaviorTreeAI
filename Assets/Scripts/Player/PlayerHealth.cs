using UnityEngine;

public class PlayerHealth : MonoBehaviour, IHealth
{
    [SerializeField]
    private int maxHealth = 100;

    private int currentHealth;

    private Animator animator;

    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DieHash = Animator.StringToHash("Die");

    public bool IsDead => currentHealth <= 0;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead) return;

        currentHealth -= damage;

        currentHealth =
            Mathf.Clamp(
                currentHealth,
                0,
                maxHealth
            );

        Debug.Log(
            "Player Health = "
            + currentHealth
        );

        if (currentHealth <= 0)
        {
            Debug.Log("Player Dead");

            if (animator != null)
            {
                animator.SetTrigger(DieHash);
            }
        }
        else if (animator != null)
        {
            animator.SetTrigger(HitHash);
        }
    }
}