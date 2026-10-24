using Content.Shared._RMC14.Weapons.Common;
using Content.Shared.Popups;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Stories.Hunter;

public sealed partial class SwitchModeSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedGunSystem _gunSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunSwitchComponent, UniqueActionEvent>(OnUniqueAction);
    }

    private void OnUniqueAction(Entity<GunSwitchComponent> ent, ref UniqueActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var user = args.UserUid;
        if (!_hands.TryGetActiveItem(user, out var heldItem))
            return;

        if (heldItem.Value != ent.Owner)
            return;

        var isLethal = ent.Comp.Mode is PlasmaGunSwitch.Lethal;
        var newMode = isLethal ? PlasmaGunSwitch.Incendiary : PlasmaGunSwitch.Lethal;

        ChangePistolModeLogic(user, (heldItem.Value, ent.Comp), newMode);
    }

    private void ChangePistolModeLogic(EntityUid user, Entity<GunSwitchComponent> ent, PlasmaGunSwitch newMode)
    {
        if (ent.Comp.Mode == newMode)
            return;

        ent.Comp.Mode = newMode;
        Dirty(ent);

        _gunSystem.RefreshModifiers(ent.Owner);

        var locId = $"st-plasma-pistol-mode-{ent.Comp.Mode.ToString().ToLower()}";
        _popup.PopupEntity(Loc.GetString("st-plasma-pistol-mode-changed", ("mode", Loc.GetString(locId))), user, user);
    }
}
