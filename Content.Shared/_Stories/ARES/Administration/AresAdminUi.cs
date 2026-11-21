using Content.Shared._Stories.ARES.Interface;
using Content.Shared._Stories.Hermes;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.ARES.Administration;

[Serializable, NetSerializable]
public sealed record AresAdminConversationView(NetEntity Core, string CoreName, int Id, string User,
    bool Archived, TimeSpan LastActivity, string Preview, int MaxTextLength);

[Serializable, NetSerializable]
public sealed record AresAdminAccessView(NetEntity Core, string CoreName, bool Online, HermesTicketView Ticket);

[Serializable, NetSerializable]
public sealed class AresAdminEuiState : EuiStateBase
{
    public readonly List<AresAdminConversationView> Conversations = [];
    public readonly List<AresAdminAccessView> AccessRequests = [];
    public AresAdminConversationView? Selected;
    public List<AresTalkLine> Transcript = [];
    public string Feedback = string.Empty;
}

[Serializable, NetSerializable]
public sealed class AresAdminRefreshMessage : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class AresAdminFocusMessage : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class AresAdminSelectMessage(NetEntity core, int conversation) : EuiMessageBase
{
    public readonly NetEntity Core = core;
    public readonly int Conversation = conversation;
}

[Serializable, NetSerializable]
public sealed class AresAdminReplyMessage(NetEntity core, int conversation, string text) : EuiMessageBase
{
    public readonly NetEntity Core = core;
    public readonly int Conversation = conversation;
    public readonly string Text = text;
}

[Serializable, NetSerializable]
public sealed class AresAdminReplyResultMessage(NetEntity core, int conversation, string text, bool success) : EuiMessageBase
{
    public readonly NetEntity Core = core;
    public readonly int Conversation = conversation;
    public readonly string Text = text;
    public readonly bool Success = success;
}

[Serializable, NetSerializable]
public sealed class AresAdminAccessMessage(NetEntity core, int ticket, HermesAction action) : EuiMessageBase
{
    public readonly NetEntity Core = core;
    public readonly int Ticket = ticket;
    public readonly HermesAction Action = action;
}

[ByRefEvent]
public readonly record struct AresConversationsChangedEvent;
