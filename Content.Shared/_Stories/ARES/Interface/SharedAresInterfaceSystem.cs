using Content.Shared.Access.Components;

namespace Content.Shared._Stories.ARES.Interface;

public abstract class SharedAresInterfaceSystem : EntitySystem
{
    public static AresAccess GetAccess(AresInterfaceCoreComponent core, AccessComponent access, bool joe, AresAccess? assigned = null)
    {
        if (!access.Enabled)
            return AresAccess.LoggedOut;
        var tags = access.Tags;
        if (tags.Contains(core.TechnicianAccess)) return AresAccess.Technician;
        if (joe) return AresAccess.Joe;
        if (assigned == AresAccess.ChiefEngineer && tags.Contains(core.ChiefEngineerAccess)) return AresAccess.ChiefEngineer;
        if (assigned == AresAccess.Synthetic && tags.Contains(core.SyntheticAccess)) return AresAccess.Synthetic;
        if (tags.Contains(core.CorporateCommandAccess)) return AresAccess.CorporateCommand;
        if (tags.Contains(core.HighCommandAccess)) return AresAccess.HighCommand;
        if (tags.Contains(core.CommandingOfficerAccess)) return AresAccess.CommandingOfficer;
        if (!tags.Contains(core.SeniorCommandAccess))
        {
            if (tags.Contains(core.SyntheticAccess)) return AresAccess.Synthetic;
            if (tags.Contains(core.ChiefEngineerAccess)) return AresAccess.ChiefEngineer;
        }
        if (tags.Contains(core.SeniorCommandAccess)) return AresAccess.SeniorCommand;
        if (tags.Contains(core.CorporateAccess)) return AresAccess.Corporate;
        return tags.Contains(core.CommandAccess) ? AresAccess.Command : AresAccess.Basic;
    }

    public static bool CanUseSecurity(AresAccess access) => access == AresAccess.Joe || access >= AresAccess.ChiefEngineer;

    public static bool CanView(AresAccess access, AresPage page) => page switch
    {
        AresPage.Announcements or AresPage.Talk => access >= AresAccess.Basic,
        AresPage.Flights or AresPage.Bioscans or AresPage.Bombardments => access >= AresAccess.Command,
        AresPage.Security or AresPage.Hermes => access >= AresAccess.Joe,
        AresPage.Requisitions or AresPage.Technology or AresPage.Emergency => access >= AresAccess.SeniorCommand,
        AresPage.Access => access >= AresAccess.ChiefEngineer,
        AresPage.CoreSecurity => CanUseSecurity(access),
        AresPage.Service => access == AresAccess.Technician,
        AresPage.DeletedRecords or AresPage.Conversations or AresPage.DeletedConversations => access >= AresAccess.HighCommand,
        _ => false,
    };

    public static bool CanDelete(AresAccess access, AresPage page) => page switch
    {
        AresPage.Announcements or AresPage.Flights or AresPage.Technology => access >= AresAccess.Corporate,
        AresPage.Bioscans or AresPage.Bombardments => access >= AresAccess.SeniorCommand,
        AresPage.Security => access >= AresAccess.CommandingOfficer,
        _ => false,
    };
}
