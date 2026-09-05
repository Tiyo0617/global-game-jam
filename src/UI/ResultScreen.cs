using System;
using Godot;

namespace GGJ;

/// <summary>
/// 结算界面。打满所有轮次（RunFinished）后显示并暂停。
/// 从上到下：评分等级（按总失败次数）/ 挑战已用时 / 再次挑战·返回主菜单。
/// </summary>
public partial class ResultScreen : UiBase
{
    private const string MainMenuScenePath = "res://Scenes/ui/main_menu.tscn";
    private const string GameScenePath = "res://Main.tscn";

    private Label _rankLabel = null!;
    private Label _deathsLabel = null!;
    private Label _timeLabel = null!;

    protected override void OnUiReady()
    {
        Root = new Control();
        Root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Root.Visible = false;   // 平时隐藏，RunFinished 才显示
        AddChild(Root);

        var dim = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.8f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Root.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Root.AddChild(center);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 24);
        center.AddChild(box);

        // 评分等级（最上面，大号）
        _rankLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _rankLabel.AddThemeFontSizeOverride("font_size", 120);
        box.AddChild(_rankLabel);

        // 总死亡次数
        _deathsLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _deathsLabel.AddThemeFontSizeOverride("font_size", 26);
        box.AddChild(_deathsLabel);

        // 挑战已用时
        _timeLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _timeLabel.AddThemeFontSizeOverride("font_size", 26);
        box.AddChild(_timeLabel);

        // 按钮（游戏内不缩放）
        box.AddChild(MakeButton(T("result_retry"), OnRetry, hoverFx: false, minSize: new Vector2(280, 56), fontSize: 24));
        box.AddChild(MakeButton(T("result_back_menu"), OnBackToMenu, hoverFx: false, minSize: new Vector2(280, 56), fontSize: 24));

        Bus.Sub<RunFinished>(this, OnFinished);
    }

    private void OnFinished(RunFinished r)
    {
        GetTree().Paused = true;   // 结算时暂停，玩家不能再动

        _rankLabel.Text = Rating.RankOf(r.TotalDeaths);
        _deathsLabel.Text = T("result_deaths") + "  " + r.TotalDeaths;
        _timeLabel.Text = T("result_time") + "  " + FormatTime(r.Time);

        Root.Visible = true;
        Root.Modulate = new Color(1f, 1f, 1f, 0f);
        var tw = CreateTween();
        tw.TweenProperty(Root, "modulate:a", 1f, 0.5f)
          .SetTrans(Tween.TransitionType.Quad)
          .SetEase(Tween.EaseType.Out);
    }

    private void OnRetry()
    {
        // 再次挑战 = 新游戏：重进 Main.tscn 从头打
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(GameScenePath);
    }

    private void OnBackToMenu()
    {
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(MainMenuScenePath);
    }

    private static string FormatTime(float sec)
    {
        int s = (int)sec;
        int m = s / 60;
        s %= 60;
        return m + ":" + s.ToString("00");
    }
}
