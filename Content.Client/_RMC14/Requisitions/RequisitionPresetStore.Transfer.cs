using System.Linq;
using System.Text;

namespace Content.Client._RMC14.Requisitions;

public sealed partial class RequisitionPresetStore
{
    public static bool TryReadClipboard(string? text, out RequisitionPreset? preset)
    {
        preset = null;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxFileBytes)
            return false;
        try
        {
            if (!ValidUnicode(text) || Encoding.UTF8.GetByteCount(text) > MaxFileBytes)
                return false;
            var presets = ReadYaml(Encoding.UTF8.GetBytes(text));
            if (presets.Count != 1 || !Validate(presets))
                return false;
            preset = presets[0];
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryExportClipboard(RequisitionPreset preset, out string text)
    {
        text = string.Empty;
        var presets = new List<RequisitionPreset> { preset };
        if (!Validate(presets))
            return false;
        var bytes = WriteYaml(presets);
        if (bytes.Length > MaxFileBytes)
            return false;
        text = Encoding.UTF8.GetString(bytes);
        return true;
    }

    public string GetAvailableName(string name)
    {
        var candidate = name;
        for (var number = 2; Presets.Any(preset =>
                 string.Equals(preset.Name, candidate, StringComparison.OrdinalIgnoreCase)); number++)
        {
            var suffix = $" ({number})";
            var length = Math.Min(name.Length, MaxNameLength - suffix.Length);
            if (length > 0 && char.IsHighSurrogate(name[length - 1]))
                length--;
            candidate = name[..length].TrimEnd() + suffix;
        }
        return candidate;
    }

    public bool Import(RequisitionPreset preset, string name, out Guid id)
    {
        id = Guid.Empty;
        if (Presets.Count >= MaxPresets || !Validate(new List<RequisitionPreset> { preset }))
            return false;
        var imported = new RequisitionPreset
        {
            Id = Guid.NewGuid(), Name = name, Reason = preset.Reason,
            Items = preset.Items.Select(item => new RequisitionPresetItem
            {
                Key = item.Key, Name = item.Name, Amount = item.Amount,
            }).ToList(),
        };
        if (!Save(imported))
            return false;
        id = imported.Id;
        return true;
    }
}
