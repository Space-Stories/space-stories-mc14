using Content.Server._Stories.ARES.Interface;
using Content.Server._Stories.Hermes;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Stories.ARES.Administration;
using Content.Shared.Administration;
using Content.Shared.Eui;

namespace Content.Server._Stories.ARES.Administration;

public sealed class AresAdminEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    private readonly AresAdminSystem _admin;
    private readonly AresInterfaceSystem _ares;
    private readonly HermesSystem _hermes;
    private NetEntity? _selectedCore;
    private int? _selectedConversation;
    private string _feedback = string.Empty;

    public AresAdminEui()
    {
        IoCManager.InjectDependencies(this);
        _admin = _entities.System<AresAdminSystem>();
        _ares = _entities.System<AresInterfaceSystem>();
        _hermes = _entities.System<HermesSystem>();
    }

    public override void Opened()
    {
        _admins.OnPermsChanged += OnPermissionsChanged;
        _admin.Register(this);
        StateDirty();
    }

    public override void Closed()
    {
        _admins.OnPermsChanged -= OnPermissionsChanged;
        _admin.Unregister(this);
    }

    public override EuiStateBase GetNewState()
        => _ares.CreateAdminState(Player, _selectedCore, _selectedConversation, _feedback);

    public override void HandleMessage(EuiMessageBase message)
    {
        base.HandleMessage(message);
        if (IsShutDown)
            return;
        if (!_admins.HasAdminFlag(Player, AdminFlags.Fun))
        {
            Close();
            return;
        }

        switch (message)
        {
            case AresAdminRefreshMessage:
                StateDirty();
                return;
            case AresAdminSelectMessage select:
                _selectedCore = select.Core;
                _selectedConversation = select.Conversation;
                _feedback = string.Empty;
                StateDirty();
                return;
            case AresAdminReplyMessage reply:
                var sent = _entities.TryGetEntity(reply.Core, out var core) &&
                    _ares.Reply(Player, core.Value, reply.Conversation, reply.Text);
                _feedback = Loc.GetString(sent ? "stories-ares-admin-reply-sent" : "stories-ares-admin-failed");
                SendMessage(new AresAdminReplyResultMessage(reply.Core, reply.Conversation, reply.Text, sent));
                StateDirty();
                return;
            case AresAdminAccessMessage access:
                var changed = _entities.TryGetEntity(access.Core, out var accessCore) &&
                    _hermes.AdminUpdateAccess(Player, accessCore.Value, access.Ticket, access.Action);
                _feedback = Loc.GetString(changed ? "stories-ares-admin-access-updated" : "stories-ares-admin-access-failed");
                StateDirty();
                return;
        }
    }

    private void OnPermissionsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admins.HasAdminFlag(Player, AdminFlags.Fun))
            Close();
    }
}
