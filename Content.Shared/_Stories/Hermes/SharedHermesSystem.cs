using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Access.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Hermes;

public abstract class SharedHermesSystem : EntitySystem
{
    private static readonly EntProtoId WorkingJoeGeneration = "RMCARESWorker";
    private static readonly EntProtoId<IFFFactionComponent> AresFaction = "STFactionARES";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SynthGenerationComponent, GetIFFFactionEvent>(OnSynthGetFaction);
    }

    protected bool IsWorkingJoe(EntityUid actor)
        => TryComp(actor, out SynthGenerationComponent? generation) && generation.Generation == WorkingJoeGeneration;

    private void OnSynthGetFaction(Entity<SynthGenerationComponent> ent, ref GetIFFFactionEvent args)
    {
        if (ent.Comp.Generation == WorkingJoeGeneration)
            args.Factions.Add(AresFaction);
    }

    public static bool IsClosed(HermesTicketStatus status) => status is
        HermesTicketStatus.Cancelled or HermesTicketStatus.Rejected or HermesTicketStatus.Completed or
        HermesTicketStatus.Revoked or HermesTicketStatus.Returned;

    public static bool CanResolve(HermesTicketStatus status) => status is
        HermesTicketStatus.Pending or HermesTicketStatus.Assigned;

    public static HermesAccess GetAccess(HermesCoreComponent core, AccessComponent access, bool workingJoe, bool chiefEngineer = false)
    {
        if (!access.Enabled)
            return HermesAccess.LoggedOut;
        if (access.Tags.Contains(core.TechnicianAccess))
            return HermesAccess.Technician;
        if (workingJoe)
            return HermesAccess.Joe;
        if (access.Tags.Overlaps(core.CertifiedAccess) || access.Tags.Contains(core.ChiefEngineerAccess) &&
            (chiefEngineer || !access.Tags.Contains(core.SeniorCommandAccess)))
            return HermesAccess.Certified;
        if (access.Tags.Contains(core.TemporaryAccess))
            return HermesAccess.Temporary;
        return access.Tags.Overlaps(core.ReporterAccess) ? HermesAccess.Reporter : HermesAccess.Visitor;
    }
}
