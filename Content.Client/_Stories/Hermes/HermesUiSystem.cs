using Content.Shared._RMC14.UserInterface;
using Content.Shared._Stories.Hermes;

namespace Content.Client._Stories.Hermes;

public sealed class HermesUiSystem : SharedHermesSystem
{
    [Dependency] private readonly RMCUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HermesTerminalComponent, AfterAutoHandleStateEvent>(OnTerminalState);
        SubscribeLocalEvent<HermesRestrictedUiComponent, AfterAutoHandleStateEvent>(OnRestrictedState);
    }

    private void OnTerminalState(Entity<HermesTerminalComponent> ent, ref AfterAutoHandleStateEvent args) => Refresh(ent);
    private void OnRestrictedState(Entity<HermesRestrictedUiComponent> ent, ref AfterAutoHandleStateEvent args) => Refresh(ent);

    private void Refresh(EntityUid terminal)
    {
        try
        {
            _ui.RefreshUIs<HermesBui>(terminal);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to refresh HERMES terminal {ToPrettyString(terminal)}: {e}");
        }
    }
}
