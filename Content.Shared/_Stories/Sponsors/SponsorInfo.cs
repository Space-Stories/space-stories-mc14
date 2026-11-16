using Content.Shared._Stories.Hunter.Profiles;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Sponsors;

[Serializable, NetSerializable]
public sealed class SponsorInfo
{
    [DataField("tier")]
    public int? Tier { get; set; }

    [DataField("tierName")]
    public string? TierName { get; set; }

    [DataField("oocColor")]
    public string? OOCColor { get; set; }

    [DataField("priorityJoin")]
    public bool HavePriorityJoin { get; set; }

    [DataField("allowedMarkings")]
    public string[] AllowedMarkings { get; set; } = Array.Empty<string>();

    [DataField("allowedTTSVoices")]
    public string[] AllowedTTSVoices { get; set; } = Array.Empty<string>();

    [DataField("allowedLoadouts")]
    public string[] AllowedLoadouts { get; set; } = Array.Empty<string>();

    [DataField("roleTimeBypass")]
    public bool RoleTimeBypass { get; set; }

    [DataField("whitelistRoleTimeBypass")]
    public bool WhitelistRoleTimeBypass { get; set; }

    [DataField("ghostSkin")]
    public string GhostSkin { get; set; } = "MobObserver";

    [DataField("sponsorPoints")]
    public int SponsorPoints { get; set; }

    [DataField("sponsorPointsAlt")]
    public int SponsorPointsAlt { get; set; }

    [DataField("xenoSkins")]
    public string[] XenoSkins { get; set; } = Array.Empty<string>();

    [DataField("canPlayHunter")]
    public bool CanPlayHunter { get; set; }

    [DataField("canUseHunterCustomization")]
    public bool CanUseHunterCustomization { get; set; }

    [DataField("maxHunterStatus")]
    public HunterStatus MaxHunterStatus { get; set; } = HunterStatus.Normal;
}
