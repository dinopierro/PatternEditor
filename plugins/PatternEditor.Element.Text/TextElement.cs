using System.Text.Json.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Element.Text;

/// <summary>
/// Modello dell'elemento SVG &lt;text&gt;: l'unico del catalogo che porta un contenuto.
///
/// <para>
/// Le lettere da disegnare stanno **fra i tag**, non in un attributo. La distinzione non è
/// accademica: dentro un attributo vanno protetti anche gli apici, dentro il contenuto di un
/// nodo no, e usare la regola sbagliata produce un documento illeggibile o, peggio, leggibile
/// ma diverso da quello scritto.
/// </para>
///
/// <para>
/// (<see cref="X"/>, <see cref="Y"/>) individua il punto di partenza della **linea di base**,
/// non l'angolo superiore sinistro: con Y pari a zero il testo risulta quasi tutto sopra il
/// bordo della cella. <see cref="TextAnchor"/> decide da che parte il testo si sviluppa
/// rispetto a quel punto: <c>start</c>, <c>middle</c> o <c>end</c>.
/// </para>
///
/// <para>
/// Il carattere è indicato per nome: chi aprirà l'SVG lo vedrà con quel carattere solo se lo
/// ha installato, altrimenti il lettore ne sostituirà un altro e il disegno cambierà. È una
/// dipendenza dall'esterno che il documento si porta dietro, e il validatore la segnala come
/// avviso senza impedire nulla.
/// </para>
/// </summary>
public sealed class TextElement : VectorElement, IOverallOpacity
{
    /// <summary>
    /// Discriminatore scritto nel JSON. È una costante e non una stringa ripetuta perché
    /// lo stesso valore serve al plugin, al serializzatore e ai test: un refuso in uno dei
    /// tre posti produrrebbe documenti che si salvano e non si rileggono.
    /// </summary>
    public const string TypeName = "text";

    /// <summary>Allineamento del testo rispetto alla coordinata X.</summary>
    public const string AnchorStart = "start";

    public const string AnchorMiddle = "middle";

    public const string AnchorEnd = "end";

    public const string WeightNormal = "normal";

    public const string WeightBold = "bold";

    /// <inheritdoc />
    public override string Type => TypeName;

    /// <summary>Testo visualizzato. Conservato così com'è, senza normalizzazioni.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Posizione del testo (asse X), interpretata secondo <see cref="TextAnchor"/>.</summary>
    public double X { get; set; }

    /// <summary>Posizione della linea di base del testo (asse Y), non del suo bordo superiore.</summary>
    public double Y { get; set; }

    /// <summary>Famiglia di caratteri, es. "sans-serif" oppure "Segoe UI, sans-serif".</summary>
    public string FontFamily { get; set; } = "sans-serif";

    public double FontSize { get; set; }

    /// <summary>Spessore del carattere: <see cref="WeightNormal"/> oppure <see cref="WeightBold"/>.</summary>
    public string FontWeight { get; set; } = WeightNormal;

    /// <summary>Allineamento: <see cref="AnchorStart"/>, <see cref="AnchorMiddle"/> o <see cref="AnchorEnd"/>.</summary>
    public string TextAnchor { get; set; } = AnchorStart;

    /// <summary>Colore del testo in formato esadecimale, oppure null per nessun riempimento.</summary>
    public string? Fill { get; set; }

    public double FillOpacity { get; set; }

    /// <summary>Colore del contorno delle lettere, oppure null per nessun contorno.</summary>
    public string? Stroke { get; set; }

    public double StrokeWidth { get; set; }

    /// <summary>
    /// Opacità del solo bordo, da 0 a 1. Vale la stessa distinzione di
    /// <see cref="FillOpacity"/> rispetto all'opacità complessiva.
    /// </summary>
    public double StrokeOpacity { get; set; }

    /// <summary>Opacità complessiva dell'elemento, distinta da FillOpacity e StrokeOpacity.</summary>
    public double Opacity { get; set; }

    /// <summary>
    /// Costruttore usato sia dal codice sia dal deserializzatore. Tutti i valori sono
    /// obbligatori e nessuno ha un predefinito implicito: un elemento a metà non deve poter
    /// esistere, e un documento che ne ometta uno è un documento malformato.
    /// </summary>
    [JsonConstructor]
    public TextElement(
        Guid id,
        string content,
        double x,
        double y,
        string fontFamily,
        double fontSize,
        string fontWeight,
        string textAnchor,
        string? fill,
        double fillOpacity,
        string? stroke,
        double strokeWidth,
        double strokeOpacity,
        double opacity)
        : base(id)
    {
        Content = content ?? string.Empty;
        X = x;
        Y = y;
        FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "sans-serif" : fontFamily;
        FontSize = fontSize;
        FontWeight = string.IsNullOrWhiteSpace(fontWeight) ? WeightNormal : fontWeight;
        TextAnchor = string.IsNullOrWhiteSpace(textAnchor) ? AnchorStart : textAnchor;
        Fill = fill;
        FillOpacity = fillOpacity;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        StrokeOpacity = strokeOpacity;
        Opacity = opacity;
    }

    /// <summary>
    /// Crea un nuovo TextElement con i valori predefiniti: una parola centrata in una
    /// cella di 50×50. Y è la linea di base, quindi vale poco più della metà dell'altezza.
    /// </summary>
    public static TextElement CreateDefault() => new(
        id: Guid.CreateVersion7(),
        content: "Text",
        x: 25,
        y: 30,
        fontFamily: "sans-serif",
        fontSize: 12,
        fontWeight: WeightNormal,
        textAnchor: AnchorMiddle,
        fill: "#000000",
        fillOpacity: 1,
        stroke: null,
        strokeWidth: 1,
        strokeOpacity: 1,
        opacity: 1);
}
