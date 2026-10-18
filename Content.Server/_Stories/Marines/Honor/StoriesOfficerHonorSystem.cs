using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Marines;
using Content.Shared._Stories.Marines.Honor;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.Dataset;
using Content.Shared._RMC14.Stun;
using Content.Shared.Buckle;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
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
    [Dependency] private readonly RMCDazedSystem _dazed = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedRankSystem _ranks = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly ProtoId<DatasetPrototype> RankHierarchy = "RMCMarineRankHierarchy";

    private readonly HashSet<EntityUid> _nearby = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<StoriesStoriesOfficerHonorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StoriesOfficerHonorComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StoriesOfficerHonorComponent, StoriesStoriesOfficerHonorActionEvent>(OnHonor);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
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

        var now = _timing.CurTime;
        if (ent.Comp.NextHonorAt > now || !TryComp(ent, out TransformComponent? transform))
            return;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(transform.Coordinates, ent.Comp.Range, _nearby);

        EntityUid? seniorOfficer = null;
        if (!TryGetRankIndex(ent.Owner, out var officerRank))
            return;
        foreach (var nearby in _nearby)
        {
            if (nearby != ent.Owner &&
                IsEligibleMarine(nearby) &&
                CanSee(ent.Owner, nearby, ent.Comp) &&
                TryGetRankIndex(nearby, out var nearbyRank) && nearbyRank > officerRank)
            {
                seniorOfficer = nearby;
                break;
            }
        }

        var callout = seniorOfficer == null
            ? _random.Pick(new[] { "stories-officer-honor-attention-1", "stories-officer-honor-attention-2", "stories-officer-honor-attention-3" })
            : "stories-officer-honor-commander";

        // The officer announcing a superior must not be affected by their own
        // attention command. Clear any stale honor speech effect before the callout.
        if (seniorOfficer != null)
            RemComp<StoriesStoriesOfficerHonorForcedWhisperComponent>(ent);

        _chat.TrySendInGameICMessage(ent, Loc.GetString(callout), InGameICChatType.Speak, false);

        var honorTarget = seniorOfficer ?? ent.Owner;
        if (!TryGetRankIndex(honorTarget, out var honorTargetRank))
            honorTargetRank = officerRank;
        if (honorTarget != ent.Owner && TryComp(honorTarget, out StoriesOfficerHonorComponent? honoredOfficer))
        {
            honoredOfficer.NextHonorAt = now + honoredOfficer.Cooldown;
            _actions.SetCooldown(honoredOfficer.ActionEntity, honoredOfficer.Cooldown);
        }

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
            ApplyCommandSpeechEffects(nearby);

            if (TryComp(nearby, out StoriesOfficerHonorComponent? officer))
            {
                officer.NextHonorAt = now + officer.Cooldown;
                _actions.SetCooldown(officer.ActionEntity, officer.Cooldown);
            }
        }

        if (seniorOfficer != null)
        {
            QueueHonor(ent.Owner, seniorOfficer.Value, ent.Comp.Range, ent.Comp.CheckVisibility);
        }

        ent.Comp.NextHonorAt = now + ent.Comp.Cooldown;
        _actions.SetCooldown(ent.Comp.ActionEntity, ent.Comp.Cooldown);
        args.Handled = true;
    }

    private void QueueHonor(EntityUid marine, EntityUid honorTarget, float range, bool checkVisibility)
    {
        // Keep a short acknowledgement delay, then give every movement in the
        // response substantial independent jitter so salutes do not synchronize.
        var reactionDelay = TimeSpan.FromSeconds(_random.NextFloat(0.35f, 0.75f));
        Timer.Spawn(reactionDelay, () =>
        {
            if (!IsEligibleMarine(marine) ||
                !Exists(honorTarget) ||
                !CanSee(marine, honorTarget, range, checkVisibility))
            {
                return;
            }

            _buckle.TryUnbuckle(marine, marine, popup: false);

            var standDelay = TimeSpan.FromSeconds(_random.NextFloat(0.15f, 0.75f));
            Timer.Spawn(standDelay, () =>
            {
                if (!IsEligibleMarine(marine) ||
                    !Exists(honorTarget) ||
                    !CanSee(marine, honorTarget, range, checkVisibility))
                {
                    return;
                }

                _standing.Stand(marine);

                var turnDelay = TimeSpan.FromSeconds(_random.NextFloat(0.15f, 0.9f));
                Timer.Spawn(turnDelay, () =>
                {
                    if (!IsEligibleMarine(marine) ||
                        !Exists(honorTarget) ||
                        !CanSee(marine, honorTarget, range, checkVisibility))
                    {
                        return;
                    }

                    Face(marine, _transform.GetWorldPosition(honorTarget));

                    var saluteDelay = TimeSpan.FromSeconds(_random.NextFloat(0.35f, 1.2f));
                    Timer.Spawn(saluteDelay, () =>
                    {
                        if (IsEligibleMarine(marine) &&
                            Exists(honorTarget) &&
                            CanSee(marine, honorTarget, range, checkVisibility))
                        {
                            _chat.TryEmoteWithChat(marine, "Salute", forceEmote: true);
                        }
                    });
                });
            });
        });
    }

    private bool CanSee(EntityUid viewer, EntityUid target, StoriesOfficerHonorComponent command)
    {
        return CanSee(viewer, target, command.Range, command.CheckVisibility);
    }

    private bool CanSee(EntityUid viewer, EntityUid target, float range, bool checkVisibility)
    {
        return !checkVisibility || _examine.InRangeUnOccluded(viewer, target, range);
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

    private void ApplyCommandSpeechEffects(EntityUid marine)
    {
        _dazed.TryDaze(marine, TimeSpan.FromSeconds(1), refresh: true, stutter: true);

        var whisper = EnsureComp<StoriesOfficerHonorForcedWhisperComponent>(marine);
        whisper.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(13);
}
}
