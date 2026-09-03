using Godot;

namespace GGJ;

/// <summary>
/// 玩家。无重力、8 向自由移动、只能向正右方开炮。
/// 移动参数全部从 GameManager.I.PlayerStats 读，本文件不写死任何数值。
/// </summary>
public partial class Player : CharacterBody2D
{
    private Health _health = null!;
    private Area2D _hurtbox = null!;
    private Vector2 _vel;

    public Health HealthComp => _health;

    public override void _Ready()
    {
        _health = GetNode<Health>("Health");
        _hurtbox = GetNode<Area2D>("Hurtbox");
        AddToGroup("player");

        CollisionLayer = Layers.Player;
        CollisionMask  = Layers.Enemy;
        MotionMode     = MotionModeEnum.Floating;    // 无重力

        GameManager.I.Player = this;

        int maxHp = (int)GameManager.I.PlayerStats.Get(PlayerStat.MaxHP);
        _health.SetMaxHP(maxHp, healToFull: true);
        GlobalPosition = ArenaBounds.Center;
    }

    /// <summary>每轮开始：回满血、回中心、清无敌。</summary>
    public void ResetForRound()
    {
        int maxHp = (int)GameManager.I.PlayerStats.Get(PlayerStat.MaxHP);
        _health.SetMaxHP(maxHp, healToFull: true);
        _health.ClearInvincible();
        GlobalPosition = ArenaBounds.Center;
        _vel = Vector2.Zero;
        Velocity = Vector2.Zero;
    }

    public override void _PhysicsProcess(double delta)
    {
        float d = (float)delta;
        UpdateMove(d);
        UpdateBlink();
        CheckContact();
    }

    private void UpdateMove(float d)
    {
        var feel = GameManager.I.Feel;
        float accel = feel?.Accel ?? 2000f;
        float friction = feel?.Friction ?? 1600f;
        float speed = GameManager.I.PlayerStats.Get(PlayerStat.MoveSpeed);

        Vector2 wish = InputState.MoveAxis() * speed;

        _vel = wish.LengthSquared() > 0f
            ? _vel.MoveToward(wish, accel * d)
            : _vel.MoveToward(Vector2.Zero, friction * d);

        Velocity = _vel;
        MoveAndSlide();
        GlobalPosition = ArenaBounds.ClampInside(GlobalPosition);   // 撞边缘停住，不反弹
    }

    private void UpdateBlink()
    {
        var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite == null) return;
        sprite.Visible = !_health.Invincible || (int)(Time.GetTicksMsec() / 80) % 2 == 0;
    }

    /// <summary>
    /// 碰撞：双方各 -1 血。无敌帧内双方都不结算（GDD §3.4）。
    /// 名刀窗口内：敌人照常死，玩家不掉血。
    ///
    /// ⚠️ 用 Area2D 受伤盒 + 每帧重叠检测，**不要**改回 GetSlideCollisionCount()。
    ///    玩家静止时自己的 MoveAndSlide 速度为 0，检测不到撞上来的敌人 —— 会漏伤害。
    /// </summary>
    private void CheckContact()
    {
        foreach (var other in _hurtbox.GetOverlappingBodies())
        {
            if (other == null || !GodotObject.IsInstanceValid(other)) continue;
            if (!other.IsInGroup("enemy")) continue;

            var enemyHealth = other.GetNodeOrNull<Health>("Health");
            if (enemyHealth == null) continue;

            bool deathblade = GameManager.I.DeathbladeActive;
            if (_health.Invincible && !deathblade) return;   // 双方都不结算

            var toEnemy = new HitInfo
            {
                Source = this, Target = other,
                SourceIsPlayer = true, TargetIsPlayer = false,
                BaseAmount = 1f, Kind = DamageKind.Contact,
                Position = other is Node2D en ? en.GlobalPosition : GlobalPosition,
            };
            DamageSystem.Deal(ref toEnemy);

            var toPlayer = new HitInfo
            {
                Source = other, Target = this,
                SourceIsPlayer = false, TargetIsPlayer = true,
                BaseAmount = 1f, Kind = DamageKind.Contact,
                Position = GlobalPosition,
            };
            DamageSystem.Deal(ref toPlayer);   // 名刀窗口内会被 DamageSystem 取消
            return;                            // 一帧只结算一次接触伤害
        }
    }
}
