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

    public override void _Ready()
    {
        CollisionLayer = Layers.PlayerBullet;
        CollisionMask  = Layers.Enemy;
        Monitoring     = true;
        BodyEntered   += OnBodyEntered;
    }

    public void Launch(Vector2 pos, Vector2 dir, float speed, int damage, int pierce)
    {
        GlobalPosition = pos;
        Direction = dir.Normalized();
        Speed = speed;
        Damage = damage;
        Pierce = pierce;
        _alreadyHit.Clear();
    }

    public override void _PhysicsProcess(double delta)
    {
        GlobalPosition += Direction * Speed * (float)delta;

        // TODO(程序A)：跳弹 FlagRicochet —— 碰边缘按概率光学反射，每发至多 1 次

        if (!ArenaBounds.Inside(GlobalPosition)) Despawn();
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
