using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Content.Shared._RMC14.Requisitions;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Requisitions;

/// <summary>
/// Local request templates. Only resolved catalog selections are sent through the existing requisitions UI.
/// </summary>
public sealed partial class RequisitionPresetStore
{
    public const int MaxPresets = 30;
    public const int MaxLines = 50;
    public const int MaxNameLength = 64;
    public const int MaxReasonLength = 512;
    public const int MaxFileBytes = 256 * 1024;
    public const int MaxKeyLength = 4096;

    private static readonly IComparer<string> ProductOrder =
        Comparer<string>.Create((left, right) => string.CompareOrdinal(left, right));

    private readonly IWritableDirProvider _data;
    public readonly ResPath Path;
    public IReadOnlyList<RequisitionPreset> Presets { get; private set; } = Array.Empty<RequisitionPreset>();
    public string? Error { get; private set; }
    public int Revision { get; private set; }

    public RequisitionPresetStore(IWritableDirProvider data, ResPath path)
    {
        _data = data;
        Path = path;
        Reload();
    }

    public static string GetKey(RequisitionsEntry entry)
    {
        // Keep the complete identity: hashing APIs are unavailable in the client sandbox.
        var identity = new StringBuilder();
        var fields = entry.Entities.Select(id => id.Id).Order(ProductOrder)
            .Prepend(entry.Unit ? "unit" : "crate").Prepend(entry.Crate.Id);
        foreach (var field in fields)
        {
            identity.Append(field.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(field);
        }
        return Convert.ToHexString(Encoding.UTF8.GetBytes(identity.ToString()));
    }

    public void Reload()
    {
        Error = null;
        Revision++;
        try
        {
            // Recover a completed old save if replacement was interrupted between the two renames.
            var backup = new ResPath(Path + ".bak");
            if (!_data.Exists(Path) && _data.Exists(backup))
                _data.Rename(backup, Path);
            if (!_data.Exists(Path))
            {
                Presets = Array.Empty<RequisitionPreset>();
                return;
            }

            using var input = _data.OpenRead(Path);
            var bytes = new byte[MaxFileBytes + 1];
            var length = 0;
            while (length < bytes.Length)
            {
                var read = input.Read(bytes, length, bytes.Length - length);
                if (read == 0)
                    break;
                length += read;
            }
            if (length > MaxFileBytes)
                throw new InvalidDataException();
            var presets = ReadYaml(bytes.AsSpan(0, length));
            if (!Validate(presets))
                throw new InvalidDataException();
            Presets = presets;
        }
        catch (Exception)
        {
            Presets = Array.Empty<RequisitionPreset>();
            Error = "rmc-requisitions-presets-read-error";
        }
    }

    public bool Save(RequisitionPreset preset)
    {
        var presets = Presets.ToList();
        var index = presets.FindIndex(value => value.Id == preset.Id);
        if (index < 0)
            presets.Add(preset);
        else
            presets[index] = preset;
        return Write(presets);
    }

    public bool Delete(Guid id)
    {
        var presets = Presets.Where(preset => preset.Id != id).ToList();
        return presets.Count != Presets.Count && Write(presets);
    }

    private bool Write(List<RequisitionPreset> presets)
    {
        if (Error == "rmc-requisitions-presets-read-error")
            return false;
        if (!Validate(presets))
        {
            Error = "rmc-requisitions-presets-invalid-save";
            return false;
        }

        var temp = new ResPath(Path + ".tmp");
        var backup = new ResPath(Path + ".bak");
        try
        {
            var bytes = WriteYaml(presets);
            if (bytes.Length > MaxFileBytes)
            {
                Error = "rmc-requisitions-presets-file-limit";
                return false;
            }
            _data.CreateDir(Path.Directory);
            using (var output = _data.OpenWrite(temp))
            {
                output.Write(bytes);
                output.Flush();
            }
            if (_data.Exists(Path))
            {
                _data.Delete(backup);
                _data.Rename(Path, backup);
            }
            try
            {
                _data.Rename(temp, Path);
            }
            catch (Exception)
            {
                if (!_data.Exists(Path) && _data.Exists(backup))
                    _data.Rename(backup, Path);
                throw;
            }
            Presets = presets;
            Revision++;
            Error = null;
            return true;
        }
        catch (Exception)
        {
            Error = "rmc-requisitions-presets-write-error";
            return false;
        }
    }

    private static bool Validate(List<RequisitionPreset>? presets)
    {
        if (presets == null || presets.Count > MaxPresets)
            return false;
        var ids = new HashSet<Guid>();
        var names = new List<string>();
        foreach (var preset in presets)
        {
            if (preset == null || preset.Id == Guid.Empty || !ids.Add(preset.Id) ||
                !ValidName(preset.Name) || names.Any(name =>
                    string.Equals(name, preset.Name, StringComparison.OrdinalIgnoreCase)) ||
                preset.Reason == null || preset.Reason.Length > MaxReasonLength || !ValidUnicode(preset.Reason) ||
                preset.Items == null || preset.Items.Count is < 1 or > MaxLines)
                return false;
            names.Add(preset.Name);
            var products = new HashSet<string>();
            foreach (var item in preset.Items)
            {
                if (item == null || item.Amount <= 0 || item.Key == null ||
                    item.Key.Length is < 2 or > MaxKeyLength || item.Key.Length % 2 != 0 ||
                    !item.Key.All(char.IsAsciiHexDigit) || !products.Add(item.Key) ||
                    string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 256 || !ValidUnicode(item.Name))
                    return false;
            }
        }
        return true;
    }

    public static bool ValidName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && name.Length <= MaxNameLength &&
               name == name.Trim() && !name.Any(char.IsControl) && ValidUnicode(name);
    }
}

public sealed class RequisitionPreset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<RequisitionPresetItem> Items { get; set; } = new();
}

public sealed class RequisitionPresetItem
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Amount { get; set; }
}
