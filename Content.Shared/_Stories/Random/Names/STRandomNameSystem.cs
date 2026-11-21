using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Stories.Random.Names;

public sealed class STRandomNameSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STRandomNameComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<STRandomNameComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var meta = MetaData(ent);

        var baseName = Loc.GetString(ent.Comp.BaseName);
        var postFix = Loc.GetString(ent.Comp.PostFix);
        var randomNumber = _random.Next(1, ent.Comp.MaxNumber);
        var finalName = $"{baseName} {postFix}{randomNumber}";

        _metaData.SetEntityName(ent, finalName, meta);
    }
}
