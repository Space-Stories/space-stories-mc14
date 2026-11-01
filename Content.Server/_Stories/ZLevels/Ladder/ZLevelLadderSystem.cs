using Content.Shared._RMC14.Ladder;
using Content.Shared._Stories.ZLevels.Ladder;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Stories.ZLevels.Ladder;

public sealed class ZLevelLadderSystem : SharedZLevelLadderSystem
{
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;

    protected override void Watch(Entity<ActorComponent?, EyeComponent?> watcher, Entity<ZLevelLadderComponent> toWatch)
    {
        base.Watch(watcher, toWatch);

        if (watcher.Owner == toWatch.Owner)
            return;

        ActorComponent? actor = watcher.Comp1;
        EyeComponent? eye = watcher.Comp2;

        if (!Resolve(watcher.Owner, ref actor, ref eye, false))
            return;

        _eye.SetTarget(watcher.Owner, toWatch.Owner, eye);
        _viewSubscriber.AddViewSubscriber(toWatch.Owner, actor.PlayerSession);

        RemoveWatcher(watcher.Owner);
        var watching = EnsureComp<LadderWatchingComponent>(watcher.Owner);
        watching.Watching = toWatch.Owner;
        Dirty(watcher.Owner, watching);
    }

    protected override void Unwatch(Entity<EyeComponent?> watcher, ICommonSession player)
    {
        if (!Resolve(watcher, ref watcher.Comp, false))
            return;

        var oldTarget = watcher.Comp.Target;

        base.Unwatch(watcher, player);

        if (oldTarget != null && oldTarget != watcher.Owner)
            _viewSubscriber.RemoveViewSubscriber(oldTarget.Value, player);

        RemoveWatcher(watcher.Owner);
    }

    private void RemoveWatcher(EntityUid toRemove)
    {
        if (!TryComp(toRemove, out LadderWatchingComponent? watching))
            return;

        watching.Watching = null;
        RemCompDeferred<LadderWatchingComponent>(toRemove);
    }
}
