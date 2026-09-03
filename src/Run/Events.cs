namespace GGJ;

public readonly struct RoundStarted
{
    public readonly int Round;
    public RoundStarted(int round) { Round = round; }
}

public readonly struct RoundWon
{
    public readonly int Round;
    public RoundWon(int round) { Round = round; }
}

public readonly struct RoundLost
{
    public readonly int Round;
    public readonly int TotalDeaths;
    public RoundLost(int round, int totalDeaths) { Round = round; TotalDeaths = totalDeaths; }
}

/// <summary>弹出三选一。ForPlayer = true 是玩家线（失败），false 是敌人线（胜利）。</summary>
public readonly struct UpgradeOffered
{
    public readonly bool ForPlayer;
    public UpgradeOffered(bool forPlayer) { ForPlayer = forPlayer; }
}

/// <summary>三选一选完之后由 UI 发出。</summary>
public readonly struct UpgradeChosen
{
    public readonly bool ForPlayer;
    public UpgradeChosen(bool forPlayer) { ForPlayer = forPlayer; }
}

public readonly struct RunFinished
{
    public readonly int TotalDeaths;
    public readonly float Time;
    public readonly string Rank;
    public RunFinished(int totalDeaths, float time, string rank)
    {
        TotalDeaths = totalDeaths; Time = time; Rank = rank;
    }
}

/// <summary>名刀窗口开始。</summary>
public readonly struct DeathbladeStarted
{
    public readonly float Duration;
    public DeathbladeStarted(float duration) { Duration = duration; }
}
