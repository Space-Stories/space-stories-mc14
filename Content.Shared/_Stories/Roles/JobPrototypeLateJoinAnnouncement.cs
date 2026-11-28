using Content.Shared.Radio;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared.Roles;
// ReSharper restore CheckNamespace

public sealed partial class JobPrototype
{
    [DataField]
    public LocId? LateJoinAnnouncement { get; private set; }

    [DataField]
    public List<ProtoId<RadioChannelPrototype>> LateJoinChannels { get; private set; } = new();
}
