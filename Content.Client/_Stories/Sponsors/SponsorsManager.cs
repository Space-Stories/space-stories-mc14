using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Stories.Sponsors;
using Robust.Shared.Player;

namespace Content.Client._Stories.Sponsors;

public sealed class SponsorsManager : ISharedSponsorsManager
{
    private SponsorInfo? _info;

    public void Initialize()
    {
    }

    public void SetSponsorInfo(SponsorInfo? info)
    {
        _info = info;
    }

    public bool TryGetInfo([NotNullWhen(true)] out SponsorInfo? sponsor)
    {
        sponsor = _info;
        return _info != null;
    }

    public bool TryGetInfo(ICommonSession? session, [NotNullWhen(true)] out SponsorInfo? sponsor)
    {
        return TryGetInfo(out sponsor);
    }

    public bool IsLoadoutAllowed(ICommonSession? session, string loadoutId)
    {
        if (TryGetInfo(out var sponsor))
        {
            return sponsor.AllowedLoadouts.Contains(loadoutId);
        }

        return false;
    }
}
