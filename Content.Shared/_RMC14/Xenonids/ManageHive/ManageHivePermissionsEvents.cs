using Content.Shared._RMC14.Xenonids.Hive;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.ManageHive;

[ByRefEvent]
[Serializable, NetSerializable]
public readonly record struct ManageHivePermissionsEvent;

[ByRefEvent]
[Serializable, NetSerializable]
public readonly record struct ManageHivePermissionsHarmEvent;

[Serializable, NetSerializable]
public record ManageHivePermissionsHarmChosenEvent(XenoHarmPermission Choice);

[ByRefEvent]
[Serializable, NetSerializable]
public readonly record struct ManageHivePermissionsConstructionEvent;

[Serializable, NetSerializable]
public record ManageHivePermissionsConstructionChosenEvent(XenoConstructionPermission Choice);

[ByRefEvent]
[Serializable, NetSerializable]
public readonly record struct ManageHivePermissionsDeconstructionEvent;

[Serializable, NetSerializable]
public record ManageHivePermissionsDeconstructionChosenEvent(XenoConstructionPermission Choice);

[ByRefEvent]
[Serializable, NetSerializable]
public readonly record struct ManageHivePermissionsUnnestEvent;

[Serializable, NetSerializable]
public record ManageHivePermissionsUnnestChosenEvent(XenoUnnestPermission Choice);
