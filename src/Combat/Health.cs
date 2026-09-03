using Godot;

namespace GGJ;

/// <summary>
/// 血量组件。挂在任何可受伤的实体下，**节点名必须叫 "Health"**
/// （DamageSystem 按这个名字找，改名就断链）。
/// </summary>
public partial class Health : Node
{
    [Export] public int MaxHP { get; set; } = 1;

    public int Current { get; private set; }
    public bool IsDead => Current <= 0;
    public bool Invincible => _invTimer > 0f;

    private float _invTimer;

    public override void _Ready()
    {
        Current = MaxHP;
    }

    public override void _Process(double delta)
    {
        if (_invTimer > 0f) _invTimer -= (float)delta;
    }

    public void FullHeal() => Current = MaxHP;

    public void SetMaxHP(int v, bool healToFull = false)
    {
        MaxHP = v;
        Current = healToFull ? MaxHP : Mathf.Min(Current, MaxHP);
    }

    public void StartInvincible(float seconds) => _invTimer = Mathf.Max(_invTimer, seconds);

    public void ClearInvincible() => _invTimer = 0f;

    /// <summary>返回 true 表示本次伤害致死。</summary>
    public bool ApplyDamage(float amount)
    {
        if (Current <= 0) return false;
        if (Invincible) return false;

        Current -= Mathf.Max(1, (int)amount);
        StartInvincible(GameManager.I.Feel?.InvincibleTime ?? 1.5f);

        if (Current <= 0)
        {
            Current = 0;
            return true;
        }
        return false;
    }
}
