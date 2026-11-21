using Robust.Shared.Configuration;

namespace Content.Shared._RMC14.CCVar;

public sealed partial class RMCCVars
{
    public static readonly CVarDef<float> RequisitionsRequestRatePeriod =
        CVarDef.Create("rmc.requisitions_request_rate_period", 1.0f, CVar.SERVERONLY);

    public static readonly CVarDef<int> RequisitionsRequestRateCount =
        CVarDef.Create("rmc.requisitions_request_rate_count", 1, CVar.SERVERONLY);
}
