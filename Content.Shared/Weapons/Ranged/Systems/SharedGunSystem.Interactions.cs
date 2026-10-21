using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Weapons.Guns;
using Content.Shared.Actions;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    private void OnExamine(EntityUid uid, GunComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !component.ShowExamineText)
            return;

        if (HasComp<XenoComponent>(args.Examiner))
            return;

        using (args.PushGroup(nameof(GunComponent)))
        {
            args.PushMarkup(Loc.GetString("gun-selected-mode-examine", ("color", ModeExamineColor),
                ("mode", GetLocSelector(component.SelectedMode))));
            args.PushMarkup(Loc.GetString("gun-fire-rate-examine", ("color", FireRateExamineColor),
                ("fireRate", $"{component.FireRateModified:0.0}")));
        }
    }

    private string GetLocSelector(SelectiveFire mode)
    {
        return Loc.GetString($"gun-{mode.ToString()}");
    }

    private void OnAltVerb(EntityUid uid, GunComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract || args.Hands == null || component.SelectedMode == component.AvailableModes)
            return;

        if (HasComp<XenoComponent>(args.User))
            return;

        var nextMode = GetNextMode(component);

        AlternativeVerb verb = new()
        {
            Act = () => SelectFire(uid, component, nextMode, args.User),
            Text = Loc.GetString("gun-selector-verb", ("mode", GetLocSelector(nextMode))),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/fold.svg.192dpi.png")),
            Priority = 1, // Stories-GunCycle
        };

        args.Verbs.Add(verb);
    }

    public SelectiveFire GetNextMode(GunComponent component) //RMC14
    {
        var modes = new List<SelectiveFire>();

        foreach (var mode in Enum.GetValues<SelectiveFire>())
        {
            if ((mode & component.AvailableModes) == 0x0)
                continue;

            modes.Add(mode);
        }

        var index = modes.IndexOf(component.SelectedMode);
        return modes[(index + 1) % modes.Count];
    }

    public void SelectFire(EntityUid uid, GunComponent component, SelectiveFire fire, EntityUid? user = null)
    {
        if (component.SelectedMode == fire)
            return;

        // Stories-GunBalance-Start
        if (component.NextCycleMode > Timing.CurTime)
            return;
        // Stories-GunBalance-End

        DebugTools.Assert((component.AvailableModes  & fire) != 0x0);
        component.SelectedMode = fire;

        if (!Paused(uid))
        {
            var curTime = Timing.CurTime;
            var cooldown = TimeSpan.FromSeconds(InteractNextFire);

            if (component.NextFire < curTime)
                component.NextFire = curTime + cooldown;
            else
                component.NextFire += cooldown;
        }

        Audio.PlayPredicted(component.SoundMode, uid, user);
        Popup(Loc.GetString("gun-selected-mode", ("mode", GetLocSelector(fire))), uid, user);

        var ev = new RMCFireModeChangedEvent();
        RaiseLocalEvent(uid, ref ev);

        Dirty(uid, component);
    }

    /// <summary>
    /// Cycles the gun's <see cref="SelectiveFire"/> to the next available one.
    /// </summary>
    public void CycleFire(EntityUid uid, GunComponent component, EntityUid? user = null)
    {
        // Stories-GunBalance-Start
        if (component.NextCycleMode > Timing.CurTime)
            return;
        // Stories-GunBalance-End

        // Noop
        if (component.SelectedMode == component.AvailableModes)
            return;

        DebugTools.Assert((component.AvailableModes & component.SelectedMode) == component.SelectedMode);

        // Stories-GunBalance-Start
        var modes = new List<SelectiveFire>();
        foreach (var mode in Enum.GetValues<SelectiveFire>())
        {
            if ((mode & component.AvailableModes) == 0x0)
                continue;

            modes.Add(mode);
        }

        if (modes.Count > 1)
        {
            var index = modes.IndexOf(component.SelectedMode);
            var nextIndex = (index + 1) % modes.Count;
            var curTime = Timing.CurTime;

            var nextMode = modes[nextIndex];
            SelectFire(uid, component, nextMode, user);

            if (nextIndex == 0)
            {
                var cycleDelay = GunBalanceSystem.GunCycleModeDelay;
                component.NextCycleMode = curTime + cycleDelay;
                if (component.NextFire < curTime + cycleDelay)
                {
                    component.NextFire = curTime + cycleDelay;
                }
                Dirty(uid, component);

                var useDelay = EnsureComp<UseDelayComponent>(uid);
                _useDelay.SetLength((uid, useDelay), cycleDelay, GunBalanceSystem.GunCycleModeDelayId);
                _useDelay.TryResetDelay((uid, useDelay), false, GunBalanceSystem.GunCycleModeDelayId);
            }
            else
            {
                var debounceDelay = TimeSpan.FromSeconds(0.25);
                component.NextCycleMode = curTime + debounceDelay;
                Dirty(uid, component);
            }
            return;
        }
        // Stories-GunBalance-End

        var defaultNextMode = GetNextMode(component);
        SelectFire(uid, component, defaultNextMode, user);
    }

    // TODO: Actions need doing for guns anyway.
    private sealed partial class CycleModeEvent : InstantActionEvent
    {
        public SelectiveFire Mode = default;
    }

    private void OnCycleMode(EntityUid uid, GunComponent component, CycleModeEvent args)
    {
        SelectFire(uid, component, args.Mode, args.Performer);
    }

    private void OnGunSelected(EntityUid uid, GunComponent component, HandSelectedEvent args)
    {
        // Stories-GunBalance-Start
        if (Paused(uid))
            return;

        if (MetaData(uid).EntityLifeStage < EntityLifeStage.MapInitialized)
            return;
        // Stories-GunBalance-End

        // Stories-GunBalance-Start
        if (!HasDualPistols(uid))
        {
            var curTime = Timing.CurTime;
            var drawDelay = TagSystem.HasTag(uid, SidearmTag)
                ? GunBalanceSystem.SidearmDrawDelay
                : GunBalanceSystem.GunDrawDelay;
            var minimum = curTime + drawDelay;

            if (component.NextFire < minimum)
            {
                component.NextFire = minimum;
                component.LastFire = curTime;
                Dirty(uid, component);
            }

            var useDelay = EnsureComp<UseDelayComponent>(uid);
            _useDelay.SetLength((uid, useDelay), drawDelay, GunBalanceSystem.GunDrawDelayId);
            _useDelay.TryResetDelay((uid, useDelay), false, GunBalanceSystem.GunDrawDelayId);

            var shooter = EnsureComp<MarineShooterComponent>(args.User);
            if (shooter.NextGlobalFire < minimum)
            {
                shooter.NextGlobalFire = minimum;
                shooter.LastFiredGun = null;
                Dirty(args.User, shooter);
            }
        }
        // Stories-GunBalance-End

        if (Timing.ApplyingState)
            return;

        if (component.FireRateModified <= 0)
            return;

        var fireDelay = 1f / component.FireRateModified;
        if (fireDelay.Equals(0f))
            return;

        if (!component.ResetOnHandSelected)
            return;

        // If someone swaps to this weapon then reset its cd.
        var min = Timing.CurTime + TimeSpan.FromSeconds(fireDelay);

        if (min > component.NextFire)
        {
            component.NextFire = min;
            DirtyField(uid, component, nameof(GunComponent.NextFire));
        }
    }
}
