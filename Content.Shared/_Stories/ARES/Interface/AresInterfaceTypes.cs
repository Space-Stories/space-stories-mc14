using Content.Shared._Stories.Hermes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stories.ARES.Interface;

[Serializable, NetSerializable]
public enum AresUiKey : byte { Key, Logs }

[Serializable, NetSerializable]
public enum AresAccess : byte
{
    LoggedOut, Basic, Command, Joe, Corporate, SeniorCommand, ChiefEngineer,
    Synthetic, CommandingOfficer, HighCommand, CorporateCommand, Technician,
}

[Serializable, NetSerializable]
public enum AresPage : byte
{
    Announcements, Flights, Bioscans, Bombardments, Security, Hermes, Requisitions,
    Technology, Access, DeletedRecords, Conversations, DeletedConversations, Talk, Emergency, CoreSecurity, Service,
}

[Serializable, NetSerializable]
public enum AresAction : byte
{
    Login, Logout, SelectPage, DeleteRecord, PrintRequisitions,
    SendMessage = 6, ClearConversation, ReadConversation, GeneralQuarters, Evacuate,
    Distress, ReleaseGas, Lockdown, ChangeIff, Sudo, EndSudo, OpenLogs,
}

[Serializable, NetSerializable]
public sealed record AresRecordView(int Id, string Text, bool CanDelete = false);

[Serializable, NetSerializable]
public sealed record AresTalkLine(TimeSpan Time, string Author, string Text);

[Serializable, NetSerializable]
public sealed record AresSecurityView(bool LockedDown, TimeSpan LockdownReadyAt, HermesIff Iff,
    int DoorCount, int SentryCount, List<HermesVentView> Vents);

[Serializable, NetSerializable]
public sealed class AresActionMessage(AresAction action, AresPage page = AresPage.Announcements,
    int record = 0, string text = "", NetEntity? target = null, HermesIff iff = HermesIff.Marines)
    : BoundUserInterfaceMessage
{
    public readonly AresAction Action = action;
    public readonly AresPage Page = page;
    public readonly int Record = record;
    public readonly string Text = text;
    public readonly NetEntity? Target = target;
    public readonly HermesIff Iff = iff;
}

[ByRefEvent]
public readonly record struct HermesCoreUpdatedEvent(EntityUid Core);

[ByRefEvent]
public readonly record struct AresAnnouncementEvent(EntityUid? Source, string Message, LocId? Title);
