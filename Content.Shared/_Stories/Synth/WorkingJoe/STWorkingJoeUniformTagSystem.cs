using Content.Shared.Clothing;
using Robust.Shared.GameObjects;

namespace Content.Shared._Stories.Synth.WorkingJoe;

public sealed class STWorkingJoeUniformTagSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private const string ErrorTag = "3RR0R";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STWorkingJoeUniformComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<STWorkingJoeUniformComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<STWorkingJoeUniformComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (TerminatingOrDeleted(args.Wearer))
            return;

        if (!TryComp<STWorkingJoeErrorTagComponent>(args.Wearer, out var tag))
            return;

        _metaData.SetEntityName(args.Wearer, tag.RealName);
        RemCompDeferred<STWorkingJoeErrorTagComponent>(args.Wearer);
    }

    private void OnUnequipped(Entity<STWorkingJoeUniformComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (TerminatingOrDeleted(args.Wearer))
            return;

        if (HasComp<STWorkingJoeErrorTagComponent>(args.Wearer))
            return;

        var tag = EnsureComp<STWorkingJoeErrorTagComponent>(args.Wearer);
        tag.RealName = MetaData(args.Wearer).EntityName;
        _metaData.SetEntityName(args.Wearer, ErrorTag);
    }
}
