using Content.Shared._RMC14.Ghost;
using Content.Shared.GameTicking;

namespace Content.Client._RMC14.Ghost;

public sealed class RMCGhostTargetSystem : EntitySystem
{
    public event Action<RMCGhostWarpsResponseEvent>? GhostWarpsResponse;
    public event Action? RoundRestartCleanup;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RMCGhostWarpsResponseEvent>(OnGhostWarpsResponse);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnGhostWarpsResponse(RMCGhostWarpsResponseEvent msg, EntitySessionEventArgs args)
    {
        GhostWarpsResponse?.Invoke(msg);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent msg, EntitySessionEventArgs args)
    {
        RoundRestartCleanup?.Invoke();
    }
}
