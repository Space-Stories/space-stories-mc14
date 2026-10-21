using Content.Server.Actions;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Marines;
using Content.Shared._Stories.Marines.Honor;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._Stories.Marines.Silence;
using Content.Shared.Chat;
using Content.Shared.Dataset;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Stories.Marines.Silence;

public sealed class StoriesMarineSilenceSystem : EntitySystem
{
    private static readonly ProtoId<DatasetPrototype> RankHierarchy = "StoriesMarineRankHierarchy";

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedRankSystem _ranks = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _nearby = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<StoriesMarineSilenceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StoriesMarineSilenceComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StoriesMarineSilenceComponent, StoriesMarineSilenceActionEvent>(OnSilence);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<StoriesMarineSilencedForcedWhisperComponent>();
        while (query.MoveNext(out var uid, out var whisper))
        {
            if (whisper.ExpiresAt <= now)
                RemCompDeferred<StoriesMarineSilencedForcedWhisperComponent>(uid);
        }
    }

    private void OnMapInit(Entity<StoriesMarineSilenceComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        _actions.SetUseDelay(ent.Comp.ActionEntity, ent.Comp.Cooldown);
    }

    private void OnShutdown(Entity<StoriesMarineSilenceComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnSilence(Entity<StoriesMarineSilenceComponent> ent, ref StoriesMarineSilenceActionEvent args)
    {
        if (args.Handled || _mobState.IsDead(ent) ||
            !TryComp(ent, out TransformComponent? transform) || !TryGetRankIndex(ent.Owner, out var issuerRank))
            return;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(transform.Coordinates, ent.Comp.Range, _nearby);
        _chat.TrySendInGameICMessage(ent, Loc.GetString(GetCallout(ent.Comp)), InGameICChatType.Speak, false);

        foreach (var nearby in _nearby)
        {
            if (nearby == ent.Owner ||
                !IsEligibleMarine(nearby) ||
                (ent.Comp.CheckVisibility && !_examine.InRangeUnOccluded(ent.Owner, nearby, ent.Comp.Range)) ||
                !TryGetRankIndex(nearby, out var targetRank) ||
                targetRank >= issuerRank)
            {
                continue;
            }

            ApplySilence(nearby, ent.Comp);
        }

        args.Handled = true;
    }

    private void ApplySilence(EntityUid marine, StoriesMarineSilenceComponent command)
    {
        var silence = EnsureComp<StoriesMarineSilencedForcedWhisperComponent>(marine);
        silence.ExpiresAt = _timing.CurTime + command.Duration;
        SendSilencedMessage(marine, command.Authority);
    }

    private void SendSilencedMessage(EntityUid marine, StoriesMarineSilenceAuthority authority)
    {
        if (!_players.TryGetSessionByEntity(marine, out var session))
            return;

        var officer = authority == StoriesMarineSilenceAuthority.Officer;
        var message = Loc.GetString(officer ? "stories-marine-silence-officer-message" : "stories-marine-silence-sergeant-message");
        _chatManager.ChatMessageToOne(
            ChatChannel.Local,
            FormattedMessage.RemoveMarkupOrThrow(message),
            message,
            EntityUid.Invalid,
            false,
            session.Channel,
            officer ? Color.FromHex("#E6D69A") : Color.FromHex("#8B0000"));
    }

    private bool TryGetRankIndex(EntityUid entity, out int rankIndex)
    {
        rankIndex = -1;
        var rank = _ranks.GetRank(entity);
        if (rank == null || !_prototypes.TryIndex(RankHierarchy, out var hierarchy))
        {
            return false;
        }

        var rankId = rank.ID;
        for (var index = 0; index < hierarchy.Values.Count; index++)
        {
            if (hierarchy.Values[index] != rankId)
                continue;

            rankIndex = index;
            return true;
        }

        return false;
    }

    private bool IsEligibleMarine(EntityUid entity)
    {
        return HasComp<MarineComponent>(entity) && HasComp<RankComponent>(entity) && !_mobState.IsDead(entity);
    }

    private LocId GetCallout(StoriesMarineSilenceComponent command)
    {
        if (command.Callouts.Count > 0)
            return _random.Pick(command.Callouts);

        return command.Authority switch
        {
            StoriesMarineSilenceAuthority.Officer => _random.Pick(new[]
            {
                "stories-marine-silence-officer-1", "stories-marine-silence-officer-2", "stories-marine-silence-officer-3",
            }),
            StoriesMarineSilenceAuthority.Sergeant => _random.Pick(new[]
            {
                "stories-marine-silence-sergeant-1", "stories-marine-silence-sergeant-2", "stories-marine-silence-sergeant-3", "stories-marine-silence-sergeant-4",
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(command.Authority), command.Authority, null),
        };
    }
}
