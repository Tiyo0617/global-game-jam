using System;
using System.Collections.Generic;
using Godot;

namespace GGJ;

/// <summary>
/// 主菜单。正上方游戏名 + 四个按钮：开始游戏 / 新游戏 / 开发者团队 / 退出游戏。
/// 继承 UiBase 复用：文案、字体兜底、按钮、弹窗、淡入淡出、悬停缩放。
/// </summary>
public partial class MainMenu : UiBase
{
    private const string GameScenePath = "res://Main.tscn";
    private const string BackgroundPath = "res://art/bg_full_1280x720.jpg";

    private Control _creditsOverlay = null!;
    private Control _slotsOverlay = null!;
    private VBoxContainer _slotsList = null!;
    private readonly List<Control> _fadeItems = new();
    private bool _leaving;

    protected override void OnUiReady()
    {
        BuildUi();
        FadeIn(_fadeItems);
    }

    private void BuildUi()
    {
        Root = new Control();
        Root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(Root);

        AddBackground(Root);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Root.AddChild(center);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 20);
        center.AddChild(layout);

        // 标题
        var title = new Label
        {
            Text = T("menu_title"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 72);
        layout.AddChild(title);
        _fadeItems.Add(title);

        // 标题与按钮之间留白
        layout.AddChild(new Control { CustomMinimumSize = new Vector2(0, 40) });

        var continueBtn = MakeButton(T("menu_continue"), OnContinuePressed);
        var newGameBtn = MakeButton(T("menu_new_game"), OnNewGamePressed);
        var creditsBtn = MakeButton(T("menu_credits"), OnCreditsPressed);
        var quitBtn = MakeButton(T("menu_quit"), OnQuitPressed);

        layout.AddChild(continueBtn);
        layout.AddChild(newGameBtn);
        layout.AddChild(creditsBtn);
        layout.AddChild(quitBtn);

        _fadeItems.Add(continueBtn);
        _fadeItems.Add(newGameBtn);
        _fadeItems.Add(creditsBtn);
        _fadeItems.Add(quitBtn);

        _creditsOverlay = BuildCreditsOverlay();
        _slotsOverlay = BuildSlotsOverlay();
    }

    /// <summary>背景：优先静态图 res://art/background.png；没有就纯色。动图可换成 AnimatedTexture / AnimatedSprite2D。</summary>
    private void AddBackground(Control parent)
    {
        var tex = Res.Load<Texture2D>(BackgroundPath);
        if (tex != null)
        {
            var tr = new TextureRect
            {
                Texture = tex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            tr.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            parent.AddChild(tr);
            return;
        }

        var bg = new ColorRect
        {
            Color = new Color(0.08f, 0.09f, 0.13f, 1f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        parent.AddChild(bg);
    }

    // ---------- 按钮 ----------

    private void OnContinuePressed()
    {
        RefreshSlots();
        ShowOverlay(_slotsOverlay);
    }

    private void OnNewGamePressed()
    {
        var svc = SaveService.I;
        int slot = svc != null ? svc.PickNewSlot() : 0;
        svc?.StartNew(slot);
        Bus.Pub(new NewGameRequested());   // 通知 P0：新开一局
        StartGame();
    }

    private void OnCreditsPressed() => ShowOverlay(_creditsOverlay);

    private void OnQuitPressed() => GetTree().Quit();

    private void OnSlotPressed(int slot)
    {
        if (!SaveService.Has(slot)) return;   // 空位点了无反应

        SaveService.I?.Continue(slot);        // 设置 ActiveSlot + PendingResume
        _slotsOverlay.Visible = false;
        Bus.Pub(new ContinueGameRequested(slot));   // 通知 P0：读档恢复
        StartGame();
    }

    private void CloseCredits() => HideOverlay(_creditsOverlay);
    private void CloseSlots() => HideOverlay(_slotsOverlay);

    private void StartGame()
    {
        if (_leaving) return;
        _leaving = true;
        FadeOut(Root, 0.3f, () => GetTree().ChangeSceneToFile(GameScenePath));
    }

    // ---------- 弹窗 ----------

    private Control BuildCreditsOverlay()
    {
        var rows = new Control[]
        {
            MakeCreditLine(T("credits_role_design"), T("credits_name_design")),
            MakeCreditLine(T("credits_role_program"), T("credits_name_program")),
            MakeCreditLine(T("credits_role_art"), T("credits_name_art")),
            MakeButton(T("menu_back"), CloseCredits),
        };
        return BuildOverlay(T("credits_title"), rows);
    }

    private Control BuildSlotsOverlay()
    {
        _slotsOverlay = BuildOverlay(T("menu_continue"), out var content);
        _slotsList = new VBoxContainer();
        _slotsList.AddThemeConstantOverride("separation", 8);
        content.AddChild(_slotsList);
        content.AddChild(MakeButton(T("menu_back"), CloseSlots));
        return _slotsOverlay;
    }

    /// <summary>重建存档位列表（每个位 = 继续按钮 + 删除按钮）。</summary>
    private void RefreshSlots()
    {
        foreach (var child in _slotsList.GetChildren()) child.QueueFree();

        for (int i = 0; i < SaveService.SlotCount; i++)
        {
            int slot = i;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var info = MakeButton(SlotButtonText(slot), () => OnSlotPressed(slot), hoverFx: false, minSize: new Vector2(300, 56), fontSize: 18);
            info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(info);

            var del = MakeButton(T("menu_delete"), () => OnDeleteSlot(slot), hoverFx: false, minSize: new Vector2(72, 56), fontSize: 18);
            row.AddChild(del);

            _slotsList.AddChild(row);
        }
    }

    private void OnDeleteSlot(int slot)
    {
        if (!SaveService.Has(slot)) return;   // 空位点了无反应
        SaveService.Delete(slot);
        RefreshSlots();
    }

    /// <summary>存档位按钮文案：名字 + 关键进度。</summary>
    private string SlotButtonText(int slot)
    {
        var d = SaveService.Read(slot);
        string head = T("menu_slot") + " " + (slot + 1);
        if (d == null) return head + "\n" + T("save_empty");

        string rating = d.Finished ? d.Rank : T("save_unfinished");
        string timeLabel = d.Finished ? T("save_time") : T("save_time_elapsed");
        return head + "\n"
            + T("save_round") + " " + d.Round + "    "
            + T("save_rating") + " " + rating + "\n"
            + T("save_deaths") + " " + d.TotalDeaths + "    "
            + timeLabel + " " + FormatTime(d.RunTime);
    }

    private static string FormatTime(float sec)
    {
        int s = (int)sec;
        int m = s / 60;
        s %= 60;
        return m + ":" + s.ToString("00");
    }

    private Control MakeCreditLine(string role, string name)
    {
        var label = new Label
        {
            Text = role + "  " + name,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        return label;
    }
}
