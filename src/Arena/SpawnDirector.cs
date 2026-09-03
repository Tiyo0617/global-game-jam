using Godot;

namespace GGJ;

/// <summary>
/// 波次刷怪导演。**定时推进**：到点就刷，不等上一波清完（GDD §3.7，刻意的压迫感）。
///
/// TODO(程序B)：
///   · 四向 FlagSpawnFourSides —— 出生点改为整个屏幕边缘，方向随机
///   · 精英 FlagElite —— 每轮每波按概率多刷 1 个大体积慢速单位
///   · 追踪怪 FlagTracker —— 每波额外刷若干追踪单位（发 SpawnEnemyRequest 时 IsTracker = true）
/// </summary>
public partial class SpawnDirector : Node
{
    private WaveConfig _wave = WaveConfig.Create(3, 3, 5f);
    private int _round = 1;
    private int _wavesSpawned;
    private float _timer;
    private bool _running;

    public bool AllWavesSpawned => _wavesSpawned >= _wave.WaveCount;
    public int WavesSpawned => _wavesSpawned;
    public int WaveCount => _wave.WaveCount;

    public void BeginRound(int round)
    {
        _round = round;
        _wave = GameManager.I.Cfg.GetWave(round);
        _wavesSpawned = 0;
        _running = true;
        SpawnWave();            // 首波立即刷出，无延迟；计时器在 SpawnWave 末尾重置
    }

    public void Stop() => _running = false;

    public override void _Process(double delta)
    {
        if (!_running || AllWavesSpawned) return;

        _timer -= (float)delta;
        if (_timer > 0f) return;

        SpawnWave();
    }

    /// <summary>密集词条在这里生效：刷怪间隔 -X%。</summary>
    private float WaveInterval =>
        _wave.WaveInterval * (1f - GameManager.I.EnemyStats.Get(EnemyStat.SpawnIntervalReduction));

    private void SpawnWave()
    {
        _wavesSpawned++;

        var st = GameManager.I.EnemyStats;
        int count = _wave.EnemiesPerWave + (int)st.Get(EnemyStat.EnemiesPerWaveBonus);
        int hp = (int)st.Get(EnemyStat.HP);
        float scale = st.Get(EnemyStat.BodyScale);
        if (scale <= 0f) scale = 1f;

        for (int i = 0; i < count; i++)
        {
            var edge = ArenaBounds.Edge.Right;      // TODO(程序B)：四向出生
            Bus.Pub(new SpawnEnemyRequest
            {
                Position  = ArenaBounds.RandomPointOnEdge(edge),
                Direction = ArenaBounds.DefaultDirFrom(edge),
                SpeedMul  = 1f,
                HP        = hp,
                Scale     = scale,
                IsTracker = false,
            });
        }

        Bus.Pub(new WaveStarted(_wavesSpawned, count));

        // ⚠️ 必须在这里重置计时器。若留给下一帧去减，0 - delta &lt; 0 会立刻再刷一波，
        //    表现成"首波和第二波同时出现"。
        _timer = WaveInterval;

        if (AllWavesSpawned)
        {
            _running = false;
            Bus.Pub(new AllWavesSpawned(_round));
        }
    }
}
