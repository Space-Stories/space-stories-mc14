using Robust.Shared.Configuration;

namespace Content.Shared._RMC14.CCVar;

public sealed partial class RMCCVars
{
    public static readonly CVarDef<int> RMCDropshipAutopilotDefaultDelaySeconds =
        CVarDef.Create("rmc.dropship_autopilot_default_delay_seconds", 30, CVar.SERVER);

    public static readonly CVarDef<int> RMCDropshipAutopilotMinDelaySeconds =
        CVarDef.Create("rmc.dropship_autopilot_min_delay_seconds", 10, CVar.SERVER);

    public static readonly CVarDef<int> RMCDropshipAutopilotMaxDelaySeconds =
        CVarDef.Create("rmc.dropship_autopilot_max_delay_seconds", 120, CVar.SERVER);

    public static readonly CVarDef<int> RMCDropshipAutopilotRetrySeconds =
        CVarDef.Create("rmc.dropship_autopilot_retry_seconds", 10, CVar.SERVER);
}
