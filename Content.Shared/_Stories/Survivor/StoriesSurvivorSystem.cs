using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Survivor;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Stories.Survivor;

public sealed class StoriesSurvivorSystem : EntitySystem
{
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<IntelRescueSurvivorObjectiveComponent, MapInitEvent>(OnSurvivorInit);
        SubscribeLocalEvent<IntelRescueSurvivorObjectiveComponent, ComponentRemove>(OnRescueObjectiveRemoved);
        SubscribeLocalEvent<StoriesSurvivorDebuffImmuneComponent, MapInitEvent>(OnImmuneInit);
    }

    private void OnSurvivorInit(Entity<IntelRescueSurvivorObjectiveComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<StoriesSurvivorDebuffImmuneComponent>(ent))
            return;

        if (!TryComp<SkillsComponent>(ent, out var skills))
            return;

        var firearmsSkill = _skills.GetSkill(ent.Owner, "RMCSkillFirearms");

        var debuff = EnsureComp<StoriesSurvivorDebuffComponent>(ent);
        debuff.OriginalFirearms = firearmsSkill;

        _skills.SetSkill(ent.Owner, "RMCSkillFirearms", Math.Max(0, firearmsSkill - 1));
    }

    private void OnImmuneInit(Entity<StoriesSurvivorDebuffImmuneComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<StoriesSurvivorDebuffComponent>(ent, out var debuff))
            return;

        _skills.SetSkill(ent.Owner, "RMCSkillFirearms", debuff.OriginalFirearms);
        RemComp<StoriesSurvivorDebuffComponent>(ent);
    }

    private void OnRescueObjectiveRemoved(Entity<IntelRescueSurvivorObjectiveComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp<StoriesSurvivorDebuffComponent>(ent, out var debuff) || debuff.RestoreTime != null)
            return;

        debuff.RestoreTime = _timing.CurTime + TimeSpan.FromMinutes(10);
        Dirty(ent, debuff);

        _popup.PopupEntity(Loc.GetString("stories-survivor-rescue-started"), ent, ent, PopupType.Large);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<StoriesSurvivorDebuffComponent, TransformComponent>();
        var curTime = _timing.CurTime;

        while (query.MoveNext(out var uid, out var debuff, out var xform))
        {
            var isOnShip = HasComp<AlmayerComponent>(xform.MapUid) || HasComp<AlmayerComponent>(xform.GridUid);

            if (isOnShip)
            {
                if (debuff.RestoreTime == null && !HasComp<IntelRescueSurvivorObjectiveComponent>(uid))
                {
                    debuff.RestoreTime = curTime + TimeSpan.FromMinutes(10);
                    Dirty(uid, debuff);
                    _popup.PopupEntity(Loc.GetString("stories-survivor-recovery-resumed"), uid, uid, PopupType.Large);
                }
                else if (debuff.RestoreTime != null && curTime >= debuff.RestoreTime)
                {
                    _skills.SetSkill(uid, "RMCSkillFirearms", debuff.OriginalFirearms);

                    _popup.PopupEntity(Loc.GetString("stories-survivor-rescued"), uid, uid, PopupType.Large);

                    RemComp<StoriesSurvivorDebuffComponent>(uid);
                }
            }
            else
            {
                if (debuff.RestoreTime != null)
                {
                    debuff.RestoreTime = null;
                    Dirty(uid, debuff);
                    _popup.PopupEntity(Loc.GetString("stories-survivor-recovery-interrupted"), uid, uid, PopupType.LargeCaution);
                }
            }
        }
    }
}
