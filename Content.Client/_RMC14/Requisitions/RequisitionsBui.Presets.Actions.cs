namespace Content.Client._RMC14.Requisitions;

public sealed partial class RequisitionsBui
{
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
        lines.Add("\n" + (string.IsNullOrWhiteSpace(preset.Reason)
            ? Loc.GetString("rmc-requisitions-presets-no-reason")
            : Loc.GetString("rmc-requisitions-presets-reason", ("reason", preset.Reason))));
        lines.Add(Loc.GetString("rmc-requisitions-presets-total", ("cost", total)));
        preview = string.Join("\n", lines);
        return cart.Count > 0 && total <= int.MaxValue && preset.Reason.Length <= _state.MaxRequestReasonLength &&
               cart.Count <= _state.MaxRequestLines;
    }

    private void LoadPreset()
    {
        if (_window == null || !RequestOnly || _submittingRequest || _clipboardBusy || _importedPreset != null ||
            SelectedPreset is not { } preset ||
            !ResolvePreset(preset, out _, out var missing, out var cart) || CartMatches(cart, preset.Reason))
            return;
        _cart.Clear();
        foreach (var (key, amount) in cart)
            _cart[key] = amount;
        _window.RequestReason.Text = preset.Reason;
        _window.SearchBar.Text = string.Empty;
        SelectedCategory = null;
        _window.BuyButton.ResetConfirmation();
        SetText(_window.RequestFeedback, missing > 0
            ? Loc.GetString("rmc-requisitions-presets-skipped", ("count", missing)) : string.Empty);
        ResetPresetConfirmation();
        _window.ShopTabs.CurrentTab = _window.CartTab.GetPositionInParent();
        RefreshVisibleEntries();
    }

    private void SavePreset(bool update)
    {
        if (_window == null || _presets == null || !RequestOnly || _submittingRequest ||
            _clipboardBusy || _importedPreset != null || update != (SelectedPreset != null) ||
            CreatePresetFromCart() is not { } preset)
            return;
        preset.Id = update ? _selectedPreset! : Guid.NewGuid().ToString("D");
        _presetFeedback = null;
        if (_presets.Save(preset))
        {
            _selectedPreset = preset.Id;
            if (!update)
                _newPresetName = string.Empty;
            _window.PresetsTab.PresetName.Text = preset.Name;
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
        _presetFeedback = null;
        if (_presets.Save(new RequisitionPreset
            {
                Id = current.Id, Name = _window.PresetsTab.PresetName.Text.Trim(),
                Reason = current.Reason, Items = current.Items,
            }))
        {
            _window.PresetsTab.PresetName.Text = _window.PresetsTab.PresetName.Text.Trim();
            _presetFeedback = "rmc-requisitions-presets-saved";
        }
        ResetPresetConfirmation();
        RefreshPresets();
    }
}
