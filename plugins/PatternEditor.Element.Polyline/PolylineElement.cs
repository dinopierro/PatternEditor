using System.Text.Json.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Element.Polyline;

/// <summary>
/// Modello dell'elemento SVG &lt;polyline&gt;: una successione di segmenti, non chiusa.
///
/// <para>
/// I punti stanno in <see cref="Points"/>, nel formato dell'attributo <c>points</c>: coppie
/// di numeri separate da spazi o virgole. A differenza di &lt;polygon&gt; il disegno finisce
/// sull'ultimo punto, senza tornare al primo.
/// </para>
///
/// <para>
/// Il riempimento resta possibile — la specifica lo consente, chiudendo idealmente la figura
/// per calcolare l'area — ma il caso normale è il solo tratto, ed è quello che il plugin
/// propone come valore predefinito.
/// </para>
/// </summary>
public sealed class PolylineElement : VectorElement, IOverallOpacity
{
    /// <summary>
    /// Discriminatore scritto nel JSON. È una costante e non una stringa ripetuta perché
    /// lo stesso valore serve al plugin, al serializzatore e ai test: un refuso in uno dei
    /// tre posti produrrebbe documenti che si salvano e non si rileggono.
    /// </summary>
    public const string TypeName = "polyline";

    /// <summary>Numero minimo di vertici perché esista almeno un segmento.</summary>
    public const int MinimumPoints = 2;

    /// <inheritdoc />
    public override string Type => TypeName;

    /// <summary>Vertici nel formato "x1,y1 x2,y2 …".</summary>
    public string Points { get; set; } = string.Empty;

    /// <summary>Colore di riempimento in formato esadecimale, oppure null (predefinito) per nessun riempimento.</summary>
    public string? Fill { get; set; }

    public double FillOpacity { get; set; }

    /// <summary>Colore del tratto in formato esadecimale, oppure null per nessun tratto.</summary>
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
    public PolylineElement(
        Guid id,
        string points,
        string? fill,
        double fillOpacity,
        string? stroke,
        double strokeWidth,
        double strokeOpacity,
        double opacity)
        : base(id)
    {
        Points = points ?? string.Empty;
        Fill = fill;
        FillOpacity = fillOpacity;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        StrokeOpacity = strokeOpacity;
        Opacity = opacity;
    }

    /// <summary>Crea un nuovo PolylineElement con i valori predefiniti: una spezzata a zig-zag in una cella di 50×50.</summary>
    public static PolylineElement CreateDefault() => new(
        id: Guid.CreateVersion7(),
        points: "2,38 14,20 26,30 38,8 48,20",
        fill: null,
        fillOpacity: 1,
        stroke: "#000000",
        strokeWidth: 2,
        strokeOpacity: 1,
        opacity: 1);
}
