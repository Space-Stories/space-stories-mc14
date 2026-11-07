using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Weapons.Parry;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(ParrySystem))]
public sealed partial class ParryComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Chance;

    [DataField, AutoNetworkedField]
    public ParryType BlockType = ParryType.Directional;

    [DataField, AutoNetworkedField]
    public float ProjectileMultiplier = 0.2f;

    [DataField, AutoNetworkedField]
    public bool BlockPounce;

    [DataField, AutoNetworkedField]
    public bool Shield;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Stories/Weapons/Parry/parry.ogg");
}

public enum ParryType : byte
{
    Directional,
    DirectionalTwoHands,
    Absolute,
    AbsoluteTwoHands,
}

public enum ParryAttack : byte
{
    Melee,
    Projectile,
    Pounce,
}
