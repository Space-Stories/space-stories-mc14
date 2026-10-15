using Content.Server.Chat.Managers;
using Content.Shared._RMC14.Emote;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.Xenonids.Predalien;
using Content.Shared.Chat;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Stories.Xenonids.Predalien;

public sealed class STAbominationAnnounceSystem : EntitySystem
{
    private static readonly SoundSpecifier ElderOverseerSound =
        new SoundCollectionSpecifier("STHunterElderOverseer", AudioParams.Default.WithVolume(-4f));

    private static readonly TimeSpan AnnounceDelay = TimeSpan.FromSeconds(3);

    [Dependency] private readonly ActorSystem _actors = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly SharedRMCEmoteSystem _emote = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAbominationSpawnedEvent>(OnAbominationSpawned);
    }

    private void OnAbominationSpawned(STAbominationSpawnedEvent ev)
    {
        var predalien = ev.Predalien;
        Timer.Spawn(AnnounceDelay, () =>
        {
            if (TerminatingOrDeleted(predalien))
                return;

            AnnounceToHunters();
            _emote.TryEmoteWithChat(predalien, "XenoRoar", forceEmote: true, ignoreActionBlocker: true);
            GreetPlayer(predalien);
        });
    }

    private void GreetPlayer(EntityUid predalien)
    {
        if (_actors.GetSession(predalien) is { } session)
            _chat.DispatchServerMessage(session, Loc.GetString("st-predalien-role-greeting"));
    }

    private void AnnounceToHunters()
    {
        var message = Loc.GetString("st-predalien-spawn-announcement");
        var title = Loc.GetString("st-predalien-spawn-announcement-title");
        var filter = Filter.Empty().AddWhereAttachedEntity(IsLivingHunter);

        var wrappedMessage = $"[bold][font size=16][color=#af0614]{FormattedMessage.EscapeText(title)}[/color][/font][/bold]\n\n" +
                             $"[bold][color=#af0614]{FormattedMessage.EscapeText(message)}[/color][/bold]";

        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, wrappedMessage, default, false, true, null);
        _audio.PlayGlobal(ElderOverseerSound, filter, true);
    }

    private bool IsLivingHunter(EntityUid uid)
    {
        return HasComp<HunterComponent>(uid) &&
               TryComp(uid, out MobStateComponent? mobState) &&
               _mobState.IsAlive(uid, mobState);
    }
}
