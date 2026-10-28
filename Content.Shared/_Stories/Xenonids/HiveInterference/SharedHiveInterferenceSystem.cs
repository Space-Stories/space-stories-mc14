using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.HiveInterference;

public abstract class SharedHiveInterferenceSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private readonly List<ushort> _toCancel = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HiveInterferenceComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<HiveInterferenceComponent, XenoOpenEvolutionsActionEvent>(OnBlockedAction, before: [typeof(XenoEvolutionSystem)]);
        SubscribeLocalEvent<HiveInterferenceComponent, XenoOpenDevolveActionEvent>(OnBlockedAction, before: [typeof(XenoEvolutionSystem)]);
        SubscribeLocalEvent<HiveInterferenceComponent, XenoTransmuteActionEvent>(OnBlockedAction, before: [typeof(XenoEvolutionSystem)]);
    }

    private void OnShutdown(Entity<HiveInterferenceComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }

    private void OnBlockedAction<T>(Entity<HiveInterferenceComponent> ent, ref T args) where T : HandledEntityEventArgs
    {
        if (args.Handled || !IsInterfered((ent, ent.Comp)))
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("stories-hive-interference-blocked"), ent, ent, PopupType.MediumCaution);
    }

    public bool IsInterfered(Entity<HiveInterferenceComponent?> ent)
    {
        return Resolve(ent, ref ent.Comp, false) && !ent.Comp.Immune;
    }

    public void Apply(EntityUid xeno, float amount, float maxBuildup = 100f)
    {
        if (_net.IsClient || amount <= 0 || !HasComp<XenoComponent>(xeno))
            return;

        var time = _timing.CurTime;
        if (TryComp(xeno, out HiveInterferenceComponent? existing))
        {
            if (existing.Immune)
            {
                var cap = time + existing.ImmunityDuration;
                var extended = existing.ImmuneUntil + existing.ImmunityExtension;
                existing.ImmuneUntil = extended > cap ? cap : extended;
                Dirty(xeno, existing);
                return;
            }

            existing.MaxBuildup = MathF.Max(existing.MaxBuildup, maxBuildup);
            var current = (float) Math.Max((existing.EndAt - time).TotalSeconds, 0);
            var total = MathF.Min(current + amount, existing.MaxBuildup);
            existing.EndAt = time + TimeSpan.FromSeconds(total);
            Dirty(xeno, existing);
            UpdateAlert((xeno, existing));
            return;
        }

        var comp = EnsureComp<HiveInterferenceComponent>(xeno);
        comp.MaxBuildup = maxBuildup;
        comp.EndAt = time + TimeSpan.FromSeconds(MathF.Min(amount, maxBuildup));
        Dirty(xeno, comp);
        UpdateAlert((xeno, comp));

        _popup.PopupEntity(Loc.GetString("stories-hive-interference-applied"), xeno, xeno, PopupType.LargeCaution);
        _ui.CloseUi(xeno, XenoEvolutionUIKey.Key);
        _ui.CloseUi(xeno, XenoDevolveUIKey.Key);
        CancelEvolution(xeno);
        OnInterferenceStarted(xeno);
    }

    public void Cleanse(EntityUid xeno)
    {
        if (_net.IsClient || !TryComp(xeno, out HiveInterferenceComponent? comp) || comp.Immune)
            return;

        comp.Immune = true;
        comp.ImmuneUntil = _timing.CurTime + comp.ImmunityDuration;
        Dirty(xeno, comp);
        _alerts.ClearAlert(xeno, comp.Alert);
    }

    private void CancelEvolution(EntityUid xeno)
    {
        if (!TryComp(xeno, out DoAfterComponent? doAfters))
            return;

        _toCancel.Clear();
        foreach (var (id, doAfter) in doAfters.DoAfters)
        {
            if (doAfter.Args.Event is XenoEvolutionDoAfterEvent)
                _toCancel.Add(id);
        }

        foreach (var id in _toCancel)
        {
            _doAfter.Cancel(xeno, id, doAfters);
        }
    }

    protected virtual void OnInterferenceStarted(EntityUid xeno)
    {
    }

    private void UpdateAlert(Entity<HiveInterferenceComponent> ent)
    {
        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert, cooldown: (_timing.CurTime, ent.Comp.EndAt));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<HiveInterferenceComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Immune ? time >= comp.ImmuneUntil : time >= comp.EndAt)
                RemCompDeferred<HiveInterferenceComponent>(uid);
        }
    }
}
