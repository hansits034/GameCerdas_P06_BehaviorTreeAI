using System.Collections.Generic;

// Skor satu action Utility, untuk ditampilkan di panel debug
public struct UtilityScore
{
    public string Name;
    public float Score;
    public bool Selected;

    public UtilityScore(string name, float score, bool selected)
    {
        Name = name;
        Score = score;
        Selected = selected;
    }
}

// Interface bersama untuk EnemyBTController, EnemyUtilityController,
// dan EnemyBTUtilityController, supaya panel debug bisa mengontrol
// ketiganya dengan cara yang sama.
public interface IEnemyAI : IHealth
{
    string CurrentAction { get; }
    EnemyBlackboard Blackboard { get; }

    // null kalau versi AI tidak memakai Behavior Tree
    BTNode RootNode { get; }

    // kosong kalau versi AI tidak memakai Utility
    IReadOnlyList<UtilityScore> UtilityScores { get; }
    string UtilityInfo { get; }

    EnemyPersonality Personality { get; }

    void TakeDamage(int damage);
    void ResetHealth();
    void ApplyPersonality(EnemyPersonality personality);
    void DealAttackDamage();
}
