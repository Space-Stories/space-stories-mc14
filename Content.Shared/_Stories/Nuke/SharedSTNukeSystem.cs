using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Camera;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.UserInterface;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Access.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Coordinates;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Nuke;

public sealed class SharedSTNukeSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly AreaSystem _area = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private readonly RMCUserInterfaceSystem _rmcUi = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedToolSystem _tools = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedXenoAnnounceSystem _xenoAnnounce = default!;

    private EntityQuery<CommunicationsTowerComponent> _towerQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _towerQuery = GetEntityQuery<CommunicationsTowerComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<STNukeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<STNukeComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
        SubscribeLocalEvent<STNukeComponent, BoundUserInterfaceMessageAttempt>(OnNukeMessageAttempt);
        SubscribeLocalEvent<STNukeComponent, BeforeActivatableUIOpenEvent>(OnBeforeUI);
        SubscribeLocalEvent<STNukeComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<STNukeComponent, InteractHandEvent>(OnInteractHand);

        SubscribeLocalEvent<STNukeComponent, STNukeDefuseDoAfterEvent>(OnDefuseComplete);
        SubscribeLocalEvent<STNukeComponent, STNukeAnchorDoAfterEvent>(OnAnchorDoAfterComplete);
        SubscribeLocalEvent<STNukeComponent, STNukeSafetyDoAfterEvent>(OnSafetyDoAfterComplete);
        SubscribeLocalEvent<STNukeComponent, STNukeEncryptionDoAfterEvent>(OnEncryptionDoAfterComplete);
        SubscribeLocalEvent<STNukeComponent, STNukeXenoResinDoAfterEvent>(OnXenoResinDoAfterComplete);

        Subs.BuiEvents<STNukeComponent>(STNukeUiKey.Key, subs =>
        {
            subs.Event<STNukeToggleAnchorMessage>(OnAnchorButtonPressed);
            subs.Event<STNukeToggleSafetyMessage>(OnSafetyButtonPressed);
            subs.Event<STNukeToggleCommandLockoutMessage>(OnCommandLockoutPressed);
            subs.Event<STNukeToggleEncryptionMessage>(OnToggleEncryption);
            subs.Event<STNukeToggleMessage>(OnToggleNuke);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<STNukeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = new Entity<STNukeComponent>(uid, comp);
            var curTime = _timing.CurTime;

            if (comp.Decryption && comp.DecryptionOn.HasValue)
            {
                UpdateDecryption(ent, curTime);
                UpdateTowerCheck(ent, curTime);
            }

            if (comp.Active && comp.ExplodeOn.HasValue)
            {
                UpdateExplosion(ent, curTime);
            }

            if (comp.ExplodeStage1At.HasValue && !comp.ExplodeSoundPlayed && curTime >= comp.ExplodeStage1At.Value)
            {
                _audio.PlayGlobal(ent.Comp.NukeSound, Filter.Broadcast(), true);
                comp.ExplodeSoundPlayed = true;
                Dirty(ent);
                Spawn(comp.Explosion, uid.ToCoordinates());
            }

            if (comp.ExplodeStage2At.HasValue && comp.ExplodeSoundPlayed && !comp.Nuked && curTime >= comp.ExplodeStage2At.Value)
            {
                PerformNuke(ent);
                comp.Nuked = true;
                PredictedQueueDel(ent.Owner);
            }
        }
    }

    private void OnMapInit(Entity<STNukeComponent> ent, ref MapInitEvent args)
    {
        LinkTowers(ent);
        UpdateUserInterface(ent);
    }

    private void OnUIOpenAttempt(Entity<STNukeComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.CommandLockout && !_access.IsAllowed(args.User, ent.Owner))
        {
            _popup.PopupCursor(Loc.GetString("rmc-access-denied"), args.User);
            args.Cancel();
        }
    }

    private void OnNukeMessageAttempt(Entity<STNukeComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.Cancelled)
            return;

        if (!_rmcUi.CanUseUI(args))
        {
            args.Cancel();
            return;
        }

        if (ent.Comp.CommandLockout && !_access.IsAllowed(args.Actor, ent.Owner))
        {
            _popup.PopupCursor(Loc.GetString("rmc-access-denied"), args.Actor);
            args.Cancel();
        }
    }

    private void OnBeforeUI(Entity<STNukeComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateUserInterface(ent);
    }

    private void OnInteractUsing(Entity<STNukeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.Active || !ent.Comp.ExplodeOn.HasValue)
            return;

        if (!_tools.HasQuality(args.Used, "Cutting"))
            return;

        args.Handled = true;
        _popup.PopupPredicted(Loc.GetString("st-nuke-defusing"), ent, args.User, PopupType.Medium);

        var delay = TimeSpan.FromSeconds(15) * _skills.GetSkillDelayMultiplier(args.User, ent.Comp.DefuseSkill);

        _tools.UseTool(args.Used, args.User, ent, (float)delay.TotalSeconds, new[] { "Cutting" }, new STNukeDefuseDoAfterEvent());
    }

    private void OnInteractHand(Entity<STNukeComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !HasComp<XenoEvolutionGranterComponent>(args.User))
            return;

        if (!ent.Comp.Decryption || !ent.Comp.DecryptionOn.HasValue)
            return;

        args.Handled = true;
        _popup.PopupPredicted(Loc.GetString("st-nuke-xeno-resin-start", ("user", args.User)), ent, null, PopupType.Medium);
        _popup.PopupPredicted(Loc.GetString("st-nuke-xeno-resin-user"), ent, args.User, PopupType.MediumCaution);

        var ev = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(5), new STNukeXenoResinDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };
        _doAfter.TryStartDoAfter(ev);
    }

    private void OnXenoResinDoAfterComplete(Entity<STNukeComponent> ent, ref STNukeXenoResinDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        ResetDecryption(ent);
        _popup.PopupPredicted(Loc.GetString("st-nuke-xeno-resin-complete"), ent, null, PopupType.Large);
        AnnounceGlobal(ent, "st-nuke-decryption-halted-marine", "st-nuke-decryption-halted-xeno");
        UpdateUserInterface(ent);
    }

    private void OnDefuseComplete(Entity<STNukeComponent> ent, ref STNukeDefuseDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        Disable(ent);
        _popup.PopupPredicted(Loc.GetString("st-nuke-defused"), ent, null, PopupType.Large);
    }

    private void OnAnchorDoAfterComplete(Entity<STNukeComponent> ent, ref STNukeAnchorDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            UpdateUserInterface(ent);
            return;
        }

        var xform = Transform(ent);
        if (xform.Anchored)
        {
            _transform.Unanchor(ent, xform);
            _popup.PopupPredicted(Loc.GetString("st-nuke-unanchored"), ent, args.User, PopupType.Medium);
        }
        else
        {
            if (!_area.CanBuildSpecial(xform.Coordinates))
            {
                _popup.PopupPredictedCursor(Loc.GetString("st-nuke-cannot-deploy-here"), args.User);
                UpdateUserInterface(ent);
                return;
            }

            _transform.SetCoordinates(ent, xform, xform.Coordinates.SnapToGrid());
            _transform.AnchorEntity(ent, xform);
            _popup.PopupPredicted(Loc.GetString("st-nuke-anchored"), ent, args.User, PopupType.Medium);
        }

        LinkTowers(ent);
        Dirty(ent);
        UpdateUserInterface(ent);
    }

    private void OnSafetyDoAfterComplete(Entity<STNukeComponent> ent, ref STNukeSafetyDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            UpdateUserInterface(ent);
            return;
        }

        ent.Comp.Safety = !ent.Comp.Safety;
        _popup.PopupPredicted(
            ent.Comp.Safety ? Loc.GetString("st-nuke-safety-enabled") : Loc.GetString("st-nuke-safety-disabled"),
            ent,
            args.User,
            PopupType.Medium
        );

        if (ent.Comp.Safety)
        {
            Disable(ent);
        }

        Dirty(ent);
        UpdateUserInterface(ent);
    }

    private void OnEncryptionDoAfterComplete(Entity<STNukeComponent> ent, ref STNukeEncryptionDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            UpdateUserInterface(ent);
            return;
        }

        ent.Comp.Decryption = !ent.Comp.Decryption;
        if (ent.Comp.Decryption)
        {
            ent.Comp.DecryptionOn = _timing.CurTime + ent.Comp.DecryptionTime;
            ent.Comp.AnnouncedHalfway = false;
            ent.Comp.AnnouncedOneMinute = false;
            ent.Comp.TowersWereOffline = false;

            var areaName = "Unknown";
            if (_area.TryGetArea(ent, out _, out var areaProto))
                areaName = areaProto.Name;

            AnnounceGlobal(ent,
                "st-nuke-decryption-started-marine",
                "st-nuke-decryption-started-xeno",
                marineArgs: new (string, object)[] { ("time", FormatTime(ent.Comp.DecryptionTime)) },
                xenoArgs: new (string, object)[] { ("area", areaName), ("time", FormatTime(ent.Comp.DecryptionTime)) }
            );
        }
        else
        {
            if (ent.Comp.DecryptionOn.HasValue)
            {
                var remaining = ent.Comp.DecryptionOn.Value - _timing.CurTime;
                var newTime = remaining + ent.Comp.PenaltionTime;
                ent.Comp.DecryptionTime = newTime > TimeSpan.FromMinutes(10) ? TimeSpan.FromMinutes(10) : newTime;
            }
            ent.Comp.DecryptionOn = null;
            ent.Comp.TowersWereOffline = false;
            AnnounceGlobal(ent, "st-nuke-decryption-halted-marine", "st-nuke-decryption-halted-xeno");
        }

        Dirty(ent);
        UpdateUserInterface(ent);
    }

    private void OnAnchorButtonPressed(Entity<STNukeComponent> ent, ref STNukeToggleAnchorMessage args)
    {
        if (_net.IsClient) return;

        if (ent.Comp.Active)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-disengage-first"), args.Actor);
            return;
        }
        if (ent.Comp.Decryption)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-stop-decrypting"), args.Actor);
            return;
        }
        if (!_area.CanBuildSpecial(ent.Owner.ToCoordinates()) || !_rmcPlanet.IsOnPlanet(ent.Owner.ToCoordinates()))
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-cannot-deploy-here"), args.Actor);
            UpdateUserInterface(ent);
            return;
        }

        StartDoAfter(ent, args.Actor, new STNukeAnchorDoAfterEvent());
    }

    private void OnSafetyButtonPressed(Entity<STNukeComponent> ent, ref STNukeToggleSafetyMessage args)
    {
        if (_net.IsClient) return;

        if (ent.Comp.Active)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-disengage-first"), args.Actor);
            return;
        }
        if (!_area.CanBuildSpecial(Transform(ent).Coordinates))
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-cannot-deploy-here"), args.Actor);
            return;
        }
        if (ent.Comp.Decryption)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-stop-decrypting"), args.Actor);
            return;
        }

        StartDoAfter(ent, args.Actor, new STNukeSafetyDoAfterEvent());
    }

    private void OnCommandLockoutPressed(Entity<STNukeComponent> ent, ref STNukeToggleCommandLockoutMessage args)
    {
        if (_net.IsClient) return;

        if (!_access.IsAllowed(args.Actor, ent.Owner))
        {
            _popup.PopupCursor(Loc.GetString("rmc-access-denied"), args.Actor);
            return;
        }

        ent.Comp.CommandLockout = !ent.Comp.CommandLockout;
        Dirty(ent);
        UpdateUserInterface(ent);
    }

    private void OnToggleEncryption(Entity<STNukeComponent> ent, ref STNukeToggleEncryptionMessage args)
    {
        if (_net.IsClient) return;
        if (!_xformQuery.TryGetComponent(ent, out var xform)) return;

        if (!xform.Anchored)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-anchor-first"), args.Actor, PopupType.Medium);
            return;
        }
        if (ent.Comp.Safety)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-safety-on"), args.Actor, PopupType.Medium);
            return;
        }
        if (!CheckTelecommsTowers(ent))
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-towers-offline"), args.Actor, PopupType.LargeCaution);
            return;
        }

        StartDoAfter(ent, args.Actor, new STNukeEncryptionDoAfterEvent());
    }

    private void OnToggleNuke(Entity<STNukeComponent> ent, ref STNukeToggleMessage args)
    {
        if (_net.IsClient) return;

        if (ent.Comp.Active)
        {
            if (ent.Comp.DecryptionOn == null)
            {
                _popup.PopupCursor(Loc.GetString("st-nuke-impossible-disengage"), args.Actor, PopupType.LargeCaution);
                return;
            }
            Disable(ent);
            return;
        }

        if (!ent.Comp.DecryptionComplete)
        {
            _popup.PopupCursor(Loc.GetString("st-nuke-decryption-not-complete"), args.Actor, PopupType.LargeCaution);
            return;
        }

        ent.Comp.Active = true;
        ent.Comp.ExplodeOn = _timing.CurTime + ent.Comp.DetonationTime;
        Dirty(ent);

        var areaName = Loc.GetString("generic-unknown-title");
        if (_area.TryGetArea(ent, out _, out var areaProto))
            areaName = areaProto.Name;

        AnnounceGlobal(ent,
            "st-nuke-activated-marine",
            "st-nuke-activated-xeno",
            marineArgs: new (string, object)[] { ("time", FormatTime(ent.Comp.DetonationTime)) },
            xenoArgs: new (string, object)[] { ("area", areaName) }
        );

        UpdateUserInterface(ent);
    }

    private void StartDoAfter<T>(Entity<STNukeComponent> ent, EntityUid user, T eventArgs, float time = 5f) where T : DoAfterEvent
    {
        UpdateUserInterface(ent);
        var ev = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(time), eventArgs, ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        };
        _doAfter.TryStartDoAfter(ev);
    }

    private void LinkTowers(Entity<STNukeComponent> ent)
    {
        var xform = _xformQuery.GetComponent(ent);

        if (xform.GridUid == null)
            return;

        ent.Comp.LinkedTowers.Clear();
        var query = EntityQueryEnumerator<CommunicationsTowerComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var towerXform))
        {
            if (towerXform.GridUid == xform.GridUid)
            {
                ent.Comp.LinkedTowers.Add(uid);
            }
        }
    }

    private bool CheckTelecommsTowers(Entity<STNukeComponent> ent)
    {
        var activeTowers = 0;
        for (var i = ent.Comp.LinkedTowers.Count - 1; i >= 0; i--)
        {
            var tower = ent.Comp.LinkedTowers[i];
            if (!_towerQuery.TryGetComponent(tower, out var towerComp))
                continue;

            if (towerComp.State == CommunicationsTowerState.On)
                activeTowers++;
        }
        return activeTowers >= ent.Comp.RequiredTowers;
    }

    private void UpdateDecryption(Entity<STNukeComponent> ent, TimeSpan curTime)
    {
        var remaining = ent.Comp.DecryptionOn!.Value - curTime;

        if (remaining <= TimeSpan.FromMinutes(5) && remaining > TimeSpan.Zero && !ent.Comp.AnnouncedHalfway)
        {
            AnnounceGlobal(ent,
                "st-nuke-decryption-halfway-marine",
                "st-nuke-decryption-halfway-xeno",
                marineArgs: new (string, object)[] { ("time", "5:00") }
            );
            ent.Comp.AnnouncedHalfway = true;
            Dirty(ent);
        }

        if (remaining <= TimeSpan.FromMinutes(1) && remaining > TimeSpan.Zero && !ent.Comp.AnnouncedOneMinute)
        {
            AnnounceGlobal(ent, "st-nuke-decryption-one-minute-marine", "st-nuke-decryption-one-minute-xeno");
            ent.Comp.AnnouncedOneMinute = true;
            Dirty(ent);
        }

        if (curTime >= ent.Comp.DecryptionOn.Value)
        {
            ent.Comp.Decryption = false;
            ent.Comp.DecryptionOn = null;
            ent.Comp.DecryptionComplete = true;
            ent.Comp.TowersWereOffline = false;
            Dirty(ent);

            _popup.PopupPredicted(Loc.GetString("st-nuke-decryption-complete"), ent, null, PopupType.Large);
            AnnounceGlobal(ent, "st-nuke-decryption-completed-marine", "st-nuke-decryption-completed-xeno");
        }

        UpdateUserInterface(ent);
    }

    private void UpdateTowerCheck(Entity<STNukeComponent> ent, TimeSpan curTime)
    {
        if (ent.Comp.LastTowerCheck.HasValue &&
            curTime - ent.Comp.LastTowerCheck.Value < ent.Comp.TowerCheckInterval)
            return;

        ent.Comp.LastTowerCheck = curTime;
        var towersOnline = CheckTelecommsTowers(ent);

        if (!towersOnline && !ent.Comp.TowersWereOffline)
        {
            ent.Comp.TowersWereOffline = true;
            if (ent.Comp.DecryptionOn.HasValue)
            {
                var newTime = ent.Comp.DecryptionOn.Value + ent.Comp.PenaltionTime;
                var maxTime = curTime + TimeSpan.FromMinutes(10);
                ent.Comp.DecryptionOn = newTime > maxTime ? maxTime : newTime;
                ent.Comp.Decryption = false;

                _popup.PopupPredicted(Loc.GetString("st-nuke-towers-offline-penalty"), ent, null, PopupType.LargeCaution);
                Dirty(ent);
            }
        }
        else if (towersOnline && ent.Comp.TowersWereOffline)
        {
            ent.Comp.TowersWereOffline = false;
        }
    }

    private void UpdateExplosion(Entity<STNukeComponent> ent, TimeSpan curTime)
    {
        if (ent.Comp.Exploded) return;

        if (curTime >= ent.Comp.ExplodeOn!.Value)
        {
            PrepareExplosion(ent);
            ent.Comp.Exploded = true;
            Dirty(ent);
        }
        UpdateUserInterface(ent);
    }

    private void PrepareExplosion(Entity<STNukeComponent> ent)
    {
        var marineQuery = EntityQueryEnumerator<MarineComponent, ActorComponent>();
        while (marineQuery.MoveNext(out var uid, out _, out _))
            _eye.SetTarget(uid, ent.Owner);

        var xenoQuery = EntityQueryEnumerator<XenoComponent, ActorComponent>();
        while (xenoQuery.MoveNext(out var uid, out _, out _))
            _eye.SetTarget(uid, ent.Owner);

        _audio.PlayGlobal(ent.Comp.BeforeNukeSound, Filter.Broadcast(), true);

        var length = TimeSpan.FromSeconds(16);
        ent.Comp.ExplodeStage1At = _timing.CurTime + length;
        ent.Comp.ExplodeStage2At = _timing.CurTime + length + TimeSpan.FromSeconds(1);
        ent.Comp.ExplodeSoundPlayed = false;
        ent.Comp.Nuked = false;

        Dirty(ent);
    }

    private void PerformNuke(Entity<STNukeComponent> ent)
    {
        var mobQuery = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (!_rmcPlanet.IsOnPlanet(uid.ToCoordinates()))
                continue;

            Spawn("Ash", xform.Coordinates);

            if (HasComp<BodyComponent>(uid))
                _body.GibBody(uid, true);
            else
                QueueDel(uid);
        }

        DeleteOnPlanet<RMCCameraComponent>();
        DeleteOnPlanet<DropshipDestinationComponent>();

        var distressQuery = EntityQueryEnumerator<CMDistressSignalRuleComponent, ActiveGameRuleComponent>();
        while (distressQuery.MoveNext(out var uid, out var comp, out _))
        {
            comp.Nuked = true;
            Dirty(uid, comp);
        }
    }

    private void DeleteOnPlanet<T>() where T : Component
    {
        var query = EntityQueryEnumerator<T, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (_rmcPlanet.IsOnPlanet(uid.ToCoordinates()))
                QueueDel(uid);
        }
    }

    public void Disable(Entity<STNukeComponent> ent)
    {
        ResetDecryption(ent);
        ent.Comp.Active = false;
        ent.Comp.ExplodeOn = null;
        ent.Comp.Exploded = false;
        AnnounceGlobal(ent, "st-nuke-deactivated-marine", "st-nuke-deactivated-xeno");
        Dirty(ent);
        UpdateUserInterface(ent);
    }

    private void ResetDecryption(Entity<STNukeComponent> ent)
    {
        ent.Comp.Decryption = false;
        ent.Comp.DecryptionOn = null;
        ent.Comp.DecryptionComplete = false;
        ent.Comp.DecryptionTime = TimeSpan.FromMinutes(10);
        ent.Comp.TowersWereOffline = false;
    }

    private void UpdateUserInterface(Entity<STNukeComponent> ent)
    {
        if (!_ui.HasUi(ent.Owner, STNukeUiKey.Key))
            return;

        var xform = Transform(ent);
        var time = _timing.CurTime;

        var decryptionTime = "00:00";
        if (ent.Comp.Decryption && ent.Comp.DecryptionOn.HasValue)
        {
            var remaining = ent.Comp.DecryptionOn.Value - time;
            decryptionTime = FormatTime(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
        }

        var timeLeft = "00:00";
        if (ent.Comp.Active && ent.Comp.ExplodeOn.HasValue)
        {
            var remaining = ent.Comp.ExplodeOn.Value - time;
            timeLeft = FormatTime(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
        }

        var state = new STNukeBuiState(
            anchor: xform.Anchored,
            safety: ent.Comp.Safety,
            timing: ent.Comp.Active,
            timeLeft: timeLeft,
            commandLockout: ent.Comp.CommandLockout,
            allowed: true,
            decryptionComplete: ent.Comp.DecryptionComplete,
            decrypting: ent.Comp.Decryption,
            decryptionTime: decryptionTime,
            canDisengage: ent.Comp.DecryptionOn.HasValue || !ent.Comp.Active
        );

        _ui.SetUiState(ent.Owner, STNukeUiKey.Key, state);
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<STNukeComponent> ent)
    {
        var xform = Transform(ent);
        _appearance.SetData(ent, STNukeVisuals.Deployed, xform.Anchored);
        _appearance.SetData(ent, STNukeVisuals.Unsafe, !ent.Comp.Safety);
        _appearance.SetData(ent, STNukeVisuals.Timing, ent.Comp.DecryptionComplete);
        _appearance.SetData(ent, STNukeVisuals.Activation, ent.Comp.Active);
    }

    private string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
    }

    private void AnnounceGlobal(Entity<STNukeComponent> ent, string marineKey, string xenoKey,
        (string, object)[]? marineArgs = null, (string, object)[]? xenoArgs = null)
    {
        if (_net.IsClient) return;

        var marineMsg = Loc.GetString(marineKey, marineArgs ?? Array.Empty<(string, object)>());
        _marineAnnounce.AnnounceARES(null, marineMsg, new SoundPathSpecifier("/Audio/_RMC14/AI/announce.ogg"));

        var xenoMsg = Loc.GetString(xenoKey, xenoArgs ?? Array.Empty<(string, object)>());
        _xenoAnnounce.AnnounceAll(ent, _xenoAnnounce.WrapHive(xenoMsg), new SoundCollectionSpecifier("XenoQueenCommand"));
    }
}
