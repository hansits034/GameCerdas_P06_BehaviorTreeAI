using System.Collections.Generic;
using UnityEngine;

// Panel kontrol + debug AI:
// - ganti versi AI (Behavior Tree / Utility / BT + Utility)
// - Behavior Tree  : tampilkan decision (status setiap node)
// - Utility        : tampilkan skor setiap action
// - damage 25, reset HP, dan personality (Normal / Aggressive / Coward)
public class BTDebugger : MonoBehaviour
{
    private enum AIMode
    {
        BehaviorTree,
        Utility,
        Hybrid
    }

    [Header("Enemy per versi AI (kosong = dicari otomatis)")]
    [SerializeField] private EnemyBTController behaviorTreeEnemy;
    [SerializeField] private EnemyUtilityController utilityEnemy;
    [SerializeField] private EnemyBTUtilityController hybridEnemy;

    [Header("Shortcut")]
    [SerializeField] private bool visible = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private KeyCode behaviorTreeKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode utilityKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode hybridKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode normalKey = KeyCode.Z;
    [SerializeField] private KeyCode aggressiveKey = KeyCode.X;
    [SerializeField] private KeyCode cowardKey = KeyCode.C;
    [SerializeField] private KeyCode damageKey = KeyCode.H;
    [SerializeField] private KeyCode resetHealthKey = KeyCode.R;

    [Header("Test")]
    [SerializeField] private int testDamage = 25;

    private AIMode mode = AIMode.BehaviorTree;
    private EnemyPersonality personality = EnemyPersonality.Normal;
    private GUIStyle style;
    private GUIStyle buttonStyle;

    // Aksi dari tombol UI dijalankan di Update, bukan di tengah OnGUI,
    // supaya layout IMGUI tidak berubah di tengah event.
    private System.Action pendingAction;

    private void Start()
    {
        if (behaviorTreeEnemy == null)
            behaviorTreeEnemy = FindEnemy<EnemyBTController>();

        if (utilityEnemy == null)
            utilityEnemy = FindEnemy<EnemyUtilityController>();

        if (hybridEnemy == null)
            hybridEnemy = FindEnemy<EnemyBTUtilityController>();

        // Mulai dari versi yang sedang aktif di scene
        if (behaviorTreeEnemy != null && behaviorTreeEnemy.gameObject.activeInHierarchy)
            mode = AIMode.BehaviorTree;
        else if (utilityEnemy != null && utilityEnemy.gameObject.activeInHierarchy)
            mode = AIMode.Utility;
        else if (hybridEnemy != null && hybridEnemy.gameObject.activeInHierarchy)
            mode = AIMode.Hybrid;

        SwitchMode(mode);
    }

    // Prioritas: yang aktif, lalu yang bernama "Enemy", lalu yang pertama ditemukan
    private static T FindEnemy<T>() where T : MonoBehaviour
    {
        T[] found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (T enemy in found)
            if (enemy.gameObject.activeInHierarchy)
                return enemy;

        foreach (T enemy in found)
            if (enemy.name == "Enemy")
                return enemy;

        return found.Length > 0 ? found[0] : null;
    }

    private MonoBehaviour EnemyFor(AIMode aiMode)
    {
        return aiMode switch
        {
            AIMode.BehaviorTree => behaviorTreeEnemy,
            AIMode.Utility => utilityEnemy,
            _ => hybridEnemy
        };
    }

    private IEnemyAI Current => EnemyFor(mode) as IEnemyAI;

    private void Update()
    {
        pendingAction?.Invoke();
        pendingAction = null;

        if (Input.GetKeyDown(toggleKey))
            visible = !visible;

        if (Input.GetKeyDown(behaviorTreeKey)) SwitchMode(AIMode.BehaviorTree);
        if (Input.GetKeyDown(utilityKey)) SwitchMode(AIMode.Utility);
        if (Input.GetKeyDown(hybridKey)) SwitchMode(AIMode.Hybrid);

        if (Input.GetKeyDown(normalKey)) SetPersonality(EnemyPersonality.Normal);
        if (Input.GetKeyDown(aggressiveKey)) SetPersonality(EnemyPersonality.Aggressive);
        if (Input.GetKeyDown(cowardKey)) SetPersonality(EnemyPersonality.Coward);

        if (Input.GetKeyDown(damageKey)) DamageEnemy();
        if (Input.GetKeyDown(resetHealthKey)) ResetEnemyHealth();
    }

    // ==================================================
    // AKSI PANEL
    // ==================================================

    private void SwitchMode(AIMode newMode)
    {
        MonoBehaviour target = EnemyFor(newMode);

        if (target == null)
        {
            Debug.LogWarning("BTDebugger: tidak ada enemy untuk mode " + newMode);
            return;
        }

        MonoBehaviour previous = EnemyFor(mode);

        // Enemy baru muncul di posisi enemy lama supaya mudah dibandingkan
        if (previous != null && previous != target && previous.gameObject.activeInHierarchy)
        {
            target.transform.SetPositionAndRotation(
                previous.transform.position,
                previous.transform.rotation
            );
        }

        foreach (AIMode m in new[] { AIMode.BehaviorTree, AIMode.Utility, AIMode.Hybrid })
        {
            MonoBehaviour enemy = EnemyFor(m);

            if (enemy != null)
                enemy.gameObject.SetActive(enemy == target);
        }

        mode = newMode;

        // Awake sudah jalan saat SetActive(true), jadi personality bisa dipasang
        Current?.ApplyPersonality(personality);
    }

    private void SetPersonality(EnemyPersonality newPersonality)
    {
        personality = newPersonality;
        Current?.ApplyPersonality(personality);
    }

