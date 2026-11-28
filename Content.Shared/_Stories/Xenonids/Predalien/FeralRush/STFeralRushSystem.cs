using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Aura;
using Content.Shared.Coordinates;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Xenonids.Predalien.FeralRush;

public sealed class STFeralRushSystem : EntitySystem
{
    [Dependency] private readonly CMArmorSystem _armor = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAuraSystem _aura = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STFeralRushComponent, STFeralRushActionEvent>(OnFeralRushAction);

        SubscribeLocalEvent<STFeralRushingComponent, ComponentShutdown>(OnRushingShutdown);
        SubscribeLocalEvent<STFeralRushingComponent, RefreshMovementSpeedModifiersEvent>(OnRushingRefreshSpeed);
        SubscribeLocalEvent<STFeralRushingComponent, CMGetArmorEvent>(OnRushingGetArmor);
    }

    private void OnFeralRushAction(Entity<STFeralRushComponent> xeno, ref STFeralRushActionEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<STFeralRushingComponent>(xeno))
        {
            _popup.PopupClient(Loc.GetString("st-predalien-feral-rush-fail-active"), xeno, xeno, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        var time = _timing.CurTime;
        var rushing = EnsureComp<STFeralRushingComponent>(xeno);
        rushing.ArmorGain = xeno.Comp.ArmorGain;
        rushing.SpeedModifier = xeno.Comp.SpeedModifier;
        rushing.SpeedExpireAt = time + xeno.Comp.SpeedDuration;
        rushing.ArmorExpireAt = time + xeno.Comp.ArmorDuration;
        Dirty(xeno, rushing);

        var longestDuration = xeno.Comp.ArmorDuration > xeno.Comp.SpeedDuration ? xeno.Comp.ArmorDuration : xeno.Comp.SpeedDuration;

        _popup.PopupClient(Loc.GetString("st-predalien-feral-rush-self"), xeno, xeno, PopupType.MediumCaution);
        _audio.PlayPredicted(xeno.Comp.Sound, xeno, xeno);
        _aura.GiveAura(xeno, xeno.Comp.RushColor, longestDuration);
        _armor.UpdateArmorValue(xeno.Owner);
        _speed.RefreshMovementSpeedModifiers(xeno.Owner);

        if (_net.IsServer)
            SpawnAttachedTo(xeno.Comp.RushEffect, xeno.Owner.ToCoordinates());
    }

    private void OnRushingShutdown(Entity<STFeralRushingComponent> xeno, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(xeno))
            return;

        _speed.RefreshMovementSpeedModifiers(xeno.Owner);
        _armor.UpdateArmorValue(xeno.Owner);

        if (_timing.IsFirstTimePredicted)
            _popup.PopupEntity(Loc.GetString("st-predalien-feral-rush-end"), xeno, xeno, PopupType.SmallCaution);
    }

    private void OnRushingRefreshSpeed(Entity<STFeralRushingComponent> xeno, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (_timing.CurTime >= xeno.Comp.SpeedExpireAt)
            return;

        args.ModifySpeed(xeno.Comp.SpeedModifier, xeno.Comp.SpeedModifier);
    }

    private void OnRushingGetArmor(Entity<STFeralRushingComponent> xeno, ref CMGetArmorEvent args)
    {
        if (_timing.CurTime >= xeno.Comp.ArmorExpireAt)
            return;

        args.XenoArmor += xeno.Comp.ArmorGain;
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<STFeralRushingComponent>();
        while (query.MoveNext(out var uid, out var rushing))
        {
            if (!rushing.SpeedExpired && time >= rushing.SpeedExpireAt)
            {
                rushing.SpeedExpired = true;
                Dirty(uid, rushing);
                _speed.RefreshMovementSpeedModifiers(uid);
            }

            if (_net.IsClient)
                continue;

            if (time < rushing.SpeedExpireAt || time < rushing.ArmorExpireAt)
                continue;

            RemCompDeferred<STFeralRushingComponent>(uid);
        }
    }
}
