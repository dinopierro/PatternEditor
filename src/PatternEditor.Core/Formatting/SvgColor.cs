namespace PatternEditor.Core.Formatting;

/// <summary>
/// Lettura e normalizzazione di un colore esadecimale.
///
/// Serve a poter scrivere un colore a mano accanto al selettore. Il selettore
/// (&lt;input type="color"&gt;) accetta e restituisce esclusivamente la forma canonica
/// <c>#rrggbb</c>: non tollera la forma abbreviata, l'assenza del cancelletto o un testo
/// incompleto. Chi scrive a mano fa naturalmente tutte e tre le cose, e senza una
/// conversione il campo di testo e il selettore finirebbero per contraddirsi.
///
/// La classe sta nel livello comune perché è logica condivisa fra più plugin: ogni
/// elemento colorabile ne ha bisogno, e nessuno di essi deve conoscerne un altro.
///
/// Non si accettano i nomi di colore previsti dalla specifica SVG ("red", "steelblue"):
/// il modello documenta il formato esadecimale, ed è l'unico che il selettore sa mostrare.
/// </summary>
public static class SvgColor
{
    /// <summary>
    /// Espressione per l'attributo <c>pattern</c> di un campo di testo HTML: consente al
    /// browser di segnalare da solo un valore incompleto mentre lo si digita, senza che
    /// il componente debba tenere traccia del testo intermedio.
    /// </summary>
    public const string HtmlPattern = "#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6})";

    /// <summary>Colore mostrato dal selettore quando il valore non è interpretabile.</summary>
    public const string Fallback = "#000000";

    /// <summary>
    /// Vero se il testo è già nella forma che il selettore usa: cancelletto e sei cifre.
    /// Distinguere questo caso serve mentre si digita: aggiornare il modello solo quando
    /// il testo è già canonico evita che il campo venga riscritto sotto le dita di chi
    /// scrive, spostando il cursore a fine riga a ogni carattere.
    /// </summary>
    public static bool IsCanonical(string? value) =>
        value is { Length: 7 } && value[0] == '#' && AreHexDigits(value.AsSpan(1));

    /// <summary>Vero se il testo è interpretabile come colore, anche in forma abbreviata.</summary>
    public static bool IsValid(string? value) => Normalize(value) is not null;

    /// <summary>
    /// Riconduce il testo alla forma <c>#rrggbb</c>, oppure restituisce null se non è un
    /// colore. Accetta l'assenza del cancelletto e la forma abbreviata a tre cifre, che
    /// espande raddoppiando ciascuna cifra (<c>#abc</c> → <c>#aabbcc</c>) come prescrive
    /// la specifica CSS.
    ///
    /// Le lettere restano come sono state scritte: maiuscole e minuscole indicano lo stesso
    /// colore, e riscriverle darebbe l'impressione che il valore sia stato cambiato.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cifre = value.AsSpan().Trim();
        if (cifre.Length > 0 && cifre[0] == '#')
        {
            cifre = cifre[1..];
        }

        if (!AreHexDigits(cifre))
        {
            return null;
        }

        return cifre.Length switch
        {
            6 => string.Concat("#", cifre),
            3 => string.Create(7, (a: cifre[0], b: cifre[1], c: cifre[2]), static (span, d) =>
            {
                span[0] = '#';
                span[1] = span[2] = d.a;
                span[3] = span[4] = d.b;
                span[5] = span[6] = d.c;
            }),
            _ => null,
        };
    }

    /// <summary>
    /// Valore da passare al selettore: sempre una forma che sa mostrare. Un colore assente
    /// o incompleto non deve far apparire il selettore vuoto o azzerarne il valore.
    /// </summary>
    public static string ForPicker(string? value) => Normalize(value) ?? Fallback;

    private static bool AreHexDigits(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
