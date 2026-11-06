using Content.Shared._RMC14.Medical.Surgery.Tools;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hunter.Medical.Components.SurgeryTools;

[RegisterComponent] [NetworkedComponent]
public sealed partial class StabilizerGelToolComponent : Component, ICMSurgeryToolComponent
{
    public LocId ToolName => "stabilizer gel";
}
