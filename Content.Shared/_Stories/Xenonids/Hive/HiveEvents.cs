using Content.Shared.FixedPoint;

namespace Content.Shared._RMC14.Xenonids.Hive;

[ByRefEvent]
public record struct HiveSetTierLimitsEvent(FixedPoint2 T2, FixedPoint2 T3);
