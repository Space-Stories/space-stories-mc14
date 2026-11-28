using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.ERT;
using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.ERT;

public sealed partial class RMCERTSystem
{
    private static readonly ProtoId<RMCERTCallPrototype> BlackMarketInvestigationCall = "RMCERTCallCMBBlackMarket";

    private void InitializeBlackMarketInvestigation()
    {
        SubscribeLocalEvent<RequisitionsAccountComponent, RMCBlackMarketInvestigationEvent>(OnBlackMarketInvestigation);
    }

    private void OnBlackMarketInvestigation(
        Entity<RequisitionsAccountComponent> account,
        ref RMCBlackMarketInvestigationEvent args)
    {
        if (!_prototypes.TryIndex(BlackMarketInvestigationCall, out var call) ||
            !call.Enabled ||
            !call.AllowedSources.Contains(RMCERTRequestSource.Ares))
        {
            Log.Warning($"Cannot start black market investigation: {BlackMarketInvestigationCall} " +
                        "is missing, disabled or unavailable to ARES.");
            return;
        }

        var result = CreateRequest(new RMCERTCreateRequestArgs
        {
            Source = RMCERTRequestSource.Ares,
            Requester = args.Actor,
            Reason = Loc.GetString("rmc-ert-announcement-cmb-black-market-request"),
            AllowedCalls = [BlackMarketInvestigationCall],
            AllowRandomSelection = false,
            AnnounceRequest = false,
        });
        if (!result.Success)
        {
            Log.Warning($"Cannot create black market investigation request: {result.Error}");
            return;
        }

        ApproveRequest(new RMCERTApproveRequestArgs
        {
            Request = result.RequestId,
            Call = BlackMarketInvestigationCall,
            ActorName = RMCERTLoc.GetSource(RMCERTRequestSource.Ares),
        });
    }
}
