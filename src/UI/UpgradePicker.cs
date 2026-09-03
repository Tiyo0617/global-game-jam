using Godot;

namespace GGJ;

/// <summary>
/// 三选一界面（游戏完全暂停时显示）。
///
/// TODO(程序C)：
///   · 订阅 UpgradeOffered → 向 UpgradeService.PickChoices 要 3 个选项 → 显示
///   · 点击某项 → UpgradeService.Apply → Bus.Pub(new UpgradeChosen{ ForPlayer = ... })
///   · ProcessMode 必须 WhenPaused（或 Always），否则三选一暂停时点不动
///
/// ⚠️ 在你做出 upgrade_picker.tscn 之前，Main 会挂 AutoUpgradeFallback 自动跳过选择；
///    一旦 res://Scenes/ui/upgrade_picker.tscn 存在，兜底自动失效，由你接管。
/// </summary>
public partial class UpgradePicker : CanvasLayer
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.WhenPaused;
        Bus.Sub<UpgradeOffered>(this, OnOffered);
    }

    private void OnOffered(UpgradeOffered e)
    {
        // TODO(程序C)：
        //   1. var choices = UpgradeService.PickChoices(e.ForPlayer, 3);
        //   2. 把 3 个词条画成卡片（名字 / 描述 / 图标 / 稀有度）
        //   3. 玩家点某张卡 → UpgradeService.Apply(选中的) → Bus.Pub(new UpgradeChosen(e.ForPlayer))
        //   4. Bus.Pub(new UpgradeChosen(...)) 之前不要忘了 GetTree().Paused = false
        GD.Print("[UpgradePicker] 收到三选一请求，UI 待实现");
    }
}
