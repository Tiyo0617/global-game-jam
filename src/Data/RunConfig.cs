using Godot;

namespace GGJ;

/// <summary>
/// 全局数值配置。策划改 data/run_config.tres 即可，不需要改代码。
/// .tres 缺失或损坏时 CreateDefault() 兜底，保证游戏永远跑得起来。
/// </summary>
[GlobalClass]
public partial class RunConfig : Resource
{
    [Export] public int TotalRounds { get; set; } = 8;
    [Export] public int PlayerMaxHP { get; set; } = 3;
    [Export] public float PlayerMoveSpeed { get; set; } = 220f;
    [Export] public float FireCooldown { get; set; } = 0.6f;
    [Export] public float BulletSpeed { get; set; } = 500f;
    [Export] public int EnemyHP { get; set; } = 1;
    [Export] public float EnemyMoveSpeed { get; set; } = 120f;

    [Export] public Godot.Collections.Array<WaveConfig> Waves { get; set; } = new();

    [Export] public PackedScene? PlayerScene { get; set; }
    [Export] public PackedScene? EnemyScene { get; set; }
    [Export] public PackedScene? BulletScene { get; set; }

    /// <summary>按轮次查表（1-based）。表不够长就用最后一轮的参数。</summary>
    public WaveConfig GetWave(int round)
    {
        if (Waves == null || Waves.Count == 0) return WaveConfig.Create(3, 3, 5f);
        int i = Mathf.Clamp(round - 1, 0, Waves.Count - 1);
        return Waves[i] ?? WaveConfig.Create(3, 3, 5f);
    }

    public static RunConfig CreateDefault() => new()
    {
        TotalRounds = 8,
        PlayerMaxHP = 3,
        PlayerMoveSpeed = 220f,
        FireCooldown = 0.6f,
        BulletSpeed = 500f,
        EnemyHP = 1,
        EnemyMoveSpeed = 120f,
        Waves = new Godot.Collections.Array<WaveConfig>
        {
            WaveConfig.Create(3, 3, 5.0f), WaveConfig.Create(3, 4, 5.0f),
            WaveConfig.Create(4, 4, 4.5f), WaveConfig.Create(4, 5, 4.5f),
            WaveConfig.Create(5, 5, 4.0f), WaveConfig.Create(5, 6, 4.0f),
            WaveConfig.Create(6, 6, 3.5f), WaveConfig.Create(6, 7, 3.5f),
        },
        PlayerScene = Res.Packed("res://" + ScenesPath.Player),
        EnemyScene  = Res.Packed("res://" + ScenesPath.Enemy),
        BulletScene = Res.Packed("res://" + ScenesPath.Bullet),
    };
}

/// <summary>场景路径常量。散落在各处写字符串最容易拼错，集中在这里。</summary>
public static class ScenesPath
{
    public const string Player = "Scenes/entities/player.tscn";
    public const string Enemy  = "Scenes/entities/enemy_base.tscn";
    public const string Bullet = "Scenes/entities/bullet.tscn";
    public const string Hud    = "Scenes/ui/hud.tscn";
    public const string Picker = "Scenes/ui/upgrade_picker.tscn";
    public const string Result = "Scenes/ui/result_screen.tscn";
}
