using Content.Client._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.TacticalMap;
using Robust.Client.Player;

namespace Content.Client._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem : SharedTacticalMapSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TacticalMapUserComponent, AfterAutoHandleStateEvent>(OnTacticalMapState);
        SubscribeLocalEvent<TacticalMapComputerComponent, AfterAutoHandleStateEvent>(OnTacticalMapState);
        SubscribeLocalEvent<TacticalMapLinesComponent, AfterAutoHandleStateEvent>(OnTacticalMapState);
        SubscribeLocalEvent<TacticalMapLabelsComponent, AfterAutoHandleStateEvent>(OnTacticalMapState);
        SubscribeLocalEvent<TacticalMapActiveLayerLinesComponent, AfterAutoHandleStateEvent>(OnTacticalMapState);
        SubscribeLocalEvent<TacticalMapActiveLayerLabelsComponent, AfterAutoHandleStateEvent>(OnTacticalMapState);
    }

    private void RefreshUser(EntityUid ent)
    {
        try
        {
            if (!TryComp(ent, out UserInterfaceComponent? ui))
                return;

            foreach (var bui in ui.ClientOpenInterfaces.Values)
            {
                if (bui is TacticalMapUserBui userBui)
                    userBui.Refresh();
            }
        }
        catch (Exception e)
        {
            Log.Error($"Error refreshing {nameof(TacticalMapUserBui)}\n{e}");
        }
    }

    private void RefreshComputer(EntityUid ent)
    {
        try
        {
            // computer state can feed tacmap and dropship weapon uis
            if (!TryComp(ent, out UserInterfaceComponent? ui))
                return;

            foreach (var bui in ui.ClientOpenInterfaces.Values)
            {
                if (bui is TacticalMapComputerBui computerBui)
                    computerBui.Refresh();
                else if (bui is DropshipWeaponsBui weaponsBui)
                    weaponsBui.Refresh();
            }
        }
        catch (Exception e)
        {
            Log.Error($"Error refreshing {nameof(TacticalMapComputerBui)}\n{e}");
        }
    }

    private void OnTacticalMapState<T>(Entity<T> ent, ref AfterAutoHandleStateEvent args) where T : Component
    {
        if (_player.LocalEntity == ent.Owner && HasComp<TacticalMapUserComponent>(ent.Owner))
            RefreshUser(ent.Owner);
        if (HasComp<TacticalMapComputerComponent>(ent.Owner))
            RefreshComputer(ent.Owner);
    }
}
