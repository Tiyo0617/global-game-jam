using Godot;

namespace GGJ;

/// <summary>
/// 特效与手感反馈。GDD §7-7 明确：受伤反馈**四件套缺一不可**
/// —— 无敌帧闪烁（已在 Player 里）+ 顿帧 + 震屏 + 受伤音效。
///
/// TODO(程序C)：
///   · 顿帧 HitStop：Engine.TimeScale = 0，持续 Feel.HitStopTime 秒后恢复 1
///     （用 SceneTreeTimer 时要注意 TimeScale=0 会让 Timer 停住，改用 Process 计数或 OS 时间）
///   · 震屏：订阅 EntityDamaged，抖 Camera2D 的 Offset，强度取 Feel.ShakeStrength
///   · 命中火花 / 死亡爆散：对象池 + AnimatedSprite2D
/// </summary>
public partial class Fx : Node
{
    private bool _inHitStop;
    private ulong _hitStopEndMs;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;   // 顿帧时自己不能被自己冻住
        Bus.Sub<EntityDamaged>(this, OnDamaged);
        Bus.Sub<EntityDied>(this, OnDied);
    }

    private void OnDamaged(EntityDamaged e)
    {
        // TODO(程序C)：震屏 + 火花
        if (e.TargetIsPlayer) HitStop(GameManager.I.Feel?.HitStopTime ?? 0.05f);
    }

    private void OnDied(EntityDied e)
    {
        // TODO(程序C)：死亡爆散
    }

    /// <summary>
    /// 顿帧。
    ///
    /// ⚠️ 两个坑，都踩过：
    ///   ① Engine.TimeScale = 0 之后**必须有代码把它恢复成 1**，否则全场永久静止
    ///      （表现：玩家一挨打，子弹不动、刷怪停摆，而且没有任何报错）。
    ///   ② TimeScale = 0 时 delta 恒为 0，**不能用 delta 倒计时**，
    ///      必须用不受 TimeScale 影响的时钟（这里用 Time.GetTicksMsec）。
    /// </summary>
    public void HitStop(float seconds)
    {
        if (seconds <= 0f) return;

        ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000f);
        if (end > _hitStopEndMs) _hitStopEndMs = end;

        if (_inHitStop) return;
        _inHitStop = true;
        Engine.TimeScale = 0.0;
    }

    public override void _Process(double delta)
    {
        if (!_inHitStop) return;
        if (Time.GetTicksMsec() < _hitStopEndMs) return;

        _inHitStop = false;
        Engine.TimeScale = 1.0;
    }
}
