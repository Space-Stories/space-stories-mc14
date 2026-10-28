using Content.Server._RMC14.Xenonids.Watch;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Watch;
using Content.Shared._Stories.Xenonids.HiveInterference;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server._Stories.Xenonids.HiveInterference;

public sealed class HiveInterferenceSystem : SharedHiveInterferenceSystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly XenoWatchSystem _watch = default!;

    private readonly List<EntityUid> _toUnwatch = new();

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
            _toUnwatch.AddRange(watched.Watching);

        foreach (var watcher in _toUnwatch)
        {
            Unwatch(watcher, watcher == xeno ? "stories-hive-interference-watch-blocked" : "stories-hive-interference-watch-target-blocked");
        }
    }

    private void Unwatch(EntityUid watcher, LocId message)
    {
        ICommonSession? session = null;
        if (TryComp(watcher, out ActorComponent? actor))
            session = actor.PlayerSession;
        else if (TryComp(watcher, out QueenEyeComponent? eye) && TryComp(eye.Queen, out ActorComponent? eyeActor))
            session = eyeActor.PlayerSession;

        if (session == null)
            return;

        _watch.Unwatch(watcher, session);
        _popup.PopupEntity(Loc.GetString(message), watcher, watcher, PopupType.MediumCaution);
    }
}
