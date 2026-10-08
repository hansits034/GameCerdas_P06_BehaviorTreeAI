using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyBTController : MonoBehaviour, IHealth
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

    // Modul 63
    [Header("Search")]
    [SerializeField] private float searchDuration = 3f;   // lama melihat-lihat di lokasi
    [SerializeField] private float searchTurnSpeed = 120f; // derajat per detik saat melihat-lihat


    [Header("Debug")]
    [SerializeField] private string currentAction = "None";

    private NavMeshAgent agent;
    private BTNode rootNode;

    private int currentHealth;
    private int currentPatrolIndex = 0;

    private EnemyBlackboard blackboard = new EnemyBlackboard();

    private float searchTimer = 0f; // Modul 63

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    // Untuk BTDebugger
    public BTNode RootNode => rootNode;
    public EnemyBlackboard Blackboard => blackboard;
    public string CurrentAction => currentAction;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        currentHealth = maxHealth;
    }

    private void Start()
    {
        BuildBehaviorTree();
    }

    private void Update()
    {
        UpdatePerception(); // Modul 62

        if (rootNode != null)
            rootNode.Tick();

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
            blackboard.hasLastSeenPosition = true; // Modul 63
            searchTimer = 0f;                      // Modul 63
        }

    }


    private void BuildBehaviorTree()
    {
        // ------------------------------
        // FLEE
        // ------------------------------

        BTNode fleeSequence =
            new SequenceNode(
                new List<BTNode>
                {
                    new ConditionNode(() => blackboard.healthLow).Named("Health Low?"), // Modul 62
                    new ActionNode(Flee)
                }
            ).Named("Flee");

        // ------------------------------
        // ATTACK
        // ------------------------------

        BTNode attackAction =
            new ActionNode(AttackPlayer);

        BTNode attackWithCooldown =
            new CooldownDecorator(
                attackAction,
                attackCooldown
            ).Named("Attack Cooldown");

        BTNode attackSequence =
            new SequenceNode(
                new List<BTNode>
                {
                    new ConditionNode(() => blackboard.canSeePlayer).Named("Can See Player?"), // Modul 62
                    new ConditionNode(() => blackboard.distanceToPlayer <= attackRange).Named("In Attack Range?"), // Modul 62
                    attackWithCooldown
                }
            ).Named("Attack");

        // ------------------------------
        // CHASE
        // ------------------------------

        BTNode chaseSequence =
            new SequenceNode(
                new List<BTNode>
                {
                    new ConditionNode(() => blackboard.canSeePlayer).Named("Can See Player?"), // Modul 62
                    new ActionNode(ChasePlayer)
                }
            ).Named("Chase");



        // ------------------------------
        // SEARCH (Modul 63)
        // ------------------------------

        BTNode searchSequence =
            new SequenceNode(
                new List<BTNode>
                {
                    new ConditionNode(() => blackboard.hasLastSeenPosition).Named("Has Last Seen Pos?"), // Modul 63
                    new ActionNode(SearchLastPosition)                       // Modul 63
                }
            ).Named("Search");


        // ------------------------------
        // PATROL
        // ------------------------------

        BTNode patrolAction =
            new ActionNode(Patrol);




        // ------------------------------
        // ROOT SELECTOR
        // ------------------------------

        rootNode =
            new SelectorNode(
                new List<BTNode>
                {
                    fleeSequence,
                    attackSequence,
                    chaseSequence,
                    searchSequence, // Modul 63
                    patrolAction
                }

            ).Named("Root");
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

    // Modul 63
    private NodeState SearchLastPosition()
    {
        currentAction = "SEARCH";

        agent.isStopped = false;
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0.2f;

        agent.SetDestination(blackboard.lastSeenPosition);

        // Belum sampai ke posisi terakhir Player
        if (agent.pathPending ||
            agent.remainingDistance > 0.5f)
        {
            return NodeState.Running;
        }

        // Sudah sampai: berputar melihat-lihat sekeliling
        searchTimer += Time.deltaTime;

        transform.Rotate(
            0f,
            searchTurnSpeed * Time.deltaTime,
            0f
        );

        if (searchTimer >= searchDuration)
        {
            // Pencarian selesai, lupakan posisi terakhir
            blackboard.hasLastSeenPosition = false;
            searchTimer = 0f;
            Debug.Log(name + " selesai mencari, kembali patrol"); // Modul 63

            return NodeState.Success;
        }

        return NodeState.Running;
    }

    private NodeState Patrol()
    {
        currentAction = "PATROL";

        if (patrolPoints == null ||
            patrolPoints.Length == 0)
        {
            return NodeState.Failure;
        }

        agent.isStopped = false;
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0.2f;

        Transform target =
            patrolPoints[currentPatrolIndex];

        agent.SetDestination(target.position);

        if (!agent.pathPending &&
            agent.remainingDistance <= 0.5f)
        {
            currentPatrolIndex++;

            if (currentPatrolIndex
                >= patrolPoints.Length)
            {
                currentPatrolIndex = 0;
            }
        }

        return NodeState.Running;
    }

    private NodeState ChasePlayer()
    {
        if (player == null)
            return NodeState.Failure;

        currentAction = "CHASE";

        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.stoppingDistance =
            attackRange * 0.8f;

        agent.SetDestination(player.position);

        return NodeState.Running;
    }

    private NodeState AttackPlayer()
    {
        if (player == null)
            return NodeState.Failure;

        currentAction = "ATTACK";
        
        if (animator != null)
        {
            animator.SetTrigger(AttackHash);
        }

        return NodeState.Success;
    }

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

    private NodeState Flee()
    {
        if (safePoint == null)
            return NodeState.Failure;

        currentAction = "FLEE";

        agent.isStopped = false;
        agent.speed = fleeSpeed;
        agent.stoppingDistance = 0.5f;

        agent.SetDestination(
            safePoint.position
        );

        if (!agent.pathPending &&
            agent.remainingDistance <= 0.7f)
        {
            agent.isStopped = true;

            return NodeState.Success;
        }

        return NodeState.Running;
    }

    private void FacePlayer()
    {
        Vector3 direction =
            player.position
            - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude
            < 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                10f * Time.deltaTime
            );
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