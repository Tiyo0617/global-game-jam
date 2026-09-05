using System;
using System.Collections.Generic;
using Godot;

namespace GGJ;

/// <summary>
/// 战斗内 HUD。
/// 左上角：轮次·波次 / 血条（数字叠中间，按血量变色）。
/// 右上角：设置（= 暂停）。
/// 设置弹窗：我方强化 / 敌方强化 / 继续游戏 / 返回主菜单（自动存档）。
/// </summary>
public partial class Hud : UiBase
{
    private const string MainMenuScenePath = "res://Scenes/ui/main_menu.tscn";

    private Label _roundLabel = null!;
    private ProgressBar _hpBar = null!;
    private Label _hpText = null!;
    private StyleBoxFlat _hpFill = null!;

    private Control _myBuffsOverlay = null!;
    private VBoxContainer _myBuffsList = null!;
    private Control _enemyBuffsOverlay = null!;
    private VBoxContainer _enemyBuffsList = null!;
    private Control _settingsOverlay = null!;

    private int _currentWave = 1;

    protected override void OnUiReady()
    {
        Root = new Control();
        Root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(Root);

        BuildTopLeft(Root);
        BuildTopRight(Root);
        _settingsOverlay = BuildSettingsOverlay();   // 先建设置（暂停）
        BuildBuffsOverlays();                        // 后建强化弹窗 → 在设置之上

        Bus.Sub<EntityDamaged>(this, _ => Refresh());
        Bus.Sub<RoundStarted>(this, _ => { _currentWave = 1; Refresh(); });
        Bus.Sub<WaveStarted>(this, e => { _currentWave = e.WaveIndex; Refresh(); });
        Bus.Sub<DeathbladeStarted>(this, _ => Refresh());

        Refresh();
    }

    /// <summary>每帧轮询调试热键（F1 跳关）。</summary>
    public override void _Process(double delta) => DebugTools.Poll(GetTree());

    // ---------- 布局 ----------

    private void BuildTopLeft(Control parent)
    {
        var corner = new MarginContainer();
        corner.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        corner.AddThemeConstantOverride("margin_left", 16);
        corner.AddThemeConstantOverride("margin_top", 16);
        corner.MouseFilter = Control.MouseFilterEnum.Ignore;
        parent.AddChild(corner);

        var box = new VBoxContainer();
        box.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        box.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        box.AddThemeConstantOverride("separation", 10);
        corner.AddChild(box);

        _roundLabel = new Label();
        _roundLabel.AddThemeFontSizeOverride("font_size", 26);
        box.AddChild(_roundLabel);

        // 血条 + 中间数字
        var barWrap = new Control { CustomMinimumSize = new Vector2(240, 28) };
        box.AddChild(barWrap);

        _hpBar = new ProgressBar { MinValue = 0, MaxValue = 3, Value = 3, ShowPercentage = false };
        _hpBar.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _hpFill = new StyleBoxFlat { BgColor = new Color(0.3f, 0.8f, 0.35f) };
        _hpBar.AddThemeStyleboxOverride("fill", _hpFill);
        barWrap.AddChild(_hpBar);

        _hpText = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _hpText.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _hpText.AddThemeFontSizeOverride("font_size", 16);
        barWrap.AddChild(_hpText);
    }

    private void BuildTopRight(Control parent)
    {
        var corner = new MarginContainer();
        corner.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        corner.AddThemeConstantOverride("margin_right", 16);
        corner.AddThemeConstantOverride("margin_top", 16);
        corner.MouseFilter = Control.MouseFilterEnum.Ignore;
        parent.AddChild(corner);

        var btn = MakeButton(T("hud_settings"), OnSettingsPressed, hoverFx: false, minSize: new Vector2(120, 44), fontSize: 22);
        btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        btn.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        corner.AddChild(btn);
    }

    private void BuildBuffsOverlays()
    {
        _myBuffsOverlay = BuildOverlay(T("hud_my_buffs"), out var myContent);
        _myBuffsList = new VBoxContainer();
        _myBuffsList.AddThemeConstantOverride("separation", 6);
        myContent.AddChild(_myBuffsList);
        myContent.AddChild(MakeButton(T("menu_back"), () => HideOverlay(_myBuffsOverlay), hoverFx: false, minSize: new Vector2(120, 36), fontSize: 18));

        _enemyBuffsOverlay = BuildOverlay(T("hud_enemy_buffs"), out var enemyContent);
        _enemyBuffsList = new VBoxContainer();
        _enemyBuffsList.AddThemeConstantOverride("separation", 6);
        enemyContent.AddChild(_enemyBuffsList);
        enemyContent.AddChild(MakeButton(T("menu_back"), () => HideOverlay(_enemyBuffsOverlay), hoverFx: false, minSize: new Vector2(120, 36), fontSize: 18));
    }

