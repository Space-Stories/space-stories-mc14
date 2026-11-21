using System.Linq;
using Content.Shared._RMC14.Access;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Construction;
using Content.Shared.Doors.Electronics;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Construction;

[DataDefinition]
public sealed partial class RMCApplyDoorElectronics : IGraphTransform
{
    [DataField(required: true)]
    public string Container = default!;

    public void Transform(EntityUid oldUid, EntityUid newUid, EntityUid? userUid, GraphTransformArgs args)
    {
        var entities = args.EntityManager;
        var containers = entities.System<SharedContainerSystem>();
        if (!containers.TryGetContainer(newUid, Container, out var installed) ||
            !entities.TryGetComponent<AccessReaderComponent>(newUid, out var doorReader))
        {
            return;
        }

        foreach (var board in installed.ContainedEntities)
        {
            if (entities.TryGetComponent<RMCDoorElectronicsComponent>(board, out var electronics))
            {
                entities.System<RMCDoorElectronicsSystem>().ApplyConfiguration((board, electronics), (newUid, doorReader));
                return;
            }

            if (!entities.HasComponent<DoorElectronicsComponent>(board) ||
                !entities.TryGetComponent<AccessReaderComponent>(board, out var boardReader))
            {
                continue;
            }

            var allowed = IoCManager.Resolve<IPrototypeManager>().Index(RMCDoorElectronicsSystem.MarineAccessCatalog).Tags;
            var accesses = boardReader.AccessLists
                .Where(group => group.Count > 0 && group.IsSubsetOf(allowed))
                .Select(group => new HashSet<ProtoId<AccessLevelPrototype>>(group))
                .ToList();
            entities.System<AccessReaderSystem>().SetAccesses((newUid, doorReader), accesses);
            return;
        }
    }
}
