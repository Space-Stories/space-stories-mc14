using System.IO;
using System.Linq;
using System.Text;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.Client._RMC14.Requisitions;

public sealed partial class RequisitionPresetStore
{
    private List<RequisitionPreset> ReadYaml(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var normalized = Encoding.UTF8.GetBytes(text);
        if (!bytes.SequenceEqual(normalized))
            throw new InvalidDataException();

        using var input = new MemoryStream(normalized);
        using var reader = new StreamReader(input, Encoding.UTF8);
        var documents = DataNodeParser.ParseYamlStream(reader).Take(2).ToArray();
        if (documents.Length != 1 ||
            !_serialization.ValidateNode<RequisitionPresetFile>(documents[0].Root).Valid)
            throw new InvalidDataException();

        var file = _serialization.Read<RequisitionPresetFile>(documents[0].Root,
            skipHook: true, notNullableOverride: true);
        if (file.Version != 1)
            throw new InvalidDataException();
        return file.Presets;
    }

    private byte[] WriteYaml(List<RequisitionPreset> presets)
    {
        var node = _serialization.WriteValue(new RequisitionPresetFile { Presets = presets },
            alwaysWrite: true, notNullableOverride: true);
        using var output = new MemoryStream();
        using (var writer = new StreamWriter(output, EncodingHelpers.UTF8, 1024, true))
            new YamlStream(new YamlDocument(node.ToYamlNode())).Save(writer, false);
        return output.ToArray();
    }
}
