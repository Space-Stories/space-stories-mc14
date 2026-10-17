using System.Numerics;
using Content.Client.StatusIcon;
using Content.Shared._RMC14.CrashLand;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.ParaDrop;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Utility;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Content.Client._RMC14.Marines;

public sealed class MarineSystem : SharedMarineSystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier.Rsi[] FireteamIcons =
    [
        new(new ResPath("_RMC14/Interface/fireteam.rsi"), "hudsquad_ft1"),
        new(new ResPath("_RMC14/Interface/fireteam.rsi"), "hudsquad_ft2"),
        new(new ResPath("_RMC14/Interface/fireteam.rsi"), "hudsquad_ft3"),
    ];
    private static readonly SpriteSpecifier.Rsi FireteamLeaderIcon =
        new(new ResPath("_RMC14/Interface/fireteam.rsi"), "hudsquad_ftl");

    private readonly Dictionary<Texture, Box2?> _fireteamBounds = new();
    private readonly List<MarineHudIcon> _hudIcons = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MarineComponent, GetStatusIconPositionEvent>(OnGetStatusIconPosition);
        _overlays.AddOverlay(new MarineOverlay());
    }

    /// <summary>
    /// Builds the icons actually visible to the local viewer, including faction substitutions.
    /// Both drawing and status icon placement use these positions and bounds.
    /// </summary>
    public void GetHudIcons(EntityUid uid, List<MarineHudIcon> icons)
    {
        icons.Clear();
        if (!TryComp<ShowMarineIconsComponent>(_players.LocalEntity, out var hud) ||
            !HasComp<MarineComponent>(uid) || !TryComp<StatusIconComponent>(uid, out var status) ||
            !TryComp<SpriteComponent>(uid, out var sprite) || !sprite.Visible ||
            _container.IsEntityOrParentInContainer(uid) || HasComp<EntityActiveInvisibleComponent>(uid))
        {
            return;
        }

        var bounds = status.Bounds ?? _sprite.GetLocalBounds((uid, sprite));
        var icon = GetMarineIcon(uid);
        var factionIcons = GetFactionIcons(uid);
        if (hud.Factions != null && !_npcFaction.IsMemberOfAny(uid, hud.Factions) && factionIcons != null &&
            TryComp<NpcFactionMemberComponent>(uid, out var factionMember) &&
            factionMember.Factions.TryFirstOrNull(out var firstFaction) &&
            factionIcons.TryGetValue(firstFaction.Value, out var factionIcon))
        {
            icon.Background = null;
            icon.Icon = factionIcon;
        }

        if (icon.Icon != null)
        {
            var texture = _sprite.Frame0(icon.Icon);
            var position = new Vector2(
                0.1f + (bounds.Width + sprite.Offset.X) / 2f - texture.Width / (float) EyeManager.PixelsPerMeter,
                0.1f + (bounds.Height + sprite.Offset.Y) / 2f - texture.Height / (float) EyeManager.PixelsPerMeter);
            if (HasComp<CrashLandingComponent>(uid) || HasComp<ParaDroppingComponent>(uid))
                position = new Vector2(0.1f) + sprite.Offset;

            if (icon.Background != null && icon.BackgroundColor?.A != 0)
            {
                var background = _sprite.Frame0(icon.Background);
                icons.Add(new MarineHudIcon(background, position, icon.BackgroundColor,
                    Box2.FromDimensions(position, background.Size / (float) EyeManager.PixelsPerMeter)));
            }
            icons.Add(new MarineHudIcon(texture, position, null,
                Box2.FromDimensions(position, texture.Size / (float) EyeManager.PixelsPerMeter)));
        }

        if (TryComp<FireteamMemberComponent>(uid, out var member) &&
            member.Fireteam >= 0 && member.Fireteam < FireteamIcons.Length)
        {
            AddFireteamIcon(FireteamIcons[member.Fireteam]);
        }
        if (HasComp<FireteamLeaderComponent>(uid))
            AddFireteamIcon(FireteamLeaderIcon);

        void AddFireteamIcon(SpriteSpecifier.Rsi specifier)
        {
            if (icon.BackgroundColor?.A == 0)
                return;

            var texture = _sprite.Frame0(specifier);
            if (GetFireteamBounds(specifier, texture) is not { } occupied)
                return;

            var offset = -_sprite.Frame0(FireteamIcons[0]).Height / 2f / EyeManager.PixelsPerMeter;
            var position = new Vector2(
                (bounds.Width + sprite.Offset.X) / 2f - texture.Width / (float) EyeManager.PixelsPerMeter,
                0.1f + (bounds.Height + sprite.Offset.Y + offset) / 2f - texture.Height / (float) EyeManager.PixelsPerMeter);
            icons.Add(new MarineHudIcon(texture, position, icon.BackgroundColor, occupied.Translated(position)));
        }
    }

    private void OnGetStatusIconPosition(Entity<MarineComponent> ent, ref GetStatusIconPositionEvent args)
    {
        if (args.Icon is not SecurityIconPrototype || args.Icon.Layer != StatusIconLayer.Base)
            return;

        GetHudIcons(ent, _hudIcons);
        foreach (var icon in _hudIcons)
        {
            // One pixel separates the status from the lowest visible job/fireteam icon.
            var top = icon.Bounds.Bottom - 1f / EyeManager.PixelsPerMeter;
            args.Position = new Vector2(args.Position.X, Math.Min(args.Position.Y, top - args.Size.Y));
        }
    }

    private Box2? GetFireteamBounds(SpriteSpecifier.Rsi specifier, Texture texture)
    {
        if (_fireteamBounds.TryGetValue(texture, out var cached))
            return cached;

        // The marker is a small glyph inside a much larger transparent frame.
        // Read its alpha bounds once on the CPU, rather than reading back the GPU texture while drawing.
        var path = new ResPath("/Textures") / specifier.RsiPath.ToRelativePath() / (specifier.RsiState + ".png");
        using var stream = _resources.ContentFileRead(path);
        using var image = Image.Load<Rgba32>(stream);
        var pixels = image.GetPixelSpan();
        var left = texture.Width;
        var top = texture.Height;
        var right = 0;
        var bottom = 0;
        for (var y = 0; y < texture.Height; y++)
        {
            for (var x = 0; x < texture.Width; x++)
            {
                if (pixels[y * image.Width + x].A == 0)
                    continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x + 1);
                bottom = Math.Max(bottom, y + 1);
            }
        }

        Box2? occupied = right > left && bottom > top
            ? new Box2(left / (float) EyeManager.PixelsPerMeter,
                (texture.Height - bottom) / (float) EyeManager.PixelsPerMeter,
                right / (float) EyeManager.PixelsPerMeter,
                (texture.Height - top) / (float) EyeManager.PixelsPerMeter)
            : null;
        _fireteamBounds[texture] = occupied;
        return occupied;
    }
}
