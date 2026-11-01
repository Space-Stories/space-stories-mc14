using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Dropship.Utility.Systems;
using Content.Shared._Stories.Hermes;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;

namespace Content.Server._Stories.Hermes;

public sealed partial class HermesSystem
{
    [Dependency] private readonly SharedRMCEquipmentDeployerSystem _equipmentDeployer = default!;

    private readonly HashSet<EntityUid> _disconnectedSentries = [];

    private void ApplySentryLockdown(Entity<HermesCoreComponent> core, EntityUid device)
    {
        if (!TerminatingOrDeleted(device) && TryComp(device, out HermesSentryComponent? sentry))
            SetSentryLockdown((device, sentry), core.Comp.LockedDown);
    }

    private void SetSentryLockdown(Entity<HermesSentryComponent> sentry, bool locked)
    {
        if (sentry.Comp.LockedDown == locked)
            return;
        sentry.Comp.LockedDown = locked;
        if (TryComp(sentry, out RMCEquipmentDeployerComponent? equipment) && equipment.IsDeployed != locked)
            _equipmentDeployer.TryDeploy(sentry, locked, equipmentDeployerComponent: equipment);
    }

    private void OnSentryDisconnected(Entity<HermesSentryComponent> ent, ref PortDisconnectedEvent args)
        => _disconnectedSentries.Add(ent);

    private void RefreshDisconnectedSentries()
    {
        foreach (var uid in _disconnectedSentries)
        {
            if (TerminatingOrDeleted(uid) || !TryComp(uid, out HermesSentryComponent? sentry))
                continue;
            var locked = false;
            if (TryComp(uid, out DeviceLinkSinkComponent? sink))
            {
                foreach (var source in sink.LinkedSources)
                {
                    if (!TerminatingOrDeleted(source) && TryComp(source, out HermesCoreComponent? core) && core.LockedDown)
                        locked = true;
                }
            }
            SetSentryLockdown((uid, sentry), locked);
        }
        _disconnectedSentries.Clear();
    }
}
