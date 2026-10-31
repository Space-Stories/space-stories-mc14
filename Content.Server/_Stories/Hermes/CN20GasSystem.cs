using Content.Server.Chat.Systems;
using Content.Shared._RMC14.BlurredVision;
using Content.Shared._RMC14.Deafness;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Hermes;
using Content.Shared.Smoking;
using Content.Shared.Damage;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stories.Hermes;

public sealed class CN20GasSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedDeafnessSystem _deafness = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private static readonly ProtoId<TagPrototype> GasMask = "GasMask";
    private static readonly ProtoId<StatusEffectPrototype> Blinded = "Blinded";
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<CN20GasComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<CN20GasComponent, EndCollideEvent>(OnEndCollide);
        SubscribeLocalEvent<CN20GasComponent, ComponentStartup>(OnGasStartup);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnGasStartup(Entity<CN20GasComponent> ent, ref ComponentStartup args)
        => _nextUpdate = TimeSpan.Zero;

    private void OnRoundRestart(RoundRestartCleanupEvent args) => _nextUpdate = TimeSpan.Zero;

    private void OnStartCollide(Entity<CN20GasComponent> ent, ref StartCollideEvent args)
    {
        if (!HasComp<MobStateComponent>(args.OtherEntity))
            return;
        ent.Comp.Touching.Add(args.OtherEntity);
        Affect(ent, args.OtherEntity);
        ent.Comp.NextTick = _timing.CurTime;
        _nextUpdate = _timing.CurTime;
    }

    private void OnEndCollide(Entity<CN20GasComponent> ent, ref EndCollideEvent args)
        => ent.Comp.Touching.Remove(args.OtherEntity);

    public bool IsProtected(EntityUid target)
    {
        if (HasComp<SynthComponent>(target))
            return true;
        if (_inventory.TryGetSlotEntity(target, "mask", out var mask) && mask is { } maskUid && _tags.HasTag(maskUid, GasMask))
            return true;
        return _inventory.TryGetSlotEntity(target, "head", out var head) && head is { } headUid && _tags.HasTag(headUid, GasMask);
    }

    public void Affect(Entity<CN20GasComponent> gas, EntityUid target)
    {
        if (TerminatingOrDeleted(target) || !HasComp<MobStateComponent>(target) ||
            _mobState.IsDead(target) || IsProtected(target) ||
            HasComp<XenoComponent>(target) && !gas.Comp.AffectsXenos)
            return;
        var attempt = new SmokeReactionAttemptEvent(target);
        RaiseLocalEvent(target, ref attempt);
        if (attempt.Cancelled || gas.Comp.Resistant != null &&
            _whitelist.IsWhitelistPass(gas.Comp.Resistant, target) && _random.Prob(gas.Comp.ResistanceChance))
            return;
        var exposure = EnsureComp<CN20ExposureComponent>(target);
        if (_timing.CurTime < exposure.NextEffect)
            return;
        exposure.NextEffect = _timing.CurTime + gas.Comp.Interval;
        var xeno = HasComp<XenoComponent>(target);
        if (!xeno)
        {
            _damage.TryChangeDamage(target, gas.Comp.OxygenDamage, true, origin: gas.Owner);
            _chat.TryEmoteWithChat(target, "Cough");
        }
        _status.TryAddStatusEffect<RMCBlindedComponent>(target, Blinded, gas.Comp.BlindDuration, true);
        _deafness.TryDeafen(target, gas.Comp.DeafDuration, true, ignoreProtection: true);
        _slow.TrySuperSlowdown(target, gas.Comp.SlowDuration);
        if (_random.Prob(gas.Comp.KnockdownChance))
            _stun.TryKnockdown(target, gas.Comp.KnockdownDuration, true);
        if (_random.Prob(gas.Comp.DropChance) && TryComp(target, out HandsComponent? hands))
        {
            foreach (var hand in hands.SortedHands)
                _hands.TryDrop(target, hand, null, false);
        }
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = TimeSpan.MaxValue;
        var gasQuery = EntityQueryEnumerator<CN20GasComponent>();
        while (gasQuery.MoveNext(out var uid, out var gas))
        {
            if (_timing.CurTime >= gas.NextTick)
            {
                gas.NextTick = _timing.CurTime + gas.Interval;
                gas.Touching.RemoveWhere(target => TerminatingOrDeleted(target));
                foreach (var target in gas.Touching)
                {
                    Affect((uid, gas), target);
                    // Entry effects can fall between cloud ticks; resume when that mob's cooldown ends.
                    if (TryComp<CN20ExposureComponent>(target, out var exposure) &&
                        exposure.NextEffect > _timing.CurTime && exposure.NextEffect < gas.NextTick)
                        gas.NextTick = exposure.NextEffect;
                }
            }
            if (gas.NextTick < _nextUpdate)
                _nextUpdate = gas.NextTick;
        }
        var exposures = EntityQueryEnumerator<CN20ExposureComponent>();
        while (exposures.MoveNext(out var uid, out var exposure))
        {
            if (_timing.CurTime >= exposure.NextEffect)
                RemCompDeferred<CN20ExposureComponent>(uid);
            else if (exposure.NextEffect < _nextUpdate)
                _nextUpdate = exposure.NextEffect;
        }
    }
}
