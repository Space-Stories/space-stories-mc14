using System.Linq;
using Content.Shared._RMC14.Prototypes;
using Content.Shared.Guidebook;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Guidebook;

public static class RMCGuidebookUtils
{
    public static Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry> GetDefaultGuides(
        IPrototypeManager prototypes, string serverGuide)
    {
        var guides = prototypes.EnumerateCM<GuideEntryPrototype>()
            .ToDictionary(guide => new ProtoId<GuideEntryPrototype>(guide.ID), guide => (GuideEntry) guide);
        var pending = new Stack<ProtoId<GuideEntryPrototype>>(guides.Keys.Where(id =>
            prototypes.Index(id).ServerGuide is { } required && required != serverGuide));
        var excluded = new HashSet<ProtoId<GuideEntryPrototype>>();
        while (pending.TryPop(out var id))
        {
            if (!excluded.Add(id) || !guides.TryGetValue(id, out var guide))
                continue;
            foreach (var child in guide.Children)
                pending.Push(child);
        }
        foreach (var id in excluded)
            guides.Remove(id);
        return guides;
    }
}
