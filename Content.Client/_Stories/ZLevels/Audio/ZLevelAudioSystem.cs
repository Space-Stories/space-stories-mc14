#pragma warning disable RA0002
using System.Numerics;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Shared;
using Robust.Shared.Audio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Client._Stories.ZLevels.Audio;

public sealed partial class ClientZLevelAudioSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevels = default!;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private float _perLevelAttenuationDb;
    private float _floorOcclusion;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(AudioSystem));
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        Subs.CVar(_cfg, CCVars.ZLevelsAudioPerLevelAttenuation, value => _perLevelAttenuationDb = value, true);
        Subs.CVar(_cfg, CCVars.ZLevelsAudioFloorOcclusion, value => _floorOcclusion = value, true);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var listener = _audio.GetListenerCoordinates();
        if (listener.MapId == MapId.Nullspace)
            return;
        if (!_map.TryGetMap(listener.MapId, out var listenerMapUid))
            return;
        var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var xform))
        {
            if (component.Global)
                continue;

            // Only interested in cross-map sounds on different Z-levels
            if (xform.MapID == listener.MapId || xform.MapID == MapId.Nullspace)
                continue;
            if (xform.MapUid is not { } entityMapUid)
                continue;
            if (!_zLevels.TryGetZLevelOffset(listenerMapUid.Value, entityMapUid, out var levelOffset) ||
                !IsWithinRenderedRange(levelOffset))
            {
                continue;
            }

            if (!component.Started)
            {
                component.Started = true;
                component.StartPlaying();
            }

            var parentUid = xform.ParentUid;
            Vector2 worldPos;
            if ((component.Flags & AudioFlags.GridAudio) != 0x0)
                worldPos = _map.GetGridPosition(parentUid);
            else
                worldPos = _transform.GetWorldPosition(uid);

            var upperMapUid = levelOffset > 0 ? entityMapUid : listenerMapUid.Value;

            var directHoleAtSound = _zLevels.IsTileTransparentOrEmpty(upperMapUid, worldPos);
            var directHoleAtListener = _zLevels.IsTileTransparentOrEmpty(upperMapUid, listener.Position);

            Vector2 openingPos;
            float dSound;
            float dListener;

            if (directHoleAtSound)
            {
                openingPos = worldPos;
                dSound = 0f;
                dListener = (openingPos - listener.Position).Length();
            }
            else if (directHoleAtListener)
            {
                openingPos = listener.Position;
                dSound = (worldPos - openingPos).Length();
                dListener = 0f;
            }
            else
            {
                // Sound must physically travel from source to an opening, then from opening to listener.
                // An opening must be within range of both the sound and the listener.
                var maxRadius = Math.Min(component.MaxDistance, 20f);
                var found = _zLevels.TryFindOpeningBetweenLevels(upperMapUid, worldPos, maxRadius, out openingPos);
                if (!found)
                    found = _zLevels.TryFindOpeningBetweenLevels(upperMapUid, listener.Position, maxRadius, out openingPos);

                if (!found)
                {
                    // No opening connecting the two floors within sound range
                    component.Gain = 0f;
                    continue;
                }

                dSound = (worldPos - openingPos).Length();
                dListener = (openingPos - listener.Position).Length();
            }

            // Total acoustic path distance includes travel to opening, vertical floor distance, and travel to listener
            var verticalDist = MathF.Abs(levelOffset) * 3.5f;
            var totalPathDistance = dSound + dListener + verticalDist;

            if (_audio.GetAudioDistance(totalPathDistance) > component.MaxDistance)
            {
                component.Gain = 0f;
                continue;
            }

            // Direction of the sound is panned towards the opening where it enters the room,
            // while distance in OpenAL is set to the full acoustic path length.
            var effectivePos = openingPos;
            if (dListener > 0.05f)
            {
                var dirToOpening = (openingPos - listener.Position) / dListener;
                effectivePos = listener.Position + (dirToOpening * totalPathDistance);
            }

            // Sound passing across Z-levels is properly attenuated based on level offset
            var attenuatedVolume = component.Params.Volume - (MathF.Abs(levelOffset) * _perLevelAttenuationDb);
            component.Volume = attenuatedVolume;
            if ((component.Flags & AudioFlags.NoOcclusion) == AudioFlags.NoOcclusion)
                component.Occlusion = 0f;
            else
                component.Occlusion = (directHoleAtSound || directHoleAtListener) ? 0f : _floorOcclusion * 0.5f;

            component.Position = effectivePos;
            if (_physicsQuery.TryGetComponent(parentUid, out var physicsComp))
                component.Velocity = _physics.GetMapLinearVelocity(parentUid, physicsComp);

            // Engine muted cross-map sound because listener.MapId != xform.MapID;
            // Un-mute so it plays at the proper attenuated volume!
            component.Gain = 1.0f;
        }
    }

    private static bool IsWithinRenderedRange(int levelOffset)
    {
        return levelOffset switch
        {
            > 0 => levelOffset <= SharedZLevelsSystem.MaxZLevelsAboveRendering,
            < 0 => -levelOffset <= SharedZLevelsSystem.MaxZLevelsBelowRendering,
            _ => true,
        };
    }
}
