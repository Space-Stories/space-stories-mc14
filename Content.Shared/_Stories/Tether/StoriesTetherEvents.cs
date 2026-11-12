using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Tether;

public sealed partial class StoriesTetherResistAlertEvent : BaseAlertEvent;

[Serializable, NetSerializable]
public sealed partial class StoriesTetherResistDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity? Origin;

    public StoriesTetherResistDoAfterEvent(NetEntity? origin)
    {
        Origin = origin;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}

[ByRefEvent]
public readonly record struct StoriesTetherReleasedEvent(EntityUid Tethered, EntityUid? Origin);
