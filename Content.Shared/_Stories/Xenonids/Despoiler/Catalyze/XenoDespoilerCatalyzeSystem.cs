using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.Despoiler;

public sealed class XenoDespoilerCatalyzeSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly XenoDespoilerHypertensionSystem _hyper = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoDespoilerComponent, XenoDespoilerCatalyzeActionEvent>(OnCatalyze);
        SubscribeLocalEvent<XenoDespoilerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnCatalyze(EntityUid uid, XenoDespoilerComponent comp, XenoDespoilerCatalyzeActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<XenoDespoilerHypertensionComponent>(uid, out var hyper))
            return;

        if (!TryComp<XenoDespoilerCatalyzeActionComponent>(args.Action, out var action))
            return;

        if (hyper.Stacks < action.HypertensionCost)
        {
            _popup.PopupClient(Loc.GetString("st-xeno-despoiler-no-hypertension"), uid, uid);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        _hyper.TryConsumeStacks(uid, hyper, action.HypertensionCost);

        comp.NextAbilityEmpowered = true;
        comp.EmpowerExpiresAt = _timing.CurTime + action.BuffDuration;
        Dirty(uid, comp);

        var visual = EnsureComp<XenoDespoilerCatalyzeVisualComponent>(uid);
        DespawnVisual(visual);

        var burst = Spawn(action.VisualProto, Transform(uid).Coordinates);
        _xform.SetParent(burst, uid);
        _hive.SetSameHive(uid, burst);
        visual.CatalyzeVisual = burst;

        _popup.PopupEntity(Loc.GetString("st-xeno-despoiler-catalyze-active"), uid, uid);
    }

    private void OnShutdown(EntityUid uid, XenoDespoilerComponent comp, ComponentShutdown args)
    {
        if (TryComp<XenoDespoilerCatalyzeVisualComponent>(uid, out var visual))
            DespawnVisual(visual);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoDespoilerComponent, XenoDespoilerCatalyzeVisualComponent>();
        while (query.MoveNext(out var uid, out var comp, out var visual))
        {
            if (comp.NextAbilityEmpowered && now > comp.EmpowerExpiresAt)
            {
                comp.NextAbilityEmpowered = false;
                Dirty(uid, comp);
            }

            if (!comp.NextAbilityEmpowered && visual.CatalyzeVisual is not null)
                DespawnVisual(visual);
        }
    }

    private void DespawnVisual(XenoDespoilerCatalyzeVisualComponent visual)
    {
        if (visual.CatalyzeVisual is not { } burst)
            return;

        if (Exists(burst) && !TerminatingOrDeleted(burst))
            QueueDel(burst);

        visual.CatalyzeVisual = null;
    }
}
