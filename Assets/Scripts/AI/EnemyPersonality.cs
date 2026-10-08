public enum EnemyPersonality
{
    Normal,
    Aggressive,
    Coward
}

// Parameter enemy yang diubah oleh personality (Modul 60 & 61)
public struct EnemyStats
{
    public float visionRange;
    public float visionAngle;
    public float attackRange;
    public float attackCooldown;
    public int lowHealthThreshold;
    public float chaseSpeed;
    public float fleeSpeed;

    // Normal = nilai Inspector apa adanya
    public EnemyStats WithPersonality(EnemyPersonality personality)
    {
        EnemyStats stats = this;

        switch (personality)
        {
            // Modul 60 — Eksperimen 4: Enemy Aggressive
            case EnemyPersonality.Aggressive:
                stats.visionRange = 15f;
                stats.visionAngle = 150f;
                stats.chaseSpeed = 5f;
                stats.attackRange = 2.5f;
                stats.attackCooldown = 0.8f;
                stats.lowHealthThreshold = 15;
                break;

            // Modul 61 — Eksperimen 5: Enemy Coward
            case EnemyPersonality.Coward:
                stats.visionRange = 10f;
                stats.visionAngle = 90f;
                stats.chaseSpeed = 3f;
                stats.attackCooldown = 2f;
                stats.lowHealthThreshold = 70;
                stats.fleeSpeed = 6f;
                break;
        }

        return stats;
    }
}
