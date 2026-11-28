using Content.Shared._Stories.Hermes;
using Content.Shared.Access.Components;
using Content.Shared.Access;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.Hermes;

public sealed partial class HermesSystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private void CreateTicket(Entity<HermesCoreComponent> core, EntityUid actor, HermesSession session,
        HermesAccess level, HermesActionMessage args)
    {
        var details = args.Text.Trim();
        var access = args.Action == HermesAction.RequestAccess;
        if (details.Length == 0 || details.Length > core.Comp.MaxDetailLength ||
            !Enum.IsDefined(args.Category) ||
            core.Comp.NextRequests.TryGetValue(session.Card, out var next) && _timing.CurTime < next ||
            access && level > HermesAccess.Reporter)
            return;
        foreach (var existing in core.Comp.Tickets)
        {
            if (existing.Card == session.Card && existing.AccessRequest == access && !IsClosed(existing.Status) &&
                (access || existing.Details == details && existing.Category == args.Category))
            {
                Error(actor, "stories-hermes-request-exists");
                return;
            }
        }
        while (core.Comp.Tickets.Count >= core.Comp.MaxTickets)
        {
            var old = core.Comp.Tickets.FindIndex(t => IsClosed(t.Status));
            if (old < 0)
            {
                Error(actor, "stories-hermes-ticket-limit");
                return;
            }
            core.Comp.Tickets.RemoveAt(old);
        }
        var ticket = new HermesTicket
        {
            Id = core.Comp.NextTicketId++,
            AccessRequest = access,
            Card = session.Card,
            Submitter = CardName(session.Card),
            Details = details,
            Category = args.Category,
            Priority = !access && (core.Comp.PriorityCategories.Contains(args.Category) ||
                level >= HermesAccess.Reporter && args.Priority),
            CreatedAt = _ticker.RoundDuration(),
        };
        core.Comp.Tickets.Add(ticket);
        core.Comp.NextRequests[session.Card] = _timing.CurTime + core.Comp.RequestCooldown;
        LogTicket(core, actor, ticket, "stories-hermes-ticket-created");
        if (access)
        {
            var ev = new HermesAccessRequestedEvent(core, ticket.Id, ticket.Submitter);
            RaiseLocalEvent(ref ev);
        }
        if (ticket.Priority || access)
            Announce(core, Loc.GetString(access ? "stories-hermes-access-announcement" : "stories-hermes-priority-announcement",
                ("id", ticket.Id), ("name", ticket.Submitter),
                ("category", Loc.GetString($"stories-hermes-category-{ticket.Category}"))));
        else
            Notify(core);
    }

    private bool UpdateTicket(Entity<HermesCoreComponent> core, EntityUid? actor, EntityUid? card,
        HermesAccess level, HermesActionMessage args, bool administrator = false)
    {
        var ticket = core.Comp.Tickets.Find(t => t.Id == args.Ticket);
        if (ticket == null || IsClosed(ticket.Status))
            return false;
        var own = ticket.Card == card;
        var assigned = administrator || ticket.AssigneeCard == null || ticket.AssigneeCard == card;
        var manager = level >= HermesAccess.Joe;
        switch (args.Action)
        {
            case HermesAction.Cancel when own && CanResolve(ticket.Status):
                ticket.Status = HermesTicketStatus.Cancelled;
                break;
            case HermesAction.Claim when manager && CanResolve(ticket.Status):
                var unclaim = ticket.AssigneeCard == card;
                ticket.AssigneeCard = unclaim ? null : card;
                ticket.Assignee = unclaim ? null : card is { } assignee ? CardName(assignee) : "ARES";
                ticket.Status = unclaim ? HermesTicketStatus.Pending : HermesTicketStatus.Assigned;
                break;
            case HermesAction.Complete when manager && assigned && !ticket.AccessRequest && CanResolve(ticket.Status):
                ticket.Status = HermesTicketStatus.Completed;
                break;
            case HermesAction.Reject when manager && assigned && CanResolve(ticket.Status):
                ticket.Status = HermesTicketStatus.Rejected;
                break;
            case HermesAction.Priority when manager && !ticket.AccessRequest && CanResolve(ticket.Status):
                ticket.Priority = !ticket.Priority;
                break;
            case HermesAction.Grant when manager && assigned && ticket.AccessRequest && CanResolve(ticket.Status):
                if (!TryComp(ticket.Card, out AccessComponent? access) || !access.Enabled ||
                    access.Tags.Contains(core.Comp.TemporaryAccess))
                    return false;
                var tags = new HashSet<ProtoId<AccessLevelPrototype>>(access.Tags)
                {
                    core.Comp.TemporaryAccess,
                };
                if (!_access.TrySetTags(ticket.Card, tags, access))
                    return false;
                ticket.Status = HermesTicketStatus.Granted;
                break;
            case HermesAction.Revoke when manager && ticket.AccessRequest && ticket.Status == HermesTicketStatus.Granted:
            case HermesAction.ReturnAccess when own && ticket.AccessRequest && ticket.Status == HermesTicketStatus.Granted:
                RemoveTemporaryAccess(core, ticket.Card);
                ticket.Status = args.Action == HermesAction.Revoke ? HermesTicketStatus.Revoked : HermesTicketStatus.Returned;
                break;
            default:
                return false;
        }
        LogTicket(core, actor, ticket, "stories-hermes-ticket-updated");
        if (ticket.Priority || ticket.AccessRequest)
            Announce(core, Loc.GetString("stories-hermes-ticket-announcement", ("id", ticket.Id),
                ("status", Loc.GetString($"stories-hermes-status-{ticket.Status}")), ("name", card is { } author ? CardName(author) : "ARES")));
        else
            Notify(core);
        return true;
    }

    private void RemoveTemporaryAccess(Entity<HermesCoreComponent> core, EntityUid card)
    {
        if (!TryComp(card, out AccessComponent? access))
            return;
        var tags = new HashSet<ProtoId<AccessLevelPrototype>>(access.Tags);
        tags.Remove(core.Comp.TemporaryAccess);
        _access.TrySetTags(card, tags, access);
    }

    private void LogTicket(Entity<HermesCoreComponent> core, EntityUid? actor, HermesTicket ticket, string message)
    {
        AddLog(core.Comp.Logs, core, CardName(ticket.Card), Loc.GetString(message, ("id", ticket.Id),
            ("status", Loc.GetString($"stories-hermes-status-{ticket.Status}"))));
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"HERMES: {ToPrettyString(actor):user} {message} ticket #{ticket.Id} for {ToPrettyString(ticket.Card):target}; status {ticket.Status}, priority {ticket.Priority}, assignee {ticket.Assignee}");
    }

    private void Error(EntityUid actor, string message) => _popup.PopupEntity(Loc.GetString(message), actor, actor);
}
