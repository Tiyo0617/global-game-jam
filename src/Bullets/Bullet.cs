using System.Collections.Generic;
using Godot;

namespace GGJ;

/// <summary>
/// 子弹。Area2D（不是物理体），出屏即回收，命中即销毁（穿透词条除外）。
/// 由 BulletService 从对象池租用，不要自己 QueueFree。
/// </summary>
public partial class Bullet : Area2D
{
    public Vector2 Direction = Vector2.Right;
    public float Speed = 500f;
    public int Damage = 1;
    public int Pierce;

    private readonly HashSet<Node> _alreadyHit = new();

    // ⚠️ 调试开关：true = 强制开启跳弹。
    private const bool DebugForceRicochet = false;

    // ---- 跳弹：发射时刻一次性判定（P2-11），飞行途中不再每帧摇骰子 ----
    /// <summary>本发是否会反弹（Launch 时摇一次骰子定格）。</summary>
    private bool _willBounce;
    /// <summary>本发是否已反射过。每发至多反射 1 次（策划案规则）。</summary>
    private bool _bounced;

    public override void _Ready()
    {
        CollisionLayer = Layers.PlayerBullet;
        CollisionMask  = Layers.Enemy;
        Monitoring     = true;
        BodyEntered   += OnBodyEntered;
    }

    /// <summary>从对象池租用时调用。⚠️ 必须重置所有状态，防止上一发的残留。</summary>
    public void Launch(Vector2 pos, Vector2 dir, float speed, int damage, int pierce)
    {
        GlobalPosition = pos;
        Direction = dir.Normalized();
        Speed = speed;
        Damage = damage;
        Pierce = pierce;
        _alreadyHit.Clear();

        // 跳弹词条在发射时刻一次性判定（P2-11）：开关 + 概率都在此刻定格，
        // 飞行途中不再每帧调用 Rng.Chance（原实现每弹每帧摇一次，60fps 下 ~60 次/秒/弹）。
        var ps = GameManager.I.PlayerStats;
        bool enabled = ps.HasFlag(PlayerStat.FlagRicochet);

        // ⚠️ 调试分支：词条系统没好之前强制开启，验证完随 DebugForceRicochet 一起移除
        if (DebugForceRicochet) enabled = true;

        float chance = ps.Get(PlayerStat.RicochetChance);

        _willBounce = enabled && Rng.Chance(chance);   // 全程唯一一次随机调用
        _bounced = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        GlobalPosition += Direction * Speed * (float)delta;

        // 跳弹：越界时按发射时刻的判定结果光学反射，每发至多 1 次。
        // 未开启 / 已弹过 / 未摇中 → 走原来的出屏销毁逻辑。
        if (!_bounced && _willBounce && TryRicochet())
        {
            _bounced = true;   // 标记已弹，后续不再反射
            return;            // 反射成功，本帧不做出屏判定
        }

        if (!ArenaBounds.Inside(GlobalPosition)) Despawn();
    }

    /// <summary>
    /// 越界时的光学反射。概率已在 Launch 时判定（_willBounce），这里只做几何判定。
    /// 复用 ArenaBounds.Reflect（已带方向判断，不会贴墙抖动）。
    /// 返回 true 表示本次发生了反射。
    /// </summary>
    private bool TryRicochet()
    {
        // 光学反射（入射角 = 反射角，速度大小不变）
        Vector2 v = Direction;
        Vector2 p = GlobalPosition;
        bool bounced = ArenaBounds.Reflect(ref v, p);
        if (!bounced) return false;   // 没碰到边界，不反射

        // 应用反射方向，并把子弹拉回场内一点，
        // 避免下一帧又被判越界直接销毁（反射就白做了）。
        Direction = v;
        GlobalPosition = ArenaBounds.ClampInside(GlobalPosition);
        return true;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (!GodotObject.IsInstanceValid(body)) return;
        if (!body.IsInGroup("enemy")) return;
        if (!_alreadyHit.Add(body)) return;

        var hit = new HitInfo
        {
            Source = null,                 // 发射者可能已被回收，传 null
            Target = body,
            SourceIsPlayer = true,
            TargetIsPlayer = false,
            BaseAmount = Damage,
            Kind = DamageKind.Bullet,
            Position = body.GlobalPosition,
        };
        DamageSystem.Deal(ref hit);

        if (Pierce <= 0) Despawn();
        else Pierce--;
    }

    private void Despawn() => Bus.Pub(new DespawnBullet { Bullet = this });
}
