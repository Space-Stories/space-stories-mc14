using Content.Server._RMC14.ERT;
using Content.Shared._RMC14.AlertLevel;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.ControlComputer;
using Content.Shared._Stories.ARES.Interface;

namespace Content.Server._Stories.ARES.Interface;

public sealed partial class AresInterfaceSystem
{
    [Dependency] private readonly RMCAlertLevelSystem _alert = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _announce = default!;
    [Dependency] private readonly RMCERTSystem _ert = default!;
    [Dependency] private readonly SharedEvacuationSystem _evacuation = default!;
    [Dependency] private readonly SharedMarineControlComputerSystem _marineControl = default!;

    private void EmergencyAction(Entity<AresTerminalComponent> terminal, Entity<AresInterfaceCoreComponent> core,
        AresAccess level, AresActionMessage message)
    {
        if (!CanView(level, AresPage.Emergency))
            return;

        switch (message.Action)
        {
            case AresAction.GeneralQuarters:
                if (_timing.CurTime < core.Comp.QuartersReadyAt)
                {
                    terminal.Comp.Feedback = Loc.GetString("stories-ares-cooldown");
                    return;
                }

                if ((_alert.Get() ?? RMCAlertLevels.Green) < RMCAlertLevels.Red)
                    _alert.Set(RMCAlertLevels.Red, message.Actor, false, false);

                _announce.AnnounceARES(core, Loc.GetString("stories-ares-quarters-announcement"), core.Comp.QuartersSound);
                core.Comp.QuartersReadyAt = _timing.CurTime + core.Comp.QuartersCooldown;
                SecurityLog(core, terminal.Comp.User, Loc.GetString("stories-ares-quarters"));
                break;
            case AresAction.Evacuate:
                if ((_alert.Get() ?? RMCAlertLevels.Green) < RMCAlertLevels.Red || !_marineControl.CanToggleEvacuation())
                {
                    terminal.Comp.Feedback = Loc.GetString("stories-ares-evacuation-unavailable");
                    return;
                }

                if (!_evacuation.IsEvacuationEnabled() && Transform(core).MapUid is { } map)
                {
                    _evacuation.ToggleEvacuation(null, null, map);
                    SecurityLog(core, terminal.Comp.User, Loc.GetString("stories-ares-evacuate"));
                }
                break;
            case AresAction.Distress:
                var result = _ert.RequestConsoleDistress(terminal, message.Actor, message.Text, core.Comp.MaxTextLength);
                terminal.Comp.Feedback = result.Success ? Loc.GetString("stories-ares-request-sent") : result.Error;
                if (result.Success)
                    SecurityLog(core, terminal.Comp.User, Loc.GetString("stories-ares-distress") + ": " + message.Text.Trim());
                break;
        }
    }

    private void SecurityLog(Entity<AresInterfaceCoreComponent> core, string user, string message)
    {
        if (core.Comp.LogTypes.TryGetValue(AresPage.Security, out var type))
            _ares.CreateARESLog(type, $"{user}: {message}", core);
    }
}
