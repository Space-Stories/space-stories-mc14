namespace Content.Shared._RMC14.Tackle;

/// <summary>Raised on the tackled target to let other systems shift its tackle Min/Max thresholds. Raise both together - a caste whose Max sits below the new Min still knocks it down early otherwise.</summary>
[ByRefEvent]
public record struct TackleGetThresholdsEvent(int Min, int Max);
