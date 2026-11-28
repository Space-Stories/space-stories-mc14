using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Xenonids.Predalien.FeralFrenzy;

[Serializable, NetSerializable]
public sealed partial class STFeralFrenzySingleDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class STFeralFrenzyAoeDoAfterEvent : SimpleDoAfterEvent;
