using System.Numerics;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.MilitaryPolice.Evidence;

[RegisterComponent]
[Access(typeof(RMCEvidenceBagVisualsSystem))]
public sealed partial class RMCEvidenceBagVisualsComponent : Component
{
    [DataField(required: true)]
    public SpriteSpecifier EmptySprite = default!;

    [DataField(required: true)]
    public SpriteSpecifier FullSprite = default!;

    [DataField]
    public Vector2 FullSpriteScale = Vector2.One;

    [DataField]
    public Vector2 FullSpriteOffset;

    [DataField]
    public Color OverlayColor = Color.White;

    [DataField(required: true)]
    public Box2 ContentsBounds;
}
