// Stories-ZLevels
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Rules;

[ByRefEvent]
public readonly record struct RMCPlanetMapLoadedEvent(EntityUid MapUid, ResPath Path);
