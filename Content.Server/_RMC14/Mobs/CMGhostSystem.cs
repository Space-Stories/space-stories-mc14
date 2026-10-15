using Content.Server.Ghost;
using Content.Shared._RMC14.Admin;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Mobs;
using Content.Shared._RMC14.PropCalling;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Overlays;
using Robust.Shared.Configuration;

namespace Content.Server._RMC14.Mobs
{
    public sealed class CMGhostSystem : SharedCMGhostSystem
    {
        [Dependency] private readonly SharedMarineSystem _marine = default!;
        [Dependency] private readonly SharedMindSystem _mind = default!;

        private bool _ghostsCanBoo;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<CMGhostComponent, ComponentStartup>(OnCMGhostStartup);
            SubscribeLocalEvent<CMGhostComponent, MapInitEvent>(OnCMGhostInit, after: [typeof(GhostSystem)]);

            SubscribeLocalEvent<CMGhostComponent, ToggleMarineHudActionEvent>(OnMarineHudAction);
            SubscribeLocalEvent<CMGhostComponent, ToggleXenoHudActionEvent>(OnXenoHudAction);

            SubscribeLocalEvent<MindAddedMessage>(OnMindAdded);
            SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);

            Subs.CVar(Config, RMCCVars.RMCGhostCanBoo, OnGhostBooChange, true);
        }

        private void OnCMGhostStartup(EntityUid uid, CMGhostComponent comp, ComponentStartup args)
        {
            Actions.AddAction(uid, ref comp.ToggleMarineHudEntity, comp.ToggleMarineHud);
            Actions.AddAction(uid, ref comp.ToggleXenoHudEntity, comp.ToggleXenoHud);
            Actions.AddAction(uid, ref comp.ToggleDeadChatEntity, comp.ToggleDeadChat);
            Actions.AddAction(uid, ref comp.FindParasiteEntity, comp.FindParasite);

            EnsureComp<ShowMarineIconsComponent>(uid);
            var bars = EnsureComp<ShowHealthBarsComponent>(uid);
            bars.DamageContainers.Add("Biological");
            EnsureComp<ShowHealthIconsComponent>(uid);
            EnsureComp<CMGhostXenoHudComponent>(uid);
            EnsureComp<PropCallingComponent>(uid);
        }

        private void OnMarineHudAction(EntityUid uid, CMGhostComponent comp, ToggleMarineHudActionEvent args)
        {
            args.Handled = true;

            if (HasComp<ShowMarineIconsComponent>(uid))
            {
                RemComp<ShowMarineIconsComponent>(uid);
                RemCompDeferred<ShowHealthIconsComponent>(uid);
                RemCompDeferred<ShowHealthBarsComponent>(uid);
                Actions.SetToggled(comp.ToggleMarineHudEntity, true);
            }
            else
            {
                EnsureComp<ShowHealthIconsComponent>(uid);

                _marine.GiveMarineHud(uid, null, true);

                var bars = EnsureComp<ShowHealthBarsComponent>(uid);
                bars.DamageContainers.Add("Biological");

                Actions.SetToggled(comp.ToggleMarineHudEntity, false);
            }
        }
        private void OnXenoHudAction(EntityUid uid, CMGhostComponent comp, ToggleXenoHudActionEvent args)
        {
            args.Handled = true;

            if (HasComp<CMGhostXenoHudComponent>(uid))
            {
                RemComp<CMGhostXenoHudComponent>(uid);
                Actions.SetToggled(comp.ToggleXenoHudEntity, true);
            }
            else
            {
                AddComp<CMGhostXenoHudComponent>(uid);
                Actions.SetToggled(comp.ToggleXenoHudEntity, false);
            }
        }

        private void OnMindAdded(MindAddedMessage args)
        {
            if (!HasComp<GhostComponent>(args.Container) &&
                TryComp(args.Container, out MobStateComponent? mobState) &&
                mobState.CurrentState != MobState.Dead)
            {
                args.Mind.Comp.TimeOfDeath = null;
            }
        }

        private void OnMobStateChanged(MobStateChangedEvent args)
        {
            if (!_mind.TryGetMind(args.Target, out _, out var mind))
                return;

            if (args.NewMobState == MobState.Dead && args.OldMobState != MobState.Dead)
            {
                mind.TimeOfDeath = GameTiming.RealTime;
            }
            else if (args.OldMobState == MobState.Dead && args.NewMobState != MobState.Dead)
            {
                mind.TimeOfDeath = null;
            }
        }

        private void OnCMGhostInit(Entity<CMGhostComponent> cmghost, ref MapInitEvent args)
        {
            if (TryComp<GhostComponent>(cmghost, out var ghost))
                ChangeGhostBoo((cmghost, ghost));
        }

        private void OnGhostBooChange(bool value, in CVarChangeInfo info)
        {
            _ghostsCanBoo = value;
            var query = EntityQueryEnumerator<GhostComponent>();
            while (query.MoveNext(out var uid, out var ghost))
            {
                ChangeGhostBoo((uid, ghost));
            }
        }

        public void SetPostDeathChatMute(EntityUid ghostUid, Entity<MindComponent> mind, EntityUid? sourceBody)
        {
            if (sourceBody is { } body && HasComp<RMCAdminSpawnedComponent>(body))
                return;

            if (mind.Comp.TimeOfDeath is not { } timeOfDeath)
                return;

            if (!TryComp(ghostUid, out CMGhostComponent? ghost))
                return;

            var duration = TimeSpan.FromSeconds(Math.Max(0, Config.GetCVar(RMCCVars.RMCPostDeathChatMuteTimeSeconds)));
            var elapsed = GameTiming.RealTime - timeOfDeath;
            var remaining = duration - (elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero);
            if (remaining <= TimeSpan.Zero)
                return;

            SetPostDeathChatMutedUntil((ghostUid, ghost), GameTiming.CurTime + remaining);
        }

        private void ChangeGhostBoo(Entity<GhostComponent> ghost)
        {
            if (_ghostsCanBoo)
            {
                Actions.AddAction(ghost.Owner, ref ghost.Comp.BooActionEntity, ghost.Comp.BooAction);
            }
            else
            {
                Actions.RemoveAction(ghost.Owner, ghost.Comp.BooActionEntity);
            }
        }
    }
}
