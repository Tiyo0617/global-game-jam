using Godot;

namespace GGJ;

/// <summary>
/// 入口。只做三件事：加载配置 → 建服务 → 启动轮次。
/// ⚠️ 主场景保持极简，实体一律运行时装配，不要在 Main.tscn 里堆节点。
/// </summary>
public partial class Main : Node2D
{
    public override void _Ready()
    {
        ArenaBounds.Init(GetViewportRect());

        // .tres 缺失或损坏时用代码默认值兜底，保证永远跑得起来
        GameManager.I.Cfg     = Res.Load<RunConfig>("res://data/run_config.tres")   ?? RunConfig.CreateDefault();
        GameManager.I.Feel    = Res.Load<FeelConfig>("res://data/feel_config.tres") ?? FeelConfig.CreateDefault();
        GameManager.I.Strings = Res.Load<StringsData>("res://data/strings.tres")    ?? StringsData.CreateDefault();

        InitStats();

        // 顺序有讲究：EnemyService / BulletService 要在 RoundDirector 之前
        var enemies  = new EnemyService  { Name = "EnemyService" };
        var bullets  = new BulletService { Name = "BulletService" };
        var spawner  = new SpawnDirector { Name = "SpawnDirector" };
        var upgrades = new UpgradeService { Name = "UpgradeService" };
        var fx       = new Fx { Name = "Fx" };
        var audio    = new AudioService { Name = "AudioService" };
        var rounds   = new RoundDirector { Name = "RoundDirector" };

        AddChild(enemies);
        AddChild(bullets);
        AddChild(spawner);
        AddChild(upgrades);
        AddChild(fx);
        AddChild(audio);
        AddChild(rounds);

        enemies.Init(GameManager.I.Cfg.EnemyScene);
        bullets.Init(GameManager.I.Cfg.BulletScene);

        SpawnPlayer();

        // UI：程序 C 建好场景后自动生效；场景不存在就跳过。
        // ⚠️ 三选一必须有回应者，否则轮次结束后会永久暂停 —— 所以缺 Picker 时挂兜底。
        TryAddUi(ScenesPath.Hud);
        if (!TryAddUi(ScenesPath.Picker)) AddChild(new AutoUpgradeFallback { Name = "AutoUpgradeFallback" });
        TryAddUi(ScenesPath.Result);

        rounds.Init(spawner, enemies);
        rounds.StartRun();
    }

    private bool TryAddUi(string path)
    {
        var scene = Res.Packed("res://" + path);
        if (scene == null) return false;
        AddChild(scene.Instantiate());
        return true;
    }

    private void SpawnPlayer()
    {
        var scene = GameManager.I.Cfg.PlayerScene;
        if (scene == null)
        {
            GD.PushWarning("[Main] 没有 PlayerScene。检查 data/run_config.tres 的 PlayerScene 字段。");
            return;
        }
        var p = scene.Instantiate<Player>();
        p.Name = "Player";
        AddChild(p);
    }

    /// <summary>
    /// 把配置写进两条属性线。**基础值只在这里设置一次**，
    /// 之后所有加成都通过 AddModifier 叠加，不要回头改 base。
    /// </summary>
    private void InitStats()
    {
        var cfg = GameManager.I.Cfg;
        var feel = GameManager.I.Feel;

        var ps = GameManager.I.PlayerStats;
        ps.SetBase(PlayerStat.MaxHP, cfg.PlayerMaxHP);
        ps.SetBase(PlayerStat.MoveSpeed, cfg.PlayerMoveSpeed);
        ps.SetBase(PlayerStat.FireCooldown, cfg.FireCooldown);
        ps.SetBase(PlayerStat.BulletSpeed, cfg.BulletSpeed);
        ps.SetBase(PlayerStat.InvincibleTime, feel.InvincibleTime);
        ps.SetBase(PlayerStat.Pierce, 0f);
        ps.SetBase(PlayerStat.ExtraShots, 0f);
        ps.SetBase(PlayerStat.HitboxScale, 1f);
        ps.SetBase(PlayerStat.LifestealKills, 0f);
        ps.SetBase(PlayerStat.LifestealAmount, 0f);
        ps.SetBase(PlayerStat.DashRange, 120f);
        ps.SetBase(PlayerStat.DashCooldown, 3f);
        ps.SetBase(PlayerStat.RicochetChance, 0.5f);
        ps.SetBase(PlayerStat.FlagLaser, 0f);
        ps.SetBase(PlayerStat.FlagRicochet, 0f);
        ps.SetBase(PlayerStat.FlagDash, 0f);
        ps.SetBase(PlayerStat.FlagDeathblade, 0f);
        ps.SetBase(PlayerStat.DeathbladeWindow, 8f);

        var es = GameManager.I.EnemyStats;
        es.SetBase(EnemyStat.HP, cfg.EnemyHP);
        es.SetBase(EnemyStat.MoveSpeed, cfg.EnemyMoveSpeed);
        es.SetBase(EnemyStat.SpawnIntervalReduction, 0f);
        es.SetBase(EnemyStat.EnemiesPerWaveBonus, 0f);
        es.SetBase(EnemyStat.BodyScale, 1f);
        es.SetBase(EnemyStat.FlagAccelOnBounce, 0f);
        es.SetBase(EnemyStat.AccelBase, 0.20f);
        es.SetBase(EnemyStat.AccelDecay, 0.50f);
        es.SetBase(EnemyStat.AccelCap, 0.50f);
        es.SetBase(EnemyStat.FlagElite, 0f);
        es.SetBase(EnemyStat.EliteChance, 0.20f);
        es.SetBase(EnemyStat.EliteHPMul, 5f);
        es.SetBase(EnemyStat.EliteSpeedMul, 0.4f);
        es.SetBase(EnemyStat.FlagSpawnFourSides, 0f);
        es.SetBase(EnemyStat.FlagSplit, 0f);
        es.SetBase(EnemyStat.FlagTracker, 0f);
        es.SetBase(EnemyStat.TrackerCount, 2f);
        es.SetBase(EnemyStat.TrackerSpeed, 40f);
        es.SetBase(EnemyStat.TrackerHP, 1f);   // [待实测] 初值 1，若"喂养"撞击流则改 2
    }
}
