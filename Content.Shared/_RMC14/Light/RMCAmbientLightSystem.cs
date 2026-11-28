using System.Linq;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.Dataset;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Light;

public sealed class RMCAmbientLightSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevels = default!; // Stories-ZLevels

    public Color GetColor(Entity<RMCAmbientLightComponent> ent, TimeSpan curTime)
    {
        if (ent.Comp.Colors.Count == 0 || ent.Comp.Duration <= TimeSpan.Zero)
            return Color.Black;
        if (ent.Comp.Colors.Count == 1)
            return ent.Comp.Colors[0];

        var elapsedTime = curTime - ent.Comp.StartTime;
        var progress = Math.Clamp(elapsedTime / ent.Comp.Duration, 0.0, 1.0);

        var segmentCount = ent.Comp.Colors.Count - 1;
        var segmentLength = 1.0/segmentCount;
        var prevColorIndex = Math.Min((int)(progress / segmentLength), segmentCount - 1);
        var nextColorIndex = prevColorIndex + 1;

        var segmentProgress = Math.Clamp((progress - (prevColorIndex * segmentLength)) / segmentLength, 0.0, 1.0);

        var prevColor = ent.Comp.Colors[prevColorIndex];
        var nextColor = ent.Comp.Colors[nextColorIndex];

        var color = Color.InterpolateBetween(prevColor, nextColor, (float)segmentProgress);

        return color;
    }

    public List<Color> ProcessPrototype(ProtoId<DatasetPrototype> protoId)
    {
        var colorList = new List<Color>();
        if (!_prototype.TryIndex(protoId, out var colorDataset))
            return colorList;
        // Stories-ZLevels-Start
        foreach (var hex in colorDataset.Values)
        {
            colorList.Add(Color.FromHex(hex, Color.Black));
        }
        return colorList;
    }

    private List<EntityUid> GetZNetworkMaps(EntityUid uid)
    {
        var result = new List<EntityUid>();
        if (_zLevels.TryGetMapNetwork(uid, out var zNet))
        {
            foreach (var val in zNet.Comp.ZLevels.Values)
            {
                if (val.HasValue)
                {
                    result.Add(val.Value);
                }
            }
        }

        if (result.Count == 0)
        {
            result.Add(uid);
        }

        return result;
        // Stories-ZLevels-End
    }

    public void SetColor(Entity<RMCAmbientLightComponent> ent, Color colorHex, TimeSpan duration)
    {
        if (_net.IsClient)
            return;

        // Stories-ZLevels-Start
        var mapsToUpdate = GetZNetworkMaps(ent.Owner);
        foreach (var mapUid in mapsToUpdate)
        {
            var ambientComp = EnsureComp<RMCAmbientLightComponent>(mapUid);
            var mapLight = EnsureComp<MapLightComponent>(mapUid);
        // Stories-ZLevels-End

            // Stories-ZLevels-Start
            ambientComp.Colors.Clear();
            ambientComp.Colors.Add(mapLight.AmbientLightColor);
            ambientComp.Colors.Add(colorHex);
            ambientComp.Duration = duration;
            ambientComp.StartTime = _timing.CurTime;
            ambientComp.IsAnimating = true;
            // Stories-ZLevels-End

            // Stories-ZLevels-Start
            Dirty(mapUid, ambientComp);
        }
            // Stories-ZLevels-End
    }

    public void SetColor(Entity<RMCAmbientLightComponent> ent, List<Color> colorList, TimeSpan duration)
    {
        if (_net.IsClient)
            return;

        if (colorList.Count == 0 || duration <= TimeSpan.Zero)
            return;

        // Stories-ZLevels-Start
        var mapsToUpdate = GetZNetworkMaps(ent.Owner);
        foreach (var mapUid in mapsToUpdate)
        {
            var ambientComp = EnsureComp<RMCAmbientLightComponent>(mapUid);
            EnsureComp<MapLightComponent>(mapUid);
        // Stories-ZLevels-End

            // Stories-ZLevels-Start
            ambientComp.Colors.Clear();
            ambientComp.Colors.AddRange(colorList);
            ambientComp.Duration = duration;
            ambientComp.StartTime = _timing.CurTime;
            ambientComp.IsAnimating = true;
            // Stories-ZLevels-End

            // Stories-ZLevels-Start
            Dirty(mapUid, ambientComp);
        }
            // Stories-ZLevels-End
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var lightQuery = EntityQueryEnumerator<RMCAmbientLightComponent, MapLightComponent>();
        var curTime = _timing.CurTime;

        while (lightQuery.MoveNext(out var uid, out var animComponent, out var lightComponent))
        {
            if (!animComponent.IsAnimating)
                continue;

            // Stories-ZLevels-Start
            var zMaps = GetZNetworkMaps(uid);

            // Stories-ZLevels-End
            if (curTime >= animComponent.EndTime)
            {
                var finalColor = animComponent.Colors[^1]; // Stories-ZLevels
                animComponent.IsAnimating = false;
                Dirty(uid, animComponent);
                // Stories-ZLevels-Start

                foreach (var zMapUid in zMaps)
                {
                    if (TryComp<MapLightComponent>(zMapUid, out var zMapLight))
                    {
                        zMapLight.AmbientLightColor = finalColor;
                        Dirty(zMapUid, zMapLight);
                    }
                    if (TryComp<RMCAmbientLightComponent>(zMapUid, out var zAnim))
                    {
                        zAnim.IsAnimating = false;
                        Dirty(zMapUid, zAnim);
                    }
                }
                // Stories-ZLevels-End
                continue;
            }

            var targetColor = GetColor((uid, animComponent), curTime);
            // Stories-ZLevels-Start
            foreach (var zMapUid in zMaps)
            {
                if (TryComp<MapLightComponent>(zMapUid, out var zMapLight))
                {
                    zMapLight.AmbientLightColor = targetColor;
                    Dirty(zMapUid, zMapLight);
                }
            }
            // Stories-ZLevels-End
        }
    }
}