    private Control BuildSettingsOverlay()
    {
        var rows = new Control[]
        {
            MakeButton(T("hud_my_buffs"), OnMyBuffsPressed, hoverFx: false, minSize: new Vector2(240, 44), fontSize: 20),
            MakeButton(T("hud_enemy_buffs"), OnEnemyBuffsPressed, hoverFx: false, minSize: new Vector2(240, 44), fontSize: 20),
            MakeButton(T("hud_continue"), OnContinueGame, hoverFx: false, minSize: new Vector2(240, 48), fontSize: 22),
            MakeButton(T("hud_back_menu"), OnBackToMenu, hoverFx: false, minSize: new Vector2(240, 48), fontSize: 22),
        };
        return BuildOverlay(T("hud_settings"), rows);
    }

    // ---------- 刷新 ----------

    private void Refresh()
    {
        int round = GameManager.I.Round;
        _roundLabel.Text = string.Format(T("hud_round"), round, _currentWave);

        int hp = 0;
        int max = (int)GameManager.I.PlayerStats.Get(PlayerStat.MaxHP);
        var p = GameManager.I.Player;
        if (p != null && GodotObject.IsInstanceValid(p))
        {
            var health = p.HealthComp;
            if (GodotObject.IsInstanceValid(health))
            {
                hp = health.Current;
                max = health.MaxHP;
            }
        }

        _hpBar.MaxValue = Math.Max(1, max);
        _hpBar.Value = Mathf.Clamp(hp, 0, max);
        _hpText.Text = hp + "/" + max;
        _hpFill.BgColor = HpColor(hp);
    }

    /// <summary>血条颜色：3+ 绿，2 黄，1 红。</summary>
    private static Color HpColor(int hp) =>
        hp >= 3 ? new Color(0.3f, 0.8f, 0.35f)
        : hp == 2 ? new Color(0.95f, 0.75f, 0.2f)
        : new Color(0.85f, 0.25f, 0.25f);

    // ---------- 按钮 ----------

    private void OnMyBuffsPressed() => ShowBuffs(forPlayer: true);
    private void OnEnemyBuffsPressed() => ShowBuffs(forPlayer: false);

    private void OnSettingsPressed()
    {
        GetTree().Paused = true;
        ShowOverlay(_settingsOverlay);
    }

    private void OnContinueGame()
    {
        HideOverlay(_settingsOverlay);
        GetTree().Paused = false;
    }

    private void OnBackToMenu()
    {
        SaveService.I?.SaveNow();   // 退出即自动存档
        GetTree().Paused = false;   // 先取消暂停，否则主菜单会被冻结
        GetTree().ChangeSceneToFile(MainMenuScenePath);
    }

    // ---------- 强化列表 ----------

    private void ShowBuffs(bool forPlayer)
    {
        var list = forPlayer ? _myBuffsList : _enemyBuffsList;
        var overlay = forPlayer ? _myBuffsOverlay : _enemyBuffsOverlay;

        foreach (var child in list.GetChildren()) child.QueueFree();

        var entries = BuildBuffEntries(forPlayer);
        if (entries.Count == 0)
        {
            var empty = new Label { Text = T("hud_no_buffs"), HorizontalAlignment = HorizontalAlignment.Center };
            empty.AddThemeFontSizeOverride("font_size", 20);
            list.AddChild(empty);
        }
        else
        {
            foreach (var e in entries) list.AddChild(e);
        }

        ShowOverlay(overlay);
    }

    private List<Control> BuildBuffEntries(bool forPlayer)
    {
        var groups = new Dictionary<string, BuffGroup>();

        if (forPlayer)
        {
            foreach (var u in GameManager.I.PlayerUpgrades)
                if (u != null) AddToGroup(groups, u.DisplayName, u.Description);
        }
        else
        {
            foreach (var u in GameManager.I.EnemyUpgrades)
                if (u != null) AddToGroup(groups, u.DisplayName, u.Description);
        }

        var entries = new List<Control>();
        foreach (var g in groups.Values)
        {
            var nameLabel = new Label
            {
                Text = g.Count > 1 ? g.Name + " ×" + g.Count : g.Name,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 22);
            entries.Add(nameLabel);

            if (!string.IsNullOrEmpty(g.Description))
            {
                var d = new Label { Text = g.Description, HorizontalAlignment = HorizontalAlignment.Left };
                d.AddThemeFontSizeOverride("font_size", 16);
                d.Modulate = new Color(0.82f, 0.82f, 0.82f, 1f);
                entries.Add(d);
            }
        }
        return entries;
    }

    private static void AddToGroup(Dictionary<string, BuffGroup> groups, string name, string desc)
    {
        if (!groups.TryGetValue(name, out var g))
        {
            g = new BuffGroup { Name = name, Description = desc };
            groups[name] = g;
        }
        g.Count++;
    }

    private sealed class BuffGroup
    {
        public string Name = "";
        public string Description = "";
        public int Count;
    }
}
