using System.Linq;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RMC14.Requisitions;

public sealed partial class RequisitionsBui
{
    private RequisitionPresetStore? _presets;
    private string? _selectedPreset;
    private string _newPresetName = string.Empty;
    private int _presetRevision = -1;
    private string? _presetFeedback;
    private readonly Dictionary<string, CartKey> _presetProducts = new();
    private int _presetCatalogRevision = -1;

    private void InitializePresets()
    {
        if (_window == null)
            return;
        _presets = EntMan.System<RequisitionPresetsSystem>().GetStore();
        _presets?.Reload();
        var view = _window.PresetsTab;
        TabContainer.SetTabTitle(view, Loc.GetString("rmc-requisitions-presets-tab"));
        view.PresetName.IsValid = text => text.Length <= RequisitionPresetStore.MaxNameLength;
        view.PresetName.OnTextChanged += _ =>
        {
            if (_selectedPreset == null && _importedPreset == null)
                _newPresetName = view.PresetName.Text;
            _presetFeedback = null;
            ResetPresetConfirmation();
            RefreshPresets();
        };
        view.Selection.OnItemSelected += args =>
        {
            if (_clipboardBusy || _importedPreset != null || _presets == null ||
                _presetRevision != _presets.Revision ||
                args.Id < 0 || args.Id > _presets.Presets.Count)
            {
                RefreshPresets();
                return;
            }
            if (_selectedPreset == null)
                _newPresetName = view.PresetName.Text;
            view.Selection.SelectId(args.Id);
            var preset = args.Id > 0 ? _presets.Presets[args.Id - 1] : null;
            _selectedPreset = preset?.Id;
            view.PresetName.Text = preset?.Name ?? _newPresetName;
            _presetFeedback = null;
            ResetPresetConfirmation();
            RefreshPresets();
        };
        view.Load.OnPressed += _ => LoadPreset();
        view.ReplaceCart.OnPressed += _ => LoadPreset();
        view.SaveNew.OnPressed += _ => SavePreset(false);
        view.Update.OnPressed += _ => SavePreset(true);
        view.Rename.OnPressed += _ => RenamePreset();
        view.Delete.OnPressed += _ =>
        {
            if (_presets == null || !RequestOnly || _selectedPreset is not { } id ||
                _submittingRequest || _clipboardBusy || _importedPreset != null)
                return;
            _presetFeedback = null;
            if (_presets.Delete(id))
            {
                _selectedPreset = null;
                view.PresetName.Text = _newPresetName;
                _presetFeedback = "rmc-requisitions-presets-deleted";
            }
            ResetPresetConfirmation();
            RefreshPresets();
        };
        InitializePresetClipboard();
    }

    private RequisitionPreset? SelectedPreset =>
        _presets?.Presets.FirstOrDefault(preset => preset.Id == _selectedPreset);

    private void ResetPresetConfirmation()
    {
        if (_window == null)
            return;
        _window.PresetsTab.ReplaceCart.ResetConfirmation();
        _window.PresetsTab.Update.ResetConfirmation();
        _window.PresetsTab.Delete.ResetConfirmation();
    }

