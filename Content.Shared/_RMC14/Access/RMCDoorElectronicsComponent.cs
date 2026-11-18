using Content.Shared.Access;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Access;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(RMCDoorElectronicsSystem))]
public sealed partial class RMCDoorElectronicsComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<AccessGroupPrototype> AccessCatalog = RMCDoorElectronicsSystem.MarineAccessCatalog;

    [DataField(required: true), AutoNetworkedField]
    public HashSet<ProtoId<AccessLevelPrototype>> EditorAccess = new();

    [DataField, AutoNetworkedField]
    public string? LastOperator;

    [DataField, AutoNetworkedField]
    public bool Locked = true;

    [DataField, AutoNetworkedField]
    public bool RequireAll;

    [DataField, AutoNetworkedField]
    public List<ProtoId<AccessLevelPrototype>> SelectedAccesses = new();
}

[Serializable, NetSerializable]
public enum RMCDoorElectronicsUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class RMCDoorElectronicsSetModeMessage(bool requireAll) : BoundUserInterfaceMessage
{
    public readonly bool RequireAll = requireAll;
}
