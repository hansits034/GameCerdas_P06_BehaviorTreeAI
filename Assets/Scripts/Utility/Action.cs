[System.Serializable]
public abstract class Action
{
    public float score;
    public abstract float CalculateScore(EnemyUtilityController controller);

    public abstract void Execute(EnemyUtilityController controller);
}