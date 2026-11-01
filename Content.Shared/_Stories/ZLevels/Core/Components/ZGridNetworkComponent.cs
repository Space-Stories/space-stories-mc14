using Robust.Shared.GameStates;

namespace Content.Shared._Stories.ZLevels.Core.Components;

/// <summary>
/// Runtime-only nullspace manager entity for a z-grid network.
/// Always reconstructed by <see cref="ZGridConnectorSystem"/> — not persisted.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZGridNetworkComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public string NetworkId = string.Empty;

    [ViewVariables, AutoNetworkedField]
    public readonly HashSet<EntityUid> Grids = new();

    /// <summary>
    /// The single authoritative grid all relative poses are stored against.
    /// Static anchors (planet) take priority; otherwise the lowest <see cref="EntityUid"/> for determinism.
    /// Its own <see cref="ZGridComponent.NetworkOffset"/>/<see cref="ZGridComponent.NetworkRotation"/> are zero.
    /// </summary>
    [ViewVariables]
    public EntityUid AnchorGrid = EntityUid.Invalid;

    /// <summary>
    /// Total cached fixture mass of all grids in the network.
    /// </summary>
    [ViewVariables]
    public float TotalCachedMass = 0f;

    /// <summary>
    /// True if network contains a static anchor (planet/terrain). Entire network is locked in place.
    /// </summary>
    [ViewVariables]
    public bool HasStaticAnchor = false;
}
