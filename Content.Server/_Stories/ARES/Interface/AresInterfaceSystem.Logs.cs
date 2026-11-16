using Content.Shared._RMC14.ARES.ExternalTerminals;
using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._Stories.ARES.Interface;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Stories.ARES.Interface;

public sealed partial class AresInterfaceSystem
{
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    [Dependency] private readonly ARESExternalTerminalSystem _externalTerminals = default!;

    private static readonly ProtoId<AccessLevelPrototype> LogsAccess = "RMCAccessLogs";
    private const int LogPageSize = 12;

    private bool IsAresUiOpen(EntityUid terminal, EntityUid actor)
        => _ui.IsUiOpen(terminal, AresUiKey.Key, actor) || _ui.IsUiOpen(terminal, AresUiKey.Logs, actor);

    private void Login(Entity<AresTerminalComponent> terminal, EntityUid actor)
    {
        if (!TryCore(terminal, out var core) || !TryIdentity(terminal, actor, out var card, out _))
            return;
        terminal.Comp.Card = card;
        Audit(core, card.Comp.FullName!, Loc.GetString("stories-ares-login-event"));
    }

    private bool CanReadLog(EntProtoId<ARESLogTypeComponent> type, AccessComponent access)
    {
        if (!access.Enabled || !access.Tags.Contains(LogsAccess) || !_externalTerminals.LogTypes.Contains(type) ||
            !_prototypes.TryIndex(type, out var prototype) ||
            !prototype.TryGetComponent<ARESLogTypeComponent>(out var log, _componentFactory))
            return false;
        return log.Permissions is not { Count: > 0 } permissions || access.Tags.Overlaps(permissions);
    }

    private void OnShowLogs(Entity<AresTerminalComponent> terminal, ref RMCARESExternalShowLogs args)
    {
        if (args.Index < 0 || !TrySession(terminal, args.Actor, out _, out _) ||
            !TryComp(terminal.Comp.Card, out AccessComponent? access) ||
            args.Type is { } type && !CanReadLog(type, access))
            return;
        terminal.Comp.SelectedLog = args.Type;
        terminal.Comp.LogIndex = args.Index;
        Refresh(terminal);
    }

    private static void ClearLogViewer(AresTerminalComponent terminal)
    {
        terminal.AvailableLogs.Clear();
        terminal.SelectedLog = null;
        terminal.LogEntries.Clear();
        terminal.LogIndex = terminal.LogCount = 0;
    }

    private void RefreshLogViewer(Entity<AresTerminalComponent> terminal, Entity<AresInterfaceCoreComponent> core,
        EntityUid actor)
    {
        if (!_ui.IsUiOpen(terminal.Owner, AresUiKey.Logs, actor) ||
            !TryComp(terminal.Comp.Card, out AccessComponent? access))
        {
            ClearLogViewer(terminal.Comp);
            return;
        }
        terminal.Comp.LogAccessRevision = access.LastModifiedTick;
        terminal.Comp.AvailableLogs = [];
        foreach (var type in _externalTerminals.LogTypes)
        {
            if (CanReadLog(type, access))
                terminal.Comp.AvailableLogs.Add(type);
        }
        terminal.Comp.LogEntries = [];
        terminal.Comp.LogCount = 0;
        if (terminal.Comp.SelectedLog is not { } selected || !CanReadLog(selected, access))
        {
            terminal.Comp.SelectedLog = null;
            terminal.Comp.LogIndex = 0;
            return;
        }
        if (!_ares.PullARESLogs(core, selected, out var records) || records == null)
            return;
        var count = Math.Min(records.Count, core.Comp.MaxRecords);
        terminal.Comp.LogCount = count;
        terminal.Comp.LogIndex = Math.Clamp(terminal.Comp.LogIndex, 0, Math.Max(0, (count - 1) / LogPageSize));
        for (var i = records.Count - 1 - terminal.Comp.LogIndex * LogPageSize;
             i >= records.Count - count && terminal.Comp.LogEntries.Count < LogPageSize; i--)
            terminal.Comp.LogEntries.Add(records[i]);
    }

    private bool LogAccessChanged(Entity<AresTerminalComponent> terminal, EntityUid actor)
        => _ui.IsUiOpen(terminal.Owner, AresUiKey.Logs, actor) &&
            TryComp(terminal.Comp.Card, out AccessComponent? access) &&
            terminal.Comp.LogAccessRevision != access.LastModifiedTick;
}
