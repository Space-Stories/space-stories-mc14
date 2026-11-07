using System.IO;

namespace Content.Client._RMC14.Requisitions;

public sealed partial class RequisitionPresetStore
{
    private const int MaxYamlDepth = 8;
    private const int MaxYamlTokens = 24576;

    /// <summary>
    /// Accepts the scalar and collection syntax used by our saves, rejecting aliases, anchors and tags
    /// before YamlStream builds a tree. Low-level YAML parser types are unavailable in the client sandbox.
    /// </summary>
    private static void ValidateYamlSyntax(string text)
    {
        var quote = '\0';
        var plain = false;
        var plainEnded = false;
        var lineStart = true;
        var indent = 0;
        var sequences = 0;
        var flowDepth = 0;
        var tokens = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (quote != '\0')
            {
                if (quote == '"' && character == '\\')
                    i++;
                else if (character == quote)
                {
                    if (quote == '\'' && i + 1 < text.Length && text[i + 1] == '\'')
                        i++;
                    else
                    {
                        quote = '\0';
                        plain = true;
                        plainEnded = true;
                    }
                }
                continue;
            }
            if (character is '\r' or '\n' or '\u0085' or '\u2028' or '\u2029')
            {
                lineStart = true;
                indent = sequences = 0;
                plain = plainEnded = false;
                continue;
            }
            if (character == ' ')
            {
                if (lineStart && ++indent > MaxYamlDepth * 2)
                    throw new InvalidDataException();
                plainEnded |= plain;
                continue;
            }
            lineStart = false;
            if (character is '"' or '\'')
            {
                if (plain)
                    throw new InvalidDataException();
                quote = character;
                tokens++;
            }
            else if (character is '[' or '{')
            {
                if (plain || ++flowDepth > MaxYamlDepth)
                    throw new InvalidDataException();
                tokens++;
            }
            else if (character is ']' or '}')
            {
                if (--flowDepth < 0)
                    throw new InvalidDataException();
                plain = plainEnded = false;
                tokens++;
            }
            else if (character is ':' or ',')
            {
                if (character == ',' && flowDepth == 0 || character == ':' && i + 1 < text.Length &&
                    text[i + 1] is not (' ' or '\r' or '\n' or ']' or '}' or ','))
                    throw new InvalidDataException();
                plain = plainEnded = false;
                tokens++;
            }
            else if (character == '-' && !plain && i + 1 < text.Length && text[i + 1] is ' ' or '\r' or '\n')
            {
                if (++sequences > MaxYamlDepth)
                    throw new InvalidDataException();
                tokens++;
            }
            else
            {
                if (plainEnded || !char.IsLetterOrDigit(character) && character is not ('_' or '-' or '+' or '.'))
                    throw new InvalidDataException();
                if (!plain)
                    tokens++;
                plain = true;
            }
            if (tokens > MaxYamlTokens)
                throw new InvalidDataException();
        }
        if (quote != '\0' || flowDepth != 0)
            throw new InvalidDataException();
    }

    private static bool ValidUnicode(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                if (++i == text.Length || !char.IsLowSurrogate(text[i]))
                    return false;
            }
            else if (char.IsLowSurrogate(text[i]))
                return false;
        }
        return true;
    }
}
