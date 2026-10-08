[System.Serializable]
public abstract class ActionBTUtility
{
    public float score;
    public abstract string Name { get; }

    public abstract float CalculateScore(EnemyBTUtilityController controller);

    public abstract NodeState Execute(EnemyBTUtilityController controller);
}
