using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ghost;

/// <summary>
/// Represents an observer ghost.
/// Handles limiting interactions, using ghost abilities, ghost visibility, and ghost warping.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedGhostSystem))]
[AutoGenerateComponentState(true)] // Stories-XenoRespawnCooldown
public sealed partial class GhostComponent : Component
{
    // Actions
    [DataField]
    public EntProtoId ToggleLightingAction = "ActionToggleLighting";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleLightingActionEntity;

    [DataField]
    public EntProtoId ToggleFoVAction = "ActionToggleFov";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleFoVActionEntity;

    [DataField]
    public EntProtoId ToggleGhostsAction = "ActionToggleGhosts";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleGhostsActionEntity;

    [DataField]
    public EntProtoId ToggleGhostHearingAction = "ActionToggleGhostHearing";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleGhostHearingActionEntity;

    [DataField]
    public EntProtoId BooAction = "ActionGhostBoo";

    [DataField, AutoNetworkedField]
    public EntityUid? BooActionEntity;

    // End actions

    // Stories-XenoRespawnCooldown-Start
    /// <summary>
    /// Real server uptime when the player died or started observing.
    /// Used for respawn delays and votekick eligibility, including time spent paused.
    /// </summary>
    [DataField]
    public TimeSpan TimeOfDeath = TimeSpan.Zero;
    // Stories-XenoRespawnCooldown-End

    /// <summary>
    /// Range of the Boo action.
    /// </summary>
    [DataField]
    public float BooRadius = 3;

    /// <summary>
    /// Maximum number of entities that can affected by the Boo action.
    /// </summary>
    [DataField]
    public int BooMaxTargets = 3;

    /// <summary>
    /// Is this ghost allowed to interact with entities?
    /// </summary>
    /// <remarks>
    /// Used to allow admins ghosts to interact with the world.
    /// Changed by <see cref="SharedGhostSystem.SetCanGhostInteract"/>.
    /// </remarks>
    [DataField("canInteract"), AutoNetworkedField]
    public bool CanGhostInteract;

    /// <summary>
    /// Is this ghost player allowed to return to their original body?
    /// </summary>
    /// <remarks>
    /// Changed by <see cref="SharedGhostSystem.SetCanReturnToBody"/>.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public bool CanReturnToBody;

    /// <summary>
    /// Ghost color
    /// </summary>
    /// <remarks>Used to allow admins to change ghost colors. Should be removed if the capability to edit existing sprite colors is ever added back.</remarks>
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;
}

public sealed partial class ToggleFoVActionEvent : InstantActionEvent { }

public sealed partial class ToggleGhostsActionEvent : InstantActionEvent { }

public sealed partial class ToggleLightingActionEvent : InstantActionEvent { }

public sealed partial class ToggleGhostHearingActionEvent : InstantActionEvent { }

public sealed partial class ToggleGhostVisibilityToAllEvent : InstantActionEvent { }

public sealed partial class BooActionEvent : InstantActionEvent { }
