using Content.Shared._Stories.Xenonids.Predalien.FeralFrenzy;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Robust.Shared.Utility;

namespace Content.Shared._Stories.Xenonids.Predalien.ToggleGutTargeting;

public sealed class STToggleGutTargetingSystem : EntitySystem
{
    private static readonly SpriteSpecifier.Rsi SingleIcon = new(
        new ResPath("/Textures/_Stories/Actions/predalien_actions.rsi"), "toggle_gut_targeting");

    private static readonly SpriteSpecifier.Rsi AoeIcon = new(
        new ResPath("/Textures/_Stories/Actions/predalien_actions.rsi"), "toggle_gut_targeting_aoe");

    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STFeralFrenzyComponent, STToggleGutTargetingActionEvent>(OnToggle);
    }

    private void OnToggle(Entity<STFeralFrenzyComponent> xeno, ref STToggleGutTargetingActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var switchingToAoe = xeno.Comp.Targeting == STFeralFrenzyTargeting.Single;
        xeno.Comp.Targeting = switchingToAoe ? STFeralFrenzyTargeting.Aoe : STFeralFrenzyTargeting.Single;
        Dirty(xeno);

        var message = switchingToAoe
            ? Loc.GetString("st-predalien-toggle-gut-targeting-aoe")
            : Loc.GetString("st-predalien-toggle-gut-targeting-single");

        _popup.PopupClient(message, xeno, xeno, PopupType.Small);
        _actions.SetIcon(args.Action.AsNullable(), switchingToAoe ? AoeIcon : SingleIcon);
    }
}
