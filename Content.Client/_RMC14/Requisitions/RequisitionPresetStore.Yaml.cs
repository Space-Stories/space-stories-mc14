using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Content.Client._RMC14.Requisitions;

public sealed partial class RequisitionPresetStore
{
    private const int MaxYamlDepth = 8;
    private const int MaxYamlEvents = 8192;

    private static List<RequisitionPreset> ReadYaml(ReadOnlySpan<byte> bytes)
    {
        var text = new UTF8Encoding(false, true).GetString(bytes);
        // Bound nesting and reject references and type tags before building the YAML tree.
        var parser = new Parser(new StringReader(text));
        var depth = 0;
        var events = 0;
        while (parser.MoveNext())
        {
            if (++events > MaxYamlEvents || parser.Current is AnchorAlias ||
                parser.Current is NodeEvent node && (!node.Anchor.IsEmpty || !node.Tag.IsEmpty))
                throw new InvalidDataException();
            if (parser.Current is MappingStart or SequenceStart && ++depth > MaxYamlDepth)
                throw new InvalidDataException();
            if (parser.Current is MappingEnd or SequenceEnd)
                depth--;
        }

        var yaml = new YamlStream();
        yaml.Load(new StringReader(text));
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
        if (node is not YamlMappingNode mapping || mapping.Children.Count != fields.Length)
            throw new InvalidDataException();
        var values = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
        foreach (var (key, value) in mapping.Children)
        {
            var name = ReadString(key);
            if (!fields.Contains(name, StringComparer.Ordinal) || !values.TryAdd(name, value))
                throw new InvalidDataException();
        }
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
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new YamlStream(new YamlDocument(root)).Save(writer, false);
        return Encoding.UTF8.GetBytes(writer.ToString());
    }

    private static YamlScalarNode Quoted(string text)
    {
        return new YamlScalarNode(text) { Style = ScalarStyle.DoubleQuoted };
    }
}
