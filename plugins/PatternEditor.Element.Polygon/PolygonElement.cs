using System.Text.Json.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Element.Polygon;

/// <summary>
/// Modello dell'elemento SVG &lt;polygon&gt;: una figura chiusa definita dai suoi vertici.
///
/// <para>
/// I vertici stanno in <see cref="Points"/>, nel formato dell'attributo <c>points</c>: coppie
/// di numeri separate da spazi o virgole. La specifica **chiude la figura da sé**, unendo
/// l'ultimo vertice al primo: ripetere il primo punto in coda non serve, e produrrebbe solo
/// un segmento di lunghezza nulla.
/// </para>
///
/// <para>
/// È questa chiusura automatica a distinguerlo da &lt;polyline&gt;, ed è la ragione per cui
/// solo qui un riempimento ha sempre senso.
/// </para>
/// </summary>
public sealed class PolygonElement : VectorElement, IOverallOpacity
{
    /// <summary>
    /// Discriminatore scritto nel JSON. È una costante e non una stringa ripetuta perché
    /// lo stesso valore serve al plugin, al serializzatore e ai test: un refuso in uno dei
    /// tre posti produrrebbe documenti che si salvano e non si rileggono.
    /// </summary>
    public const string TypeName = "polygon";

    /// <summary>Numero minimo di vertici perché la figura racchiuda una superficie.</summary>
    public const int MinimumPoints = 3;

    /// <inheritdoc />
    public override string Type => TypeName;

    /// <summary>Vertici nel formato "x1,y1 x2,y2 …".</summary>
    public string Points { get; set; } = string.Empty;

    /// <summary>Colore di riempimento in formato esadecimale, oppure null per nessun riempimento.</summary>
    public string? Fill { get; set; }

    public double FillOpacity { get; set; }

    /// <summary>Colore del bordo in formato esadecimale, oppure null per nessun bordo.</summary>
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
    public PolygonElement(
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

    /// <summary>Crea un nuovo PolygonElement con i valori predefiniti: un pentagono in una cella di 50×50.</summary>
    public static PolygonElement CreateDefault() => new(
        id: Guid.CreateVersion7(),
        points: "25,4 46,19 38,44 12,44 4,19",
        fill: "#AEFAEF",
        fillOpacity: 1,
        stroke: null,
        strokeWidth: 1,
        strokeOpacity: 1,
        opacity: 1);
}
