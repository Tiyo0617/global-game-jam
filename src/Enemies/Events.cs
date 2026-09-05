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
    /// <summary>
    /// 死亡时是否允许分裂（配合 FlagSplit 词条）。
    /// 普通怪 / 精英 = true；分裂出的小怪 = false（防无限裂）。
    /// struct 字段默认 false，所以漏传时安全地表现为"不可分裂"。
    /// </summary>
    public bool CanSplit;
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
