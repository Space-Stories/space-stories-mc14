using Content.Server._RMC14.Marines;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Dropship.Utility.Systems;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Stories.ARES;

public sealed class StoriesThreatAutoDeployerSystem : EntitySystem
{
    [Dependency] private readonly SharedRMCEquipmentDeployerSystem _deployer = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly GunIFFSystem _iff = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _nearby = new();
    private TimeSpan _nextAnnouncement;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _nextAnnouncement = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<StoriesThreatAutoDeployerComponent, RMCEquipmentDeployerComponent>();
        while (query.MoveNext(out var uid, out var auto, out var deployer))
        {
            if (now < auto.NextCheck)
                continue;

            auto.NextCheck = now + auto.CheckInterval;

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
        _nearby.Clear();
        _lookup.GetEntitiesInRange(ent.Owner, ent.Comp.Range, _nearby, LookupFlags.Uncontained);

        foreach (var target in _nearby)
        {
            if (!IsThreat(ent, target))
                continue;

            if (_examine.InRangeUnOccluded(ent.Owner, target, ent.Comp.Range))
                return true;
        }

        return false;
    }

    private bool IsThreat(Entity<StoriesThreatAutoDeployerComponent> ent, EntityUid target)
    {
        if (HasComp<XenoWeedsComponent>(target) || HasComp<XenoConstructComponent>(target))
            return true;

        if (HasComp<XenoComponent>(target))
            return !_mobState.IsDead(target);

        if (HasComp<UserIFFComponent>(target))
            return !_mobState.IsDead(target) && !_iff.IsInFaction(target, ent.Comp.FriendlyFaction);

        return false;
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
