using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Player;

namespace Content.Shared._Stories.Sponsors;

public interface ISharedSponsorsManager
{
    bool TryGetInfo(ICommonSession? session, [NotNullWhen(true)] out SponsorInfo? sponsor);
    bool IsLoadoutAllowed(ICommonSession? session, string loadoutId);
}
