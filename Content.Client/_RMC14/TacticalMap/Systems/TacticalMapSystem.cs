using Content.Client._RMC14.Dropship.Weapon;
using Content.Client._RMC14.TacticalMap.Controls;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.GameTicking;
using Robust.Client.Player;

namespace Content.Client._RMC14.TacticalMap;

public sealed partial class TacticalMapSystem : SharedTacticalMapSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

    internal readonly Dictionary<EntityUid, TacticalMapControl.CachedMapTexture> TextureCache = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<AreaGridComponent, ComponentRemove>(OnAreaGridRemove);
        SubscribeLocalEvent<TacticalMapComponent, ComponentRemove>(OnTacticalMapRemove);
        SubscribeLocalEvent<TacticalMapUserComponent, AfterAutoHandleStateEvent>(OnTacticalMapState);
        SubscribeLocalEvent<TacticalMapComputerComponent, AfterAutoHandleStateEvent>(OnTacticalMapState);
        SubscribeLocalEvent<TacticalMapLinesComponent, AfterAutoHandleStateEvent>(OnTacticalMapState);
        SubscribeLocalEvent<TacticalMapLabelsComponent, AfterAutoHandleStateEvent>(OnTacticalMapState);
        SubscribeLocalEvent<TacticalMapActiveLayerLinesComponent, AfterAutoHandleStateEvent>(OnTacticalMapState);
        SubscribeLocalEvent<TacticalMapActiveLayerLabelsComponent, AfterAutoHandleStateEvent>(OnTacticalMapState);
    }

    public override void Shutdown()
    {
        TextureCache.Clear();
        base.Shutdown();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        TextureCache.Clear();
        var personal = EntityQueryEnumerator<TacticalMapPersonalLabelsComponent>();
        while (personal.MoveNext(out _, out var labels))
            labels.Labels.Clear();
    }

    private void OnAreaGridRemove(Entity<AreaGridComponent> ent, ref ComponentRemove args)
    {
        TextureCache.Remove(ent.Owner);
    }

    private void OnTacticalMapRemove(Entity<TacticalMapComponent> ent, ref ComponentRemove args)
    {
        var map = GetNetEntity(ent.Owner);
        var personal = EntityQueryEnumerator<TacticalMapPersonalLabelsComponent>();
        while (personal.MoveNext(out _, out var labels))
            labels.Labels.Remove(map);
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
