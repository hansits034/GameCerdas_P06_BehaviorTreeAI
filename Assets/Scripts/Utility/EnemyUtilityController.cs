using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Attack, Chase, Flee, Patrol
public class EnemyUtilityController : MonoBehaviour, IEnemyAI
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
    [SerializeField] private float patrolScore = 0.1f;            // Modul 70 — default action
    [SerializeField] private float threatMemory = 5f;             // detik Player masih dianggap ancaman setelah hilang
    [SerializeField] private float minimumActionDuration = 0.5f;  // Modul 73 — Action Commitment
    [SerializeField] private float hysteresisMargin = 0.15f;      // Modul 74 — Hysteresis

    [Header("Debug")]
    [SerializeField] private string currentAction = "None";

    private NavMeshAgent agent;

    private int currentHealth;
    private int currentPatrolIndex = 0;

    private EnemyBlackboard blackboard = new();

    private Action selectedAction;
    private float actionStartTime;
    private float lastSeenTime = -999f;
    private float nextAttackTime;
    private readonly List<UtilityScore> scoreView = new();

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    // Untuk panel debug
    public string CurrentAction => currentAction;
    public EnemyBlackboard Blackboard => blackboard;
    public BTNode RootNode => null; // versi ini murni Utility, tanpa Behavior Tree
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

        // Hitung semua skor dulu (Modul 71)
        foreach (Action action in actions)
            action.score = action.CalculateScore(this);

        // Pilih skor tertinggi + Action Commitment + Hysteresis (Modul 71, 73, 74)
        Action bestAction = UtilitySelector.Select(
            actions,
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

        RefreshScoreView();

        selectedAction.Execute(this);

        UpdateAnimator();
    }

    private void RefreshScoreView()
    {
        scoreView.Clear();

        foreach (Action action in actions)
            scoreView.Add(new UtilityScore(action.Name, action.score, action == selectedAction));
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
            lastSeenTime = Time.time;
        }
    }

    // ==================================================
    // CONSIDERATIONS (Modul 67 - 70)
    // ==================================================

    // Modul 69 — visibility sebagai faktor wajib
    private float VisibilityScore => blackboard.canSeePlayer ? 1f : 0f;

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
        public override string Name => "Attack";

        public override float CalculateScore(EnemyUtilityController controller)
        {
            // Modul 68 — semakin dekat, semakin tinggi
            float distanceScore =
                Mathf.Clamp01(1f - (controller.blackboard.distanceToPlayer / controller.attackRange));

            // Modul 69 — dikalikan, jadi tidak terlihat = 0
            return distanceScore * controller.VisibilityScore;
        }

        public override void Execute(EnemyUtilityController controller)
        {
            if (controller.player == null)
                return;

            controller.currentAction = "ATTACK";

            controller.agent.isStopped = true;
            controller.FacePlayer();

            // Cooldown supaya tidak menyerang setiap frame (Modul 45)
            if (Time.time < controller.nextAttackTime)
                return;

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
    }

    public class ChaseAction : Action
    {
        public override string Name => "Chase";

        public override float CalculateScore(EnemyUtilityController controller)
        {
            // Modul 70 — distanceNeedScore: semakin jauh, semakin perlu dikejar
            float distanceNeedScore =
                Mathf.Clamp01(controller.blackboard.distanceToPlayer / controller.visionRange);

            return controller.VisibilityScore * distanceNeedScore;
        }

        public override void Execute(EnemyUtilityController controller)
        {
            if (controller.player == null)
                return;

            controller.currentAction = "CHASE";

            controller.agent.isStopped = false;
            controller.agent.speed = controller.chaseSpeed;
            controller.agent.stoppingDistance =
                controller.attackRange * 0.5f;

            controller.agent.SetDestination(controller.player.position);
        }
    }

    public class FleeAction : Action
    {
        public override string Name => "Flee";

        public override float CalculateScore(EnemyUtilityController controller)
        {
            // Modul 67 — pakai float supaya tidak terjadi pembagian integer
            float healthPercent =
                (float)controller.currentHealth / controller.maxHealth;

            float thresholdPercent =
                (float)controller.lowHealthThreshold / controller.maxHealth;

            // 1 - healthPercent, dinormalisasi supaya bernilai 1 tepat di
            // lowHealthThreshold. Dengan begitu personality ikut berpengaruh.
            float lowHealthScore =
                Mathf.Clamp01((1f - healthPercent) / Mathf.Max(0.01f, 1f - thresholdPercent));

            // Kurva kuadrat: naik pelan saat HP masih tinggi, cepat saat kritis
            lowHealthScore *= lowHealthScore;

            // Di bawah threshold = darurat, kabur walaupun Player tidak terlihat
            if (lowHealthScore >= 1f)
                return 1f;

            // Modul 70 — threatScore: 1 saat Player terlihat, lalu turun
            // perlahan selama threatMemory detik setelah Player hilang
            float threatScore = controller.blackboard.canSeePlayer
                ? 1f
                : Mathf.Clamp01(1f - (Time.time - controller.lastSeenTime) / controller.threatMemory);

            return lowHealthScore * threatScore;
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
        public override string Name => "Patrol";

        public override float CalculateScore(EnemyUtilityController controller)
        {
            // Modul 70 — default action, selalu ada minimal satu pilihan
            return controller.patrolScore;
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
