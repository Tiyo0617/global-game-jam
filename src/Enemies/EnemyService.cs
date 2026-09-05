using System.Collections.Generic;
using Godot;

namespace GGJ;

/// <summary>
/// 敌人服务：对象池 + 存活计数（胜利条件的"场上清空"判定靠 AliveCount）。
/// 由 Main 创建。其他模块只发 SpawnEnemyRequest，不直接实例化敌人。
/// </summary>
public partial class EnemyService : Node
{
    // ⚠️ 调试开关：true = 强制开启分裂。
    private const bool DebugForceSplit = false;

    /// <summary>分裂小怪参数（策划案：1 血、速度 150%）。</summary>
    private const int   SplitHP       = 1;
    private const float SplitSpeedMul = 1.5f;
    private const float SplitScale    = 0.5f;   // 体积减半（策划案未定，取视觉可辨识的值）

    private Pool<EnemyBase>? _pool;
    private readonly List<EnemyBase> _active = new();

    /// <summary>场上活着的敌人数 —— 胜利双条件之一。</summary>
    public int AliveCount => _active.Count;

    public override void _Ready()
    {
        // ⚠️ 调试日志：仅 DebugForceSplit 开启时打印（用于确认游戏加载的是新代码）
        if (DebugForceSplit) GD.Print("[分裂调试] ✔ 调试模式：分裂强制开启");

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

        // P2-16：精英体积倍率的基础值在此设置（Main.InitStats 之后执行，不冲突）。
        // 策划调整数值改这里的 2f 即可，SpawnDirector 改为从 StatBlock 读取。
        GameManager.I.EnemyStats.SetBase(EnemyStat.EliteScaleMul, 2f);
    }

    private void OnSpawn(SpawnEnemyRequest r)
    {
        if (_pool == null) return;

        // 注：存活上限机制已按策划要求移除（P2-15），场上敌人数量不再设限。
        // _active 列表仍保留 —— AliveCount（胜利条件的"场上清空"判定）依赖它。
        var e = _pool.Rent();
        e.Configure(r.Position, r.Direction, r.SpeedMul, r.HP, r.Scale, r.IsTracker, r.CanSplit);
        _active.Add(e);
        Bus.Pub(new EnemySpawned(e));
    }

    private void OnEntityDied(EntityDied d)
    {
        if (d.Target is not EnemyBase eb) return;

        // ---- 分裂：母体允许分裂 + 敌人线开启分裂词条 → 在 despawn 前刷 2 个小的 ----
        // 双重判断：CanSplit（实例级，防小怪再裂）+ FlagSplit（全局词条开关）
        bool flag = GameManager.I.EnemyStats.HasFlag(EnemyStat.FlagSplit);

        // ⚠️ 调试分支：词条系统没好之前强制开启，验证完随 DebugForceSplit 一起移除
        if (DebugForceSplit) flag = true;

        // ⚠️ 调试日志：仅 DebugForceSplit 开启时打印
        if (DebugForceSplit) GD.Print($"[分裂调试] 敌人死亡：CanSplit={eb.CanSplit}，Flag={flag}");

        Vector2 deathPos = eb.GlobalPosition;   // 先记位置，despawn 后节点仍有效但稳妥起见提前取
        if (eb.CanSplit && flag)
        {
            if (DebugForceSplit) GD.Print("[分裂调试] >>> 触发分裂，生成 2 只小怪 <<<");
            SpawnSplit(deathPos);
        }

        Despawn(eb);
    }

    /// <summary>
    /// 在死亡位置分裂出 2 个小怪：1 血、速度 150%、体积减半、方向随机散开。
    /// 小怪 CanSplit = false —— 防止"裂→死→再裂"无限套娃。
    /// 走正常 SpawnEnemyRequest 流程，自动由对象池复用。
    /// </summary>
    private void SpawnSplit(Vector2 pos)
    {
        for (int i = 0; i < 2; i++)
        {
            Bus.Pub(new SpawnEnemyRequest
            {
                Position  = pos,
                Direction = Rng.Direction(),   // 两只各自随机方向，避免完全重叠
                SpeedMul  = SplitSpeedMul,
                HP        = SplitHP,
                Scale     = SplitScale,
                IsTracker = false,
                CanSplit  = false,             // 小怪不再裂
            });
        }
    }

    /// <summary>清场。每轮开始 / 名刀成功时调用。</summary>
    public void ClearAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            Despawn(_active[i]);
    }

    private void Despawn(EnemyBase e)
    {
        // ⚠️ P2-10：无效实例也必须从 _active 移除！
        // 否则调用方拿到失效引用时（历史上：存活上限循环拿 _active[0]）列表长度不变 → 死循环冻死整局。
        // 失效的 Godot 对象无法归还对象池，只能直接移除引用。
        if (!GodotObject.IsInstanceValid(e))
        {
            _active.Remove(e);
            return;
        }
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