    private void RefreshPresets()
    {
        if (_window == null)
            return;
        var view = _window.PresetsTab;
        TabContainer.SetTabVisible(view, RequestOnly);
        _window.RequestReasonPanel.Visible = RequestOnly &&
                                             _window.ShopTabs.CurrentTab != view.GetPositionInParent();
        if (!RequestOnly)
            return;

        if (_presetCatalogRevision != _supplyCatalog.Revision)
        {
            ResetPresetConfirmation();
            _presetCatalogRevision = _supplyCatalog.Revision;
            _presetProducts.Clear();
            foreach (var product in _supplyCatalog.Items)
            {
                if (TryGetEntry(product.Key, out var entry))
                    _presetProducts.TryAdd(RequisitionPresetStore.GetKey(entry), product.Key);
            }
        }

        var importing = _importedPreset != null;
        if (!importing && _selectedPreset != null && SelectedPreset == null)
        {
            _selectedPreset = null;
            view.PresetName.Text = _newPresetName;
            _presetRevision = -1;
            ResetPresetConfirmation();
        }
        if (_presets != null && _presetRevision != _presets.Revision)
        {
            ResetPresetConfirmation();
            _presetRevision = _presets.Revision;
            view.Selection.Clear();
            view.Selection.AddItem(Loc.GetString(importing
                ? "rmc-requisitions-presets-import-preview" : "rmc-requisitions-presets-new"), 0);
            var selected = 0;
            for (var i = 0; i < _presets.Presets.Count; i++)
            {
                var preset = _presets.Presets[i];
                view.Selection.AddItem(preset.Name, i + 1);
                if (!importing && preset.Id == _selectedPreset)
                    selected = i + 1;
            }
            view.Selection.SelectId(selected);
        }

        var current = SelectedPreset;
        var creating = !importing && current == null;
        var cartPreset = CreatePresetFromCart();
        var name = view.PresetName.Text.Trim();
        var validName = RequisitionPresetStore.ValidName(name);
        var duplicateName = _presets?.Presets.Any(preset =>
            string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (importing || preset.Id != current?.Id)) == true;
        var writable = _presets != null && _presets.Error != "rmc-requisitions-presets-read-error" &&
                       !_submittingRequest && !_clipboardBusy;
        var canSave = writable && !importing && validName && !duplicateName && cartPreset != null;
        var unchanged = current != null && cartPreset != null && SamePresetContents(current, cartPreset);
        SetText(view.Help, Loc.GetString(importing ? "rmc-requisitions-presets-import-review" : creating
            ? "rmc-requisitions-presets-new-help" : "rmc-requisitions-presets-help"));
        view.Composition.Text = Loc.GetString(creating
            ? "rmc-requisitions-presets-cart-composition" : "rmc-requisitions-presets-composition");
        view.Selection.Disabled = _submittingRequest || _clipboardBusy || importing;
        view.PresetName.Editable = !_submittingRequest && !_clipboardBusy;
        view.Rename.Visible = !importing && current != null;
        view.ImportActions.Visible = importing;
        view.LoadActions.Visible = !importing && current != null;
        view.SaveActions.Visible = !importing;
        var full = _presets?.Presets.Count >= RequisitionPresetStore.MaxPresets;
        view.ImportClipboard.Disabled = !writable || full || importing;
        view.ImportClipboard.ToolTip = Loc.GetString(full
            ? "rmc-requisitions-presets-full" : "rmc-requisitions-presets-import-tooltip");
        view.ExportClipboard.Disabled = current == null || _submittingRequest || _clipboardBusy || importing;
        view.ConfirmImport.Disabled = !writable || !validName || duplicateName || full;
        view.CancelImport.Disabled = _submittingRequest || _clipboardBusy;
        view.SaveNew.Visible = creating;
        view.SaveNew.Disabled = !creating || !canSave || full;
        view.Update.Visible = !importing && current != null;
        SetPresetActionDisabled(view.Update, !canSave || current == null || unchanged && name == current.Name);
        view.Rename.Disabled = !writable || !validName || duplicateName || current == null || name == current.Name;
        SetPresetActionDisabled(view.Delete, !writable || current == null || importing);
        var resolved = ResolvePreset(_importedPreset ?? current ?? cartPreset,
            out var contents, out var missing, out var resolvedCart);
        view.Load.Disabled = current == null || !resolved || _submittingRequest || _clipboardBusy || importing ||
                             CartMatches(resolvedCart, current.Reason);
        SetPresetActionDisabled(view.ReplaceCart, view.Load.Disabled);
        view.Load.Visible = !importing && current != null && _cart.Count == 0 && missing == 0;
        view.ReplaceCart.Visible = !importing && current != null && !view.Load.Visible;
        view.ReplaceCart.Text = Loc.GetString(missing > 0
            ? "rmc-requisitions-presets-load-available" : "rmc-requisitions-presets-load");
        view.ReplaceCart.ConfirmationText = Loc.GetString(missing > 0
            ? "rmc-requisitions-presets-skip-confirm" : "rmc-requisitions-presets-replace-confirm");
        SetText(view.Preview, creating && cartPreset == null
            ? Loc.GetString(_cart.Count == 0
                ? "rmc-requisitions-presets-empty" : "rmc-requisitions-presets-cart-invalid")
            : contents);

        var status = _presets?.Error is { } error
            ? Loc.GetString(error, ("path", _presets.Path.ToString()))
            : Loc.GetString("rmc-requisitions-presets-count", ("count", _presets?.Presets.Count ?? 0),
                ("limit", RequisitionPresetStore.MaxPresets));
        if (current != null && !importing)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-cart-source", ("count", _cart.Count));
        if (missing > 0)
            status += "\n" + Loc.GetString(importing
                ? "rmc-requisitions-presets-import-unavailable" : resolved
                    ? "rmc-requisitions-presets-unavailable" : "rmc-requisitions-presets-none-available",
                ("count", missing));
        else if (current != null && !resolved)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-invalid");
        if (duplicateName)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-duplicate-name");
        else if (!validName && name.Length > 0)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-invalid-name");
        if (!importing && _cart.Count > 0 && cartPreset == null)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-cart-invalid");
        if (unchanged && name == current?.Name && !importing)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-unchanged");
        if (creating && full)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-full");
        if (_clipboardBusy)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-clipboard-reading");
        if (_presetFeedback != null)
            status += "\n" + Loc.GetString(_presetFeedback);
        SetText(view.Status, status);
    }

    private static void SetPresetActionDisabled(ConfirmButton button, bool disabled)
    {
        if (disabled)
            button.ResetConfirmation();
        button.Disabled = disabled;
    }
}
