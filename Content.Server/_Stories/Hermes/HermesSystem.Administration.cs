using Content.Server.Administration.Managers;
using Content.Shared._Stories.Hermes;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Player;

namespace Content.Server._Stories.Hermes;

public sealed partial class HermesSystem
{
    [Dependency] private readonly IAdminManager _admin = default!;

    public List<HermesTicketView> GetAccessRequests(Entity<HermesCoreComponent> core)
    {
        var requests = new List<HermesTicketView>();
        foreach (var ticket in core.Comp.Tickets)
        {
            if (ticket.AccessRequest)
                requests.Add(TicketView(ticket));
        }
        return requests;
    }

    private HermesTicketView TicketView(HermesTicket ticket)
        => new(ticket.Id, ticket.AccessRequest, ticket.Submitter, CardNet(ticket.Card) ?? default, ticket.Details,
            ticket.Category, ticket.Priority, ticket.Status, ticket.Assignee, CardNet(ticket.AssigneeCard), ticket.CreatedAt);

    public bool AdminUpdateAccess(ICommonSession admin, EntityUid coreUid, int ticket, HermesAction action)
    {
        if (!_admin.HasAdminFlag(admin, AdminFlags.Fun) ||
            action is not (HermesAction.Grant or HermesAction.Reject or HermesAction.Revoke) ||
            !TryComp(coreUid, out HermesCoreComponent? component) || !IsOnline((coreUid, component)))
            return false;

        Entity<HermesCoreComponent> core = (coreUid, component);
        var request = core.Comp.Tickets.Find(t => t.Id == ticket && t.AccessRequest);
        if (request == null || !UpdateTicket(core, admin.AttachedEntity, null, HermesAccess.Technician,
                new HermesActionMessage(action, ticket), administrator: true))
            return false;

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"ARES: admin {admin.Name} performed {action} on HERMES access ticket #{ticket} for {ToPrettyString(request.Card):target}");
        Refresh(core);
        return true;
    }
}
