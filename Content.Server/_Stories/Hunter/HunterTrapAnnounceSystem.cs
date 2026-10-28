using Content.Server.Chat.Managers;
using Content.Shared._RMC14.Areas;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.Hunter.Traps;
using Content.Shared.Chat;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Stories.Hunter;

public sealed class HunterTrapAnnounceSystem : EntitySystem
{
    [Dependency] private readonly AreaSystem _area = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterTrapComponent, HunterTrapCaughtEvent>(OnCaught);
    }

    private void OnCaught(Entity<HunterTrapComponent> ent, ref HunterTrapCaughtEvent args)
    {
        if (!ent.Comp.Announce)
            return;

        var areaName = _area.TryGetArea(Transform(ent).Coordinates, out _, out var areaProto)
            ? areaProto.Name
            : Loc.GetString("stories-hunter-trap-announce-unknown-area");

        var message = Loc.GetString("stories-hunter-trap-announce", ("area", areaName));
        var wrappedMessage = $"[bold][color=#af0614]{FormattedMessage.EscapeText(message)}[/color][/bold]";
        var filter = Filter.Empty().AddWhereAttachedEntity(IsLivingHunter);

        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, wrappedMessage, default, false, true, null);
    }

    private bool IsLivingHunter(EntityUid uid)
    {
        return HasComp<HunterComponent>(uid) &&
               TryComp(uid, out MobStateComponent? mobState) &&
               _mobState.IsAlive(uid, mobState);
    }
}
