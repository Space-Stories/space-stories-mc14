using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.ERT;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Server._RMC14.ERT;

public sealed partial class RMCERTSystem
{
    [Dependency] private readonly ARESCoreSystem _ares = default!;

    private static readonly EntProtoId<IFFFactionComponent> ConsoleFaction = "FactionMarine";

    /// <summary>
    /// Sends the same distress request from communications consoles, command tablets and ARES terminals.
    /// The caller checks the operator's access and interaction with the originating terminal.
    /// </summary>
    public RMCERTRequestResult RequestConsoleDistress(EntityUid console, EntityUid user, string reason, int reasonLimit)
    {
        reason = reason.Trim();
        if (TerminatingOrDeleted(console) || TerminatingOrDeleted(user) || reason.Length == 0 || reason.Length > reasonLimit)
            return ResultFailure(Guid.Empty, null, Loc.GetString("stories-ares-reason-required"));

        if (!_alertLevel.IsRedOrDeltaAlert())
            return ResultFailure(Guid.Empty, null, Loc.GetString("rmc-ert-popup-console-alert-required"));

        var calls = GetCallOptions(new RMCERTCallQueryArgs
        {
            EnabledOnly = true,
            Source = RMCERTRequestSource.Console,
        });
        var allowed = new List<ProtoId<RMCERTCallPrototype>>();
        foreach (var call in calls)
            allowed.Add(new ProtoId<RMCERTCallPrototype>(call.Id));

        return CreateRequest(new RMCERTCreateRequestArgs
        {
            Source = RMCERTRequestSource.Console,
            SourceEntity = console,
            Requester = user,
            Reason = reason,
            AllowedCalls = allowed,
            AllowRandomSelection = true,
            AllowSpecificSelection = false,
            AutoResolution = GetConsoleAutoResolutionOptions(),
        });
    }

    private EntityUid GetRequestSource(RMCERTRequestSource type, EntityUid source)
    {
        if (type == RMCERTRequestSource.Console && _ares.TryGetARES(ConsoleFaction, out var core) && !TerminatingOrDeleted(core.Value))
            return core.Value;

        return source;
    }
}
