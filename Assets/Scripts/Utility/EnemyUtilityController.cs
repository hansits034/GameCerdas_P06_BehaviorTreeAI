using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Attack, Chase, Flee, Patrol
public class EnemyUtilityController : MonoBehaviour, IHealth
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private Transform safePoint;

    [Header("Perception")]
    [SerializeField] private float visionRange = 10f;
    [SerializeField] private float visionAngle = 90f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Combat")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private int attackDamage = 10;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int lowHealthThreshold = 30;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float fleeSpeed = 5f;

    [Header("Utility AI")]
    [SerializeField] private List<Action> actions;

    [Header("Debug")]
    [SerializeField] private string currentAction = "None";

    private NavMeshAgent agent;

    private int currentHealth;
    private int currentPatrolIndex = 0;

    private EnemyBlackboard blackboard = new();

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        currentHealth = maxHealth;
    }

    private void Start()
    {
        actions = new()
        {
            new AttackAction(),
            new ChaseAction(),
            new FleeAction(),
            new PatrolAction()
        };
    }

    private void Update()
    {
        UpdatePerception();
        
        Action bestAction = null;
        float bestScore = -1;

        foreach(Action action in actions)
        {
            float score = action.CalculateScore(this);
            if(score > bestScore)
            {
                bestAction = action;
                bestScore = score;
            }
        }

        bestAction.Execute(this);

        UpdateAnimator();
    }

    // Modul 62
    private void UpdatePerception()
    {
        blackboard.player = player;
        blackboard.canSeePlayer = CanSeePlayer();
        blackboard.distanceToPlayer = player != null
            ? Vector3.Distance(transform.position, player.position)
            : Mathf.Infinity;
        blackboard.health = currentHealth;
        blackboard.healthLow = currentHealth <= lowHealthThreshold;

        if (blackboard.canSeePlayer)
        {
            blackboard.lastSeenPosition = player.position;
            blackboard.hasLastSeenPosition = true;
        }
    }

    // ==================================================
    // CONDITIONS
    // ==================================================

    private bool IsHealthLow()
    {
        return currentHealth <= lowHealthThreshold;
    }

    private bool IsPlayerInAttackRange()
    {
        if (player == null)
            return false;

        float distance =
            Vector3.Distance(
                transform.position,
                player.position
            );

        return distance <= attackRange;
    }

    public class AttackAction : Action
    {
        public override float CalculateScore(EnemyUtilityController controller)
        {
            float attackRangeScore;
            attackRangeScore = Mathf.Clamp(1 - (controller.blackboard.distanceToPlayer / controller.attackRange), 0f, 1f);

            int canSeePlayer;
            canSeePlayer = controller.CanSeePlayer() ? 1 : 0;
            
            Debug.Log(Mathf.Clamp(attackRangeScore * 0.5f + canSeePlayer * 0.4f, 0f, 1f));
            return Mathf.Clamp(attackRangeScore * 0.5f + canSeePlayer * 0.4f, 0f, 1f);
        }

        public override void Execute(EnemyUtilityController controller)
        {
            if (controller.player == null)
                return;

            controller.currentAction = "ATTACK";
            
            if (controller.animator != null)
            {
                controller.animator.SetTrigger(AttackHash);
            }
        }
    }
    
    public class ChaseAction : Action
    {
        public override float CalculateScore(EnemyUtilityController controller)
        {
            int canSeePlayer;
            canSeePlayer = controller.CanSeePlayer() ? 1 : 0;
            
            Debug.Log(Mathf.Clamp(canSeePlayer * 0.5f, 0f, 1f));
            return Mathf.Clamp(canSeePlayer * 0.5f, 0f, 1f);
        }

        public override void Execute(EnemyUtilityController controller)
        {
            if (controller.player == null)
                return;

            controller.currentAction = "CHASE";

            controller.agent.isStopped = false;
            controller.agent.speed = controller.chaseSpeed;
            controller.agent.stoppingDistance =
                controller.attackRange * 0.8f;

            controller.agent.SetDestination(controller.player.position);
        }
    }
    
    public class FleeAction : Action
    {
        public override float CalculateScore(EnemyUtilityController controller)
        {
            float fleeHealthScore;
            fleeHealthScore = Mathf.Clamp(1 - (controller.currentHealth/controller.lowHealthThreshold), 0f, 1f);

            Debug.Log(fleeHealthScore);
            return fleeHealthScore;
        }

        public override void Execute(EnemyUtilityController controller)
        {
            if (controller.safePoint == null)
                return;

            controller.currentAction = "FLEE";

            controller.agent.isStopped = false;
            controller.agent.speed = controller.fleeSpeed;
            controller.agent.stoppingDistance = 0.5f;

            controller.agent.SetDestination(
                controller.safePoint.position
            );

            if (!controller.agent.pathPending &&
                controller.agent.remainingDistance <= 0.7f)
            {
                controller.agent.isStopped = true;
            }
        }
    }
    
    public class PatrolAction : Action
    {
        public override float CalculateScore(EnemyUtilityController controller)
        {
            Debug.Log(0.2f);
            return 0.2f;
        }

        public override void Execute(EnemyUtilityController controller)
        {
            if (controller.patrolPoints == null ||
                controller.patrolPoints.Length == 0)
            {
                return;
            }

            controller.currentAction = "PATROL";

            controller.agent.isStopped = false;
            controller.agent.speed = controller.patrolSpeed;
            controller.agent.stoppingDistance = 0.2f;

            Transform target =
                controller.patrolPoints[controller.currentPatrolIndex];

            controller.agent.SetDestination(target.position);

            if (!controller.agent.pathPending &&
                controller.agent.remainingDistance <= 0.5f)
            {
                controller.currentPatrolIndex++;

                if (controller.currentPatrolIndex
                    >= controller.patrolPoints.Length)
                {
                    controller.currentPatrolIndex = 0;
                }
            }
        }
    }

    private bool CanSeePlayer()
    {
        if (player == null)
            return false;

        Vector3 eyePosition =
            transform.position
            + Vector3.up * 1.5f;

        Vector3 targetPosition =
            player.position
            + Vector3.up * 1f;

        Vector3 directionToPlayer =
            targetPosition - eyePosition;

        float distanceToPlayer =
            directionToPlayer.magnitude;

        // Player terlalu jauh.
        if (distanceToPlayer > visionRange)
            return false;

        // Player berada di luar Field of View.
        float angle =
            Vector3.Angle(
                transform.forward,
                directionToPlayer
            );

        if (angle > visionAngle * 0.5f)
            return false;

        // Periksa apakah ada obstacle.
        bool blocked =
            Physics.Raycast(
                eyePosition,
                directionToPlayer.normalized,
                distanceToPlayer,
                obstacleMask
            );

        return !blocked;
    }

    // ==================================================
    // ACTIONS
    // ==================================================

    public void DealAttackDamage()
    {
        if (player == null || !blackboard.canSeePlayer || blackboard.distanceToPlayer > attackRange)
        {
            Debug.Log(name + " attack missed");
            return;
        }

        Debug.Log(name + " attacks Player! Damage = " + attackDamage);

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();

        if (playerHealth != null)
            playerHealth.TakeDamage(attackDamage);
    }

    // ==================================================
    // HEALTH
    // ==================================================

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        currentHealth =
            Mathf.Clamp(
                currentHealth,
                0,
                maxHealth
            );

        if (animator != null)
        {
            animator.SetTrigger(HitHash);
        }

        Debug.Log(
            name
            + " Health = "
            + currentHealth
        );
    }

    [ContextMenu("Test Damage 25")]
    private void TestDamage25()
    {
        TakeDamage(25);
    }

    [ContextMenu("Reset Health")]
    private void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    // ==================================================
    // ANIMATION
    // ==================================================

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        animator.SetFloat(
            SpeedHash,
            agent.velocity.magnitude,
            0.1f,
            Time.deltaTime
        );
    }

    // ==================================================
    // GIZMOS
    // ==================================================

    private void OnDrawGizmosSelected()
    {
        // Vision Range
        Gizmos.DrawWireSphere(
            transform.position,
            visionRange
        );

        // Attack Range
        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );

        // FOV direction
        Vector3 leftDirection =
            Quaternion.Euler(
                0,
                -visionAngle * 0.5f,
                0
            )
            * transform.forward;

        Vector3 rightDirection =
            Quaternion.Euler(
                0,
                visionAngle * 0.5f,
                0
            )
            * transform.forward;

        Gizmos.DrawRay(
            transform.position,
            leftDirection * visionRange
        );

        Gizmos.DrawRay(
            transform.position,
            rightDirection * visionRange
        );

        // Last Seen Position (Modul 63)
        if (blackboard.hasLastSeenPosition)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(blackboard.lastSeenPosition, 0.5f);
            Gizmos.DrawLine(transform.position, blackboard.lastSeenPosition);
        }

    }
}