using Content.Server._RMC14.Marines;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Dropship.Utility.Systems;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared._Stories.Hermes;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stories.ARES;

public sealed class StoriesThreatAutoDeployerSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedRMCEquipmentDeployerSystem _deployer = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly GunIFFSystem _iff = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<XenoComponent>> _xenos = new();
    private readonly HashSet<Entity<UserIFFComponent>> _iffUsers = new();
    private readonly HashSet<Entity<XenoConstructComponent>> _constructs = new();
    private readonly HashSet<Entity<XenoWeedsComponent>> _weeds = new();
    private readonly HashSet<EntProtoId<IFFFactionComponent>> _factions = new();
    private TimeSpan _nextAnnouncement;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _nextAnnouncement = TimeSpan.Zero;
        _xenos.Clear();
        _iffUsers.Clear();
        _constructs.Clear();
        _weeds.Clear();
        _factions.Clear();
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<StoriesThreatAutoDeployerComponent, RMCEquipmentDeployerComponent>();
        while (query.MoveNext(out var uid, out var auto, out var deployer))
        {
            if (now < auto.NextCheck || TerminatingOrDeleted(uid))
                continue;

            auto.NextCheck = now + auto.CheckInterval;

            if (TryComp(uid, out HermesSentryComponent? sentry) && sentry.LockedDown)
            {
                auto.PendingAnnouncement = false;
                if (!deployer.IsDeployed)
                    _deployer.TryDeploy(uid, true, equipmentDeployerComponent: deployer);
                continue;
            }

            if (deployer.IsDeployed && now < auto.LastThreat + auto.RetractDelay)
            {
                if (auto.PendingAnnouncement)
                    Announce((uid, auto), now);

                continue;
            }

            if (HasThreat((uid, auto)))
            {
                auto.LastThreat = now;
                if (!deployer.IsDeployed && _deployer.TryDeploy(uid, true, equipmentDeployerComponent: deployer))
                    auto.PendingAnnouncement = true;

                if (auto.PendingAnnouncement && deployer.IsDeployed)
                    Announce((uid, auto), now);

                continue;
            }

            if (deployer.IsDeployed &&
                now >= auto.LastThreat + auto.RetractDelay &&
                _deployer.TryDeploy(uid, false, equipmentDeployerComponent: deployer))
            {
                auto.PendingAnnouncement = false;
            }
        }
    }

    private bool HasThreat(Entity<StoriesThreatAutoDeployerComponent> ent)
    {
        if (ent.Comp.LastThreatTarget is { } previous &&
            !TerminatingOrDeleted(previous) &&
            !_container.IsEntityOrParentInContainer(previous) &&
            IsThreat(ent, previous) &&
            _examine.InRangeUnOccluded(ent.Owner, previous, ent.Comp.Range))
            return true;

        ent.Comp.LastThreatTarget = null;
        var coordinates = _transform.GetMapCoordinates(ent.Owner);
        return HasThreat(ent, coordinates, _xenos) ||
               HasThreat(ent, coordinates, _iffUsers) ||
               HasThreat(ent, coordinates, _constructs) ||
               HasThreat(ent, coordinates, _weeds);
    }

    private bool HasThreat<T>(Entity<StoriesThreatAutoDeployerComponent> ent, MapCoordinates coordinates,
        HashSet<Entity<T>> targets) where T : IComponent
    {
        targets.Clear();
        // The visibility check below also checks distance, so fixture overlap tests are unnecessary.
        _lookup.GetEntitiesInRange(coordinates, ent.Comp.Range, targets,
            LookupFlags.Uncontained | LookupFlags.Approximate);

        foreach (var target in targets)
        {
            if (target.Owner == ent.Owner || TerminatingOrDeleted(target) || !IsThreat(ent, target))
                continue;

            if (_examine.InRangeUnOccluded(ent.Owner, target, ent.Comp.Range))
            {
                ent.Comp.LastThreatTarget = target;
                return true;
            }
        }

        return false;
    }

    private bool IsThreat(Entity<StoriesThreatAutoDeployerComponent> ent, EntityUid target)
    {
        if (HasComp<XenoWeedsComponent>(target) || HasComp<XenoConstructComponent>(target))
            return true;

        if (HasComp<XenoComponent>(target))
            return !_mobState.IsDead(target);

        if (TryComp(target, out UserIFFComponent? user))
        {
            if (_mobState.IsDead(target))
                return false;

            _iff.TryGetFactions((target, user), _factions);
            if (!ent.Comp.HermesControlled)
                return !_factions.Contains(ent.Comp.FriendlyFaction);

            return !_factions.Overlaps(ent.Comp.HermesFriendlyFactions);
        }

        return false;
    }

    public void SetFriendlyFactions(Entity<StoriesThreatAutoDeployerComponent> ent, HashSet<string> factions)
    {
        ent.Comp.HermesControlled = true;
        ent.Comp.HermesFriendlyFactions.Clear();
        foreach (var faction in factions)
        {
            if (Content.Shared._RMC14.Sentry.SharedSentryTargetingSystem.SentryFactionToIff.TryGetValue(faction, out var iff))
                ent.Comp.HermesFriendlyFactions.Add(iff);
        }
    }

    private void Announce(Entity<StoriesThreatAutoDeployerComponent> ent, TimeSpan now)
    {
        if (ent.Comp.Announcement is not { } announcement)
        {
            ent.Comp.PendingAnnouncement = false;
            return;
        }

        if (now < _nextAnnouncement)
            return;

        ent.Comp.PendingAnnouncement = false;
        _nextAnnouncement = now + ent.Comp.AnnouncementCooldown;
        _marineAnnounce.AnnounceARES(ent.Owner, Loc.GetString(announcement), ent.Comp.AnnouncementSound);
    }
}
