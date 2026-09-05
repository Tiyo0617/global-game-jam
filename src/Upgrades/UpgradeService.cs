using System.Collections.Generic;
using Godot;

namespace GGJ;

/// <summary>
/// 词条抽取与生效。
///
/// P2-18 落地清单（照文件顶部 TODO 逐条实现）：
///   ✔ _Ready 时用 DirAccess 扫两个目录加载全部 .tres，加载失败容错跳过
///   ✔ 抽 3 个不重复；过滤已满层的（对照 GameManager 已有层数 vs MaxStack）
///   ✔ 保底：池里还有机制类时，3 个选项至少 1 个机制类
///   ✔ 名刀特例：DeathbladeConsumed == true 后永久移出池
///   ✔ 四向 × 追踪互斥：GDD 标注"待实测"，先按不互斥实现（TODO 见 PickChoices）
/// </summary>
public partial class UpgradeService : Node
{
    private readonly List<PlayerUpgradeData> _playerPool = new();
    private readonly List<EnemyUpgradeData> _enemyPool = new();

    public override void _Ready()
    {
        LoadPools();
        Bus.Sub<UpgradeOffered>(this, OnOffered);
        Bus.Sub<UpgradeChosen>(this, OnChosen);

        // ===== ⚠️ 临时诊断日志：定位"未拿分裂卡就分裂/首波空刷"用，定位后整段删除 =====
        Bus.Sub<RoundStarted>(this, e => GD.Print(
            $"[诊断] ===== 第 {e.Round} 轮开始（t={Time.GetTicksMsec()}ms）FlagSplit={GameManager.I.EnemyStats.HasFlag(EnemyStat.FlagSplit)} ====="));
        Bus.Sub<WaveStarted>(this, e => GD.Print(
            $"[诊断] 第 {e.WaveIndex} 波刷出，本波 {e.Count} 只（t={Time.GetTicksMsec()}ms）"));
        Bus.Sub<EnemySpawned>(this, e =>
        {
            var pos = e.Enemy is Node2D n ? n.GlobalPosition : Vector2.Zero;
            GD.Print($"[诊断] 敌人出生 @({pos.X:F0},{pos.Y:F0})（t={Time.GetTicksMsec()}ms）");
        });
        Bus.Sub<EntityDied>(this, e =>
        {
            if (e.TargetIsPlayer) return;   // 玩家死亡不记，只记敌人
            GD.Print($"[诊断] 敌人死亡 @({e.Position.X:F0},{e.Position.Y:F0})（t={Time.GetTicksMsec()}ms）");
        });
        // ===== 临时诊断结束 =====
    }

    // ==================== 词条池加载 ====================

    /// <summary>
    /// 扫描两个词条目录加载全部 .tres。
    /// ⚠️ 单个文件加载失败必须容错跳过（缺一张卡不能崩整局）。
    /// </summary>
    private void LoadPools()
    {
        LoadDir("res://data/player_upgrades", _playerPool);
        LoadDir("res://data/enemy_upgrades", _enemyPool);
        GD.Print($"[UpgradeService] 词条池加载完成：玩家 {_playerPool.Count} 张，敌人 {_enemyPool.Count} 张");
    }

    private void LoadDir<T>(string dir, List<T> pool) where T : Resource
    {
        using var d = DirAccess.Open(dir);
        if (d == null)
        {
            GD.PushWarning($"[UpgradeService] 词条目录打不开（跳过）：{dir}");
            return;
        }

        foreach (var f in d.GetFiles())
        {
            if (!f.EndsWith(".tres")) continue;

            var res = ResourceLoader.Load<T>(dir + "/" + f);
            if (res == null)
            {
                GD.PushWarning($"[UpgradeService] 词条加载失败（跳过）：{f}");
                continue;
            }
            pool.Add(res);
        }
    }

    // ==================== 事件响应 ====================

    private void OnOffered(UpgradeOffered e)
    {
        GD.Print($"[UpgradeService] 需要展示三选一：{(e.ForPlayer ? "玩家线" : "敌人线")}");
    }

    private void OnChosen(UpgradeChosen e)
    {
        GD.Print($"[UpgradeService] 已选择：{(e.ForPlayer ? "玩家线" : "敌人线")}");
    }

    // ==================== 抽取 ====================

