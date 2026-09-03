using Godot;

namespace GGJ;

/// <summary>
/// HUD：生命 / 轮次 / 剩余敌人 / 名刀倒计时。
///
/// TODO(程序C)：
///   · 建 Scenes/ui/hud.tscn（CanvasLayer 根节点 + Label）
///   · ProcessMode 必须 Always：HUD 在游戏进行中要刷新，三选一暂停时也要看得见
///     （WhenPaused = 只在暂停时跑，会让 HUD 在战斗中整个冻住）
///   · 订阅 EntityDamaged / RoundStarted / DeathbladeStarted 刷新
/// </summary>
public partial class Hud : CanvasLayer
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Bus.Sub<EntityDamaged>(this, _ => Refresh());
        Bus.Sub<RoundStarted>(this, _ => Refresh());
        Bus.Sub<DeathbladeStarted>(this, _ => Refresh());
    }

    public override void _Process(double delta) => Refresh();

    private void Refresh()
    {
        // TODO(程序C)
    }
}
