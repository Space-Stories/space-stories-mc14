using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Hunter.Traps;

[Serializable, NetSerializable]
public sealed partial class HunterTrapArmDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum HunterTrapVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum HunterTrapVisualState : byte
{
    Disarmed,
    Armed,
    Grass,
    Dirt,
    Caught,
}

[ByRefEvent]
public readonly record struct HunterTrapCaughtEvent(EntityUid Trap, EntityUid Victim);
