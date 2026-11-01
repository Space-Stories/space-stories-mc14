using Content.Shared.Tools.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Stories.ZLevels.Tiles;

/// <summary>
/// Marker for entities that also have <see cref="ToolTileCompatibleComponent"/>, allowing them to
/// deconstruct the tile on the z-level directly above the wielder while <see cref="Content.Shared._Stories.ZLevels.Core.Components.ZLevelViewerComponent.LookUp"/>
/// is enabled. Reuses <see cref="ToolTileCompatibleComponent.Delay"/> and <see cref="ToolTileCompatibleComponent.RequiresUnobstructed"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(ZLevelToolTileSystem))]
public sealed partial class ZLevelToolTileComponent : Component;
