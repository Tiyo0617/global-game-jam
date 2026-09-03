using System.Collections.Generic;
using Godot;

namespace GGJ;

/// <summary>
/// 全局单例（Autoload: GameManager）。只存状态和引用，不写玩法逻辑。
/// 谁都可读；只有 Main 和 RoundDirector 可写。
/// </summary>
public partial class GameManager : Node
{
    public static GameManager I { get; private set; } = null!;

    // ---- 两条独立的成长线 ----
    public StatBlock PlayerStats { get; } = new();
    public StatBlock EnemyStats { get; } = new();

    // ---- 配置（Main 启动时注入，.tres 坏了也有代码默认值兜底）----
    public RunConfig Cfg { get; set; } = null!;
    public FeelConfig Feel { get; set; } = null!;
    public StringsData Strings { get; set; } = null!;

    // ---- 运行时状态 ----
    public Player? Player { get; set; }
    public int Round { get; set; } = 1;
    public int TotalDeaths { get; set; }
    public int TotalKills { get; set; }
    public float RunTime { get; set; }

    /// <summary>名刀窗口：玩家不死，但敌人照常被击杀（GDD §4.1.1，是 §3.4 的显式例外）。</summary>
    public bool DeathbladeActive { get; set; }
    public bool DeathbladeConsumed { get; set; }
    public bool HasDeathblade => !DeathbladeConsumed && PlayerStats.HasFlag(PlayerStat.FlagDeathblade);

    public List<PlayerUpgradeData> PlayerUpgrades { get; } = new();
    public List<EnemyUpgradeData> EnemyUpgrades { get; } = new();

    public override void _Ready()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;   // 暂停时仍需计时
    }

    public override void _Process(double delta)
    {
        RunTime += (float)delta;
    }

    public string T(string key) => Strings.Get(key);

    public void ResetRun()
    {
        Round = 1;
        TotalDeaths = 0;
        TotalKills = 0;
        RunTime = 0f;
        DeathbladeActive = false;
        DeathbladeConsumed = false;
        PlayerUpgrades.Clear();
        EnemyUpgrades.Clear();
    }
}
