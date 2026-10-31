using System.Linq;
using Content.Shared._Stories.ARES.Administration;
using Content.Shared._Stories.ARES.Interface;
using Content.Shared._Stories.Hermes;
using Content.Shared.Administration;
using Robust.Shared.Player;

namespace Content.Server._Stories.ARES.Interface;

public sealed partial class AresInterfaceSystem
{
    public AresAdminEuiState CreateAdminState(ICommonSession admin, NetEntity? selectedCore = null,
        int? selectedConversation = null, string feedback = "")
    {
        var state = new AresAdminEuiState();
        if (!_admins.HasAdminFlag(admin, AdminFlags.Fun))
            return state;

        state.Feedback = feedback;
        var cores = EntityQueryEnumerator<AresInterfaceCoreComponent>();
        while (cores.MoveNext(out var uid, out var core))
        {
            if (TerminatingOrDeleted(uid))
                continue;
            var net = GetNetEntity(uid);
            foreach (var conversation in core.Conversations)
            {
                var last = conversation.Lines.LastOrDefault();
                var view = new AresAdminConversationView(net, Name(uid), conversation.Id, conversation.User,
                    conversation.Archived, last?.Time ?? TimeSpan.Zero, last?.Text ?? string.Empty, core.MaxTextLength);
                state.Conversations.Add(view);
                if (net == selectedCore && conversation.Id == selectedConversation)
                {
                    state.Selected = view;
                    state.Transcript = new(conversation.Lines);
                }
            }
        }

        var hermesCores = EntityQueryEnumerator<HermesCoreComponent>();
        while (hermesCores.MoveNext(out var uid, out var core))
        {
            if (TerminatingOrDeleted(uid))
                continue;
            var online = _hermes.IsOnline((uid, core));
            foreach (var ticket in _hermes.GetAccessRequests((uid, core)))
                state.AccessRequests.Add(new AresAdminAccessView(GetNetEntity(uid), Name(uid), online, ticket));
        }

        return state;
    }

    private void ConversationsChanged()
    {
        var ev = new AresConversationsChangedEvent();
        RaiseLocalEvent(ref ev);
    }

    private void OnAccessRequested(ref HermesAccessRequestedEvent args)
    {
        if (TryComp(args.Core, out AresInterfaceCoreComponent? core))
            NotifyAdmins((args.Core, core), Loc.GetString("stories-ares-admin-access-requested",
                ("id", args.Ticket), ("user", args.User)));
    }
}