    private void DamageEnemy()
    {
        Current?.TakeDamage(testDamage);
    }

    private void ResetEnemyHealth()
    {
        Current?.ResetHealth();
    }

    // ==================================================
    // UI
    // ==================================================

    private void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 14,
                wordWrap = false,
                stretchWidth = false,
                margin = new RectOffset(4, 4, 1, 1)
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                richText = true,
                fontSize = 13
            };
        }

        if (!visible)
        {
            GUI.Label(new Rect(10, 80, 300, 24), $"<color=white>{toggleKey}: tampilkan panel AI</color>", style);
            return;
        }

        IEnemyAI enemy = Current;

        GUILayout.BeginArea(new Rect(10, 80, 470, Screen.height - 90));
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

        GUILayout.Label($"<b>AI Control Panel</b>   {toggleKey}: hide", style);

        // Versi AI
        GUILayout.Label("Versi AI", style);
        GUILayout.BeginHorizontal();
        ModeButton(AIMode.BehaviorTree, "[1] Behavior Tree");
        ModeButton(AIMode.Utility, "[2] Utility");
        ModeButton(AIMode.Hybrid, "[3] BT + Utility");
        GUILayout.EndHorizontal();

        // Personality
        GUILayout.Label("Personality", style);
        GUILayout.BeginHorizontal();
        PersonalityButton(EnemyPersonality.Normal, $"[{normalKey}] Normal");
        PersonalityButton(EnemyPersonality.Aggressive, $"[{aggressiveKey}] Aggressive");
        PersonalityButton(EnemyPersonality.Coward, $"[{cowardKey}] Coward");
        GUILayout.EndHorizontal();

        // Test
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"[{damageKey}] Damage {testDamage}", buttonStyle)) pendingAction = DamageEnemy;
        if (GUILayout.Button($"[{resetHealthKey}] Reset HP", buttonStyle)) pendingAction = ResetEnemyHealth;
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        if (enemy == null)
        {
            GUILayout.Label("<color=#F44336>Enemy untuk versi ini tidak ditemukan</color>", style);
        }
        else
        {
            DrawEnemyInfo(enemy);

            if (enemy.RootNode != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("<b>Behavior Tree - decision</b>", style);
                DrawNode(enemy.RootNode, 0);
            }

            if (mode != AIMode.BehaviorTree)
            {
                GUILayout.Space(8);
                DrawUtilityScores(enemy);
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void ModeButton(AIMode buttonMode, string label)
    {
        bool selected = mode == buttonMode;

        if (GUILayout.Toggle(selected, label, buttonStyle) && !selected)
            pendingAction = () => SwitchMode(buttonMode);
    }

    private void PersonalityButton(EnemyPersonality buttonPersonality, string label)
    {
        bool selected = personality == buttonPersonality;

        if (GUILayout.Toggle(selected, label, buttonStyle) && !selected)
            pendingAction = () => SetPersonality(buttonPersonality);
    }

    private void DrawEnemyInfo(IEnemyAI enemy)
    {
        EnemyBlackboard bb = enemy.Blackboard;
        string enemyName = (enemy as MonoBehaviour).name;

        GUILayout.Label($"<b>{enemyName}</b>   ({enemy.Personality})", style);
        GUILayout.Label($"Action: <b>{enemy.CurrentAction}</b>   HP: {enemy.CurrentHealth}/{enemy.MaxHealth}", style);
        GUILayout.Label($"See: {bb.canSeePlayer}   Dist: {bb.distanceToPlayer:0.0}   Low HP: {bb.healthLow}", style);
        GUILayout.Label($"Last seen: {(bb.hasLastSeenPosition ? bb.lastSeenPosition.ToString("F1") : "-")}", style);
    }

    private void DrawUtilityScores(IEnemyAI enemy)
    {
        GUILayout.Label("<b>Utility - score</b>", style);

        IReadOnlyList<UtilityScore> scores = enemy.UtilityScores;

        if (scores.Count == 0)
        {
            // Hybrid: skor hanya dihitung di dalam cabang Combat
            GUILayout.Label("<color=#808080>(cabang Combat tidak aktif)</color>", style);
            return;
        }

        foreach (UtilityScore score in scores)
        {
            GUILayout.BeginHorizontal();

            string label = score.Selected ? $"<b>> {score.Name}</b>" : $"   {score.Name}";
            GUILayout.Label(label, style, GUILayout.Width(90));

            Rect bar = GUILayoutUtility.GetRect(220, 16, GUILayout.Width(220));
            bar.y += 3;

            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = score.Selected ? new Color(1f, 0.76f, 0.03f) : new Color(0.6f, 0.6f, 0.6f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(score.Score), bar.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            GUILayout.Label($"{score.Score:0.00}", style, GUILayout.Width(50));

            GUILayout.EndHorizontal();
        }

        GUILayout.Label($"<color=#808080>{enemy.UtilityInfo}</color>", style);
    }

    private void DrawNode(BTNode node, int depth)
    {
        bool tickedThisFrame = node.LastTickFrame == Time.frameCount;

        string color = !tickedThisFrame ? "#808080" : node.LastState switch
        {
            NodeState.Success => "#4CAF50",
            NodeState.Failure => "#F44336",
            _                 => "#FFC107"
        };

        string state = tickedThisFrame ? node.LastState.ToString() : "-";

        string indent = new string(' ', depth * 4);

        GUILayout.Label($"{indent}<color={color}>{node.Name}  [{state}]</color>  {node.DebugInfo}", style);

        foreach (BTNode child in node.Children)
            DrawNode(child, depth + 1);
    }
}
