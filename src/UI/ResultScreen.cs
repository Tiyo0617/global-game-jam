using Godot;

namespace GGJ;

/// <summary>
/// 结算界面。
/// TODO(程序C)：评级 / 总失败次数 / 总用时 / 总击杀 / **最终 build 图标墙**
///   （build 墙是玩家的游戏记忆，GDD §6 明确要有）
/// </summary>
public partial class ResultScreen : CanvasLayer
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;   // 结算发生在非暂停状态，WhenPaused 会收不到
        Bus.Sub<RunFinished>(this, OnFinished);
    }

    private void OnFinished(RunFinished r)
    {
        GD.Print($"[ResultScreen] 通关！评级 {r.Rank}，失败 {r.TotalDeaths} 次，用时 {r.Time:F1}s");
        GetTree().Paused = true;
    }
}
