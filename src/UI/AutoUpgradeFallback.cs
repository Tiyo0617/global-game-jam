using Godot;

namespace GGJ;

/// <summary>
/// 三选一 UI 还没做出来时的兜底：自动跳过选择，让主循环不卡死。
///
/// 为什么需要：RoundDirector 在轮次结束时会 GetTree().Paused = true 并等 UpgradeChosen。
/// 如果场上没有任何订阅者回应，游戏就会永久暂停（而且没有任何报错）。
/// Scenes/ui/upgrade_picker.tscn 建好后，Main 会优先加载它，本类自动不启用。
/// </summary>
public partial class AutoUpgradeFallback : Node
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;   // 暂停 / 非暂停都要能收到事件
        Bus.Sub<UpgradeOffered>(this, OnOffered);
    }

    private void OnOffered(UpgradeOffered e)
    {
        GD.Print($"[兜底] 三选一 UI 尚未制作，自动跳过（{(e.ForPlayer ? "重打本轮" : "进入下一轮")}）");
        GetTree().Paused = false;
        Bus.Pub(new UpgradeChosen(e.ForPlayer));
    }
}
