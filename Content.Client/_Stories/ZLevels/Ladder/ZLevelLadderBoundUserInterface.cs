using Content.Client.UserInterface.Controls;
using Content.Shared._Stories.ZLevels.Ladder;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._Stories.ZLevels.Ladder;

public sealed class ZLevelLadderBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();
        UpdateMenu();
    }

    private void UpdateMenu()
    {
        if (!EntMan.TryGetComponent<ZLevelLadderComponent>(Owner, out var ladder))
            return;

        if (_menu != null)
        {
            _menu.OnClose -= OnMenuClosed;
            _menu.Close();
            _menu = null;
        }

        var options = new List<RadialMenuOption>();

        if (ladder.HasUp)
        {
            var upSprite = new SpriteSpecifier.Rsi(new ResPath("_Stories/Interface/ladders.rsi"), "up");
            options.Add(new RadialMenuActionOption<int>(_ =>
            {
                SendMessage(new ZLevelLadderClimbBuiMessage(true));
                Close();
            }, 0)
            {
                ToolTip = Loc.GetString("stories-zlevel-ladder-radial-up"),
                Sprite = upSprite,
            });
        }

        if (ladder.HasDown)
        {
            var downSprite = new SpriteSpecifier.Rsi(new ResPath("_Stories/Interface/ladders.rsi"), "down");
            options.Add(new RadialMenuActionOption<int>(_ =>
            {
                SendMessage(new ZLevelLadderClimbBuiMessage(false));
                Close();
            }, 0)
            {
                ToolTip = Loc.GetString("stories-zlevel-ladder-radial-down"),
                Sprite = downSprite,
            });
        }

        if (options.Count == 0)
            return;

        _menu = new SimpleRadialMenu();
        _menu.Track(Owner);
        _menu.SetButtons(options);
        _menu.OpenOverMouseScreenPosition();
        _menu.OnClose += OnMenuClosed;
    }

    private void OnMenuClosed()
    {
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        if (_menu != null)
        {
            _menu.OnClose -= OnMenuClosed;
            _menu.Close();
            _menu = null;
        }
    }
}
