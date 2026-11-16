using Content.Shared._Stories.Ordnance;
using Content.Shared._Stories.Ordnance.Assemblies;
using Content.Shared._Stories.Ordnance.Triggers;
using Content.Shared._RMC14.UserInterface;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Sticky.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;

namespace Content.Server._Stories.Ordnance.Triggers;

public sealed class OrdnanceTriggerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly GunIFFSystem _gunIff = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RMCUserInterfaceSystem _rmcUi = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private readonly HashSet<Entity<TransformComponent>> _proxEntities = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrdnanceTimerComponent, BoundUserInterfaceMessageAttempt>(OnTriggerUiAttempt);
        SubscribeLocalEvent<OrdnanceProxSensorComponent, BoundUserInterfaceMessageAttempt>(OnTriggerUiAttempt);
        SubscribeLocalEvent<OrdnanceSignallerComponent, BoundUserInterfaceMessageAttempt>(OnTriggerUiAttempt);
        SubscribeLocalEvent<OrdnanceAssemblyHolderComponent, BoundUserInterfaceMessageAttempt>(OnTriggerUiAttempt);

        SubscribeLocalEvent<OrdnanceAssemblyComponent, OrdnancePrimeEvent>(OnPrime);
        SubscribeLocalEvent<OrdnanceTimerComponent, OrdnanceTimerSetMessage>(OnTimerUiMessage);
        SubscribeLocalEvent<OrdnanceProxSensorComponent, OrdnanceProxSensorConfigMessage>(OnProxUiMessage);
        SubscribeLocalEvent<OrdnanceSignallerComponent, OrdnanceSignallerUpdateMessage>(OnSignallerUpdateUiMessage);
        SubscribeLocalEvent<OrdnanceSignallerComponent, OrdnanceSignallerTriggerMessage>(OnSignallerTriggerUiMessage);
        SubscribeLocalEvent<OrdnanceIgniterComponent, OrdnancePulseEvent>(OnIgniterPulse);

        SubscribeLocalEvent<OrdnanceTimerComponent, UseInHandEvent>(OnTimerUseInHand);
        SubscribeLocalEvent<OrdnanceProxSensorComponent, UseInHandEvent>(OnProxSensorUseInHand);
        SubscribeLocalEvent<OrdnanceSignallerComponent, UseInHandEvent>(OnSignallerUseInHand);

        SubscribeLocalEvent<OrdnanceAssemblyHolderComponent, OrdnanceTimerSetMessage>(OnHolderTimerUiMessage);
        SubscribeLocalEvent<OrdnanceAssemblyHolderComponent, OrdnanceProxSensorConfigMessage>(OnHolderProxUiMessage);
        SubscribeLocalEvent<OrdnanceAssemblyHolderComponent, OrdnanceSignallerUpdateMessage>(OnHolderSignallerUpdateUiMessage);
        SubscribeLocalEvent<OrdnanceAssemblyHolderComponent, OrdnanceSignallerTriggerMessage>(OnHolderSignallerTriggerUiMessage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var timerQuery = EntityQueryEnumerator<OrdnanceTimerComponent, OrdnanceAssemblyComponent>();
        while (timerQuery.MoveNext(out var uid, out var timer, out var assembly))
        {
            if (!timer.Enabled || assembly.Holder is not { } holderUid) continue;

            timer.TimeRemaining -= frameTime;
            if (timer.TimeRemaining <= 0)
            {
                TriggerPulse(uid, holderUid, timer.Primer);
                timer.Enabled = false;
                timer.TimeRemaining = timer.SelectedTime;
                Dirty(uid, timer);
            }
        }

        var proxQuery = EntityQueryEnumerator<OrdnanceProxSensorComponent, OrdnanceAssemblyComponent>();
        while (proxQuery.MoveNext(out var uid, out var prox, out var assembly))
        {
            if (prox.Enabled && !prox.Armed)
            {
                prox.ArmingTimeRemaining -= frameTime;
                if (prox.ArmingTimeRemaining <= 0)
                {
                    prox.Armed = true;
                    prox.TriggerDelayRemaining = 0;
                    Dirty(uid, prox);
                }
            }

            if (prox.Armed && prox.TriggerDelayRemaining > 0)
            {
                prox.TriggerDelayRemaining -= frameTime;
                if (prox.TriggerDelayRemaining <= 0)
                {
                    if (assembly.Holder is { } holderDelay)
                        TriggerPulse(uid, holderDelay, prox.Primer);
                    prox.Armed = false;
                    prox.ArmingTimeRemaining = prox.ArmingTime;
                    prox.TriggerDelayRemaining = 0;
                    Dirty(uid, prox);
                }
                continue;
            }

            if (prox.Armed && prox.TriggerDelayRemaining <= 0 && assembly.Holder is { } holder)
            {
                var mapCoords = _transform.GetMapCoordinates(holder);
                var range = prox.Range;

                _proxEntities.Clear();
                _lookup.GetEntitiesInRange(mapCoords, range, _proxEntities);

                foreach (var other in _proxEntities)
                {
                    if (other.Owner == holder || other.Owner == prox.Primer) continue;

                    if (!HasComp<MobStateComponent>(other.Owner) || _mobState.IsDead(other.Owner))
                        continue;

                    if (prox.Primer is { } primer && _gunIff.TryGetFaction(primer, out var faction))
                    {
                        if (_gunIff.IsInFaction(other.Owner, faction))
                            continue;
                    }

                    float delay = prox.Delay;
                    if (TryComp<OrdnanceCasingComponent>(_transform.GetParentUid(holder), out var casing) && casing.UseDirection)
                        delay = 0;

                    if (delay <= 0)
                    {
                        TriggerPulse(uid, holder, prox.Primer);
                        prox.Armed = false;
                        prox.ArmingTimeRemaining = prox.ArmingTime;
                        Dirty(uid, prox);
                    }
                    else
                    {
                        prox.TriggerDelayRemaining = delay;
                        Dirty(uid, prox);
                    }
                    break;
                }
            }
        }
    }

    private void OnPrime(Entity<OrdnanceAssemblyComponent> ent, ref OrdnancePrimeEvent args)
    {
        if (TryComp<OrdnanceTimerComponent>(ent, out var timer))
        {
            timer.Enabled = true;
            timer.Primer = args.User;
            timer.TimeRemaining = timer.SelectedTime;
            Dirty(ent.Owner, timer);

            bool isC4 = false;
            if (ent.Comp.Holder is { } holderUid && TryComp<TransformComponent>(holderUid, out var xform))
            {
                if (HasComp<StickyComponent>(xform.ParentUid))
                    isC4 = true;
            }

            if (!isC4)
            {
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Explosion/armbomb.ogg") { Params = AudioParams.Default.WithVolume(-5f) }, ent.Owner);
            }
        }
        else if (TryComp<OrdnanceProxSensorComponent>(ent, out var prox))
        {
            prox.Enabled = true;
            prox.Primer = args.User;
            prox.ArmingTimeRemaining = prox.ArmingTime;
            prox.TriggerDelayRemaining = 0;
            Dirty(ent.Owner, prox);
        }
    }

    private void TriggerPulse(EntityUid source, EntityUid holder, EntityUid? user)
    {
        var ev = new OrdnancePulseEvent(source, user);
        RaiseLocalEvent(holder, ref ev);
    }

    private void OnTriggerUiAttempt(EntityUid uid, Component comp, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.Cancelled)
            return;

        if (!_rmcUi.CanUseUI(args))
            args.Cancel();
    }

    private void OnTimerUiMessage(Entity<OrdnanceTimerComponent> ent, ref OrdnanceTimerSetMessage args)
    {
        ent.Comp.SelectedTime = Math.Clamp(args.Time, 3f, 120f);
        ent.Comp.TimeRemaining = ent.Comp.SelectedTime;
        Dirty(ent);
    }

    private void OnProxUiMessage(Entity<OrdnanceProxSensorComponent> ent, ref OrdnanceProxSensorConfigMessage args)
    {
        ent.Comp.ArmingTime = Math.Clamp(args.ArmTime, 2f, 120f);
        ent.Comp.Range = Math.Clamp(args.Range, 1f, 5f);
        ent.Comp.Delay = Math.Clamp(args.Delay, 1f, 10f);
        Dirty(ent);
    }

    private void OnSignallerUpdateUiMessage(Entity<OrdnanceSignallerComponent> ent, ref OrdnanceSignallerUpdateMessage args)
    {
        ent.Comp.Frequency = args.Frequency;
        ent.Comp.Code = args.Code;
        Dirty(ent);
    }

    private void OnSignallerTriggerUiMessage(Entity<OrdnanceSignallerComponent> ent, ref OrdnanceSignallerTriggerMessage args)
    {
        SendSignal(ent.Comp.Frequency, ent.Comp.Code, args.Actor);
    }

    private void SendSignal(float freq, int code, EntityUid? user)
    {
        var query = EntityQueryEnumerator<OrdnanceSignallerComponent, OrdnanceAssemblyComponent>();
        while (query.MoveNext(out var uid, out var sig, out var asm))
        {
            if (MathHelper.CloseTo(sig.Frequency, freq) && sig.Code == code)
            {
                if (asm.Holder is { } holder)
                    TriggerPulse(uid, holder, user);
            }
        }
    }

    private void OnIgniterPulse(Entity<OrdnanceIgniterComponent> ent, ref OrdnancePulseEvent args)
    {
        if (TryComp<OrdnanceAssemblyComponent>(ent, out var asm) && asm.Holder is { } holder)
        {
            if (TryComp<TransformComponent>(holder, out var holderXform) && holderXform.ParentUid.IsValid())
            {
                var detonateEv = new OrdnancePulseEvent(ent.Owner, args.User);
                RaiseLocalEvent(holderXform.ParentUid, ref detonateEv);
            }
        }
    }

    private void OnTimerUseInHand(Entity<OrdnanceTimerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled) return;
        _ui.TryToggleUi(ent.Owner, OrdnanceTimerUiKey.Key, args.User);
        args.Handled = true;
    }

    private void OnProxSensorUseInHand(Entity<OrdnanceProxSensorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled) return;
        _ui.TryToggleUi(ent.Owner, OrdnanceProxSensorUiKey.Key, args.User);
        args.Handled = true;
    }

    private void OnSignallerUseInHand(Entity<OrdnanceSignallerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled) return;
        _ui.TryToggleUi(ent.Owner, OrdnanceSignallerUiKey.Key, args.User);
        args.Handled = true;
    }

    private void OnHolderTimerUiMessage(Entity<OrdnanceAssemblyHolderComponent> ent, ref OrdnanceTimerSetMessage args)
    {
        if (ent.Comp.Part1 is { } part1 && TryComp<OrdnanceTimerComponent>(part1, out var timer1))
        {
            timer1.SelectedTime = Math.Clamp(args.Time, 3f, 120f);
            timer1.TimeRemaining = timer1.SelectedTime;
            Dirty(part1, timer1);
        }
        if (ent.Comp.Part2 is { } part2 && TryComp<OrdnanceTimerComponent>(part2, out var timer2))
        {
            timer2.SelectedTime = Math.Clamp(args.Time, 3f, 120f);
            timer2.TimeRemaining = timer2.SelectedTime;
            Dirty(part2, timer2);
        }
    }

    private void OnHolderProxUiMessage(Entity<OrdnanceAssemblyHolderComponent> ent, ref OrdnanceProxSensorConfigMessage args)
    {
        if (ent.Comp.Part1 is { } part1 && TryComp<OrdnanceProxSensorComponent>(part1, out var prox1))
        {
            prox1.ArmingTime = Math.Clamp(args.ArmTime, 2f, 120f);
            prox1.Range = Math.Clamp(args.Range, 1f, 5f);
            prox1.Delay = Math.Clamp(args.Delay, 1f, 10f);
            Dirty(part1, prox1);
        }
        if (ent.Comp.Part2 is { } part2 && TryComp<OrdnanceProxSensorComponent>(part2, out var prox2))
        {
            prox2.ArmingTime = Math.Clamp(args.ArmTime, 2f, 120f);
            prox2.Range = Math.Clamp(args.Range, 1f, 5f);
            prox2.Delay = Math.Clamp(args.Delay, 1f, 10f);
            Dirty(part2, prox2);
        }
    }

    private void OnHolderSignallerUpdateUiMessage(Entity<OrdnanceAssemblyHolderComponent> ent, ref OrdnanceSignallerUpdateMessage args)
    {
        if (ent.Comp.Part1 is { } part1 && TryComp<OrdnanceSignallerComponent>(part1, out var sig1))
        {
            sig1.Frequency = args.Frequency;
            sig1.Code = args.Code;
            Dirty(part1, sig1);
        }
        if (ent.Comp.Part2 is { } part2 && TryComp<OrdnanceSignallerComponent>(part2, out var sig2))
        {
            sig2.Frequency = args.Frequency;
            sig2.Code = args.Code;
            Dirty(part2, sig2);
        }
    }

    private void OnHolderSignallerTriggerUiMessage(Entity<OrdnanceAssemblyHolderComponent> ent, ref OrdnanceSignallerTriggerMessage args)
    {
        if (ent.Comp.Part1 is { } part1 && TryComp<OrdnanceSignallerComponent>(part1, out var sig1))
        {
            SendSignal(sig1.Frequency, sig1.Code, args.Actor);
        }
        else if (ent.Comp.Part2 is { } part2 && TryComp<OrdnanceSignallerComponent>(part2, out var sig2))
        {
            SendSignal(sig2.Frequency, sig2.Code, args.Actor);
        }
    }
}
