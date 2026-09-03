using System.Collections.Generic;
using Godot;

namespace GGJ;

/// <summary>
/// 词条抽取与生效。
///
/// TODO(程序B) 待办清单：
///   1. 从 data/player_upgrades/ 和 data/enemy_upgrades/ 目录加载全部 .tres
///   2. 抽取：平铺单池 + 权重随机抽 3 个不重复项
///      · 已满层的过滤掉
///      · 互斥项过滤掉（「四向」与「追踪怪」是否互斥 —— 策划实测后定，见 GDD §4.2）
///      · 保底：池中仍有机制类时，三个选项至少 1 个是机制类
///      · 名刀特例：拥有后触发即消耗，永久移出池
///   3. 生效：选中后 AddModifier 到对应 StatBlock，并记进 GameManager 的 Owned 列表
///   4. 稀有度权重 [待定] —— 策划完成词条设计后再给，别自己拍脑袋定
///      （GDD §11 待办，初版占位 普通 60 / 稀有 30 / 史诗 10）
/// </summary>
public partial class UpgradeService : Node
{
    public override void _Ready()
    {
        Bus.Sub<UpgradeOffered>(this, OnOffered);
        Bus.Sub<UpgradeChosen>(this, OnChosen);
    }

    private void OnOffered(UpgradeOffered e)
    {
        // UI（程序 C）也订阅 UpgradeOffered 并展示三选一，展示内容向 PickChoices 要。
        GD.Print($"[UpgradeService] 需要展示三选一：{(e.ForPlayer ? "玩家线" : "敌人线")}");
    }

    private void OnChosen(UpgradeChosen e)
    {
        GD.Print($"[UpgradeService] 已选择：{(e.ForPlayer ? "玩家线" : "敌人线")}");
    }

    /// <summary>抽 3 个不重复项。当前是占位实现，程序 B 接手后替换。</summary>
    public IReadOnlyList<Resource> PickChoices(bool forPlayer, int count = 3)
    {
        // TODO(程序B)
        return new List<Resource>();
    }

    /// <summary>把选中的词条应用到对应 StatBlock。</summary>
    public void Apply(Resource upgrade)
    {
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
