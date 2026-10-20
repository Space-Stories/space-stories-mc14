using Content.Shared._RMC14.Dialog;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Xenonids.Banish;

[Serializable, NetSerializable]
public sealed record STManageHiveBanishEvent;

[Serializable, NetSerializable]
public sealed record STManageHiveBanishTargetEvent(NetEntity Target);

[Serializable, NetSerializable]
public sealed record STManageHiveBanishConfirmEvent(NetEntity Target);

[Serializable, NetSerializable]
public sealed record STManageHiveBanishReasonEvent(NetEntity Target, string Message = "") : DialogInputEvent(Message);

[Serializable, NetSerializable]
public sealed record STManageHiveReadmitEvent;

[Serializable, NetSerializable]
public sealed record STManageHiveReadmitTargetEvent(Guid UserId);

[Serializable, NetSerializable]
public sealed record STManageHiveReadmitConfirmEvent(Guid UserId);
