using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Server._RMC14.Language.Systems;
using Content.Shared._Stories.ZLevels.Core.Components;
using Content.Shared._Stories.ZLevels.Core.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Content.Shared.Speech;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using InGameICChatType = Content.Server.Chat.Systems.InGameICChatType;

namespace Content.Server._Stories.ZLevels.Chat;

public sealed partial class ZLevelsSpeakingSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelsSystem _zLevels = default!;

    private EntityQuery<MapComponent> _mapQuery;

    private const float TransmitterLifetime = 3f;
    private const int MessageDelayMilliseconds = 333;

    public override void Initialize()
    {
        base.Initialize();

        _mapQuery = GetEntityQuery<MapComponent>();
        SubscribeLocalEvent<ZLevelViewerComponent, EntitySpokeEvent>(OnSpoke);
    }

    private void OnSpoke(Entity<ZLevelViewerComponent> ent, ref EntitySpokeEvent args)
    {
        if (args.Channel != null)
            return;

        if (args.ObfuscatedMessage is not null) // Curse of chatcode: this is only way detect whispers
            return;

        TransmitToAdjacentZLevels(ent, args.Message, Identity.Name(ent, EntityManager), InGameICChatType.Whisper);
    }

    /// <summary>
    /// Repeats a message one z-level above and below the source, at the same world position.
    /// </summary>
    public void TransmitToAdjacentZLevels(
        EntityUid source,
        string message,
        string name,
        InGameICChatType chatType,
        ProtoId<SpeechVerbPrototype>? speechVerb = null)
    {
        var xform = Transform(source);
        var sourceMap = xform.MapUid;
        if (sourceMap is null)
            return;

        var globalPosition = _transform.GetWorldPosition(xform);

        // Try transmit message to 1 zlevel down
        if (_zLevels.TryMapDown(sourceMap.Value, out var belowMapUid) &&
            _mapQuery.TryComp(belowMapUid, out var belowMapComp))
        {
            TransmitMessageToZLevel(
                source,
                belowMapComp,
                globalPosition,
                message,
                Loc.GetString("stories-zlevel-voice-from-up", ("name", name)),
                chatType,
                speechVerb);
        }

        // Try transmit message to 1 zlevel up
        if (_zLevels.TryMapUp(sourceMap.Value, out var aboveMapUid) &&
            _mapQuery.TryComp(aboveMapUid, out var aboveMapComp))
        {
            TransmitMessageToZLevel(
                source,
                aboveMapComp,
                globalPosition,
                message,
                Loc.GetString("stories-zlevel-voice-from-down", ("name", name)),
                chatType,
                speechVerb);
        }
    }

    private void TransmitMessageToZLevel(
        EntityUid source,
        MapComponent mapComp,
        Vector2 position,
        string message,
        string nameOverride,
        InGameICChatType chatType,
        ProtoId<SpeechVerbPrototype>? speechVerb)
    {
        var targetPos = new MapCoordinates(position, mapComp.MapId);
        var transmit = Spawn(null, targetPos);
        EnsureComp<TimedDespawnComponent>(transmit).Lifetime = TransmitterLifetime;
        EnsureComp<ZLevelSpeechTransmitterComponent>(transmit);

        if (TryComp<LanguageComponent>(source, out var sourceLang))
        {
            EnsureComp<LanguageComponent>(transmit);
            foreach (var lang in sourceLang.SpokenLanguages)
            {
                _language.AddLanguage(transmit, lang, addSpoken: true, addUnderstood: false);
            }
            foreach (var lang in sourceLang.UnderstoodLanguages)
            {
                _language.AddLanguage(transmit, lang, addSpoken: false, addUnderstood: true);
            }
            if (sourceLang.CurrentLanguage is { } currentLang)
            {
                _language.SetLanguage(transmit, currentLang);
            }
        }

        if (HasComp<XenoComponent>(source))
            EnsureComp<XenoComponent>(transmit);

        if (HasComp<MarineComponent>(source))
            EnsureComp<MarineComponent>(transmit);

        if (HasComp<HunterComponent>(source))
            EnsureComp<HunterComponent>(transmit);

        if (speechVerb is not null)
        {
            var speech = EnsureComp<SpeechComponent>(transmit);
            speech.SpeechVerb = speechVerb.Value;
            speech.SuffixSpeechVerbs.Clear();
        }

        Timer.Spawn(MessageDelayMilliseconds,
            () =>
            {
                _chat.TrySendInGameICMessage(
                    transmit,
                    message,
                    chatType,
                    false,
                    nameOverride: nameOverride,
                    ignoreActionBlocker: true);
            });
    }
}
