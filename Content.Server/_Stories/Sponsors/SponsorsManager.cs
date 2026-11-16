using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Shared._Stories.Hunter.Profiles;
using Content.Shared._Stories.SCCVars;
using Content.Shared._Stories.Sponsors;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Stories.Sponsors;

public sealed class SponsorsManager : ISharedSponsorsManager
{
    private readonly Dictionary<NetUserId, SponsorInfo> _cachedSponsors = new();
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private readonly HttpClient _httpClient = new();
    [Dependency] private readonly IServerNetManager _netMgr = default!;
    private string _apiUrl = string.Empty;

    private ISawmill _sawmill = default!;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("sponsors");
        _cfg.OnValueChanged(SCCVars.SponsorsApiUrl, s => _apiUrl = s, true);

        _netMgr.Connecting += OnConnecting;
        _netMgr.Connected += OnConnected;
        _netMgr.Disconnect += OnDisconnect;
    }

    public bool TryGetInfo(NetUserId userId, [NotNullWhen(true)] out SponsorInfo? sponsor)
    {
        return _cachedSponsors.TryGetValue(userId, out sponsor);
    }

    public bool TryGetInfo(ICommonSession? session, [NotNullWhen(true)] out SponsorInfo? sponsor)
    {
        sponsor = null;
        if (session == null)
            return false;
        return TryGetInfo(session.UserId, out sponsor);
    }

    public bool IsLoadoutAllowed(ICommonSession? session, string loadoutId)
    {
        if (TryGetInfo(session, out var sponsor))
        {
            return sponsor.AllowedLoadouts.Contains(loadoutId);
        }

        return false;
    }

    private async Task OnConnecting(NetConnectingArgs e)
    {
        var info = await LoadSponsorInfo(e.UserId);
        if (info?.Tier == null)
        {
            _cachedSponsors.Remove(e.UserId);
            return;
        }

        DebugTools.Assert(!_cachedSponsors.ContainsKey(e.UserId), "Cached data was found on client connect");

        _cachedSponsors[e.UserId] = info;
    }

    private void OnConnected(object? sender, NetChannelArgs e)
    {
        var info = _cachedSponsors.TryGetValue(e.Channel.UserId, out var sponsor) ? sponsor : null;
        var ev = new SponsorInfoUpdatedEvent { Info = info };
        _entityManager.EntityNetManager.SendSystemNetworkMessage(ev, e.Channel);
    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs e)
    {
        _cachedSponsors.Remove(e.Channel.UserId);
    }

    private async Task<SponsorInfo?> LoadSponsorInfo(NetUserId userId)
    {
        if (string.IsNullOrEmpty(_apiUrl))
            return null;

        var url = $"{_apiUrl}/{userId.ToString()}";
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url);
        }
        catch (HttpRequestException e)
        {
            _sawmill.Error($"Failed to connect to sponsors API: {e.Message}");
            return null;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            _sawmill.Error(
                "Failed to get player sponsor info from API: [{StatusCode}] {Response}",
                response.StatusCode,
                errorText);
            return null;
        }

        var apiInfo = await response.Content.ReadFromJsonAsync<ApiSponsorInfo>();
        if (apiInfo == null)
            return null;

        return new SponsorInfo
        {
            Tier = apiInfo.Tier,
            TierName = apiInfo.TierName,
            OOCColor = apiInfo.OOCColor,
            HavePriorityJoin = apiInfo.HavePriorityJoin,
            AllowedMarkings = apiInfo.AllowedMarkings,
            AllowedTTSVoices = apiInfo.AllowedTTSVoices ?? Array.Empty<string>(),
            AllowedLoadouts = apiInfo.AllowedLoadouts ?? Array.Empty<string>(),
            RoleTimeBypass = apiInfo.RoleTimeBypass,
            WhitelistRoleTimeBypass = apiInfo.WhitelistRoleTimeBypass,
            GhostSkin = apiInfo.GhostSkin,
            SponsorPoints = apiInfo.SponsorPoints,
            SponsorPointsAlt = apiInfo.SponsorPointsAlt,
            XenoSkins = apiInfo.XenoSkins,
            CanPlayHunter = apiInfo.CanPlayHunter,
            CanUseHunterCustomization = apiInfo.CanUseHunterCustomization,
            MaxHunterStatus = apiInfo.MaxHunterStatus,
        };
    }

    private sealed record ApiSponsorInfo
    {
        [JsonPropertyName("tier")]
        public int? Tier { get; set; }

        [JsonPropertyName("tierName")]
        public string? TierName { get; set; }

        [JsonPropertyName("oocColor")]
        public string? OOCColor { get; set; }

        [JsonPropertyName("priorityJoin")]
        public bool HavePriorityJoin { get; set; }

        [JsonPropertyName("allowedMarkings")]
        public string[] AllowedMarkings { get; set; } = Array.Empty<string>();

        [JsonPropertyName("allowedTTSVoices")]
        public string[] AllowedTTSVoices { get; set; } = Array.Empty<string>();

        [JsonPropertyName("allowedLoadouts")]
        public string[] AllowedLoadouts { get; set; } = Array.Empty<string>();

        [JsonPropertyName("roleTimeBypass")]
        public bool RoleTimeBypass { get; set; }

        [JsonPropertyName("whitelistRoleTimeBypass")]
        public bool WhitelistRoleTimeBypass { get; set; }

        [JsonPropertyName("ghostSkin")]
        public string GhostSkin { get; set; } = "MobObserver";

        [JsonPropertyName("sponsorPoints")]
        public int SponsorPoints { get; set; }

        [JsonPropertyName("sponsorPointsAlt")]
        public int SponsorPointsAlt { get; set; }

        [JsonPropertyName("xenoSkins")]
        public string[] XenoSkins { get; set; } = Array.Empty<string>();

        [JsonPropertyName("canPlayHunter")]
        public bool CanPlayHunter { get; set; }

        [JsonPropertyName("canUseHunterCustomization")]
        public bool CanUseHunterCustomization { get; set; }

        [JsonPropertyName("maxHunterStatus")]
        public HunterStatus MaxHunterStatus { get; set; } = HunterStatus.Normal;
    }
}
