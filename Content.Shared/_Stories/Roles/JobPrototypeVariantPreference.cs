using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared.Roles;
// ReSharper restore CheckNamespace

public sealed partial class JobPrototype
{
    [DataField]
    public bool SetVariantPreference { get; private set; } = false;

    [DataField]
    public readonly Dictionary<string, LocId>? Variants;

    [DataField]
    public bool HideVariantPreferenceInJobList { get; private set; } = false;
}
