// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.Dropship.Weapon;

public abstract partial class SharedDropshipWeaponSystem
{
    private void InitializePlanetTargets()
    {
        SubscribeLocalEvent<DropshipTargetComponent, MapUidChangedEvent>(OnTargetMapChanged);
        SubscribeLocalEvent<DropshipTargetComponent, ComponentShutdown>(OnTargetMapRemove);
        SubscribeLocalEvent<DropshipTargetComponent, MetaFlagRemoveAttemptEvent>(OnTargetMetaFlagRemoveAttempt);
    }

    private void InitializeTargetMap(Entity<DropshipTargetComponent> ent)
    {
        ent.Comp.TargetMap = Transform(ent).MapUid;
        _metaData.AddFlag(ent, MetaDataFlags.ExtraTransformEvents);
        Dirty(ent);
    }

    private void OnTargetMapChanged(Entity<DropshipTargetComponent> ent, ref MapUidChangedEvent args)
    {
        if (!ent.Comp.IsTargetableByWeapons)
            return;

        if (ent.Comp.TargetMap == null)
        {
            ent.Comp.TargetMap = args.NewMap;
            Dirty(ent);
            return;
        }

        if (ent.Comp.TargetMap != args.NewMap)
            RemCompDeferred<DropshipTargetComponent>(ent);
    }

    private void OnTargetMapRemove(Entity<DropshipTargetComponent> ent, ref ComponentShutdown args)
    {
        _metaData.RemoveFlag(ent, MetaDataFlags.ExtraTransformEvents);
    }

    private void OnTargetMetaFlagRemoveAttempt(Entity<DropshipTargetComponent> ent, ref MetaFlagRemoveAttemptEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.ToRemove &= ~MetaDataFlags.ExtraTransformEvents;
    }
}
