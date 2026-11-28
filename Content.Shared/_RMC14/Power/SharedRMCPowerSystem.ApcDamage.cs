using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.ClawSharpness;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Wires;

namespace Content.Shared._RMC14.Power;

public abstract partial class SharedRMCPowerSystem
{
    private void OnApcBeforeDamageChanged(Entity<RMCApcComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled ||
            !args.Damage.AnyPositive() ||
            args.Origin is not { } attacker ||
            args.Source != attacker ||
            !HasComp<XenoComponent>(attacker) ||
            !HasComp<XenoClawsComponent>(attacker))
        {
            return;
        }

        args.Cancelled = true;
        if (_net.IsClient)
            return;

        if (ent.Comp.Broken)
        {
            _popup.PopupEntity(Loc.GetString("rmc-apc-xeno-already-broken", ("apc", ent)), ent, attacker);
            return;
        }

        if (ent.Comp.XenoHits >= ent.Comp.XenoHitsToCutWires &&
            (!TryComp(attacker, out RMCSizeComponent? size) || size.Size < ent.Comp.XenoElectronicsDamageSize))
        {
            _popup.PopupEntity(Loc.GetString("rmc-apc-xeno-too-small", ("apc", ent)), ent, attacker);
            return;
        }

        _audio.PlayPvs(ent.Comp.XenoAttackSound, ent);
        if (ent.Comp.XenoHits >= ent.Comp.XenoHitsToCutWires)
        {
            BreakApc(ent);
            _popup.PopupEntity(Loc.GetString("rmc-apc-xeno-electronics-destroyed", ("apc", ent)), ent, PopupType.MediumCaution);
            return;
        }

        if (ent.Comp.WiresExposed)
        {
            if (!TryApcElectrocute(ent, attacker, probability: 1f, siemensCoeff: 1f))
                SpawnApcSparks(ent);

            SetApcWiresCut(ent, attacker, true);
            ent.Comp.XenoHits = ent.Comp.XenoHitsToCutWires;
            ent.Comp.XenoWiresDamaged = true;
            SetAllApcChannels(ent, false);
            ToUpdate.Add(ent);
            _popup.PopupEntity(Loc.GetString("rmc-apc-xeno-wires-cut", ("apc", ent)), ent, PopupType.MediumCaution);
            return;
        }

        if (ent.Comp.XenoHits >= _random.Next(ent.Comp.XenoExposeWiresMinHits, ent.Comp.XenoExposeWiresMaxHits + 1) &&
            TryComp(ent, out WiresPanelComponent? panel))
        {
            if (_wires.TogglePanel(ent, panel, true))
                _popup.PopupEntity(Loc.GetString("rmc-apc-xeno-wires-exposed", ("apc", ent)), ent, PopupType.MediumCaution);
        }
        else
        {
            ent.Comp.XenoHits++;
        }
    }

    private void ResetApcXenoDamage(Entity<RMCApcComponent> ent)
    {
        ent.Comp.XenoHits = 0;
        ent.Comp.XenoWiresDamaged = false;
    }

    protected virtual void SetApcWiresCut(Entity<RMCApcComponent> ent, EntityUid user, bool cut)
    {
    }

    protected virtual void SpawnApcSparks(Entity<RMCApcComponent> ent)
    {
    }

    protected virtual bool TryApcElectrocute(Entity<RMCApcComponent> ent, EntityUid user, float probability = 0.5f, float siemensCoeff = 1f)
    {
        return false;
    }
}
