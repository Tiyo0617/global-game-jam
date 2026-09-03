using Godot;

namespace GGJ;

public struct SpawnEnemyRequest
{
    public Vector2 Position;
    public Vector2 Direction;
    public float SpeedMul;
    public int HP;
    public float Scale;
    public bool IsTracker;
}

public readonly struct EnemySpawned
{
    public readonly Node? Enemy;
    public EnemySpawned(Node? e) { Enemy = e; }
}

public readonly struct EnemyDespawned
{
    public readonly Node? Enemy;
    public EnemyDespawned(Node? e) { Enemy = e; }
}
