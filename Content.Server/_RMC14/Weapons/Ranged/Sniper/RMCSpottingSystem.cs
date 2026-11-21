using Content.Shared._RMC14.Rangefinder.Spotting;

namespace Content.Server._RMC14.Weapons.Ranged.Sniper;

public sealed class RMCSpottingSystem : SharedRMCSpottingSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestSpotEvent>(OnSpotRequest);
    }

    private void OnSpotRequest(RequestSpotEvent ev, EntitySessionEventArgs args)
    {
        // Stories-FixSpottingExploit-Start
        if (args.SenderSession.AttachedEntity is not { } user || user != GetEntity(ev.User))
            return;
        // Stories-FixSpottingExploit-End

        SpotRequested(ev.SpottingTool, ev.User, ev.Target);
    }
}
