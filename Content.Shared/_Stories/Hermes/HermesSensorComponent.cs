using Robust.Shared.Audio;

namespace Content.Shared._Stories.Hermes;

[RegisterComponent]
public sealed partial class HermesSensorComponent : Component
{
    [DataField] public string Group = "AresCore";
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(60);
    [DataField] public bool AllowTemporary = true;
    [DataField] public bool RevokeOnExit;
    [DataField] public Direction ExitDirection = Direction.South;
    [DataField] public SoundSpecifier DetectionSound = new SoundPathSpecifier("/Audio/Machines/beep.ogg");
    [DataField] public SoundSpecifier RevocationSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");
}
