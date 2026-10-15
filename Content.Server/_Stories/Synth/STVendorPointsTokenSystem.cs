using Content.Server._RMC14.Vendors;
using Content.Shared._RMC14.Vendors;
using Content.Shared._Stories.Synth;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server._Stories.Synth;

public sealed class STVendorPointsTokenSystem : EntitySystem
{
    [Dependency] private readonly CMAutomatedVendorSystem _vendor = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<STVendorPointsTokenComponent, AfterInteractEvent>(OnTokenAfterInteract);
    }

    private void OnTokenAfterInteract(Entity<STVendorPointsTokenComponent> token, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !args.CanReach)
            return;

        if (!TryComp(target, out CMAutomatedVendorComponent? vendor))
            return;

        if (vendor.PointsType != token.Comp.PointsType)
        {
            _popup.PopupEntity(Loc.GetString("st-vendor-points-token-wrong-vendor", ("token", token.Owner), ("vendor", target)), target, args.User);
            args.Handled = true;
            return;
        }

        var user = EnsureComp<CMVendorUserComponent>(args.User);
        var current = user.ExtraPoints?.GetValueOrDefault(token.Comp.PointsType) ?? 0;
        _vendor.SetExtraPoints((args.User, user), token.Comp.PointsType, current + token.Comp.Points);

        _popup.PopupEntity(Loc.GetString("st-vendor-points-token-redeem", ("token", token.Owner), ("vendor", target), ("points", token.Comp.Points)), target, args.User);
        _ui.ServerSendUiMessage(target, CMAutomatedVendorUI.Key, new CMVendorRefreshBuiMsg(), args.User);
        QueueDel(token.Owner);
        args.Handled = true;
    }
}
