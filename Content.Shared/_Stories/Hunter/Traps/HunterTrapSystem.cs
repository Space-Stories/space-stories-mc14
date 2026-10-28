using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.Tether;
using Content.Shared._Stories.Xenonids.HiveInterference;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Hunter.Traps;

public sealed class HunterTrapSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly RMCDazedSystem _dazed = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedHiveInterferenceSystem _hiveInterference = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly StepTriggerSystem _stepTrigger = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StoriesTetherSystem _tether = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterTrapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HunterTrapComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<HunterTrapComponent, HunterTrapArmDoAfterEvent>(OnArmDoAfter);
        SubscribeLocalEvent<HunterTrapComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<HunterTrapComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<HunterTrapComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<HunterTrapComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<HunterTrapComponent, StoriesTetherReleasedEvent>(OnTetherReleased);
        SubscribeLocalEvent<HunterTrapComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<HunterTrapComponent, ComponentShutdown>(OnTrapShutdown);

        SubscribeLocalEvent<HunterSnareProjectileComponent, ProjectileHitEvent>(OnSnareHit);

        SubscribeLocalEvent<HunterHealBlockedComponent, XenoHealAttemptEvent>(OnHealAttempt);
    }

    private void OnMapInit(Entity<HunterTrapComponent> ent, ref MapInitEvent args)
    {
        SetStepTriggerActive(ent, ent.Comp.Armed);
        UpdateAppearance(ent);
    }

    private void OnUseInHand(Entity<HunterTrapComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !ent.Comp.Armable || ent.Comp.Armed || ent.Comp.Victim != null)
            return;

        args.Handled = true;

        if (!HasTrapSkill(ent, args.User))
        {
            _popup.PopupClient(Loc.GetString("stories-hunter-trap-no-skill"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.ArmDelay, new HunterTrapArmDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDropItem = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnArmDoAfter(Entity<HunterTrapComponent> ent, ref HunterTrapArmDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Armed)
            return;

        args.Handled = true;

        var user = args.User;
        if (!_hands.TryDrop(user, ent.Owner))
            return;

        var userXform = Transform(user);
        _transform.SetCoordinates(ent, userXform.Coordinates.SnapToGrid(EntityManager));
        _transform.AnchorEntity(ent);

        ent.Comp.Armed = true;
        Dirty(ent);

        SetStepTriggerActive(ent, true);
        UpdateAppearance(ent);
        _popup.PopupClient(Loc.GetString("stories-hunter-trap-armed"), user, user);
    }

    private void OnInteractHand(Entity<HunterTrapComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        if (ent.Comp.Victim == user)
            return;

        if (ent.Comp.Victim != null)
        {
            if (!HasTrapSkill(ent, user))
                return;

            args.Handled = true;
            Disarm(ent);
            _popup.PopupClient(Loc.GetString("stories-hunter-trap-disarmed"), user, user);
            return;
        }

        if (!ent.Comp.Armed)
            return;

        args.Handled = true;

        if (HasTrapSkill(ent, user))
        {
            Disarm(ent);
            _popup.PopupClient(Loc.GetString("stories-hunter-trap-disarmed"), user, user);
            return;
        }

        if (!CanCatch(user))
            return;

        _popup.PopupPredicted(Loc.GetString("stories-hunter-trap-foolish-touch"), user, user, PopupType.MediumCaution);
        Catch(ent, user);
    }

    private void OnPickupAttempt(Entity<HunterTrapComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (ent.Comp.Armed || ent.Comp.Victim != null)
            args.Cancel();
    }

    private void OnStepTriggerAttempt(Entity<HunterTrapComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (!ent.Comp.Armed || !TryComp(args.Tripper, out MobStateComponent? _) || HasComp<HunterComponent>(args.Tripper))
            return;

        args.Continue = true;
    }

    private void OnStepTriggered(Entity<HunterTrapComponent> ent, ref StepTriggeredOffEvent args)
    {
        if (!ent.Comp.Armed)
            return;

        var tripper = args.Tripper;
        if (CanCatch(tripper))
        {
            Catch(ent, tripper);
            _popup.PopupPredicted(
                Loc.GetString("stories-hunter-trap-caught-self", ("trap", ent)),
                Loc.GetString("stories-hunter-trap-caught-others", ("victim", tripper), ("trap", ent)),
                tripper,
                tripper,
                PopupType.MediumCaution);
            return;
        }

        if (ent.Comp.AnimalDamage is { } damage)
            _damageable.TryChangeDamage(tripper, damage, origin: ent);

        Disarm(ent);
    }

    private void OnTetherReleased(Entity<HunterTrapComponent> ent, ref StoriesTetherReleasedEvent args)
    {
        if (ent.Comp.Victim == args.Tethered)
            Disarm(ent);
    }

    private void OnGetVerbs(Entity<HunterTrapComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.Configurable || !HasTrapSkill(ent, args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("stories-hunter-trap-verb-range", ("range", ent.Comp.Range)),
            Act = () =>
            {
                var range = ent.Comp.Range + 1;
                if (range > ent.Comp.MaxRange)
                    range = ent.Comp.MinRange;

                ent.Comp.Range = range;
                Dirty(ent);
                _popup.PopupClient(Loc.GetString("stories-hunter-trap-range-set", ("range", range)), user, user);
            },
        });
    }

    private void OnTrapShutdown(Entity<HunterTrapComponent> ent, ref ComponentShutdown args)
    {
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.Victim is { } victim)
            ClearVictim(ent, victim);
    }

    private void OnSnareHit(Entity<HunterSnareProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (_net.IsClient)
            return;

        var target = args.Target;
        var canCatch = CanCatch(target);
        var snare = Spawn(ent.Comp.Deployed, _transform.GetMapCoordinates(canCatch ? target : ent.Owner));

        if (!canCatch || !TryComp(snare, out HunterTrapComponent? trap))
            return;

        _stun.TryStun(target, ent.Comp.StunTime, true);
        _dazed.TryDaze(target, ent.Comp.DazeTime);

        if (args.Shooter is { } shooter && !_standing.IsDown(target))
        {
            var from = _transform.GetMapCoordinates(shooter);
            var to = _transform.GetMapCoordinates(target);
            if (from.MapId == to.MapId && (to.Position - from.Position).Length() <= ent.Comp.PushMaxRange)
                _sizeStun.KnockBack(target, from, 1f, 1f);
        }

        Catch((snare, trap), target);
        _popup.PopupEntity(Loc.GetString("stories-hunter-trap-caught-self", ("trap", snare)), target, target, PopupType.MediumCaution);
    }

    private void OnHealAttempt(Entity<HunterHealBlockedComponent> ent, ref XenoHealAttemptEvent args)
    {
        args.Cancelled = true;
    }

    public void Catch(Entity<HunterTrapComponent> trap, EntityUid victim)
    {
        if (trap.Comp.Victim != null)
            return;

        var xform = Transform(trap);
        if (!xform.Anchored)
            _transform.AnchorEntity(trap);

        trap.Comp.Armed = false;
        trap.Comp.Victim = victim;
        trap.Comp.ReleaseAt = _timing.CurTime + trap.Comp.Duration;
        EnsureComp<HunterTrapActiveComponent>(trap);
        Dirty(trap);

        SetStepTriggerActive(trap, false);
        UpdateAppearance(trap);

        _tether.Tether(victim, trap, trap.Comp.Range, trap.Comp.TetherVisualState);

        if (HasComp<XenoComponent>(victim))
        {
            _hiveInterference.Apply(victim, trap.Comp.Interference, trap.Comp.Interference);
            var blocked = EnsureComp<HunterHealBlockedComponent>(victim);
            blocked.Sources.Add(trap);
            Dirty(victim, blocked);
        }

        if (_net.IsServer)
            _audio.PlayPvs(trap.Comp.CatchSound, trap);

        var ev = new HunterTrapCaughtEvent(trap, victim);
        RaiseLocalEvent(trap, ref ev);
    }

    public void Disarm(Entity<HunterTrapComponent> trap)
    {
        var victim = trap.Comp.Victim;

        trap.Comp.Armed = false;
        trap.Comp.Victim = null;
        trap.Comp.ReleaseAt = null;
        RemComp<HunterTrapActiveComponent>(trap);
        Dirty(trap);

        SetStepTriggerActive(trap, false);
        UpdateAppearance(trap);

        if (Transform(trap).Anchored)
            _transform.Unanchor(trap);

        if (victim != null)
            ClearVictim(trap, victim.Value);
    }

    private void ClearVictim(EntityUid trap, EntityUid victim)
    {
        if (_tether.IsTetheredTo(victim, trap))
            _tether.Release(victim);

        if (!TryComp(victim, out HunterHealBlockedComponent? blocked))
            return;

        blocked.Sources.Remove(trap);
        if (blocked.Sources.Count == 0)
            RemComp<HunterHealBlockedComponent>(victim);
        else
            Dirty(victim, blocked);
    }

    private void SetStepTriggerActive(EntityUid trap, bool active)
    {
        if (TryComp(trap, out StepTriggerComponent? stepTrigger))
            _stepTrigger.SetActive(trap, active, stepTrigger);
    }

    private bool CanCatch(EntityUid target)
    {
        if (HasComp<HunterComponent>(target))
            return false;

        return HasComp<XenoComponent>(target) || HasComp<HumanoidAppearanceComponent>(target);
    }

    private bool HasTrapSkill(Entity<HunterTrapComponent> trap, EntityUid user)
    {
        return _skills.HasSkill(user, trap.Comp.Skill, 1);
    }

    private void UpdateAppearance(Entity<HunterTrapComponent> ent)
    {
        var state = HunterTrapVisualState.Disarmed;
        if (ent.Comp.Victim != null)
        {
            state = HunterTrapVisualState.Caught;
        }
        else if (ent.Comp.Armed)
        {
            state = GetCamouflage(ent);
        }

        _appearance.SetData(ent, HunterTrapVisuals.State, state);
    }

    private HunterTrapVisualState GetCamouflage(EntityUid trap)
    {
        var xform = Transform(trap);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return HunterTrapVisualState.Armed;

        var tile = _map.GetTileRef(gridUid, grid, xform.Coordinates);
        var id = _tileDefinitions[tile.Tile.TypeId].ID;

        if (id.Contains("grass", StringComparison.OrdinalIgnoreCase))
            return HunterTrapVisualState.Grass;

        if (id.Contains("dirt", StringComparison.OrdinalIgnoreCase))
            return HunterTrapVisualState.Dirt;

        return HunterTrapVisualState.Armed;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<HunterTrapActiveComponent, HunterTrapComponent>();
        while (query.MoveNext(out var uid, out _, out var trap))
        {
            if (trap.ReleaseAt is { } releaseAt && time >= releaseAt)
                Disarm((uid, trap));
        }
    }
}
