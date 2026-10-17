using Content.Shared._RMC14.MilitaryPolice;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.MilitaryPolice;

[RegisterComponent]
public sealed partial class RMCClueScannerComponent : Component
{
    [DataField]
    public TimeSpan ScanDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public float Range = 2;

    [DataField]
    public int MaxClues = 256;

    [DataField]
    public EntProtoId<SkillDefinitionComponent> Skill = "RMCSkillPolice";

    [DataField]
    public int RequiredSkill = 2;

    [DataField]
    public List<RMCPoliceClue> Clues = new();

    [DataField]
    public bool Scanning;
}
