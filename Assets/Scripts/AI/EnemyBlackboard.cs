using UnityEngine;

public class EnemyBlackboard
{
    public Transform player;

    public bool canSeePlayer;

    public float distanceToPlayer;

    public Vector3 lastSeenPosition;

    public bool hasLastSeenPosition; // Modul 63

    public int health;

    public bool healthLow;
}