using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyBTUtilityController : MonoBehaviour, IEnemyAI
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

    [Header("Utility AI (Combat)")]
    [SerializeField] private float minimumActionDuration = 0.5f;  // Modul 73 — Action Commitment
    [SerializeField] private float hysteresisMargin = 0.15f;      // Modul 74 — Hysteresis

    // Dibuat sekali, bukan setiap frame
    private readonly List<ActionBTUtility> combatActions = new()
    {
        new AttackAction(),
        new ChaseAction()
    };

    private ActionBTUtility selectedAction;
    private float actionStartTime;
    private float nextAttackTime;
    private readonly List<UtilityScore> scoreView = new();

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

    // Skor Utility hanya dihitung saat cabang Combat aktif
    public IReadOnlyList<UtilityScore> UtilityScores => scoreView;

    public string UtilityInfo
    {
        get
        {
            float commit = Mathf.Max(0f, minimumActionDuration - (Time.time - actionStartTime));
            return $"commitment {commit:0.0}s   hysteresis +{hysteresisMargin:0.00}";
        }
    }

    // Personality (Modul 60 & 61)
    private EnemyPersonality personality = EnemyPersonality.Normal;
    private EnemyStats normalStats;

    public EnemyPersonality Personality => personality;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        currentHealth = maxHealth;
        normalStats = ReadStats();
    }

    private void Start()
    {
        BuildBehaviorTree();
    }

    private void Update()
    {
        UpdatePerception(); // Modul 62

        // Kosongkan skor; diisi lagi oleh CombatUtility kalau cabang Combat aktif
        scoreView.Clear();

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
        // COMBAT
        // ------------------------------

        BTNode combatSequence =
            new SequenceNode(
                new List<BTNode>
                {
                    new ConditionNode(() => blackboard.canSeePlayer).Named("Can See Player?"), // Modul 76
                    new ActionNode(CombatUtility),
                }
            ).Named("Combat");

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
                    combatSequence,
                    searchSequence, // Modul 63
                    patrolAction
                }

            ).Named("Root");
    }

    // Modul 76 — BT mengatur struktur besar, Utility memilih aksi Combat
    private NodeState CombatUtility()
    {
        // Hitung semua skor dulu (Modul 71)
        foreach (ActionBTUtility action in combatActions)
            action.score = action.CalculateScore(this);

        // Pilih skor tertinggi + Action Commitment + Hysteresis (Modul 71, 73, 74)
        ActionBTUtility bestAction = UtilitySelector.Select(
            combatActions,
            action => action.score,
            selectedAction,
            actionStartTime,
            minimumActionDuration,
            hysteresisMargin
        );

        if (bestAction != selectedAction)
        {
            selectedAction = bestAction;
            actionStartTime = Time.time;
        }

        foreach (ActionBTUtility action in combatActions)
            scoreView.Add(new UtilityScore(action.Name, action.score, action == selectedAction));

        return selectedAction.Execute(this);
    }

    public class AttackAction : ActionBTUtility
    {
        public override string Name => "Attack";

        public override float CalculateScore(EnemyBTUtilityController controller)
        {
            // Modul 68 — semakin dekat, semakin tinggi
            float distanceScore =
                Mathf.Clamp01(1f - (controller.blackboard.distanceToPlayer / controller.attackRange));

            // Modul 69 — visibility dikalikan, jadi tidak terlihat = 0
            float visibilityScore = controller.blackboard.canSeePlayer ? 1f : 0f;

            return distanceScore * visibilityScore;
        }

        public override NodeState Execute(EnemyBTUtilityController controller)
        {
            if (controller.player == null)
                return NodeState.Failure;

            controller.currentAction = "ATTACK";

            controller.agent.isStopped = true;
            controller.FacePlayer();

            // Cooldown supaya tidak menyerang setiap frame (Modul 45)
            if (Time.time >= controller.nextAttackTime)
            {
                controller.nextAttackTime = Time.time + controller.attackCooldown;

                if (controller.animator != null)
                {
                    controller.animator.SetTrigger(AttackHash);
                }
                else
                {
                    // Tanpa Animator tidak ada animation event, jadi damage langsung
                    controller.DealAttackDamage();
                }
            }

            return NodeState.Running;
        }
    }

    public class ChaseAction : ActionBTUtility
    {
        public override string Name => "Chase";

        public override float CalculateScore(EnemyBTUtilityController controller)
        {
            // Modul 70 — distanceNeedScore: semakin jauh, semakin perlu dikejar
            float distanceNeedScore =
                Mathf.Clamp01(controller.blackboard.distanceToPlayer / controller.visionRange);

            float visibilityScore = controller.blackboard.canSeePlayer ? 1f : 0f;

            return visibilityScore * distanceNeedScore;
        }

        public override NodeState Execute(EnemyBTUtilityController controller)
        {
            if (controller.player == null)
                return NodeState.Failure;

            controller.currentAction = "CHASE";

            controller.agent.isStopped = false;
            controller.agent.speed = controller.chaseSpeed;
            controller.agent.stoppingDistance =
                controller.attackRange * 0.5f;

            controller.agent.SetDestination(controller.player.position);

            return NodeState.Running;
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
    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    // ==================================================
    // PERSONALITY (Modul 60 & 61)
    // ==================================================

    public void ApplyPersonality(EnemyPersonality newPersonality)
    {
        personality = newPersonality;
        WriteStats(normalStats.WithPersonality(newPersonality));
    }

    private EnemyStats ReadStats()
    {
        return new EnemyStats
        {
            visionRange = visionRange,
            visionAngle = visionAngle,
            attackRange = attackRange,
            attackCooldown = attackCooldown,
            lowHealthThreshold = lowHealthThreshold,
            chaseSpeed = chaseSpeed,
            fleeSpeed = fleeSpeed
        };
    }

    private void WriteStats(EnemyStats stats)
    {
        visionRange = stats.visionRange;
        visionAngle = stats.visionAngle;
        attackRange = stats.attackRange;
        attackCooldown = stats.attackCooldown;
        lowHealthThreshold = stats.lowHealthThreshold;
        chaseSpeed = stats.chaseSpeed;
        fleeSpeed = stats.fleeSpeed;
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