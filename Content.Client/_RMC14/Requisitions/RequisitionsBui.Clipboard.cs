using System.Threading.Tasks;

namespace Content.Client._RMC14.Requisitions;

public sealed partial class RequisitionsBui
{
    private bool _clipboardBusy;
    private RequisitionPreset? _importedPreset;
    private string _presetNameBeforeImport = string.Empty;

    private void InitializePresetClipboard()
    {
        if (_window == null)
            return;
        var view = _window.PresetsTab;
        view.ImportClipboard.OnPressed += async _ => await ImportPresetClipboard();
        view.ExportClipboard.OnPressed += _ => ExportPresetClipboard();
        view.ConfirmImport.OnPressed += _ => ConfirmPresetImport();
        view.CancelImport.OnPressed += _ =>
        {
            _importedPreset = null;
            _presetRevision = -1;
            view.PresetName.Text = _presetNameBeforeImport;
            _presetFeedback = null;
            RefreshPresets();
        };
    }

    private async Task ImportPresetClipboard()
    {
        if (_window == null || _presets == null || !RequestOnly || _submittingRequest ||
            _clipboardBusy || _importedPreset != null)
            return;
        _clipboardBusy = true;
        _presetFeedback = null;
        RefreshPresets();
        try
        {
            var text = await _clipboard.GetText();
            if (_uiDisposed || _window == null || !RequestOnly || _submittingRequest)
                return;
            if (_presets.Presets.Count >= RequisitionPresetStore.MaxPresets)
            {
                _presetFeedback = "rmc-requisitions-presets-full";
                return;
            }
            if (!RequisitionPresetStore.TryReadClipboard(text, out var preset) || preset == null)
            {
                _presetFeedback = "rmc-requisitions-presets-import-invalid";
                return;
            }
            _presetNameBeforeImport = _window.PresetsTab.PresetName.Text;
            _importedPreset = preset;
            _presetRevision = -1;
            _window.PresetsTab.PresetName.Text = _presets.GetAvailableName(preset.Name);
            ResetPresetConfirmation();
        }
        catch
        {
            if (!_uiDisposed)
                _presetFeedback = "rmc-requisitions-presets-clipboard-error";
        }
        finally
        {
            _clipboardBusy = false;
            if (!_uiDisposed)
                RefreshPresets();
        }
    }

    private void ExportPresetClipboard()
    {
        if (!RequestOnly || _submittingRequest || _clipboardBusy || _importedPreset != null ||
            SelectedPreset is not { } preset)
            return;
        try
        {
            if (RequisitionPresetStore.TryExportClipboard(preset, out var text))
            {
                _clipboard.SetText(text);
                _presetFeedback = "rmc-requisitions-presets-exported";
            }
            else
                _presetFeedback = "rmc-requisitions-presets-import-invalid";
        }
        catch
        {
            _presetFeedback = "rmc-requisitions-presets-clipboard-error";
        }
        RefreshPresets();
    }

    private void ConfirmPresetImport()
    {
        if (_window == null || _presets == null || !RequestOnly || _submittingRequest ||
            _clipboardBusy || _importedPreset is not { } preset)
            return;
        if (_presets.Import(preset, _window.PresetsTab.PresetName.Text.Trim(), out var id))
        {
            _selectedPreset = id;
            _importedPreset = null;
            _presetFeedback = "rmc-requisitions-presets-imported";
        }
        RefreshPresets();
    }
}
