using System.Linq;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Construction.Prototypes;
using Content.Shared.Stacks;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Construction;

[UsedImplicitly]
public sealed class RMCConstructionBui : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly SpriteSystem _sprite;

    private RMCConstructionWindow? _window;
    private ProtoId<RMCConstructionPrototype>[]? _currentList;
    private string _searchQuery = string.Empty;

    public RMCConstructionBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _sprite = EntMan.System<SpriteSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<RMCConstructionWindow>();
        _window.BackButton.OnPressed += _ =>
        {
            _currentList = null;
            Populate();
        };
        _window.SearchEdit.OnTextChanged += args =>
        {
            _searchQuery = args.Text;
            Populate();
        };

        Populate();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        Populate();
    }

    private void Populate()
    {
        if (_window == null)
            return;

        if (!EntMan.TryGetComponent(Owner, out RMCConstructionItemComponent? construction))
            return;

        var stackCount = EntMan.TryGetComponent(Owner, out StackComponent? stack) ? stack.Count : 0;
        var materialName = EntMan.TryGetComponent(Owner, out MetaDataComponent? meta) ? meta.EntityName : string.Empty;
        _window.MaterialLabel.Text = $"{materialName}: {stackCount}";

        Texture? materialIcon = null;
        if (meta?.EntityPrototype != null)
            materialIcon = _sprite.GetPrototypeIcon(meta.EntityPrototype).GetFrame(RsiDirection.South, 0);

        _window.MaterialIcon.Texture = materialIcon;
        _window.MaterialIcon.Visible = materialIcon != null;
        _window.BackButton.Visible = _currentList != null;

        _window.ConstructionContainer.DisposeAllChildren();

        var items = _currentList ?? construction.Buildable;
        if (items == null)
            return;

        var filtering = !string.IsNullOrWhiteSpace(_searchQuery);

        foreach (var buildId in items)
        {
            if (!_prototype.TryIndex(buildId, out var build))
                continue;

            if (build.IsDivider)
            {
                if (filtering)
                    continue;

                var divider = new PanelContainer
                {
                    MinHeight = 2,
                    Margin = new Thickness(4, 6, 4, 6),
                    PanelOverride = new StyleBoxFlat
                    {
                        BackgroundColor = Color.FromHex("#3A3A3A"),
                    },
                };
                _window.ConstructionContainer.AddChild(divider);
                continue;
            }

            if (filtering && !build.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
                continue;

            Texture? icon = null;
            if (_prototype.TryIndex<EntityPrototype>(build.Prototype, out var entProto))
                icon = _sprite.GetPrototypeIcon(entProto).GetFrame(RsiDirection.South, 0);

            var isSubmenu = build.Listed is { Length: > 0 };
            var canAfford = isSubmenu || stackCount >= build.MaterialCost;
            var costText = isSubmenu
                ? "▸"
                : build.Amount > 1
                    ? $"{build.Amount}x ({build.MaterialCost})"
                    : $"{build.MaterialCost}";

            var control = new RMCBuildChoiceControl();
            control.Set(build.Name, icon, costText, canAfford);

            if (isSubmenu)
            {
                var subList = build.Listed!;
                control.Button.OnPressed += _ =>
                {
                    _currentList = subList;
                    Populate();
                };
            }
            else
            {
                var capturedId = build.ID;
                var capturedAmount = build.Amount;
                control.Button.OnPressed += _ => SendPredictedMessage(new RMCConstructionBuiMsg(capturedId, capturedAmount));

                if (build.StackAmounts is { Count: > 0 })
                {
                    control.StackAmountContainer.Visible = true;
                    foreach (var amount in build.StackAmounts)
                    {
                        var capturedStackAmount = amount;
                        var stackBtn = new Button
                        {
                            Text = $"x{amount}",
                            Margin = new Thickness(0, 0, 4, 0),
                            Disabled = stackCount < build.MaterialCost * amount,
                        };
                        stackBtn.OnPressed += _ => SendPredictedMessage(new RMCConstructionBuiMsg(capturedId, capturedStackAmount));
                        control.StackAmountContainer.AddChild(stackBtn);
                    }
                }
            }

            _window.ConstructionContainer.AddChild(control);
        }
    }
}
