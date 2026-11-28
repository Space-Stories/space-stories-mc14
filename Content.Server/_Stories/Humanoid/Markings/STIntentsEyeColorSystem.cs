using Content.Server.Humanoid;
using Content.Server.Humanoid.Systems;
using Content.Shared._Stories.Humanoid.Markings;
using Content.Shared.CombatMode;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Stories.Humanoid.Markings;

public sealed class STIntentsEyeColorSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STIntentsEyeColorComponent, MapInitEvent>(OnMapInit,
            after: new[] { typeof(RandomHumanoidAppearanceSystem) });

        SubscribeLocalEvent<STIntentsEyeColorComponent, ToggleCombatActionEvent>(OnCombatModeChanged,
            after: new[] { typeof(SharedCombatModeSystem) }
        );

        SubscribeLocalEvent<STIntentsEyeColorComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMapInit(Entity<STIntentsEyeColorComponent> ent, ref MapInitEvent args)
    {
        var color = GetCombatModeColor(ent);
        SetEyeColor(ent.Owner, color);
    }

    private void OnCombatModeChanged(Entity<STIntentsEyeColorComponent> ent, ref ToggleCombatActionEvent args)
    {
        if (_mobState.IsDead(ent.Owner))
            return;

        var color = GetCombatModeColor(ent);
        SetEyeColor(ent.Owner, color);
    }

    private void OnMobStateChanged(Entity<STIntentsEyeColorComponent> ent, ref MobStateChangedEvent args)
    {
        if (_mobState.IsDead(ent.Owner))
        {
            SetEyeColor(ent.Owner, ent.Comp.DeadEyeColor);
        }
        else
        {
            var color = GetCombatModeColor(ent);
            SetEyeColor(ent.Owner, color);
        }
    }

    private Color GetCombatModeColor(Entity<STIntentsEyeColorComponent> ent)
    {
        var modeColor = _combatMode.IsInCombatMode(ent) switch
        {
            true => ent.Comp.EyeColorHarm,
            false => ent.Comp.EyeColorHelp,
        };

        return modeColor;
    }

    public void SetEyeColor(EntityUid uid, Color color)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return;

        humanoid.EyeColor = color;
        Dirty(uid, humanoid);
    }
}
