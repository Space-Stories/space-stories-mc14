using Content.Server.Forensics;
using Content.Shared._Stories.Blood;
using Content.Shared.Chemistry;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._Stories.Blood;

public sealed class BloodCleaningSystem : EntitySystem
{
    private static readonly SpriteSpecifier VerbIcon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/bubbles.svg.192dpi.png"));

    [Dependency] private readonly BloodStainSystem _bloodStain = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodStainComponent, GetVerbsEvent<UtilityVerb>>(OnStainUtilityVerb);
        SubscribeLocalEvent<BloodyComponent, GetVerbsEvent<UtilityVerb>>(OnBloodyUtilityVerb);
        SubscribeLocalEvent<BloodStainComponent, BloodCleanDoAfterEvent>(OnStainCleanDoAfter);
        SubscribeLocalEvent<BloodyComponent, BloodCleanDoAfterEvent>(OnBloodyCleanDoAfter);
        SubscribeLocalEvent<BloodStainComponent, ReactionEntityEvent>(OnStainReaction);
        SubscribeLocalEvent<BloodyComponent, ReactionEntityEvent>(OnBloodyReaction);
    }

    private void OnStainUtilityVerb(Entity<BloodStainComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (ent.Comp.Color == null)
            return;

        AddCleanVerb(ref args, ent.Comp.CleanDelay);
    }

    private void OnBloodyUtilityVerb(Entity<BloodyComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!_bloodStain.IsWearerBloody(ent.AsNullable()))
            return;

        AddCleanVerb(ref args, ent.Comp.CleanDelay);
    }

    private void AddCleanVerb(ref GetVerbsEvent<UtilityVerb> args, TimeSpan delay)
    {
        if (!args.CanInteract || !args.CanAccess || !HasComp<CleansForensicsComponent>(args.Using))
            return;

        var user = args.User;
        var target = args.Target;
        var used = args.Using.Value;
        args.Verbs.Add(new UtilityVerb
        {
            Act = () => StartCleaning(user, target, used, delay),
            Icon = VerbIcon,
            Text = Loc.GetString("stories-blood-stain-clean-verb"),
            DoContactInteraction = false,
        });
    }

    private void StartCleaning(EntityUid user, EntityUid target, EntityUid used, TimeSpan delay)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, delay, new BloodCleanDoAfterEvent(), target, target, used)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("stories-blood-stain-clean-start", ("target", target)), user, user);
    }

    private void OnStainCleanDoAfter(Entity<BloodStainComponent> ent, ref BloodCleanDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        if (_bloodStain.TryClean(ent))
            _popup.PopupEntity(Loc.GetString("stories-blood-stain-clean-finish", ("target", ent.Owner)), args.User, args.User);
    }

    private void OnBloodyCleanDoAfter(Entity<BloodyComponent> ent, ref BloodCleanDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        if (_bloodStain.CleanWearer(ent.AsNullable()))
            _popup.PopupEntity(Loc.GetString("stories-blood-stain-clean-finish", ("target", ent.Owner)), args.User, args.User);
    }

    private void OnStainReaction(Entity<BloodStainComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch || !_bloodStain.IsCleaningReagent(ent, args.Reagent.ID))
            return;

        _bloodStain.TryClean(ent);
    }

    private void OnBloodyReaction(Entity<BloodyComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch || !ent.Comp.CleaningReagents.Contains(args.Reagent.ID))
            return;

        _bloodStain.CleanWearer(ent.AsNullable());
    }
}
