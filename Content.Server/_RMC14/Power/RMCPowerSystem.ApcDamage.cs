using Content.Server.Wires;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Electrocution;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RMC14.Power;

public sealed partial class RMCPowerSystem
{
    private static readonly ProtoId<DamageTypePrototype> ShockDamageType = "Shock";

    protected override void SetApcWiresCut(Entity<RMCApcComponent> ent, EntityUid user, bool cut)
    {
        if (TryComp(ent, out WiresComponent? wires))
            _wires.SetWiresCut((ent, wires), user, cut);
    }

    protected override void SpawnApcSparks(Entity<RMCApcComponent> ent)
    {
        Spawn(ent.Comp.SparksEffect, Transform(ent).Coordinates);
        _audio.PlayPvs(ent.Comp.SparksSound, ent);
    }

    public bool TryElectrocuteApcUser(Entity<RMCApcComponent?> ent, EntityUid user, float probability = 0.5f, float siemensCoeff = 0.7f)
    {
        if (!_apcQuery.Resolve(ent, ref ent.Comp, false))
            return false;

        return TryApcElectrocute((ent.Owner, ent.Comp), user, probability, siemensCoeff);
    }

    protected override bool TryApcElectrocute(Entity<RMCApcComponent> ent, EntityUid user, float probability = 0.5f, float siemensCoeff = 1f)
    {
        if (ent.Comp.Broken || !_random.Prob(probability))
            return false;

        var cell = TryGetApcCell(ent);
        var cellDamage = cell != null ? GetCellElectrocuteDamage(cell.Value.Comp.CurrentCharge) : 0;

        var netPower = 0f;
        if (TryGetPowerNetwork(ent, out var key))
        {
            var stats = GetNetworkStats(key);
            netPower = stats.AvailableGeneration + stats.StorageDischarge;
            if (stats.StorageCharge > 0)
                netPower = Math.Max(netPower, stats.StorageCharge);
        }

        if (ent.Comp.ExternalPower && netPower < 1000f)
            netPower = Math.Max(netPower, ent.Comp.DeliveredPower);

        var netDamage = GetPowernetElectrocuteDamage(netPower);
        var baseDamage = Math.Max(netDamage, cellDamage);

        if (baseDamage == 0 && _area.TryGetArea(ent, out var area, out _) && area.Value.Comp.AlwaysPowered)
            baseDamage = Math.Min(_random.Next(25, 76), _random.Next(25, 76));

        if (baseDamage < 1)
            return false;

        SpawnApcSparks(ent);

        if (HasComp<XenoComponent>(user))
        {
            var shockDamage = baseDamage * siemensCoeff;
            if (shockDamage < 1f)
                return false;

            var dmgInt = Math.Max(1, (int) shockDamage);
            _damageable.TryChangeDamage(user, new DamageSpecifier(_prototype.Index(ShockDamageType), dmgInt), origin: ent);
            Spawn(ent.Comp.SparksEffect, Transform(user).Coordinates);
            _audio.PlayPvs(ent.Comp.SparksSound, user);

            if (shockDamage >= 7f)
            {
                var stunTime = TryComp(user, out RMCSizeComponent? size) && size.Size >= ent.Comp.XenoElectronicsDamageSize
                    ? TimeSpan.FromSeconds(4.0 / 3.0)
                    : TimeSpan.FromSeconds(8);
                _stun.TryParalyze(user, stunTime, true);
                _popup.PopupEntity(Loc.GetString("rmc-apc-shock-heavy-self"), user, user, PopupType.LargeCaution);
                _popup.PopupEntity(
                    Loc.GetString("rmc-apc-shock-heavy-others", ("target", user), ("source", ent)),
                    user,
                    Filter.PvsExcept(user),
                    true,
                    PopupType.MediumCaution);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("rmc-apc-shock-mild-self"), user, user, PopupType.SmallCaution);
                _popup.PopupEntity(
                    Loc.GetString("rmc-apc-shock-mild-others", ("target", user), ("source", ent)),
                    user,
                    Filter.PvsExcept(user),
                    true,
                    PopupType.SmallCaution);
            }

            if (cell != null && cellDamage > netDamage)
                _battery.UseCharge(cell.Value.Owner, dmgInt * 20f * Cm13CellRate, cell.Value.Comp);

            return true;
        }

        var attempt = new ElectrocutionAttemptEvent(user, ent, siemensCoeff, ~SlotFlags.POCKET);
        RaiseLocalEvent(user, attempt, true);
        if (attempt.Cancelled || attempt.SiemensCoefficient <= 0f)
            return false;

        var finalDamage = baseDamage * attempt.SiemensCoefficient;
        if (finalDamage < 1f)
            return false;

        var finalInt = Math.Max(1, (int) finalDamage);
        if (finalDamage > 10f)
        {
            if (!_electrocution.TryDoElectrocution(user, ent, baseDamage, TimeSpan.FromSeconds(12), refresh: true, siemensCoefficient: siemensCoeff))
                return false;

            Spawn(ent.Comp.SparksEffect, Transform(user).Coordinates);
            _audio.PlayPvs(ent.Comp.SparksSound, user);
            if (cell != null && cellDamage > netDamage)
                _battery.UseCharge(cell.Value.Owner, finalInt * 20f * Cm13CellRate, cell.Value.Comp);

            return true;
        }

        _damageable.TryChangeDamage(user, new DamageSpecifier(_prototype.Index(ShockDamageType), finalInt), origin: ent);
        Spawn(ent.Comp.SparksEffect, Transform(user).Coordinates);
        _audio.PlayPvs(ent.Comp.SparksSound, user);
        _popup.PopupEntity(Loc.GetString("rmc-apc-shock-mild-self"), user, user, PopupType.SmallCaution);
        _popup.PopupEntity(
            Loc.GetString("rmc-apc-shock-mild-others", ("target", user), ("source", ent)),
            user,
            Filter.PvsExcept(user),
            true,
            PopupType.SmallCaution);

        if (cell != null && cellDamage > netDamage)
            _battery.UseCharge(cell.Value.Owner, finalInt * 20f * Cm13CellRate, cell.Value.Comp);

        return true;
    }

    private int GetCellElectrocuteDamage(float charge)
    {
        return charge switch
        {
            >= 1_000_000f => Math.Min(_random.Next(50, 161), _random.Next(50, 161)),
            >= 200_000f => Math.Min(_random.Next(25, 81), _random.Next(25, 81)),
            >= 100_000f => Math.Min(_random.Next(20, 61), _random.Next(20, 61)),
            >= 50_000f => Math.Min(_random.Next(15, 41), _random.Next(15, 41)),
            >= 1_000f => Math.Min(_random.Next(10, 21), _random.Next(10, 21)),
            _ => 0,
        };
    }

    private int GetPowernetElectrocuteDamage(float avail)
    {
        return avail switch
        {
            >= 1_000_000f => Math.Min(_random.Next(50, 86), _random.Next(50, 86)),
            >= 200_000f => Math.Min(_random.Next(25, 76), _random.Next(25, 76)),
            >= 100_000f => Math.Min(_random.Next(20, 61), _random.Next(20, 61)),
            >= 50_000f => Math.Min(_random.Next(15, 41), _random.Next(15, 41)),
            >= 1_000f => Math.Min(_random.Next(10, 21), _random.Next(10, 21)),
            _ => 0,
        };
    }
}
