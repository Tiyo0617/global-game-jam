using Godot;

namespace GGJ;

/// <summary>
/// 敌人基类。恒定速度大小 + 四边光学反射（入射角 = 反射角），不追踪玩家。
///
/// ⚠️ 铁律：Velocity 只允许在 _PhysicsProcess 这三步里写，别处一律不许改：
///   ① 合成所有速度效果 → ② 反射（唯一入口）→ ③ MoveAndSlide
///   多处写 Velocity 是本项目最容易出的 bug，改之前先看架构文档 §5.6。
/// </summary>
public partial class EnemyBase : CharacterBody2D
{
    private Health _health = null!;
    private float _baseSpeed = 120f;
    private float _speedMul = 1f;
    private int _bounceCount;
    private bool _isTracker;

    public bool Active { get; private set; }

    public override void _Ready()
    {
        _health = GetNode<Health>("Health");
        AddToGroup("enemy");

        CollisionLayer = Layers.Enemy;
        CollisionMask  = Layers.Player;     // 故意不勾 Enemy：敌人之间互相穿过
        MotionMode     = MotionModeEnum.Floating;
    }

    public void Configure(Vector2 pos, Vector2 dir, float speedMul, int hp, float scale, bool isTracker)
    {
        GlobalPosition = pos;
        _isTracker = isTracker;
        _speedMul = speedMul;

        _baseSpeed = isTracker
            ? GameManager.I.EnemyStats.Get(EnemyStat.TrackerSpeed)   // 追踪怪：恒定速度，免疫所有 buff
            : GameManager.I.EnemyStats.Get(EnemyStat.MoveSpeed);

        _health.SetMaxHP(hp, healToFull: true);
        // ⚠️ 必须清无敌帧。敌人是从对象池复用的，上一条的无敌计时器可能还没走完，
        //    不清的话新刷出来的敌人会"打不死"（子弹照样销毁，但血量不掉）。
        _health.ClearInvincible();
        _bounceCount = 0;
        Scale = Vector2.One * (scale > 0f ? scale : 1f);

        Velocity = dir.Normalized() * _baseSpeed * _speedMul;
        Active = true;
    }

    public void Deactivate()
    {
        Active = false;
        Velocity = Vector2.Zero;
    }

    public override void _PhysicsProcess(double delta)
    {
        float d = (float)delta;

        Vector2 v = ComposeVelocity(Velocity, d);            // ① 合成
        if (ArenaBounds.Reflect(ref v, GlobalPosition)) OnBounced();   // ② 反射
        Velocity = v;                                        // ③ 移动
        MoveAndSlide();
        GlobalPosition = ArenaBounds.ClampInside(GlobalPosition);
    }

    /// <summary>
    /// 所有"影响速度"的效果都在这里合成。加新 buff（追踪 / 摇摆 / 狂暴）就扩展这个方法，
    /// 不要在别处写 Velocity。
    /// </summary>
    private Vector2 ComposeVelocity(Vector2 current, float d)
    {
        var st = GameManager.I.EnemyStats;

        if (_isTracker)
        {
            var p = GameManager.I.Player;
            if (p != null && GodotObject.IsInstanceValid(p))
                return (p.GlobalPosition - GlobalPosition).Normalized() * _baseSpeed;
            return current;
        }

        if (st.HasFlag(EnemyStat.FlagAccelOnBounce))
        {
            float speed = _baseSpeed * _speedMul;
            return current.LengthSquared() > 0.0001f
                ? current.Normalized() * speed
                : Vector2.Left * speed;
        }

        return current;
    }

    /// <summary>
    /// 撞墙回调：加速反弹，带衰减防后期指数爆炸。
    /// 第 n 次增量 = AccelBase × AccelDecay^(n-1)，硬顶 AccelCap。
    /// </summary>
    private void OnBounced()
    {
        var st = GameManager.I.EnemyStats;
        if (!st.HasFlag(EnemyStat.FlagAccelOnBounce)) return;

        float baseInc = st.Get(EnemyStat.AccelBase);    // 0.20
        float decay   = st.Get(EnemyStat.AccelDecay);   // 0.50
        float cap     = st.Get(EnemyStat.AccelCap);     // 0.50

        float inc = baseInc * Mathf.Pow(decay, _bounceCount);
        _speedMul = Mathf.Min(_speedMul + inc, 1f + cap);
        _bounceCount++;
    }
}
