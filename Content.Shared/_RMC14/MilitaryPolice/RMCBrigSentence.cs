using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.MilitaryPolice;

[Serializable, NetSerializable]
public enum RMCBrigSentenceStatus : byte
{
    Ready,
    Active,
    Paused,
    Served,
    Pardoned,
}

/// <summary>
/// Execution of a particular issued sentence travels with its original report when a prisoner changes cells.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public partial struct RMCBrigSentence
{
    [DataField]
    public RMCBrigSentenceStatus Status;

    [DataField]
    public TimeSpan TimeServed;

    /// <summary>Time of day on the ship's clocks, rather than engine uptime.</summary>
    [DataField]
    public TimeSpan? StartedAt;

    [DataField]
    public TimeSpan? FinishedAt;

    [DataField]
    public string? PardonedBy;

    [DataField]
    public TimeSpan? PardonedAt;

    public readonly string GetDescription()
    {
        var text = Status == RMCBrigSentenceStatus.Active
            ? Loc.GetString("rmc-brig-detention-active")
            : Loc.GetString("rmc-brig-detention-value",
                ("status", Loc.GetString($"rmc-brig-status-{Status}")),
                ("time", FormatTime(TimeServed, roundUp: false)));
        if (StartedAt is { } started)
            text += "\n" + Loc.GetString("rmc-brig-started-at", ("time", started.ToString(@"hh\:mm\:ss")));
        if (FinishedAt is { } finished && PardonedBy == null)
            text += "\n" + Loc.GetString("rmc-brig-finished-at", ("time", finished.ToString(@"hh\:mm\:ss")));
        if (PardonedBy != null)
            text += "\n" + Loc.GetString("rmc-brig-pardoned-by", ("officer", PardonedBy),
                ("time", PardonedAt?.ToString(@"hh\:mm\:ss") ?? ""));
        return text;
    }

    /// <summary>Counts active segments only, retaining checkpoints across pauses and transfers.</summary>
    public readonly TimeSpan GetTimeServed(TimeSpan now, TimeSpan? activeStartedAt, RMCMarineSentence? sentence)
    {
        var served = TimeServed + (activeStartedAt is { } started ? now - started : TimeSpan.Zero);
        if (sentence is { Punishment: RMCMarineLawPunishment.Brig } timed)
            served = TimeSpan.FromTicks(Math.Min(served.Ticks, TimeSpan.FromMinutes(timed.BrigTime).Ticks));
        return served < TimeSpan.Zero ? TimeSpan.Zero : served;
    }

    public static string FormatTime(TimeSpan time, bool roundUp = true)
    {
        var totalSeconds = Math.Max(0, time.TotalSeconds);
        var seconds = (long) (roundUp ? Math.Ceiling(totalSeconds) : Math.Floor(totalSeconds));
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
