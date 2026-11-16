using Content.Server._RMC14.Xenonids.Watch;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Watch;
using Content.Shared._Stories.Xenonids.HiveInterference;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server._Stories.Xenonids.HiveInterference;

public sealed class HiveInterferenceSystem : SharedHiveInterferenceSystem
{
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly XenoWatchSystem _watch = default!;

    private readonly HashSet<EntityUid> _toUnwatch = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HiveInterferenceComponent, XenoWatchActionEvent>(OnWatchAction, before: [typeof(XenoWatchSystem)]);
        SubscribeLocalEvent<XenoWatchingComponent, XenoWatchEvent>(OnWatchStarted);
    }

    private void OnWatchStarted(Entity<XenoWatchingComponent> ent, ref XenoWatchEvent args)
    {
        if (IsInterfered(ent.Owner))
        {
            Unwatch(ent, "stories-hive-interference-watch-blocked");
            return;
        }

        if (ent.Comp.Watching is { } target && IsInterfered(target))
            Unwatch(ent, "stories-hive-interference-watch-target-blocked");
    }

    private void OnWatchAction(Entity<HiveInterferenceComponent> ent, ref XenoWatchActionEvent args)
    {
        if (args.Handled || !IsInterfered((ent, ent.Comp)))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("stories-hive-interference-watch-blocked"), ent, ent, PopupType.MediumCaution);
    }

    protected override void OnInterferenceStarted(EntityUid xeno)
    {
        CutWatching(xeno);
    }

    private void CutWatching(EntityUid xeno)
    {
        _toUnwatch.Clear();

        if (HasComp<XenoWatchingComponent>(xeno))
            _toUnwatch.Add(xeno);

        if (TryComp(xeno, out XenoWatchedComponent? watched))
            _toUnwatch.UnionWith(watched.Watching);

        var watchers = EntityQueryEnumerator<XenoWatchingComponent>();
        while (watchers.MoveNext(out var uid, out var watching))
        {
            if (watching.Watching == xeno)
                _toUnwatch.Add(uid);
        }

        var eyes = EntityQueryEnumerator<EyeComponent>();
        while (eyes.MoveNext(out var uid, out var eye))
        {
            if (uid != xeno && eye.Target == xeno)
                _toUnwatch.Add(uid);
        }

        foreach (var watcher in _toUnwatch)
        {
            if (TerminatingOrDeleted(watcher))
                continue;

            Unwatch(watcher, watcher == xeno ? "stories-hive-interference-watch-blocked" : "stories-hive-interference-watch-target-blocked");
        }
    }

    private void Unwatch(EntityUid watcher, LocId message)
    {
        var popupTarget = watcher;
        if (TryComp(watcher, out QueenEyeComponent? queenEye) && queenEye.Queen is { } queen)
            popupTarget = queen;

        if (GetSession(watcher) is { } session)
        {
            _watch.Unwatch(watcher, session);
        }
        else
        {
            _eye.SetTarget(watcher, null);
            var ev = new XenoUnwatchEvent();
            RaiseLocalEvent(watcher, ref ev);
            RemCompDeferred<XenoWatchingComponent>(watcher);
        }

        if (TryComp(watcher, out QueenEyeActionComponent? action) && action.Eye is { } eyeEnt && !TerminatingOrDeleted(eyeEnt))
            _eye.SetTarget(watcher, eyeEnt);

        _popup.PopupEntity(Loc.GetString(message), popupTarget, popupTarget, PopupType.MediumCaution);
    }

    private ICommonSession? GetSession(EntityUid watcher)
    {
        if (TryComp(watcher, out ActorComponent? actor))
            return actor.PlayerSession;

        if (TryComp(watcher, out QueenEyeComponent? eye) && TryComp(eye.Queen, out ActorComponent? eyeActor))
            return eyeActor.PlayerSession;

        return null;
    }
}
