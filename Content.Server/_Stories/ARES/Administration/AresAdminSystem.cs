using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Stories.ARES.Administration;
using Content.Shared._Stories.ARES.Interface;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Robust.Shared.Player;

namespace Content.Server._Stories.ARES.Administration;

public sealed class AresAdminSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly EuiManager _eui = default!;

    private readonly HashSet<AresAdminEui> _open = [];

    public override void Initialize()
    {
        SubscribeLocalEvent<AresConversationsChangedEvent>(OnConversationsChanged);
        SubscribeLocalEvent<HermesCoreUpdatedEvent>(OnHermesChanged);
        SubscribeLocalEvent<AresInterfaceCoreComponent, ComponentShutdown>(OnCoreShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
    }

    public bool Open(ICommonSession admin)
    {
        if (!_admins.HasAdminFlag(admin, AdminFlags.Fun))
            return false;

        foreach (var existing in _open)
        {
            if (existing.Player != admin)
                continue;
            existing.SendMessage(new AresAdminFocusMessage());
            existing.StateDirty();
            return true;
        }

        _eui.OpenEui(new AresAdminEui(), admin);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"ARES: admin {admin.Name} opened the remote interface");
        return true;
    }

    public void Register(AresAdminEui eui) => _open.Add(eui);
    public void Unregister(AresAdminEui eui) => _open.Remove(eui);

    private void OnConversationsChanged(ref AresConversationsChangedEvent args) => Refresh();
    private void OnHermesChanged(ref HermesCoreUpdatedEvent args) => Refresh();
    private void OnCoreShutdown(Entity<AresInterfaceCoreComponent> ent, ref ComponentShutdown args) => Refresh();

    private void OnRestart(RoundRestartCleanupEvent args)
    {
        foreach (var eui in _open.ToArray())
            eui.Close();
    }

    private void Refresh()
    {
        foreach (var eui in _open)
            eui.StateDirty();
    }
}
