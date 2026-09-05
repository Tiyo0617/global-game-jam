using Godot;

namespace GGJ;

/// <summary>
/// 调试工具（发布前删掉本文件 + Hud 里那行 Poll 调用即可）。
/// F1：跳过当前轮，视为胜利，进下一轮（三选一暂停 / 结算暂停时不生效）。
/// </summary>
public static class DebugTools
{
    private static bool _f1Down;

    public static void Poll(SceneTree tree)
    {
        bool down = Input.IsKeyPressed(Key.F1);

        // 只在“非暂停 + 按下瞬间”触发一次，避免按住连跳
        if (!tree.Paused && down && !_f1Down)
        {
            // ForPlayer = false → 相当于本轮胜利，RoundDirector 会推进到下一轮
            Bus.Pub(new UpgradeChosen(false));
            GD.Print("[Debug] 跳过当前轮 → 下一轮");
        }

        _f1Down = down;
    }
}
