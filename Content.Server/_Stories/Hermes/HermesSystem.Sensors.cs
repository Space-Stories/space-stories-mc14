using System.Numerics;
using Content.Shared._Stories.Hermes;
using Content.Shared.Access.Components;
using Content.Shared.Database;
using Content.Shared.DeviceLinking;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Physics.Events;

namespace Content.Server._Stories.Hermes;

public sealed partial class HermesSystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private void InitializeSensors()
    {
        SubscribeLocalEvent<HermesSensorComponent, StartCollideEvent>(OnSensorEnter);
        SubscribeLocalEvent<HermesSensorComponent, EndCollideEvent>(OnSensorLeave);
    }

    private bool TrySensorCore(EntityUid sensor, out Entity<HermesCoreComponent> core)
    {
        core = default;
        if (!TryComp(sensor, out DeviceLinkSinkComponent? sink))
            return false;
        foreach (var source in sink.LinkedSources)
        {
            if (TryComp(source, out HermesCoreComponent? comp) && IsOnline((source, comp)))
            {
                core = (source, comp);
                return true;
            }
        }
        return false;
    }

    private void OnSensorEnter(Entity<HermesSensorComponent> ent, ref StartCollideEvent args)
    {
        var target = args.OtherEntity;
        if (ent.Comp.RevokeOnExit || !HasComp<MobStateComponent>(target) || _mobState.IsDead(target) ||
            HasComp<GhostComponent>(target) || !TrySensorCore(ent, out var core))
            return;
        if (_idCard.TryFindIdCard(target, out var card) && TryComp(card, out AccessComponent? access))
        {
            var level = GetCardAccess(core.Comp, card, access, IsWorkingJoe(target));
            if (level >= HermesAccess.Certified || ent.Comp.AllowTemporary && level == HermesAccess.Temporary)
                return;
        }
        if (core.Comp.SensorCooldowns.TryGetValue(ent.Comp.Group, out var ready) && _timing.CurTime < ready)
            return;
        core.Comp.SensorCooldowns[ent.Comp.Group] = _timing.CurTime + ent.Comp.Cooldown;
        _audio.PlayPvs(ent.Comp.DetectionSound, ent);
        _popup.PopupEntity(Loc.GetString("stories-hermes-sensor-beep"), target, target);
        var message = Loc.GetString("stories-hermes-sensor-alert", ("sensor", Name(ent)));
        Announce(core, message);
        _ares.CreateARESLog(SecurityLog, message, core);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"HERMES: {ToPrettyString(target):user} triggered {ToPrettyString(ent):target} at core {ToPrettyString(core)}");
        Refresh(core);
    }

    private void OnSensorLeave(Entity<HermesSensorComponent> ent, ref EndCollideEvent args)
    {
        var target = args.OtherEntity;
        if (!ent.Comp.RevokeOnExit || TerminatingOrDeleted(target) || !TrySensorCore(ent, out var core))
            return;
        var offset = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(ent);
        var exit = _transform.GetWorldRotation(ent).RotateVec(ent.Comp.ExitDirection.ToVec());
        if (Vector2.Dot(offset, exit) <= 0)
            return;
        var revoked = _idCard.TryFindIdCard(target, out var card) && RevokeAtExit(core, card);
        foreach (var ticket in core.Comp.Tickets)
        {
            if (ticket.Status == HermesTicketStatus.Granted && !TerminatingOrDeleted(ticket.Card) &&
                _transform.ContainsEntity(target, ticket.Card))
                revoked |= RevokeAtExit(core, ticket.Card);
        }
        if (!revoked)
            return;
        _audio.PlayPvs(ent.Comp.RevocationSound, ent);
        if (HasComp<MobStateComponent>(target))
            _popup.PopupEntity(Loc.GetString("stories-hermes-sensor-access-revoked"), target, target);
    }

    public bool RevokeAtExit(Entity<HermesCoreComponent> core, EntityUid card)
    {
        if (!TryComp(card, out AccessComponent? access) || access.Tags.Overlaps(core.Comp.CertifiedAccess) ||
            access.Tags.Contains(core.Comp.TechnicianAccess))
            return false;
        foreach (var ticket in core.Comp.Tickets)
        {
            if (ticket.Card != card || ticket.Status != HermesTicketStatus.Granted)
                continue;
            RemoveTemporaryAccess(core, card);
            ticket.Status = HermesTicketStatus.Revoked;
            Announce(core, Loc.GetString("stories-hermes-exit-revoked", ("name", ticket.Submitter)));
            Refresh(core);
            return true;
        }
        return false;
    }
}
