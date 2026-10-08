using UnityEngine;

public class BTDebugger : MonoBehaviour
{
    [SerializeField] private bool visible = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private KeyCode nextEnemyKey = KeyCode.Tab;

    private EnemyBTController[] enemies;
    private int index;
    private GUIStyle style;

    private void Start()
    {
        enemies = FindObjectsByType<EnemyBTController>(FindObjectsSortMode.None);
        System.Array.Sort(enemies, (a, b) => string.Compare(a.name, b.name));
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            visible = !visible;

        if (Input.GetKeyDown(nextEnemyKey) && enemies.Length > 0)
            index = (index + 1) % enemies.Length;
    }

    private void OnGUI()
    {
        if (!visible || enemies == null || enemies.Length == 0)
            return;

        EnemyBTController enemy = enemies[index];

        if (enemy == null || enemy.RootNode == null)
            return;

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
        }

        EnemyBlackboard bb = enemy.Blackboard;

        GUILayout.BeginArea(new Rect(10, 80, Screen.width / 3, Screen.height - 90));
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

        GUILayout.Label($"<b>{enemy.name}</b>  ({index + 1}/{enemies.Length})  Tab: next, F1: hide", style);
        GUILayout.Label($"Action: <b>{enemy.CurrentAction}</b>   HP: {enemy.CurrentHealth}/{enemy.MaxHealth}", style);
        GUILayout.Label($"See: {bb.canSeePlayer}   Dist: {bb.distanceToPlayer:0.0}   Low HP: {bb.healthLow}", style);
        GUILayout.Label($"Last seen: {(bb.hasLastSeenPosition ? bb.lastSeenPosition.ToString("F1") : "-")}", style);
        GUILayout.Space(8);

        DrawNode(enemy.RootNode, 0);

        GUILayout.EndVertical();
        GUILayout.EndArea();
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
