using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.ZLevels.Core.Components;

/// <summary>
/// Tracker that tracks all maps added to the zLevel network. Usually, entity in Nullspace,
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZMapNetworkComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public readonly Dictionary<int, EntityUid?> ZLevels = new();

    /// <summary>
    /// Shared components for all zLevels maps
    /// </summary>
    [DataField(serverOnly: true)]
    public ComponentRegistry Components = new();

    /// <remarks>
    /// Reversed version of <see cref="ZLevels"/>
    /// </remarks>
    [ViewVariables, AutoNetworkedField]
    public readonly Dictionary<EntityUid, int> ZLevelByEntity = new();

    [ViewVariables, AutoNetworkedField]
    public readonly List<EntityUid> SortedZLevels = new();

    [ViewVariables, AutoNetworkedField]
    public int SortedMin = 0;

    [ViewVariables, AutoNetworkedField]
    public int SortedMax = 0;
}
