using System.Text;

namespace PatternEditor.Services;

/// <summary>
/// Riformatta un SVG per la sola visualizzazione a schermo, mettendo ogni attributo su una
/// riga a sé e rientrando i tag in base all'annidamento.
///
/// Serve esclusivamente al riquadro "SVG generato" dell'editor: un elemento con molti
/// attributi produrrebbe altrimenti una riga lunghissima, leggibile solo scorrendo in
/// orizzontale. L'SVG scaricato e quello copiato NON passano da qui: restano quelli prodotti
/// da <see cref="IPatternSvgRenderer"/>, che è l'unica fonte del markup effettivo.
/// </summary>
public static class SvgSourceFormatter
{
    private const int IndentSize = 2;

    /// <summary>Numero di attributi oltre il quale il tag viene spezzato su più righe.</summary>
    private const int InlineAttributeLimit = 1;

    public static string Format(string svg)
    {
        if (string.IsNullOrWhiteSpace(svg))
        {
            return string.Empty;
        }

        var output = new StringBuilder();
        var depth = 0;
        var position = 0;

        while (position < svg.Length)
        {
            var tagStart = svg.IndexOf('<', position);
            if (tagStart < 0)
            {
                AppendText(output, svg.AsSpan(position), depth);
                break;
            }

            AppendText(output, svg.AsSpan(position, tagStart - position), depth);

            var tagEnd = FindTagEnd(svg, tagStart);
            if (tagEnd < 0)
            {
                // Markup non chiuso: viene riportato tale e quale, senza tentare di ripararlo.
                AppendText(output, svg.AsSpan(tagStart), depth);
                break;
            }

            var tag = svg[tagStart..(tagEnd + 1)];
            var isClosing = tag.StartsWith("</", StringComparison.Ordinal);
            var isSelfClosing = tag.EndsWith("/>", StringComparison.Ordinal);

            if (isClosing)
            {
                depth = Math.Max(0, depth - 1);
            }

            AppendTag(output, tag, depth, isClosing, isSelfClosing);

            if (!isClosing && !isSelfClosing)
            {
                depth++;
            }

            position = tagEnd + 1;
        }

        return output.ToString().TrimEnd();
    }

    private static void AppendText(StringBuilder output, ReadOnlySpan<char> text, int depth)
    {
        // Fra i tag dell'SVG generato c'è solo spaziatura: viene scartata perché il rientro
        // è ricalcolato qui. Un eventuale contenuto testuale, invece, va conservato.
        var trimmed = text.Trim();
        if (trimmed.IsEmpty)
        {
            return;
        }

        output.Append(' ', depth * IndentSize).Append(trimmed).Append('\n');
    }

    private static void AppendTag(StringBuilder output, string tag, int depth, bool isClosing, bool isSelfClosing)
    {
        var indent = new string(' ', depth * IndentSize);

        if (isClosing)
        {
            output.Append(indent).Append(tag).Append('\n');
            return;
        }

        var inner = tag[1..^1];
        if (isSelfClosing)
        {
            inner = inner[..^1];
        }

        var nameEnd = 0;
        while (nameEnd < inner.Length && !char.IsWhiteSpace(inner[nameEnd]))
        {
            nameEnd++;
        }

        var name = inner[..nameEnd];
        var attributes = ParseAttributes(inner[nameEnd..]);
        var close = isSelfClosing ? "/>" : ">";

        if (attributes.Count <= InlineAttributeLimit)
        {
            output.Append(indent).Append('<').Append(name);
            foreach (var attribute in attributes)
            {
                output.Append(' ').Append(attribute);
            }

            output.Append(isSelfClosing ? " />" : ">").Append('\n');
            return;
        }

        output.Append(indent).Append('<').Append(name).Append('\n');
        foreach (var attribute in attributes)
        {
            output.Append(indent).Append(' ', IndentSize).Append(attribute).Append('\n');
        }

        output.Append(indent).Append(close).Append('\n');
    }

    /// <summary>
    /// Separa gli attributi tenendo conto degli apici: il valore di un attributo può
    /// contenere spazi (es. patternTransform="scale(2) rotate(45)") e non va spezzato.
    /// </summary>
    private static List<string> ParseAttributes(string text)
    {
        var attributes = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';

        foreach (var c in text)
        {
            if (quote != '\0')
            {
                current.Append(c);
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                current.Append(c);
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                Flush(attributes, current);
                continue;
            }

            current.Append(c);
        }

        Flush(attributes, current);
        return attributes;

        static void Flush(List<string> attributes, StringBuilder current)
        {
            if (current.Length > 0)
            {
                attributes.Add(current.ToString());
                current.Clear();
            }
        }
    }

    /// <summary>Trova il '&gt;' che chiude il tag, ignorando quelli contenuti nei valori fra apici.</summary>
    private static int FindTagEnd(string svg, int tagStart)
    {
        var quote = '\0';
        for (var i = tagStart + 1; i < svg.Length; i++)
        {
            var c = svg[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                return i;
            }
        }

        return -1;
    }
}
