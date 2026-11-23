using System.Text;

namespace DepotUploader;

internal static class KeyValuesText
{
    public static KeyValuesNode Parse(string text)
    {
        int position = 0;
        string? rootKey = ReadToken(text, ref position, out bool rootKeyIsBrace);
        if (rootKey is null || rootKeyIsBrace)
        {
            throw new FormatException("The script has no root key.");
        }

        if (ReadToken(text, ref position, out bool isBrace) != "{" || !isBrace)
        {
            throw new FormatException($"Expected a block after \"{rootKey}\".");
        }

        KeyValuesNode root = new KeyValuesNode(rootKey);
        ReadBlock(text, ref position, root);

        return root;
    }

    public static string Write(KeyValuesNode root)
    {
        StringBuilder builder = new StringBuilder();
        WriteNode(builder, root, 0);

        return builder.ToString();
    }

    private static void ReadBlock(string text, ref int position, KeyValuesNode block)
    {
        while (true)
        {
            string? key = ReadToken(text, ref position, out bool keyIsBrace);
            if (key is null)
            {
                throw new FormatException($"The block \"{block.Key}\" is not closed.");
            }

            if (keyIsBrace && key == "}")
            {
                return;
            }

            string? value = ReadToken(text, ref position, out bool valueIsBrace);
            if (value is null || (valueIsBrace && value == "}"))
            {
                throw new FormatException($"\"{key}\" has no value.");
            }

            if (valueIsBrace)
            {
                KeyValuesNode child = new KeyValuesNode(key);
                ReadBlock(text, ref position, child);
                block.Children!.Add(child);
            }
            else
            {
                block.Children!.Add(new KeyValuesNode(key, value));
            }
        }
    }

    private static string? ReadToken(string text, ref int position, out bool isBrace)
    {
        isBrace = false;
        SkipWhitespaceAndComments(text, ref position);
        if (position >= text.Length)
        {
            return null;
        }

        char first = text[position];
        if (first == '{' || first == '}')
        {
            position++;
            isBrace = true;

            return first.ToString();
        }

        if (first == '"')
        {
            int closingQuote = text.IndexOf('"', position + 1);
            if (closingQuote < 0)
            {
                throw new FormatException("A quoted value is not closed.");
            }

            string quoted = text.Substring(position + 1, closingQuote - position - 1);
            position = closingQuote + 1;

            return quoted;
        }

        int start = position;
        while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] != '{'
            && text[position] != '}' && text[position] != '"')
        {
            position++;
        }

        return text.Substring(start, position - start);
    }

    private static void SkipWhitespaceAndComments(string text, ref int position)
    {
        while (position < text.Length)
        {
            if (char.IsWhiteSpace(text[position]))
            {
                position++;
            }
            else if (text[position] == '/' && position + 1 < text.Length && text[position + 1] == '/')
            {
                int lineEnd = text.IndexOf('\n', position);
                position = lineEnd < 0 ? text.Length : lineEnd + 1;
            }
            else
            {
                return;
            }
        }
    }

    private static void WriteNode(StringBuilder builder, KeyValuesNode node, int depth)
    {
        string indent = new string('\t', depth);
        RejectQuoteOrLineBreak(node.Key, node.Key);
        if (node.Children is null)
        {
            RejectQuoteOrLineBreak(node.Value!, node.Key);
            builder.Append(indent).Append('"').Append(node.Key).Append("\" \"").Append(node.Value).Append("\"\r\n");

            return;
        }

        builder.Append(indent).Append('"').Append(node.Key).Append("\"\r\n");
        builder.Append(indent).Append("{\r\n");
        foreach (KeyValuesNode child in node.Children)
        {
            WriteNode(builder, child, depth + 1);
        }

        builder.Append(indent).Append("}\r\n");
    }

    private static void RejectQuoteOrLineBreak(string text, string key)
    {
        if (text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
        {
            throw new FormatException($"\"{key}\" can't contain a double quote or a line break.");
        }
    }
}
