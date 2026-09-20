using System.Globalization;

namespace PatternEditor.Core.Formatting;

/// <summary>
/// Lettura dell'attributo "points" della specifica SVG, usato da &lt;polygon&gt; e
/// &lt;polyline&gt;: una sequenza di coppie di coordinate separate da spazi o virgole,
/// ad esempio <c>"25,4 46,19 38,44 12,44 4,19"</c>.
///
/// Serve a due plugin distinti, che non possono e non devono conoscersi: la logica comune
/// sta quindi qui, alla loro base comune. Il testo originale non viene mai riscritto —
/// la formattazione scelta da chi lo ha inserito è parte del documento.
/// </summary>
public static class SvgPoints
{
    private static readonly char[] Separators = [' ', '\t', '\r', '\n', ','];

    /// <summary>
    /// Conta le coppie di coordinate contenute nel testo. Restituisce false se un valore
    /// non è un numero valido o se le coordinate sono in numero dispari (l'ultimo punto
    /// resterebbe senza ordinata).
    /// </summary>
    public static bool TryCountPoints(string? points, out int pointCount)
    {
        pointCount = 0;

        var tokens = (points ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }
        }

        if (tokens.Length % 2 != 0)
        {
            return false;
        }

        pointCount = tokens.Length / 2;
        return true;
    }
}
