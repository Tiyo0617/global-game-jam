using Godot;

namespace GGJ;

/// <summary>
/// 射击。固定向正右方，不可瞄准（核心机制，不要加瞄准）。
///
/// TODO(程序A)：
///   · 激光 FlagLaser —— 改成穿透 + 发射方向时刻顺时针环绕玩家
///   · 跳弹 FlagRicochet —— 概率光学反射（做在 Bullet 里）
///   · 闪现 FlagDash —— 鼠标右键，向当前移动方向瞬移
/// </summary>
public partial class Weapon : Node
{
    // ==================== 常量 ====================

    /// <summary>连发补射间隔（秒）。策划案：T=0.08s。与主开火 CD 分开，保证"哒哒哒"手感。</summary>
    private const float ExtraShotInterval = 0.08f;

    /// <summary>激光环绕角速度（度/秒，顺时针）。策划案：90°/s。</summary>
    /// <remarks>Godot 2D 的 Y 轴向下，角度递增即为顺时针。</remarks>
    private const float LaserSpinDegPerSec = 90f;

    /// <summary>激光穿透个数。取极大值等效"穿透路径上所有敌人"（策划案激光效果）。</summary>
    /// <remarks>复用 Bullet 现有的 Pierce-- 逻辑，无需改动 Bullet。</remarks>
    private const int LaserPierce = int.MaxValue;

    // ⚠️ 调试开关：true = 强制开启对应词条（词条系统未完成时的临时验证手段）。
    //    正常游玩保持 false —— 词条由三选一系统启用。
    private const bool DebugForceExtraShots = false;
    /// <summary>调试模式下强制补射的次数。</summary>
    private const int DebugExtraShots = 2;

    // ⚠️ 调试开关：true = 强制开启激光。
    private const bool DebugForceLaser = false;

    // ==================== 字段 ====================

    /// <summary>主开火冷却计时（秒）。> 0 时不能开火。</summary>
    private float _cd;
    /// <summary>挂载节点（玩家）。Weapon 挂在 Player 下，用于读位置。</summary>
    private Node2D _owner = null!;

    // ---- 连发补射队列：主发射后按固定间隔补射 ExtraShots 发 ----
    /// <summary>剩余待补射的子弹数。</summary>
    private int _pendingShots;
    /// <summary>距下一次补射的剩余时间（秒）。</summary>
    private float _extraTimer;

    // ---- 激光环绕角度（度）：0 = 正右方，顺时针递增 ----
    /// <summary>激光当前发射角度。每帧持续推进（无论是否开火，保持环绕节奏）。</summary>
    private float _laserAngle;

    // ---- 闪现：右键事件触发 + 冷却 ----
    // ⚠️ 文档第六章要求"输入加在 InputState.cs"，但 Core 目录只读（第五章严格 #2），存在冲突。
    //    输入检测暂做在 Weapon 内部，功能等价；如 P0/P1 同意改 InputState 再迁移。
    /// <summary>本帧是否捕获到右键按下事件（_Input 写入，UpdateDash 消费后清零）。</summary>
    private bool _dashQueued;
    /// <summary>闪现冷却计时（秒）。> 0 时不能闪现。</summary>
    private float _dashCd;

    // ⚠️ 调试开关：true = 强制开启闪现。
    private const bool DebugForceDash = false;

    // ==================== 生命周期 ====================

    public override void _Ready()
    {
        _owner = GetParent<Node2D>();
    }

