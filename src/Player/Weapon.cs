using Godot;

namespace GGJ;

/// <summary>
/// 射击。固定向正右方，不可瞄准（核心机制，不要加瞄准）。
///
/// TODO(程序A)：
///   · 激光 FlagLaser —— 改成穿透 + 发射方向时刻顺时针环绕玩家
///   · 跳弹 FlagRicochet —— 概率光学反射（做在 Bullet 里）
///   · 连发 ExtraShots —— 每次发射后隔一小段补射
///   · 闪现 FlagDash —— 鼠标右键，向当前移动方向瞬移
/// </summary>
public partial class Weapon : Node
{
    private float _cd;
    private Node2D _owner = null!;

    public override void _Ready()
    {
        _owner = GetParent<Node2D>();
    }

    public override void _Process(double delta)
    {
        if (_cd > 0f) _cd -= (float)delta;
        if (_cd > 0f) return;
        if (!InputState.FireHeld) return;

        Fire();
        _cd = Mathf.Max(0.05f, GameManager.I.PlayerStats.Get(PlayerStat.FireCooldown));
    }

    private void Fire()
    {
        var st = GameManager.I.PlayerStats;

        Bus.Pub(new SpawnBulletRequest
        {
            Position  = _owner.GlobalPosition + new Vector2(16f, 0f),
            Direction = Vector2.Right,
            Speed     = st.Get(PlayerStat.BulletSpeed),
            Damage    = 1,
            Pierce    = (int)st.Get(PlayerStat.Pierce),
        });

        Bus.Pub(new SfxRequest { Key = "shoot" });
    }
}
