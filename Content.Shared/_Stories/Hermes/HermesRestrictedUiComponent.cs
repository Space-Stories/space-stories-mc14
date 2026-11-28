using Robust.Shared.GameStates;

namespace Content.Shared._Stories.Hermes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedHermesSystem))]
public sealed partial class HermesRestrictedUiComponent : Component
{
    public override bool SessionSpecific => true;

    [AutoNetworkedField] public List<HermesLogEntry> Logs = [];
    [AutoNetworkedField] public List<HermesLogEntry> LoginLogs = [];
    [AutoNetworkedField] public List<HermesVentView> Vents = [];
    [AutoNetworkedField] public bool LockedDown;
    [AutoNetworkedField] public HermesIff Iff;
    [AutoNetworkedField] public TimeSpan LockdownReadyAt;
    [AutoNetworkedField] public int DoorCount;
    [AutoNetworkedField] public int SentryCount;
}
