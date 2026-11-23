using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DepotUploader;

internal static class JsonText
{
    private const int DeepestNesting = 64;

    private static readonly Regex Number = new Regex(@"\G-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?");

    public static object? Parse(string json)
    {
        int position = 0;
        object? value = ParseValue(json, ref position, 0);
        if (CharacterAfterWhitespace(json, ref position) != '\0' || position < json.Length)
        {
            throw Error("has text after its value", position);
        }

        return value;
    }

    public static string Quote(string text)
    {
        StringBuilder json = new StringBuilder("\"");
        foreach (char character in text)
        {
            if (character == '"' || character == '\\')
            {
                json.Append('\\').Append(character);
            }
            else if (character < ' ')
            {
                json.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
            }
            else
            {
                json.Append(character);
            }
        }

        return json.Append('"').ToString();
    }

    private static object? ParseValue(string json, ref int position, int nesting)
    {
        switch (CharacterAfterWhitespace(json, ref position))
        {
            case '{':
                return ParseObject(json, ref position, nesting + 1);
            case '[':
                return ParseArray(json, ref position, nesting + 1);
            case '"':
                return ParseString(json, ref position);
            case 't':
                SkipWord(json, ref position, "true");

                return true;
            case 'f':
                SkipWord(json, ref position, "false");

                return false;
            case 'n':
                SkipWord(json, ref position, "null");

                return null;
            default:
                return ParseNumber(json, ref position);
        }
    }

    private static List<KeyValuePair<string, object?>> ParseObject(string json, ref int position, int nesting)
    {
        ThrowIfNestedTooDeep(nesting, position);
        List<KeyValuePair<string, object?>> members = new List<KeyValuePair<string, object?>>();
        position++;
        if (CharacterAfterWhitespace(json, ref position) == '}')
        {
            position++;

            return members;
        }

        while (true)
        {
            if (CharacterAfterWhitespace(json, ref position) != '"')
            {
                throw Error("has no member name in double quotes", position);
            }

            string name = ParseString(json, ref position);
            if (CharacterAfterWhitespace(json, ref position) != ':')
            {
                throw Error("has no : after a member name", position);
            }

            position++;
            members.Add(new KeyValuePair<string, object?>(name, ParseValue(json, ref position, nesting)));
            char separator = CharacterAfterWhitespace(json, ref position);
            if (separator == '}')
            {
                position++;

                return members;
            }

            if (separator != ',')
            {
                throw Error("has no , or } after a member", position);
            }

            position++;
        }
    }

    private static List<object?> ParseArray(string json, ref int position, int nesting)
    {
        ThrowIfNestedTooDeep(nesting, position);
        List<object?> items = new List<object?>();
        position++;
        if (CharacterAfterWhitespace(json, ref position) == ']')
        {
            position++;

            return items;
        }

        while (true)
        {
            items.Add(ParseValue(json, ref position, nesting));
            char separator = CharacterAfterWhitespace(json, ref position);
            if (separator == ']')
            {
                position++;

                return items;
            }

            if (separator != ',')
            {
                throw Error("has no , or ] after an item", position);
            }

            position++;
        }
    }

    private static string ParseString(string json, ref int position)
    {
        StringBuilder text = new StringBuilder();
        position++;
        while (true)
        {
            if (position == json.Length)
            {
                throw Error("ends inside a string", position);
            }

            char character = json[position++];
            if (character == '"')
            {
                if (HasHalfOfASurrogatePair(text))
                {
                    throw Error("has half of a surrogate pair inside a string", position - 1);
                }

                return text.ToString();
            }

            if (character < ' ')
            {
                throw Error("has a control character inside a string", position - 1);
            }

            if (character != '\\')
            {
                text.Append(character);
                continue;
            }

            if (position == json.Length)
            {
                throw Error("ends inside a string", position);
            }

            char escaped = json[position++];
            switch (escaped)
            {
                case '"':
                case '\\':
                case '/':
                    text.Append(escaped);
                    break;
                case 'b':
                    text.Append('\b');
                    break;
                case 'f':
                    text.Append('\f');
                    break;
                case 'n':
                    text.Append('\n');
                    break;
                case 'r':
                    text.Append('\r');
                    break;
                case 't':
                    text.Append('\t');
                    break;
                case 'u':
                    if (position + 4 > json.Length || !json.Substring(position, 4).All(Uri.IsHexDigit))
                    {
                        throw Error("has a \\u escape without four hex digits", position);
                    }

                    text.Append((char)Convert.ToUInt16(json.Substring(position, 4), 16));
                    position += 4;
                    break;
                default:
                    throw Error("has an unknown escape inside a string", position - 1);
            }
        }
    }

    private static bool HasHalfOfASurrogatePair(StringBuilder text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            if (char.IsLowSurrogate(text[index]))
            {
                return true;
            }

            if (char.IsHighSurrogate(text[index]))
            {
                index++;
                if (index == text.Length || !char.IsLowSurrogate(text[index]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static double ParseNumber(string json, ref int position)
    {
        Match number = Number.Match(json, position);
        if (!number.Success)
        {
            throw Error(position == json.Length ? "ends where a value belongs" : "has an unknown value", position);
        }

        position += number.Length;
        double.TryParse(number.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value);

        return value;
    }

    private static void SkipWord(string json, ref int position, string word)
    {
        if (string.CompareOrdinal(json, position, word, 0, word.Length) != 0)
        {
            throw Error("has an unknown value", position);
        }

        position += word.Length;
    }

    private static char CharacterAfterWhitespace(string json, ref int position)
    {
        while (position < json.Length && (json[position] == ' ' || json[position] == '\t'
                   || json[position] == '\r' || json[position] == '\n'))
        {
            position++;
        }

        return position < json.Length ? json[position] : '\0';
    }

    private static void ThrowIfNestedTooDeep(int nesting, int position)
    {
        if (nesting > DeepestNesting)
        {
            throw Error($"is nested deeper than {DeepestNesting} objects and arrays", position);
        }
    }

    private static FormatException Error(string problem, int position)
    {
        return new FormatException($"The JSON text {problem} at character {position + 1}.");
    }
}
