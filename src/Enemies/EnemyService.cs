using System.Collections.Generic;
using Godot;

namespace GGJ;

/// <summary>
/// 敌人服务：对象池 + 存活计数（胜利条件的"场上清空"判定靠 AliveCount）。
/// 由 Main 创建。其他模块只发 SpawnEnemyRequest，不直接实例化敌人。
/// </summary>
public partial class EnemyService : Node
{
    private Pool<EnemyBase>? _pool;
    private readonly List<EnemyBase> _active = new();

    /// <summary>场上活着的敌人数 —— 胜利双条件之一。</summary>
    public int AliveCount => _active.Count;

    public override void _Ready()
    {
        Bus.Sub<SpawnEnemyRequest>(this, OnSpawn);
        Bus.Sub<EntityDied>(this, OnEntityDied);
    }

    public void Init(PackedScene? scene)
    {
        if (scene == null)
        {
            GD.PushWarning("[EnemyService] 没有 EnemyScene，刷怪功能关闭。检查 data/run_config.tres。");
            return;
        }
        _pool = new Pool<EnemyBase>(scene, this);
    }

    private void OnSpawn(SpawnEnemyRequest r)
    {
        if (_pool == null) return;
        var e = _pool.Rent();
        e.Configure(r.Position, r.Direction, r.SpeedMul, r.HP, r.Scale, r.IsTracker);
        _active.Add(e);
        Bus.Pub(new EnemySpawned(e));
    }

    private void OnEntityDied(EntityDied d)
    {
        if (d.Target is EnemyBase eb) Despawn(eb);
    }

    /// <summary>清场。每轮开始 / 名刀成功时调用。</summary>
    public void ClearAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            Despawn(_active[i]);
    }

    private void Despawn(EnemyBase e)
    {
        if (!GodotObject.IsInstanceValid(e)) return;
        e.Deactivate();
        _pool?.Return(e);
        _active.Remove(e);
        Bus.Pub(new EnemyDespawned(e));
    }

    public override void _Process(double delta)
    {
        // 防御性清理：Godot 里被销毁的 Node 不是 null，必须用 IsInstanceValid 判断
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (!GodotObject.IsInstanceValid(_active[i]) || !_active[i].Active)
                _active.RemoveAt(i);
        }
    }
}
