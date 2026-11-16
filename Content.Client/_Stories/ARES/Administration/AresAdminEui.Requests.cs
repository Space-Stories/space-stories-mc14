using System.Linq;
using Content.Shared._Stories.ARES.Administration;
using Content.Shared._Stories.Hermes;

namespace Content.Client._Stories.ARES.Administration;

public sealed partial class AresAdminEui
{
    private void RenderAccessRequests()
    {
        var window = _window!;
        var requests = _state.AccessRequests.Where(r => window.ShowClosed.Pressed || !SharedHermesSystem.IsClosed(r.Ticket.Status))
            .OrderByDescending(r => r.Ticket.CreatedAt).ToList();
        foreach (var request in Paginate(requests))
        {
            var ticket = request.Ticket;
            var row = new AresAdminAccessRow();
            row.Header.Text = Loc.GetString("stories-ares-admin-access-header", ("id", ticket.Id), ("user", ticket.Submitter),
                ("status", Loc.GetString($"stories-hermes-status-{ticket.Status}")));
            row.Source.Text = Loc.GetString("stories-ares-admin-access-source", ("core", request.CoreName),
                ("status", Loc.GetString(request.Online ? "stories-ares-admin-online" : "stories-hermes-offline")));
            row.Details.Text = ticket.Details;
            var active = SharedHermesSystem.CanResolve(ticket.Status);
            row.Grant.Disabled = !request.Online || !active;
            row.Reject.Disabled = !request.Online || !active;
            row.Revoke.Disabled = !request.Online || ticket.Status != HermesTicketStatus.Granted;
            row.Grant.OnPressed += _ => Confirm(new AresAdminAccessMessage(request.Core, ticket.Id, HermesAction.Grant),
                Loc.GetString("stories-ares-admin-confirm-grant", ("user", ticket.Submitter)));
            row.Reject.OnPressed += _ => Confirm(new AresAdminAccessMessage(request.Core, ticket.Id, HermesAction.Reject),
                Loc.GetString("stories-ares-admin-confirm-reject", ("user", ticket.Submitter)));
            row.Revoke.OnPressed += _ => Confirm(new AresAdminAccessMessage(request.Core, ticket.Id, HermesAction.Revoke),
                Loc.GetString("stories-ares-admin-confirm-revoke", ("user", ticket.Submitter)));
            window.Requests.AddChild(row);
        }
    }
}
