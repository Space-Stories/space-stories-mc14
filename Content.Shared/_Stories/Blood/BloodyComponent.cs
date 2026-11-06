using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Blood;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BloodyComponent : Component
{
    [DataField]
    public Color? BloodColor;

    [DataField]
    public bool CanBeStained = true;

    [DataField]
    public bool LeavesTracks = true;

    [DataField, AutoNetworkedField]
    public Color? HandsColor;

    [DataField, AutoNetworkedField]
    public Color? FeetColor;

    [DataField]
    public int HandTransfers;

    [DataField]
    public int MinHandTransfers = 2;

    [DataField]
    public int MaxHandTransfers = 4;

    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> BloodReagents = new()
    {
        "Blood",
        "RMCSynthBlood",
        "CopperBlood",
        "InsectBlood",
        "AmmoniaBlood",
        "ZombieBlood",
        "STHunterBlood",
    };

    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> CleaningReagents = new()
    {
        "Water",
        "SpaceCleaner",
        "SoapReagent",
        "Bleach",
    };

    [DataField]
    public float MinPuddleVolume = 1f;

    [DataField]
    public float VolumePerStep = 5f;

    [DataField]
    public int MaxSteps = 6;

    [DataField]
    public float StepDistance = 0.5f;

    [DataField]
    public float StepSideOffset = 0.1f;

    [DataField]
    public float DragDistance = 0.4f;

    [DataField]
    public int MaxDragSteps = 8;

    [DataField]
    public float MinTrackAlpha = 0.35f;

    [DataField]
    public float MaxTrackAlpha = 0.9f;

    [DataField]
    public float BleedingDragAlpha = 0.8f;

    [DataField]
    public float MaxTrackJump = 2f;

    [DataField]
    public float PuddlePickupVolume = 2f;

    [DataField]
    public float WetSplatterVolume = 10f;

    [DataField]
    public float BaseStainChance = 0.25f;

    [DataField]
    public float StainChancePerDamage = 0.02f;

    [DataField]
    public float SplashChance = 0.33f;

    [DataField]
    public float HeadSplashChance = 0.25f;

    [DataField]
    public float MeleeContactRange = 1.5f;

    [DataField]
    public float MeleeFloorChance = 0.33f;

    [DataField]
    public float RangedFloorChance = 0.15f;

    [DataField]
    public float FloorSplatterShift = 0.3f;

    [DataField]
    public TimeSpan CleanDelay = TimeSpan.FromSeconds(6);

    [ViewVariables]
    public int Steps;

    [ViewVariables]
    public int DragSteps;

    [ViewVariables]
    public Color TrackColor = Color.White;

    [ViewVariables]
    public bool RightFoot;

    [ViewVariables]
    public EntityUid? TrackGrid;

    [ViewVariables]
    public Vector2i LastTile;

    [ViewVariables]
    public Vector2 LastTrackPosition;
}
