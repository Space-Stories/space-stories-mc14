using Robust.Shared.Serialization;

namespace Content.Shared._Stories.Hermes;

[Serializable, NetSerializable]
public enum HermesUiKey : byte { Key }

[Serializable, NetSerializable]
public enum HermesAccess : byte { LoggedOut, Visitor, Reporter, Temporary, Certified, Joe, Technician }

[Serializable, NetSerializable]
public enum HermesTicketStatus : byte { Pending, Assigned, Cancelled, Rejected, Completed, Granted, Revoked, Returned }

[Serializable, NetSerializable]
public enum HermesCategory : byte
{
    BrokenLight, ShatteredGlass, MinorDamage, MajorDamage, Janitorial, ChemicalSpill,
    Fire, Communications, PowerGeneration, ElectricalFault, Support, Other,
}

[Serializable, NetSerializable]
public enum HermesIff : byte { Marines, Corporate, Combined, Ares }

[Serializable, NetSerializable]
public enum HermesAction : byte
{
    Login, Logout, ToggleSound, Report, RequestAccess, Claim, Cancel, Complete, Reject,
    Priority, Grant, Revoke, ReturnAccess, ReleaseGas, Lockdown, ChangeIff, OpenAresLogs,
}

[Serializable, NetSerializable]
public sealed record HermesTicketView(
    int Id, bool AccessRequest, string Submitter, NetEntity Card, string Details,
    HermesCategory Category, bool Priority, HermesTicketStatus Status,
    string? Assignee, NetEntity? AssigneeCard, TimeSpan CreatedAt);

[Serializable, NetSerializable]
public sealed record HermesVentView(NetEntity Entity, string Name, bool Available, TimeSpan ReadyAt, bool Welded);

[Serializable, NetSerializable]
public sealed record HermesLogEntry(TimeSpan Time, string Author, string Message);

[ByRefEvent]
public readonly record struct HermesAccessRequestedEvent(EntityUid Core, int Ticket, string User);

[Serializable, NetSerializable]
public sealed class HermesActionMessage(
    HermesAction action, int ticket = 0, string text = "", HermesCategory category = HermesCategory.Other,
    bool priority = false, NetEntity? target = null, HermesIff iff = HermesIff.Marines) : BoundUserInterfaceMessage
{
    public readonly HermesAction Action = action;
    public readonly int Ticket = ticket;
    public readonly string Text = text;
    public readonly HermesCategory Category = category;
    public readonly bool Priority = priority;
    public readonly NetEntity? Target = target;
    public readonly HermesIff Iff = iff;
}

[Serializable, NetSerializable]
public sealed class HermesSessionMessage(HermesAccess access, string name, NetEntity? card, bool sound, bool connected)
    : BoundUserInterfaceMessage
{
    public readonly HermesAccess Access = access;
    public readonly string Name = name;
    public readonly NetEntity? Card = card;
    public readonly bool Sound = sound;
    public readonly bool Connected = connected;
}
