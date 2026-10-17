using Robust.Shared.Configuration;

namespace Content.Shared._RMC14.CCVar;

public sealed partial class RMCCVars
{
    /// <summary>Marine law catalogue used by police consoles, selected at the start of each round.</summary>
    public static readonly CVarDef<string> RMCMarineLawCatalogue =
        CVarDef.Create("rmc.marine_law_catalogue", "RMCMarineLaw", CVar.SERVERONLY);
}
