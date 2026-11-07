using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Client._RMC14.Requisitions;

public sealed partial class RequisitionPresetStore
{
    private static List<RequisitionPreset> ReadYaml(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var normalized = Encoding.UTF8.GetBytes(text);
        if (!bytes.SequenceEqual(normalized))
            throw new InvalidDataException();
        ValidateYamlSyntax(text);

        var yaml = new YamlStream();
        using var input = new MemoryStream(normalized);
        using var reader = new StreamReader(input, Encoding.UTF8);
        yaml.Load(reader);
        if (yaml.Documents.Count != 1)
            throw new InvalidDataException();
        var document = ReadMapping(yaml.Documents[0].RootNode, "version", "presets");
        if (ReadInteger(document["version"]) != 1)
            throw new InvalidDataException();

        var presets = new List<RequisitionPreset>();
        foreach (var value in ReadSequence(document["presets"], MaxPresets))
        {
            var fields = ReadMapping(value, "id", "name", "reason", "items");
            if (!Guid.TryParseExact(ReadString(fields["id"]), "D", out var id))
                throw new InvalidDataException();
            var preset = new RequisitionPreset
            {
                Id = id, Name = ReadString(fields["name"]), Reason = ReadString(fields["reason"]),
            };
            foreach (var item in ReadSequence(fields["items"], MaxLines))
            {
                var contents = ReadMapping(item, "key", "name", "amount");
                preset.Items.Add(new RequisitionPresetItem
                {
                    Key = ReadString(contents["key"]), Name = ReadString(contents["name"]),
                    Amount = ReadInteger(contents["amount"]),
                });
            }
            presets.Add(preset);
        }
        return presets;
    }

    private static Dictionary<string, YamlNode> ReadMapping(YamlNode node, params string[] fields)
    {
        if (node is not YamlMappingNode mapping)
            throw new InvalidDataException();
        var values = new Dictionary<string, YamlNode>();
        foreach (var (key, value) in mapping)
        {
            var name = ReadString(key);
            if (!fields.Contains(name) || !values.TryAdd(name, value))
                throw new InvalidDataException();
        }
        if (values.Count != fields.Length)
            throw new InvalidDataException();
        return values;
    }

    private static IEnumerable<YamlNode> ReadSequence(YamlNode node, int limit)
    {
        if (node is not YamlSequenceNode sequence || sequence.Children.Count > limit)
            throw new InvalidDataException();
        return sequence.Children;
    }

    private static string ReadString(YamlNode node)
    {
        return node is YamlScalarNode { Value: { } value } ? value : throw new InvalidDataException();
    }

    private static int ReadInteger(YamlNode node)
    {
        return int.TryParse(ReadString(node), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number : throw new InvalidDataException();
    }

    private static byte[] WriteYaml(List<RequisitionPreset> presets)
    {
        var saved = new YamlSequenceNode();
        foreach (var preset in presets)
        {
            var items = new YamlSequenceNode();
            foreach (var item in preset.Items)
            {
                items.Add(new YamlMappingNode
                {
                    { "key", Quoted(item.Key) }, { "name", Quoted(item.Name) },
                    { "amount", new YamlScalarNode(item.Amount.ToString(CultureInfo.InvariantCulture)) },
                });
            }
            saved.Add(new YamlMappingNode
            {
                { "id", new YamlScalarNode(preset.Id.ToString("D")) }, { "name", Quoted(preset.Name) },
                { "reason", Quoted(preset.Reason) }, { "items", items },
            });
        }
        var root = new YamlMappingNode
        {
            { "version", new YamlScalarNode("1") }, { "presets", saved },
        };
        using var output = new MemoryStream();
        using (var writer = new StreamWriter(output, EncodingHelpers.UTF8, 1024, true))
            new YamlStream(new YamlDocument(root)).Save(writer, false);
        return output.ToArray();
    }

    private static YamlScalarNode Quoted(string text)
    {
        return new YamlScalarNode(text) { Style = ScalarStyle.DoubleQuoted };
    }
}
