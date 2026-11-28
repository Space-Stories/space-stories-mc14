using Content.Shared._RMC14.UserInterface;
using Content.Shared._Stories.ARES.Interface;

namespace Content.Client._Stories.ARES.Interface;

public sealed class AresInterfaceUiSystem : SharedAresInterfaceSystem
{
    [Dependency] private readonly RMCUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AresTerminalComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<AresTerminalComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        try
        {
            _ui.RefreshUIs<AresBui>(ent.Owner);
            _ui.RefreshUIs<AresLogsBui>(ent.Owner);
        }
        catch (Exception exception) { Log.Error($"Failed to refresh ARES terminal {ToPrettyString(ent)}: {exception}"); }
    }
}
