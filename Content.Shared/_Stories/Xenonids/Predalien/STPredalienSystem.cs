using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Stab;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stories.Xenonids.Predalien;

public sealed class STPredalienSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STPredalienComponent, ComponentStartup>(OnPredalienStartup);
        SubscribeLocalEvent<STPredalienLarvaComponent, ComponentStartup>(OnPredalienLarvaStartup);
        SubscribeLocalEvent<STPredalienComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<STPredalienComponent, RMCGetTailStabBonusDamageEvent>(OnGetTailStabBonusDamage);
        SubscribeLocalEvent<STPredalienComponent, ExaminedEvent>(OnPredalienExamine);
        SubscribeLocalEvent<HunterComponent, BeforeDamageChangedEvent>(OnHunterBeforeDamageChanged);
        SubscribeLocalEvent<MobStateComponent, DamageChangedEvent>(OnVictimDamageChanged,
            after: new[] { typeof(MobThresholdSystem) });
        SubscribeLocalEvent<STPredalienKillCreditedComponent, MobStateChangedEvent>(OnKillCreditedMobStateChanged);
    }

    private void OnPredalienStartup(Entity<STPredalienComponent> ent, ref ComponentStartup args)
    {
        MarkDishonored(ent.Owner);

        RaiseLocalEvent(new STAbominationSpawnedEvent(ent.Owner));
    }

    private void OnPredalienLarvaStartup(Entity<STPredalienLarvaComponent> ent, ref ComponentStartup args)
    {
        MarkDishonored(ent.Owner);
    }

    private void MarkDishonored(EntityUid uid)
    {
        var marked = EnsureComp<HunterMarkedComponent>(uid);
        marked.Marks |= HunterMarkType.Dishonored;
        marked.DishonoredReason = Loc.GetString("st-predalien-dishonored-reason");
        Dirty(uid, marked);
    }

    private void OnGetMeleeDamage(Entity<STPredalienComponent> predalien, ref GetMeleeDamageEvent args)
    {
        var kills = Math.Min(predalien.Comp.Kills, predalien.Comp.MaxKills);
        if (kills > 0 && _proto.TryIndex<DamageGroupPrototype>("Brute", out var brute))
            args.Damage += new DamageSpecifier(brute, predalien.Comp.DamagePerKill * kills);
    }

    private void OnGetTailStabBonusDamage(Entity<STPredalienComponent> predalien, ref RMCGetTailStabBonusDamageEvent args)
    {
        var kills = Math.Min(predalien.Comp.Kills, predalien.Comp.MaxKills);
        if (kills > 0 && _proto.TryIndex<DamageGroupPrototype>("Brute", out var brute))
            args.Damage += new DamageSpecifier(brute, predalien.Comp.DamagePerKill * kills);
    }

    private void OnPredalienExamine(Entity<STPredalienComponent> predalien, ref ExaminedEvent args)
    {
        if (!HasComp<XenoComponent>(args.Examiner))
            return;

        using (args.PushGroup(nameof(STPredalienComponent)))
        {
            args.PushMarkup(Loc.GetString("st-predalien-kills-examine",
                ("xeno", predalien.Owner), ("amount", predalien.Comp.Kills), ("max", predalien.Comp.MaxKills)));
        }
    }

    private void OnHunterBeforeDamageChanged(Entity<HunterComponent> hunter, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin is not { } origin || !TryComp<STPredalienComponent>(origin, out var predalien))
            return;

        args.Damage *= predalien.HunterDamageMultiplier;
    }

    private void OnVictimDamageChanged(EntityUid victim, MobStateComponent victimComp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } origin)
            return;

        if (!HasComp<HumanoidAppearanceComponent>(victim))
            return;

        if (HasComp<STPredalienKillCreditedComponent>(victim))
            return;

        if (!TryComp<STPredalienComponent>(origin, out var predalien))
            return;

        if (!_mobState.IsDead(victim, victimComp))
            return;

        EnsureComp<STPredalienKillCreditedComponent>(victim);

        if (predalien.Kills >= predalien.MaxKills)
            return;

        predalien.Kills++;
        Dirty(origin, predalien);
    }

    private void OnKillCreditedMobStateChanged(Entity<STPredalienKillCreditedComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.OldMobState != MobState.Dead || args.NewMobState == MobState.Dead)
            return;

        RemCompDeferred<STPredalienKillCreditedComponent>(ent);
    }
}
