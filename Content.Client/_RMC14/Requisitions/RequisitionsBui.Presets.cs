using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared._RMC14.Requisitions;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RMC14.Requisitions;

public sealed partial class RequisitionsBui
{
    private RequisitionPresetStore? _presets;
    private Guid? _selectedPreset;
    private int _presetRevision = -1;
    private string? _presetFeedback;
    private readonly Dictionary<string, CartKey> _presetProducts = new(StringComparer.Ordinal);
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
            view.Selection.SelectId(args.Id);
            var preset = args.Id > 0 ? _presets.Presets[args.Id - 1] : null;
            _selectedPreset = preset?.Id;
            view.PresetName.Text = preset?.Name ?? string.Empty;
            _presetFeedback = null;
            ResetPresetConfirmation();
            RefreshPresets();
        };
        _window.SavePresetButton.OnPressed += _ =>
        {
            _window.ShopTabs.CurrentTab = view.GetPositionInParent();
            view.PresetName.GrabKeyboardFocus();
        };
        view.Load.OnPressed += _ => LoadPreset();
        view.ReplaceCart.OnPressed += _ => RunPresetConfirmation(view.ReplaceCart, LoadPreset);
        view.SaveNew.OnPressed += _ => SavePreset(false);
        view.Update.OnPressed += _ => RunPresetConfirmation(view.Update, () => SavePreset(true));
        view.Rename.OnPressed += _ => RenamePreset();
        view.Delete.OnPressed += _ => RunPresetConfirmation(view.Delete, () =>
        {
            if (_presets == null || !RequestOnly || _selectedPreset is not { } id ||
                _submittingRequest || _clipboardBusy || _importedPreset != null)
                return;
            if (_presets.Delete(id))
            {
                _selectedPreset = null;
                view.PresetName.Text = string.Empty;
                _presetFeedback = "rmc-requisitions-presets-deleted";
            }
            RefreshPresets();
        });
        InitializePresetClipboard();
    }

    private static void RunPresetConfirmation(ConfirmButton button, Action action)
    {
        action();
        // ConfirmButton toggles this after OnPressed; the action may have refreshed or switched tabs.
        button.IsConfirming = true;
    }

    private RequisitionPreset? SelectedPreset =>
        _presets?.Presets.FirstOrDefault(preset => preset.Id == _selectedPreset);

    private void ResetPresetConfirmation()
    {
        if (_window == null)
            return;
        _window.PresetsTab.ReplaceCart.IsConfirming = false;
        _window.PresetsTab.Update.IsConfirming = false;
        _window.PresetsTab.Delete.IsConfirming = false;
    }

    private void RefreshPresets()
    {
        if (_window == null)
            return;
        var view = _window.PresetsTab;
        TabContainer.SetTabVisible(view, RequestOnly);
        _window.RequestReasonPanel.Visible = RequestOnly &&
                                             _window.ShopTabs.CurrentTab != view.GetPositionInParent();
        _window.SavePresetButton.Visible = RequestOnly;
        _window.SavePresetButton.Disabled = _cart.Count == 0 || _submittingRequest ||
                                           _clipboardBusy || _importedPreset != null;
        if (!RequestOnly)
            return;

        if (_presetCatalogRevision != _supplyCatalog.Revision)
        {
            _presetCatalogRevision = _supplyCatalog.Revision;
            _presetProducts.Clear();
            foreach (var product in _supplyCatalog.Items)
            {
                if (TryGetEntry(product.Key, out var entry))
                    _presetProducts.TryAdd(RequisitionPresetStore.GetKey(entry), product.Key);
            }
        }

        var importing = _importedPreset != null;
        if (_presets != null && _presetRevision != _presets.Revision)
        {
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
        var name = view.PresetName.Text.Trim();
        var validName = RequisitionPresetStore.ValidName(name);
        var duplicateName = _presets?.Presets.Any(preset =>
            string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (importing || preset.Id != current?.Id)) == true;
        var writable = _presets != null && _presets.Error != "rmc-requisitions-presets-read-error" &&
                       !_submittingRequest && !_clipboardBusy;
        var canSave = writable && !importing && validName && !duplicateName &&
                      _cart.Count is > 0 and <= RequisitionPresetStore.MaxLines;
        view.Selection.Disabled = _submittingRequest || _clipboardBusy || importing;
        view.PresetName.Editable = !_submittingRequest && !_clipboardBusy;
        view.Rename.Visible = !importing;
        view.ImportActions.Visible = importing;
        view.LoadActions.Visible = !importing;
        view.SaveActions.Visible = !importing;
        var full = _presets?.Presets.Count >= RequisitionPresetStore.MaxPresets;
        view.ImportClipboard.Disabled = !writable || full || importing;
        view.ImportClipboard.ToolTip = Loc.GetString(full
            ? "rmc-requisitions-presets-full" : "rmc-requisitions-presets-import-tooltip");
        view.ExportClipboard.Disabled = current == null || _submittingRequest || _clipboardBusy || importing;
        view.ConfirmImport.Disabled = !writable || !validName || duplicateName || full;
        view.CancelImport.Disabled = _submittingRequest || _clipboardBusy;
        view.SaveNew.Disabled = !canSave || _presets!.Presets.Count >= RequisitionPresetStore.MaxPresets ||
                                _presets.Presets.Any(preset =>
                                    string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
        view.Update.Disabled = !canSave || current == null;
        view.Rename.Disabled = !writable || !validName || duplicateName || current == null || name == current.Name;
        view.Delete.Disabled = !writable || current == null;
        var resolved = ResolvePreset(_importedPreset ?? current, out var contents, out var missing, out _);
        view.Load.Disabled = !resolved || _submittingRequest || _clipboardBusy || importing;
        view.ReplaceCart.Disabled = view.Load.Disabled;
        view.Load.Visible = _cart.Count == 0 && missing == 0;
        view.ReplaceCart.Visible = !view.Load.Visible;
        view.ReplaceCart.Text = Loc.GetString(missing > 0
            ? "rmc-requisitions-presets-load-available" : "rmc-requisitions-presets-load");
        view.ReplaceCart.ConfirmationText = Loc.GetString(missing > 0
            ? "rmc-requisitions-presets-skip-confirm" : "rmc-requisitions-presets-replace-confirm");
        SetText(view.Preview, contents);

        var status = _presets?.Error is { } error
            ? Loc.GetString(error, ("path", _presets.Path.ToString()))
            : Loc.GetString("rmc-requisitions-presets-count", ("count", _presets?.Presets.Count ?? 0),
                ("limit", RequisitionPresetStore.MaxPresets));
        if (missing > 0)
            status += "\n" + Loc.GetString(importing
                ? "rmc-requisitions-presets-import-unavailable" : "rmc-requisitions-presets-unavailable",
                ("count", missing));
        else if (current != null && !resolved)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-invalid");
        if (duplicateName)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-duplicate-name");
        else if (_cart.Count > RequisitionPresetStore.MaxLines)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-lines-limit",
                ("limit", RequisitionPresetStore.MaxLines));
        if (importing)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-import-review");
        if (_clipboardBusy)
            status += "\n" + Loc.GetString("rmc-requisitions-presets-clipboard-reading");
        if (_presetFeedback != null)
            status += "\n" + Loc.GetString(_presetFeedback);
        SetText(view.Status, status);
    }

    private bool ResolvePreset(RequisitionPreset? preset, out string preview, out int missing,
        out Dictionary<CartKey, int> cart)
    {
        preview = Loc.GetString("rmc-requisitions-presets-empty");
        missing = 0;
        cart = new Dictionary<CartKey, int>();
        if (preset == null || _state == null)
            return false;
        var lines = new List<string>();
        var total = 0L;
        foreach (var item in preset.Items)
        {
            if (!_presetProducts.TryGetValue(item.Key, out var key) || !TryGetEntry(key, out var entry) ||
                entry.BlackMarket ||
                entry.BlackMarketCost != 0 || entry.Cost < 0 || item.Amount > _state.MaxRequestItems)
            {
                missing++;
                lines.Add(Loc.GetString("rmc-requisitions-presets-missing-item",
                    ("amount", item.Amount), ("name", item.Name)));
                continue;
            }
            total += (long) entry.Cost * item.Amount;
            cart[key] = item.Amount;
            lines.Add(Loc.GetString("rmc-requisitions-presets-item",
                ("amount", item.Amount), ("name", GetDisplayText(entry).Name),
                ("cost", (long) entry.Cost * item.Amount)));
        }
        lines.Add("\n" + Loc.GetString("rmc-requisitions-presets-reason", ("reason", preset.Reason)));
        lines.Add(Loc.GetString("rmc-requisitions-presets-total", ("cost", total)));
        preview = string.Join("\n", lines);
        return cart.Count > 0 && total <= int.MaxValue && preset.Reason.Length <= _state.MaxRequestReasonLength &&
               cart.Count <= _state.MaxRequestLines;
    }

    private void LoadPreset()
    {
        if (_window == null || !RequestOnly || _submittingRequest || _clipboardBusy || _importedPreset != null ||
            SelectedPreset is not { } preset ||
            !ResolvePreset(preset, out _, out var missing, out var cart))
            return;
        _cart.Clear();
        foreach (var (key, amount) in cart)
            _cart[key] = amount;
        _window.RequestReason.Text = preset.Reason;
        _window.SearchBar.Text = string.Empty;
        SelectedCategory = null;
        _window.BuyButton.IsConfirming = false;
        SetText(_window.RequestFeedback, missing > 0
            ? Loc.GetString("rmc-requisitions-presets-skipped", ("count", missing)) : string.Empty);
        ResetPresetConfirmation();
        _window.ShopTabs.CurrentTab = _window.CartTab.GetPositionInParent();
        RefreshVisibleEntries();
    }

    private void SavePreset(bool update)
    {
        if (_window == null || _state == null || _presets == null || !RequestOnly || _submittingRequest ||
            _clipboardBusy || _importedPreset != null ||
            _cart.Count is < 1 or > RequisitionPresetStore.MaxLines || update && SelectedPreset == null)
            return;
        var items = new List<RequisitionPresetItem>();
        foreach (var (key, amount) in _cart)
        {
            if (!TryGetEntry(key, out var entry) || entry.BlackMarket || entry.BlackMarketCost != 0 ||
                amount <= 0 || amount > _state.MaxRequestItems)
                return;
            items.Add(new RequisitionPresetItem
            {
                Key = RequisitionPresetStore.GetKey(entry),
                Name = GetDisplayText(entry).Name,
                Amount = amount,
            });
        }
        var preset = new RequisitionPreset
        {
            Id = update ? _selectedPreset!.Value : Guid.NewGuid(),
            Name = _window.PresetsTab.PresetName.Text.Trim(),
            Reason = _window.RequestReason.Text.Trim(),
            Items = items,
        };
        if (_presets.Save(preset))
        {
            _selectedPreset = preset.Id;
            _presetFeedback = "rmc-requisitions-presets-saved";
        }
        ResetPresetConfirmation();
        RefreshPresets();
    }

    private void RenamePreset()
    {
        if (_window == null || _presets == null || !RequestOnly || _submittingRequest ||
            _clipboardBusy || _importedPreset != null ||
            SelectedPreset is not { } current)
            return;
        if (_presets.Save(new RequisitionPreset
            {
                Id = current.Id, Name = _window.PresetsTab.PresetName.Text.Trim(),
                Reason = current.Reason, Items = current.Items,
            }))
            _presetFeedback = "rmc-requisitions-presets-saved";
        ResetPresetConfirmation();
        RefreshPresets();
    }
}
