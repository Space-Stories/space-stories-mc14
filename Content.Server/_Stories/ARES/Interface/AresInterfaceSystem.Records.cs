using System.Text;
using Content.Server.Paper;
using Content.Shared._Stories.ARES.Interface;
using Content.Shared._Stories.Hermes;
using Content.Shared.Database;
using Content.Shared.Paper;

namespace Content.Server._Stories.ARES.Interface;

public sealed partial class AresInterfaceSystem
{
    [Dependency] private readonly PaperSystem _paper = default!;

    private void RefreshRecords(Entity<AresTerminalComponent> terminal, Entity<AresInterfaceCoreComponent> core, AresAccess level)
    {
        var page = terminal.Comp.Page;
        terminal.Comp.Records = [];
        terminal.Comp.LogReferences.Clear();
        if (page == AresPage.DeletedRecords)
        {
            for (var i = core.Comp.DeletedRecords.Count - 1; i >= 0; i--)
            {
                var entry = core.Comp.DeletedRecords[i];
                terminal.Comp.Records.Add(new AresRecordView(i, FormatLine(entry)));
            }
        }
        else if (page is AresPage.Conversations or AresPage.DeletedConversations)
        {
            foreach (var conversation in core.Comp.Conversations)
            {
                if (conversation.Archived == (page == AresPage.DeletedConversations))
                    terminal.Comp.Records.Add(new AresRecordView(conversation.Id, $"#{conversation.Id} — {conversation.User}"));
            }
        }
        else if (page == AresPage.Hermes && TryComp(core, out HermesCoreComponent? hermes))
        {
            var logs = _hermes.GetLogs((core, hermes));
            for (var i = logs.Count - 1; i >= 0; i--)
            {
                var entry = logs[i];
                terminal.Comp.Records.Add(new AresRecordView(i, FormatLine(new AresTalkLine(entry.Time, entry.Author, entry.Message))));
            }
        }
        else if (core.Comp.LogTypes.TryGetValue(page, out var type) && _ares.PullARESLogs(core, type, out var records) && records != null)
        {
            var count = Math.Min(records.Count, core.Comp.MaxRecords);
            for (var i = records.Count - 1; i >= records.Count - count; i--)
            {
                terminal.Comp.Records.Add(new AresRecordView(i, records[i], CanDelete(level, page)));
                terminal.Comp.LogReferences[i] = new AresLogReference(type, i, records[i]);
            }
        }
    }

    private void RecordAction(Entity<AresTerminalComponent> terminal, Entity<AresInterfaceCoreComponent> core,
        AresAccess level, AresActionMessage message)
    {
        if (message.Action == AresAction.DeleteRecord)
        {
            if (!CanDelete(level, terminal.Comp.Page) ||
                !terminal.Comp.LogReferences.TryGetValue(message.Record, out var record) || message.Text != record.Text ||
                !_ares.DeleteLog(core, record.Type, record.Index, record.Text))
                return;
            var text = Loc.GetString("stories-ares-deleted-event", ("page", Loc.GetString($"stories-ares-page-{terminal.Comp.Page}")),
                ("text", record.Text));
            AppendBounded(core.Comp.DeletedRecords, new AresTalkLine(_ticker.RoundDuration(), terminal.Comp.User, text), core.Comp.MaxRecords);
            Audit(core, RealUser(terminal), text);
            _adminLog.Add(LogType.Action, LogImpact.Medium, $"ARES: {ToPrettyString(terminal.Comp.Operator):user} deleted {record.Type} record: {record.Text}");
        }
        else if (CanView(level, AresPage.Requisitions) && _timing.CurTime >= core.Comp.PrinterReadyAt &&
                 core.Comp.LogTypes.TryGetValue(AresPage.Requisitions, out var type) &&
                 _ares.PullARESLogs(core, type, out var records) && records is { Count: > 0 })
        {
            var content = new StringBuilder(Loc.GetString("stories-ares-print-title"));
            foreach (var record in records)
            {
                if (content.Length + record.Length + 1 > core.Comp.MaxPrintCharacters) break;
                content.Append('\n').Append(record);
            }
            var paper = SpawnAtPosition(core.Comp.PaperPrototype, Transform(terminal).Coordinates);
            if (TryComp(paper, out PaperComponent? paperComponent))
                _paper.SetContent((paper, paperComponent), content.ToString());
            core.Comp.PrinterReadyAt = _timing.CurTime + core.Comp.PrinterCooldown;
            terminal.Comp.Feedback = Loc.GetString("stories-ares-printed");
        }
    }

    private static void AppendBounded<T>(List<T> entries, T entry, int maximum)
    {
        while (entries.Count >= Math.Max(1, maximum)) entries.RemoveAt(0);
        entries.Add(entry);
    }

    private static string FormatLine(AresTalkLine line)
        => $"{(int) line.Time.TotalHours:00}:{line.Time.Minutes:00}:{line.Time.Seconds:00} | {line.Author}: {line.Text}";
}