    /// <summary>
    /// 抽 count 个不重复词条。
    /// 规则：满层过滤 → 名刀消耗过滤 → 平铺随机抽 → 保底至少 1 个机制类。
    /// </summary>
    public IReadOnlyList<Resource> PickChoices(bool forPlayer, int count = 3)
    {
        var result = new List<Resource>();

        if (forPlayer)
        {
            // 候选池：过滤满层（同名卡在 GameManager 已有层数 vs MaxStack）
            var candidates = new List<PlayerUpgradeData>();
            foreach (var c in _playerPool)
            {
                // 名刀特例：触发即消耗，永久移出池
                if (c.Stat == PlayerStat.FlagDeathblade && GameManager.I.DeathbladeConsumed)
                    continue;

                int owned = CountOwned(GameManager.I.PlayerUpgrades, c.DisplayName);
                if (owned >= c.MaxStack) continue;   // 已满层，不再出现

                candidates.Add(c);
            }

            Shuffle(candidates);

            // 抽 count 个
            for (int i = 0; i < candidates.Count && result.Count < count; i++)
                result.Add(candidates[i]);

            // 保底：池里还有机制类时，3 个选项至少 1 个机制类
            EnsureMechanic(candidates, result);
        }
        else
        {
            var candidates = new List<EnemyUpgradeData>();
            foreach (var c in _enemyPool)
            {
                int owned = CountOwned(GameManager.I.EnemyUpgrades, c.DisplayName);
                if (owned >= c.MaxStack) continue;

                candidates.Add(c);
            }

            Shuffle(candidates);

            for (int i = 0; i < candidates.Count && result.Count < count; i++)
                result.Add(candidates[i]);

            EnsureMechanic(candidates, result);
        }

        // TODO(词条调优)：「四向」×「追踪怪」互斥 —— GDD 标注"待实测，由策划定"，
        // 当前按不互斥实现。策划拍板后在这里加互斥过滤（同批选项中二选一）。

        return result;
    }

    /// <summary>数一下玩家/敌人已拥有列表里同名卡出现几次（即当前层数）。</summary>
    private static int CountOwned(List<PlayerUpgradeData> owned, string displayName)
    {
        int n = 0;
        foreach (var o in owned) if (o.DisplayName == displayName) n++;
        return n;
    }

    private static int CountOwned(List<EnemyUpgradeData> owned, string displayName)
    {
        int n = 0;
        foreach (var o in owned) if (o.DisplayName == displayName) n++;
        return n;
    }

    /// <summary>
    /// 保底规则（GDD §4.3）：池中仍有机制类时，三个选项至少 1 个是机制类。
    /// 实现：结果里没有机制类时，从候选里找出机制类换掉结果中的一个非机制类。
    /// </summary>
    private static void EnsureMechanic<T>(List<T> candidates, List<Resource> result) where T : Resource
    {
        bool hasMechanic = false;
        foreach (var r in result)
            if (IsMechanic(r)) { hasMechanic = true; break; }
        if (hasMechanic) return;

        // 结果全非机制 → 从候选（未进结果的）里找一个机制类换进来
        var inResult = new HashSet<Resource>(result);
        foreach (var c in candidates)
        {
            Resource res = c;
            if (inResult.Contains(res)) continue;
            if (!IsMechanic(res)) continue;

            // 换掉结果里的第一个非机制类
            for (int i = 0; i < result.Count; i++)
            {
                if (!IsMechanic(result[i])) { result[i] = res; return; }
            }
            result.Add(res);   // 结果不足 count 时直接补
            return;
        }
    }

    private static bool IsMechanic(Resource r) => r switch
    {
        PlayerUpgradeData p => p.IsMechanic,
        EnemyUpgradeData e => e.IsMechanic,
        _ => false,
    };

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Rng.Index(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ==================== 生效 ====================

    /// <summary>把选中的词条应用到对应 StatBlock（数值立即生效）。</summary>
    public void Apply(Resource upgrade)
    {
        // ⚠️ 临时诊断：记录每张卡的生效时刻与目标线（定位分裂 bug 用，定位后删除）
        if (upgrade is PlayerUpgradeData pd)
            GD.Print($"[诊断] 玩家线生效：{pd.DisplayName}（t={Time.GetTicksMsec()}ms）");
        else if (upgrade is EnemyUpgradeData ed)
            GD.Print($"[诊断] 敌人线生效：{ed.DisplayName}（t={Time.GetTicksMsec()}ms）");

        if (upgrade is PlayerUpgradeData p)
        {
            GameManager.I.PlayerStats.AddModifier(new StatModifier(p.Stat, p.Op, p.Value));
            GameManager.I.PlayerUpgrades.Add(p);
        }
        else if (upgrade is EnemyUpgradeData en)
        {
            GameManager.I.EnemyStats.AddModifier(new StatModifier(en.Stat, en.Op, en.Value));
            GameManager.I.EnemyUpgrades.Add(en);
        }
    }
}