    /// <summary>
    /// 捕获右键"按下"事件（事件驱动，替代轮询边沿检测）。
    /// 一次按下只派发一个 Pressed 事件 —— 按住不松不会重复触发闪现。
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb
            && mb.ButtonIndex == MouseButton.Right
            && mb.Pressed)
        {
            _dashQueued = true;
        }
    }

    public override void _Process(double delta)
    {
        float d = (float)delta;

        // 0) 激光环绕角度持续推进（无论是否开火，保持转动节奏）
        _laserAngle += LaserSpinDegPerSec * d;
        if (_laserAngle >= 360f) _laserAngle -= 360f;   // 取模防无限增长

        // 0.5) 闪现（边沿触发，独立于开火 CD）
        UpdateDash(d);

        // 1) 推进连发补射队列（优先于主开火 CD，保证补射不被吞）
        if (_pendingShots > 0)
        {
            _extraTimer -= d;
            if (_extraTimer <= 0f)
            {
                FireOneBullet();                             // 补射一发
                _pendingShots--;
                _extraTimer += ExtraShotInterval;            // 累加而非赋值，防帧率波动累积漂移
                // ⚠️ 调试日志：仅 DebugForceExtraShots 开启时打印
                if (DebugForceExtraShots) GD.Print($"[连发调试] 补射 1 发，剩余 {_pendingShots} 发");
                if (_pendingShots <= 0) _extraTimer = 0f;    // 补完清零
            }
        }

        // 2) 主开火 CD
        if (_cd > 0f) _cd -= d;
        if (_cd > 0f) return;              // 还在冷却
        if (!InputState.FireHeld) return;  // 没按住左键

        FireMain();
        _cd = Mathf.Max(0.05f, GameManager.I.PlayerStats.Get(PlayerStat.FireCooldown));
    }

    // ==================== 闪现 ====================

    /// <summary>
    /// 闪现：右键"按下瞬间"，向当前移动方向瞬移 DashRange 距离，冷却 DashCooldown 秒。
    /// 策划案：D=120px、T=3s。玩家静止时按右键不闪现（不浪费冷却）。
    /// 瞬移后位置被 ArenaBounds.ClampInside 夹住，不会出屏。
    /// </summary>
    private void UpdateDash(float d)
    {
        // 推进冷却
        if (_dashCd > 0f) _dashCd -= d;

        // 消费本帧捕获的按下事件（无论是否成功闪现都清掉，避免残留到后续帧）
        if (!_dashQueued) return;
        _dashQueued = false;

        var st = GameManager.I.PlayerStats;
        bool enabled = st.HasFlag(PlayerStat.FlagDash);

        // ⚠️ 调试分支：词条系统没好之前强制开启，验证完随 DebugForceDash 一起移除
        if (DebugForceDash) enabled = true;

        if (!enabled) return;
        if (_dashCd > 0f) return;   // 还在冷却

        // 方向 = 当前移动方向（WASD）。静止时不闪现（不浪费冷却）
        Vector2 move = InputState.MoveAxis();
        if (move.LengthSquared() < 0.0001f) return;

        // 瞬移：位置 = 当前 + 单位方向 × 距离，再夹进场内防出屏
        float range = st.Get(PlayerStat.DashRange);
        if (range <= 0f) range = 120f;   // 策划案 D=120px
        _owner.GlobalPosition = ArenaBounds.ClampInside(_owner.GlobalPosition + move.Normalized() * range);

        _dashCd = st.Get(PlayerStat.DashCooldown);
        if (_dashCd <= 0f) _dashCd = 3f;   // 策划案 T=3s

        // ⚠️ 调试日志：仅 DebugForceDash 开启时打印
        if (DebugForceDash) GD.Print($"[闪现调试] 瞬移到 {_owner.GlobalPosition}，冷却 {_dashCd}s");
    }

    // ==================== 开火 ====================

    /// <summary>主发射：射出第一发，并按 ExtraShots 词条排入补射队列。</summary>
    private void FireMain()
    {
        FireOneBullet();

        // 读连发词条层数（词条未生效时为 0 = 不补射）
        int extra = (int)GameManager.I.PlayerStats.Get(PlayerStat.ExtraShots);

        // ⚠️ 调试分支：词条系统没好之前强制开启，验证完随 DebugForceExtraShots 一起移除
        if (DebugForceExtraShots && extra <= 0) extra = DebugExtraShots;

        // ⚠️ 调试日志：仅 DebugForceExtraShots 开启时打印
        if (DebugForceExtraShots) GD.Print($"[连发调试] 主射 1 发，排队补射 {extra} 发");

        if (extra > 0)
        {
            _pendingShots = extra;
            _extraTimer = ExtraShotInterval;   // 第一发补射的等待时间
        }
    }

    /// <summary>
    /// 实际射出一发子弹。主射与补射复用同一逻辑。
    /// 激光词条开启时：方向 = 当前环绕角度、穿透 = 无限；否则：固定向右 + 读 Pierce 词条。
    /// </summary>
    private void FireOneBullet()
    {
        var st = GameManager.I.PlayerStats;
        bool laser = st.HasFlag(PlayerStat.FlagLaser);

        // ⚠️ 调试分支：词条系统没好之前强制开启，验证完随 DebugForceLaser 一起移除
        if (DebugForceLaser) laser = true;

        Vector2 dir = laser
            ? AngleToDirection(_laserAngle)    // 激光：当前环绕角度（顺时针转动中）
            : Vector2.Right;                   // 普通炮弹：固定向右（核心机制，不可瞄准）
        int pierce = laser
            ? LaserPierce                      // 激光：穿透路径上所有敌人
            : (int)st.Get(PlayerStat.Pierce);  // 普通：读穿透词条（默认 0 = 命中即销毁）

        Bus.Pub(new SpawnBulletRequest
        {
            Position  = _owner.GlobalPosition + new Vector2(16f, 0f),   // 枪口偏移
            Direction = dir,
            Speed     = st.Get(PlayerStat.BulletSpeed),
            Damage    = 1,
            Pierce    = pierce,
        });

        Bus.Pub(new SfxRequest { Key = "shoot" });
    }

    /// <summary>
    /// 角度（度）→ 单位方向向量。0° = 正右，90° = 正下（Godot 2D Y 轴向下，正角度顺时针）。
    /// </summary>
    private static Vector2 AngleToDirection(float deg)
    {
        float rad = Mathf.DegToRad(deg);
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}
