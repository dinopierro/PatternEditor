using System.Globalization;

namespace PatternEditor.Core.Formatting;

/// <summary>
/// Formattazione e parsing dei valori numerici in cultura invariante.
///
/// È indispensabile che i numeri siano sempre scritti con il punto come separatore
/// decimale: sia l'SVG sia il JSON sia l'attributo <c>value</c> di un
/// <c>&lt;input type="number"&gt;</c> lo richiedono. In Blazor WebAssembly la cultura
/// corrente è quella del browser (es. it-IT), quindi un banale <c>ToString()</c>
/// produrrebbe "1,5": il campo numerico HTML lo considererebbe un valore non valido e
/// si svuoterebbe.
/// </summary>
public static class InvariantNumber
{
    public static string Format(double value) =>
        value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Converte il valore proveniente da un campo di input. Accetta sia il punto sia la
    /// virgola come separatore decimale (alcuni browser/localizzazioni possono restituire
    /// il valore già localizzato) e restituisce <paramref name="fallback"/> quando il
    /// testo non è un numero valido, ad esempio mentre il campo è temporaneamente vuoto.
    /// </summary>
    public static double Parse(object? value, double fallback)
    {
        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
    }
}
