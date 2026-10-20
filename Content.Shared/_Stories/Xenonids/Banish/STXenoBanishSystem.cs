using System.Linq;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared._RMC14.Xenonids.Devour;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.HiveLeader;
using Content.Shared._RMC14.Xenonids.ManageHive;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.Banish;

public sealed class STXenoBanishSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly AreaSystem _areas = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DialogSystem _dialog = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly HiveLeaderSystem _hiveLeader = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThresholds = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedCMChatSystem _rmcChat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedXenoWeedsSystem _weeds = default!;
    [Dependency] private readonly SharedXenoAnnounceSystem _xenoAnnounce = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;

    private EntityQuery<STXenoBanishedComponent> _banishedQuery;
    private EntityQuery<STBanishedHiveComponent> _banishedHiveQuery;

    public override void Initialize()
    {
        _banishedQuery = GetEntityQuery<STXenoBanishedComponent>();
        _banishedHiveQuery = GetEntityQuery<STBanishedHiveComponent>();

        SubscribeLocalEvent<ManageHiveComponent, STManageHiveBanishEvent>(OnBanish);
        SubscribeLocalEvent<ManageHiveComponent, STManageHiveBanishTargetEvent>(OnBanishTarget);
        SubscribeLocalEvent<ManageHiveComponent, STManageHiveBanishConfirmEvent>(OnBanishConfirm);
        SubscribeLocalEvent<ManageHiveComponent, STManageHiveBanishReasonEvent>(OnBanishReason);
        SubscribeLocalEvent<ManageHiveComponent, STManageHiveReadmitEvent>(OnReadmit);
        SubscribeLocalEvent<ManageHiveComponent, STManageHiveReadmitTargetEvent>(OnReadmitTarget);
        SubscribeLocalEvent<ManageHiveComponent, STManageHiveReadmitConfirmEvent>(OnReadmitConfirm);

        SubscribeLocalEvent<STXenoBanishedComponent, MobStateChangedEvent>(OnBanishedMobStateChanged);
        SubscribeLocalEvent<STXenoBanishedComponent, ComponentShutdown>(OnBanishedShutdown);
        SubscribeLocalEvent<STXenoBanishedComponent, AttackAttemptEvent>(OnBanishedAttackAttempt);
        SubscribeLocalEvent<STXenoBanishedComponent, PullStoppedMessage>(OnBanishedPullStopped);
        SubscribeLocalEvent<STXenoBanishedComponent, RegurgitateEvent>(OnBanishedRegurgitate);

        SubscribeLocalEvent<XenoComponent, BeforeDamageChangedEvent>(OnXenoBeforeDamageChanged);
        SubscribeLocalEvent<ActionComponent, ActionAttemptEvent>(OnActionAttempt);

        SubscribeLocalEvent<STHiveBanishmentsComponent, XenoHiveQueenChangedEvent>(OnHiveQueenChanged);
    }

    public bool IsBanished(EntityUid xeno)
    {
        return _banishedQuery.HasComp(xeno);
    }

    public bool IsBanishedHive(EntityUid? hive)
    {
        return hive != null && _banishedHiveQuery.HasComp(hive);
    }

    public bool IsBanishedFrom(EntityUid xeno, EntityUid target)
    {
        return _banishedQuery.TryComp(xeno, out var banished) &&
               HasComp<XenoComponent>(target) &&
               _hive.IsMember(target, banished.OriginalHive);
    }

    public bool IsBannedFromHive(NetUserId user, EntityUid? hive)
    {
        if (!TryComp(hive, out STHiveBanishmentsComponent? banishments) ||
            !banishments.Banished.TryGetValue(user, out var entry))
        {
            return false;
        }

        if (_timing.CurTime < entry.ExpiresAt || entry.Xeno != null)
            return true;

        banishments.Banished.Remove(user);
        return false;
    }

    public bool IsBannedFromHivePopup(EntityUid user, NetUserId userId, EntityUid? hive)
    {
        if (!IsBannedFromHive(userId, hive))
            return false;

        _popup.PopupEntity(Loc.GetString("st-banish-banned-from-hive"), user, user, PopupType.MediumCaution);
        return true;
    }

    private void OnBanish(Entity<ManageHiveComponent> manage, ref STManageHiveBanishEvent args)
    {
        if (_net.IsClient)
            return;

        if (_hive.GetHive(manage.Owner) is not { } hive)
            return;

        var banishments = EnsureComp<STHiveBanishmentsComponent>(hive);
        if (!_xenoPlasma.HasPlasmaPopup(manage.Owner, banishments.BanishPlasmaCost, false))
            return;

        var options = new List<DialogOption>();
        var xenos = EntityQueryEnumerator<XenoComponent, HiveMemberComponent>();
        while (xenos.MoveNext(out var uid, out _, out var member))
        {
            if (member.Hive != hive.Owner || !CanBanishTarget(manage, uid, false))
                continue;

            options.Add(new DialogOption(Name(uid), new STManageHiveBanishTargetEvent(GetNetEntity(uid))));
        }

        _dialog.OpenOptions(manage, Loc.GetString("st-banish-title"), options, Loc.GetString("st-banish-choose"));
    }

    private void OnBanishTarget(Entity<ManageHiveComponent> manage, ref STManageHiveBanishTargetEvent args)
    {
        if (_net.IsClient)
            return;

        if (GetEntity(args.Target) is not { Valid: true } target || !CanBanishTarget(manage, target))
            return;

        var msg = Loc.GetString("st-banish-confirm", ("target", target));
        _dialog.OpenConfirmation(manage, Loc.GetString("st-banish-title"), msg, new STManageHiveBanishConfirmEvent(args.Target));
    }

    private void OnBanishConfirm(Entity<ManageHiveComponent> manage, ref STManageHiveBanishConfirmEvent args)
    {
        if (_net.IsClient)
            return;

        if (GetEntity(args.Target) is not { Valid: true } target || !CanBanishTarget(manage, target))
            return;

        var msg = Loc.GetString("st-banish-reason", ("target", target));
        _dialog.OpenInput(manage, msg, new STManageHiveBanishReasonEvent(args.Target), true, minCharacterLimit: 1);
    }

    private void OnBanishReason(Entity<ManageHiveComponent> manage, ref STManageHiveBanishReasonEvent args)
    {
        if (_net.IsClient)
            return;

        if (GetEntity(args.Target) is not { Valid: true } target || !CanBanishTarget(manage, target))
            return;

        if (string.IsNullOrWhiteSpace(args.Message))
        {
            _popup.PopupEntity(Loc.GetString("st-banish-need-reason", ("target", target)), manage, manage, PopupType.MediumCaution);
            return;
        }

        if (_hive.GetHive(manage.Owner) is not { } hive)
            return;

        var banishments = EnsureComp<STHiveBanishmentsComponent>(hive);
        if (!_xenoPlasma.TryRemovePlasmaPopup(manage.Owner, banishments.BanishPlasmaCost))
            return;

        Banish(target, (hive, banishments));

        var announcement = Loc.GetString("st-banish-announce", ("queen", manage.Owner), ("target", target), ("reason", args.Message));
        _xenoAnnounce.AnnounceToHive(manage.Owner, hive, announcement);

        var targetMsg = Loc.GetString("st-banish-target", ("queen", manage.Owner));
        _rmcChat.ChatMessageToOne(targetMsg, target);
        _popup.PopupEntity(targetMsg, target, target, PopupType.LargeCaution);

        _adminLog.Add(LogType.STXenoBanish, LogImpact.High, $"{ToPrettyString(manage):player} banished {ToPrettyString(target):target} from hive {ToPrettyString(hive):hive}. Reason: {args.Message}");
    }

    private void Banish(EntityUid target, Entity<STHiveBanishmentsComponent> hive)
    {
        _hiveLeader.RevokeLeader(target);

        var banishedHive = Spawn(hive.Comp.BanishedHiveId);
        _metaData.SetEntityName(banishedHive, Loc.GetString("st-banish-hive-name", ("target", target)));

        var banished = EnsureComp<STXenoBanishedComponent>(target);
        banished.OriginalHive = hive;
        banished.BanishedHive = banishedHive;
        banished.NextDamageAt = _timing.CurTime + banished.DamageEvery;
        Dirty(target, banished);

        _hive.SetHive(target, banishedHive);

        if (_mind.TryGetMind(target, out _, out var mind) && mind.UserId is { } userId)
            hive.Comp.Banished[userId] = new STBanishmentEntry(Name(target), _timing.CurTime + hive.Comp.Duration, target);
    }

    private bool CanBanishTarget(Entity<ManageHiveComponent> manage, EntityUid target, bool popup = true)
    {
        if (target == manage.Owner)
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("st-banish-self"), manage, manage, PopupType.MediumCaution);

            return false;
        }

        if (IsBanished(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("st-banish-already", ("target", target)), manage, manage, PopupType.MediumCaution);

            return false;
        }

        if (!_hive.FromSameHive(manage.Owner, target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("st-banish-other-hive", ("target", target)), manage, manage, PopupType.MediumCaution);

            return false;
        }

        if (_mobState.IsIncapacitated(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("st-banish-incapacitated", ("target", target)), manage, manage, PopupType.MediumCaution);

            return false;
        }

        return true;
    }

    private void OnReadmit(Entity<ManageHiveComponent> manage, ref STManageHiveReadmitEvent args)
    {
        if (_net.IsClient)
            return;

        if (_hive.GetHive(manage.Owner) is not { } hive)
            return;

        var banishments = EnsureComp<STHiveBanishmentsComponent>(hive);
        var options = new List<DialogOption>();
        foreach (var (userId, entry) in banishments.Banished.ToArray())
        {
            if (!IsBannedFromHive(userId, hive))
                continue;

            var text = Loc.GetString("st-readmit-option",
                ("name", entry.Name),
                ("deliveries", GetDeliveries(entry)),
                ("cost", GetReadmitCost(banishments, entry).Int()));
            options.Add(new DialogOption(text, new STManageHiveReadmitTargetEvent(userId.UserId)));
        }

        _dialog.OpenOptions(manage, Loc.GetString("st-readmit-title"), options, Loc.GetString("st-readmit-choose"));
    }

    private void OnReadmitTarget(Entity<ManageHiveComponent> manage, ref STManageHiveReadmitTargetEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryGetBanishment(manage, args.UserId, out var hive, out var entry))
            return;

        var cost = GetReadmitCost(hive.Comp, entry);
        if (!_xenoPlasma.HasPlasmaPopup(manage.Owner, cost, false))
            return;

        var msg = Loc.GetString("st-readmit-confirm", ("target", entry.Name), ("cost", cost.Int()));
        _dialog.OpenConfirmation(manage, Loc.GetString("st-readmit-title"), msg, new STManageHiveReadmitConfirmEvent(args.UserId));
    }

    private void OnReadmitConfirm(Entity<ManageHiveComponent> manage, ref STManageHiveReadmitConfirmEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryGetBanishment(manage, args.UserId, out var hive, out var entry))
            return;

        var cost = GetReadmitCost(hive.Comp, entry);
        if (cost > 0 && !_xenoPlasma.TryRemovePlasmaPopup(manage.Owner, cost))
            return;

        hive.Comp.Banished.Remove(new NetUserId(args.UserId));

        if (entry.Xeno is { } xeno && !TerminatingOrDeleted(xeno))
        {
            Readmit(xeno);

            var msg = Loc.GetString("st-readmit-target", ("queen", manage.Owner));
            _rmcChat.ChatMessageToOne(msg, xeno);
            _popup.PopupEntity(msg, xeno, xeno, PopupType.Large);

            if (_hive.IsMember(xeno, hive))
                _xenoAnnounce.AnnounceToHive(manage.Owner, hive, Loc.GetString("st-readmit-announce", ("queen", manage.Owner), ("target", xeno)));
        }

        _popup.PopupEntity(Loc.GetString("st-readmit-done", ("target", entry.Name)), manage, manage);
        _adminLog.Add(LogType.STXenoBanish, LogImpact.Medium, $"{ToPrettyString(manage):player} readmitted {entry.Name} into hive {ToPrettyString(hive):hive}");
    }

    private int GetDeliveries(STBanishmentEntry entry)
    {
        return _banishedQuery.TryComp(entry.Xeno, out var banished) ? banished.Deliveries : 0;
    }

    private FixedPoint2 GetReadmitCost(STHiveBanishmentsComponent banishments, STBanishmentEntry entry)
    {
        return GetDeliveries(entry) > 0 ? FixedPoint2.Zero : banishments.ReadmitPlasmaCost;
    }

    private bool TryGetBanishment(EntityUid manage, Guid userId, out Entity<STHiveBanishmentsComponent> hive, out STBanishmentEntry entry)
    {
        hive = default;
        entry = default!;
        if (_hive.GetHive(manage) is not { } userHive ||
            !TryComp(userHive, out STHiveBanishmentsComponent? banishments))
        {
            return false;
        }

        var netUserId = new NetUserId(userId);
        if (!IsBannedFromHive(netUserId, userHive) ||
            !banishments.Banished.TryGetValue(netUserId, out var found))
        {
            return false;
        }

        hive = (userHive, banishments);
        entry = found;
        return true;
    }

    private void Readmit(EntityUid xeno)
    {
        if (!_banishedQuery.TryComp(xeno, out var banished))
            return;

        _hive.SetHive(xeno, banished.OriginalHive);
        RemComp<STXenoBanishedComponent>(xeno);
    }

    private void OnHiveQueenChanged(Entity<STHiveBanishmentsComponent> ent, ref XenoHiveQueenChangedEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(ent, out HiveComponent? hive) ||
            hive.CurrentQueen != null ||
            hive.LastQueenDeath != _timing.CurTime)
        {
            return;
        }

        foreach (var entry in ent.Comp.Banished.Values)
        {
            if (entry.Xeno is { } xeno && !TerminatingOrDeleted(xeno))
                Readmit(xeno);
        }

        ent.Comp.Banished.Clear();
    }

    private void OnBanishedMobStateChanged(Entity<STXenoBanishedComponent> ent, ref MobStateChangedEvent args)
    {
        if (_net.IsClient || args.NewMobState != MobState.Dead)
            return;

        if (TryComp(ent.Comp.OriginalHive, out HiveComponent? hive))
        {
            var noRefund = TryComp(ent.Comp.BanishedHive, out STBanishedHiveComponent? banishedHive) &&
                           Prototype(ent)?.ID is { } protoId &&
                           banishedHive.NoLarvaRefund.Contains(protoId);

            if (!noRefund)
                _hive.ChangeBurrowedLarva((ent.Comp.OriginalHive.Value, hive), 1);

            var location = Loc.GetString("generic-unknown-title");
            if (_areas.TryGetArea(ent, out _, out var areaProto))
                location = areaProto.Name;

            var msg = Loc.GetString("st-banish-death", ("xeno", ent.Owner), ("location", location));
            _xenoAnnounce.AnnounceToHive(ent.Comp.OriginalHive.Value, ent.Comp.OriginalHive.Value, msg);
        }

        if (TryComp(ent.Comp.OriginalHive, out STHiveBanishmentsComponent? banishments))
        {
            foreach (var entry in banishments.Banished.Values)
            {
                if (entry.Xeno == ent.Owner)
                    entry.Xeno = null;
            }
        }

        RemCompDeferred<STXenoBanishedComponent>(ent);
    }

    private void OnBanishedShutdown(Entity<STXenoBanishedComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient)
            return;

        if (ent.Comp.BanishedHive is { } banishedHive && !TerminatingOrDeleted(banishedHive))
            QueueDel(banishedHive);
    }

    private void OnBanishedAttackAttempt(Entity<STXenoBanishedComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Target is { } target && IsBanishedFrom(ent, target))
            args.Cancel();
    }

    private void OnBanishedPullStopped(Entity<STXenoBanishedComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PullerUid != ent.Owner)
            return;

        TryMarkDelivery(ent, args.PulledUid, _transform.GetMoverCoordinates(args.PulledUid));
    }

    private void OnBanishedRegurgitate(Entity<STXenoBanishedComponent> ent, ref RegurgitateEvent args)
    {
        if (GetEntity(args.NetRegurgitated) is not { Valid: true } host)
            return;

        TryMarkDelivery(ent, host, _transform.GetMoverCoordinates(ent));
    }

    private void TryMarkDelivery(Entity<STXenoBanishedComponent> banished, EntityUid host, EntityCoordinates coordinates)
    {
        if (_net.IsClient || banished.Comp.OriginalHive is not { } hive)
            return;

        if (!HasComp<XenoNestableComponent>(host) ||
            HasComp<XenoNestedComponent>(host) ||
            HasComp<STBanishDeliveryComponent>(host) ||
            _mobState.IsDead(host) ||
            banished.Comp.DeliveredHosts.Contains(host))
        {
            return;
        }

        if (GetHiveWeeds(coordinates, hive) is not { } weeds)
            return;

        var delivery = EnsureComp<STBanishDeliveryComponent>(host);
        delivery.Banished = banished;
        delivery.Hive = hive;
        delivery.ExpiresAt = _timing.CurTime + banished.Comp.DeliveryTimeout;

        var location = Loc.GetString("generic-unknown-title");
        if (_areas.TryGetArea(weeds.Owner, out _, out var areaProto))
            location = areaProto.Name;

        var msg = Loc.GetString("st-banish-delivery-left", ("xeno", banished.Owner), ("host", host), ("location", location));
        _xenoAnnounce.AnnounceToHive(hive, hive, msg);
    }

    private Entity<XenoWeedsComponent>? GetHiveWeeds(EntityCoordinates coordinates, EntityUid hive)
    {
        if (_transform.GetGrid(coordinates) is not { } gridUid ||
            !TryComp(gridUid, out MapGridComponent? grid))
        {
            return null;
        }

        if (_weeds.GetWeedsOnFloor((gridUid, grid), coordinates) is not { } weeds ||
            !_hive.IsMember(weeds.Owner, hive))
        {
            return null;
        }

        return weeds;
    }

    private bool IsOnCoreWeeds(EntityUid uid, EntityUid hive)
    {
        var coordinates = _transform.GetMoverCoordinates(uid);
        if (GetHiveWeeds(coordinates, hive) == null ||
            _transform.GetGrid(coordinates) is not { } gridUid ||
            !TryComp(gridUid, out MapGridComponent? grid))
        {
            return false;
        }

        return _weeds.IsOnHiveWeeds((gridUid, grid), coordinates);
    }

    private void CreditDelivery(Entity<STBanishDeliveryComponent> delivery)
    {
        RemCompDeferred<STBanishDeliveryComponent>(delivery);

        if (!_banishedQuery.TryComp(delivery.Comp.Banished, out var banished) ||
            banished.OriginalHive != delivery.Comp.Hive ||
            !TryComp(delivery, out XenoNestedComponent? nested) ||
            !_hive.IsMember(nested.Nest, delivery.Comp.Hive) && !_hive.IsMember(nested.Nest, banished.BanishedHive) ||
            !banished.DeliveredHosts.Add(delivery.Owner))
        {
            return;
        }

        banished.Deliveries++;
        Dirty(delivery.Comp.Banished, banished);

        if (_hive.IsMember(nested.Nest, banished.BanishedHive))
            _hive.SetHive(nested.Nest, delivery.Comp.Hive);

        var msg = Loc.GetString("st-banish-delivery-accepted", ("xeno", delivery.Comp.Banished), ("count", banished.Deliveries));
        _xenoAnnounce.AnnounceToHive(delivery.Comp.Hive, delivery.Comp.Hive, msg);

        var targetMsg = Loc.GetString("st-banish-delivery-accepted-target", ("count", banished.Deliveries));
        _rmcChat.ChatMessageToOne(targetMsg, delivery.Comp.Banished);
        _popup.PopupEntity(targetMsg, delivery.Comp.Banished, delivery.Comp.Banished, PopupType.Large);
    }

    private void OnXenoBeforeDamageChanged(Entity<XenoComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled)
            return;

        if (_banishedQuery.HasComp(ent) && args.Damage.GetTotal() < 0)
        {
            args.Cancelled = true;
            return;
        }

        if (args.Origin is { } origin && IsBanishedFrom(origin, ent))
            args.Cancelled = true;
    }

    private void OnActionAttempt(Entity<ActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled || !_banishedQuery.TryComp(args.User, out var banished))
            return;

        if (!_banishedHiveQuery.TryComp(banished.BanishedHive, out var banishedHive) ||
            _actions.GetEvent(ent) is not { } ev ||
            !banishedHive.BlockedActionEvents.Contains(ev.GetType().Name))
        {
            return;
        }

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("st-banish-action-blocked"), args.User, args.User, PopupType.SmallCaution);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<STXenoBanishedComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var banished, out var damageable))
        {
            if (time < banished.NextDamageAt)
                continue;

            banished.NextDamageAt = time + banished.DamageEvery;
            Dirty(uid, banished);

            if (_mobState.IsDead(uid) ||
                banished.OriginalHive is not { } hive ||
                !IsOnCoreWeeds(uid, hive))
            {
                continue;
            }

            var health = 0f;
            if (_mobThresholds.TryGetIncapThreshold(uid, out var threshold))
                health = (threshold.Value - damageable.TotalDamage).Float();

            var amount = Math.Max(MathF.Ceiling(health / banished.DamageDivisor), 1);
            var damage = new DamageSpecifier();
            damage.DamageDict[banished.DamageType] = amount;
            _damageable.TryChangeDamage(uid, damage, true, false, damageable);
        }

        var deliveries = EntityQueryEnumerator<STBanishDeliveryComponent>();
        while (deliveries.MoveNext(out var uid, out var delivery))
        {
            if (HasComp<XenoNestedComponent>(uid))
            {
                CreditDelivery((uid, delivery));
                continue;
            }

            if (time >= delivery.ExpiresAt || _mobState.IsDead(uid) || !_banishedQuery.HasComp(delivery.Banished))
                RemCompDeferred<STBanishDeliveryComponent>(uid);
        }
    }
}
