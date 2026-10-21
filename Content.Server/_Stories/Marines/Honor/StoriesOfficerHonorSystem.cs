using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._Stories.Marines.Honor;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.Dataset;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.Tag;
using Content.Shared.Vehicle.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.Marines.Honor;

public sealed class StoriesOfficerHonorSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedRankSystem _ranks = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly ProtoId<DatasetPrototype> RankHierarchy = "StoriesMarineRankHierarchy";
    private const string SeatTag = "RMCSeat";

    private readonly HashSet<EntityUid> _nearby = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<StoriesOfficerHonorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StoriesOfficerHonorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StoriesOfficerHonorComponent, StoriesOfficerHonorActionEvent>(OnHonor);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var reactions = EntityQueryEnumerator<StoriesOfficerHonorReactionComponent>();
        while (reactions.MoveNext(out var uid, out var reaction))
        {
            if (reaction.NextStepAt > now)
                continue;

            if (!IsEligibleMarine(uid) ||
                !Exists(reaction.Target) ||
                !CanSee(uid, reaction.Target, reaction.Range, reaction.CheckVisibility))
            {
                RemCompDeferred<StoriesOfficerHonorReactionComponent>(uid);
                continue;
            }

            switch (reaction.Step)
            {
                case StoriesOfficerHonorReactionStep.Unbuckle:
                    if (TryComp(uid, out BuckleComponent? buckle) &&
                        buckle.BuckledTo is { } strap &&
                        _tags.HasTag(strap, SeatTag) &&
                        !HasComp<StrapVehicleComponent>(strap) &&
                        !HasComp<PowerLoaderComponent>(strap) &&
                        !HasComp<VehicleDriverSeatComponent>(strap) &&
                        !HasComp<VehicleWeaponsSeatComponent>(strap) &&
                        !HasComp<VehiclePortGunSeatComponent>(strap))
                    {
                        _buckle.TryUnbuckle(uid, uid, popup: false);
                    }

                    reaction.Step = StoriesOfficerHonorReactionStep.Stand;
                    ScheduleNextReactionStep(reaction, 0.15f, 0.75f);
                    break;
                case StoriesOfficerHonorReactionStep.Stand:
                    _standing.Stand(uid);
                    reaction.Step = StoriesOfficerHonorReactionStep.Face;
                    ScheduleNextReactionStep(reaction, 0.15f, 0.9f);
                    break;
                case StoriesOfficerHonorReactionStep.Face:
                    Face(uid, _transform.GetWorldPosition(reaction.Target));
                    reaction.Step = StoriesOfficerHonorReactionStep.Salute;
                    ScheduleNextReactionStep(reaction, 0.35f, 1.2f);
                    break;
                case StoriesOfficerHonorReactionStep.Salute:
                    _chat.TryEmoteWithChat(uid, "Salute", forceEmote: true);
                    RemCompDeferred<StoriesOfficerHonorReactionComponent>(uid);
                    break;
            }
        }

        var query = EntityQueryEnumerator<StoriesOfficerHonorForcedWhisperComponent>();
        while (query.MoveNext(out var uid, out var whisper))
        {
            if (whisper.ExpiresAt <= now)
                RemCompDeferred<StoriesOfficerHonorForcedWhisperComponent>(uid);
        }
    }

    private void OnMapInit(Entity<StoriesOfficerHonorComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        _actions.SetUseDelay(ent.Comp.ActionEntity, ent.Comp.Cooldown);
    }

    private void OnShutdown(Entity<StoriesOfficerHonorComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnHonor(Entity<StoriesOfficerHonorComponent> ent, ref StoriesOfficerHonorActionEvent args)
    {
        if (args.Handled || _mobState.IsDead(ent))
            return;

        if (!TryComp(ent, out TransformComponent? transform))
            return;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(transform.Coordinates, ent.Comp.Range, _nearby);

        EntityUid? seniorOfficer = null;
        if (!TryGetRankIndex(ent.Owner, out var officerRank))
            return;
        var seniorOfficerRank = officerRank;
        foreach (var nearby in _nearby)
        {
            if (nearby != ent.Owner &&
                IsEligibleMarine(nearby) &&
                CanSee(ent.Owner, nearby, ent.Comp) &&
                TryGetRankIndex(nearby, out var nearbyRank) && nearbyRank > seniorOfficerRank)
            {
                seniorOfficer = nearby;
                seniorOfficerRank = nearbyRank;
            }
        }

        LocId callout;
        if (seniorOfficer != null)
            callout = "stories-officer-honor-commander";
        else if (ent.Comp.Callouts.Count > 0)
            callout = _random.Pick(ent.Comp.Callouts);
        else
            callout = _random.Pick(DefaultCallouts);

        // The officer announcing a superior must not be affected by their own
        // attention command. Clear any stale honor speech effect before the callout.
        if (seniorOfficer != null)
            RemComp<StoriesOfficerHonorForcedWhisperComponent>(ent);

        _chat.TrySendInGameICMessage(ent, Loc.GetString(callout), InGameICChatType.Speak, false);

        var honorTarget = seniorOfficer ?? ent.Owner;
        var honorTargetRank = seniorOfficer == null ? officerRank : seniorOfficerRank;

        foreach (var nearby in _nearby)
        {
            if (nearby == ent.Owner ||
                nearby == honorTarget ||
                !IsEligibleMarine(nearby) ||
                !TryGetRankIndex(nearby, out var nearbyRank) ||
                nearbyRank >= honorTargetRank ||
                !CanSee(ent.Owner, nearby, ent.Comp))
                continue;

            QueueHonor(nearby, honorTarget, ent.Comp.Range, ent.Comp.CheckVisibility);
            ApplyCommandSpeechEffects(nearby, ent.Comp.ForcedWhisperDuration);
        }

        if (seniorOfficer != null)
        {
            QueueHonor(ent.Owner, seniorOfficer.Value, ent.Comp.Range, ent.Comp.CheckVisibility);
        }

        args.Handled = true;
    }

    private void QueueHonor(EntityUid marine, EntityUid honorTarget, float range, bool checkVisibility)
    {
        var reaction = EnsureComp<StoriesOfficerHonorReactionComponent>(marine);
        reaction.Target = honorTarget;
        reaction.Range = range;
        reaction.CheckVisibility = checkVisibility;
        reaction.Step = StoriesOfficerHonorReactionStep.Unbuckle;
        ScheduleNextReactionStep(reaction, 0.35f, 0.75f);
    }

    private void ScheduleNextReactionStep(StoriesOfficerHonorReactionComponent reaction, float minSeconds, float maxSeconds)
    {
        reaction.NextStepAt = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(minSeconds, maxSeconds));
    }

    private bool CanSee(EntityUid viewer, EntityUid target, StoriesOfficerHonorComponent command)
    {
        return CanSee(viewer, target, command.Range, command.CheckVisibility);
    }

    private bool CanSee(EntityUid viewer, EntityUid target, float range, bool checkVisibility)
    {
        if (checkVisibility)
            return _examine.InRangeUnOccluded(viewer, target, range);

        var viewerCoordinates = _transform.GetMapCoordinates(viewer);
        var targetCoordinates = _transform.GetMapCoordinates(target);
        if (viewerCoordinates.MapId != targetCoordinates.MapId)
            return false;

        var distance = viewerCoordinates.Position - targetCoordinates.Position;
        return range <= 0 || distance.LengthSquared() <= range * range;
    }

    private void Face(EntityUid entity, System.Numerics.Vector2 targetPosition)
    {
        var direction = targetPosition - _transform.GetWorldPosition(entity);
        if (direction.LengthSquared() > 0)
            _transform.SetWorldRotation(entity, direction.ToWorldAngle());
    }

    private bool TryGetRankIndex(EntityUid entity, out int rankIndex)
    {
        rankIndex = -1;
        var rank = _ranks.GetRank(entity);
        if (rank == null || !_prototypes.TryIndex(RankHierarchy, out var hierarchy))
            return false;

        for (var index = 0; index < hierarchy.Values.Count; index++)
        {
            if (hierarchy.Values[index] != rank.ID)
                continue;

            rankIndex = index;
            return true;
        }

        return false;
    }

    private bool IsEligibleMarine(EntityUid entity)
    {
        return HasComp<MarineComponent>(entity) &&
               TryComp(entity, out RankComponent? rank) &&
               rank.Rank != null &&
               !_mobState.IsDead(entity);
    }

    private void ApplyCommandSpeechEffects(EntityUid marine, TimeSpan whisperDuration)
    {
        var whisper = EnsureComp<StoriesOfficerHonorForcedWhisperComponent>(marine);
        whisper.ExpiresAt = _timing.CurTime + whisperDuration;
    }

    private static readonly LocId[] DefaultCallouts =
    {
        "stories-officer-honor-attention-1",
        "stories-officer-honor-attention-2",
        "stories-officer-honor-attention-3",
    };
}
