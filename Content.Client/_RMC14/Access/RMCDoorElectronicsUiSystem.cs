using Content.Shared._RMC14.Access;
using Content.Shared._RMC14.UserInterface;

namespace Content.Client._RMC14.Access;

public sealed class RMCDoorElectronicsUiSystem : EntitySystem
{
    [Dependency] private readonly RMCUserInterfaceSystem _rmcUi = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCDoorElectronicsComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<RMCDoorElectronicsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _rmcUi.RefreshUIs<RMCDoorElectronicsBui>(ent.Owner);
    }
}
