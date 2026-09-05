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

    /// <summary>
    /// 皮肤类别：决定敌人从哪个贴图池随机换皮。
    /// - Normal：普通池（鸟/虫/甲虫随机）
    /// - Elite：精英池（考拉/鸽子随机）
    /// - Hive：马蜂窝母体（分裂词条开启后的普通母体生前造型）
    /// - Bee：马蜂（分裂出的子怪造型）
    /// struct 默认值 = Normal，漏传时安全落到普通随机皮。
    /// </summary>
    public EnemySkinKind SkinKind;
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
