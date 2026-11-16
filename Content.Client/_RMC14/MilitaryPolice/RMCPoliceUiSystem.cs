using Content.Shared._RMC14.MilitaryPolice;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.MilitaryPolice;

public sealed class RMCPoliceUiSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCPoliceUiComponent, AfterAutoHandleStateEvent>(OnPoliceState);
        SubscribeLocalEvent<RMCBrigCellUiComponent, AfterAutoHandleStateEvent>(OnCellState);
    }

    private void OnPoliceState(Entity<RMCPoliceUiComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        try
        {
            if (_ui.TryGetOpenUi<RMCPoliceConsoleBui>(ent.Owner, RMCPoliceUiKey.Key, out var bui))
                bui.Refresh();
        }
        catch (Exception exception)
        {
            Log.Error($"Error updating police console UI on {ToPrettyString(ent)}: {exception}");
        }
    }

    private void OnCellState(Entity<RMCBrigCellUiComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        try
        {
            if (_ui.TryGetOpenUi<RMCBrigCellBui>(ent.Owner, RMCBrigCellUiKey.Key, out var bui))
                bui.Refresh();
        }
        catch (Exception exception)
        {
            Log.Error($"Error updating brig controller UI on {ToPrettyString(ent)}: {exception}");
        }
    }
}
