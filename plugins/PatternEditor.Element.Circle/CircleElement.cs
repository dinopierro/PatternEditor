using System.Text.Json.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Element.Circle;

/// <summary>
/// Modello dell'elemento SVG &lt;circle&gt;: un cerchio dato da centro e raggio.
///
/// <para>
/// (<see cref="Cx"/>, <see cref="Cy"/>) è il centro e <see cref="R"/> il raggio. La specifica
/// prevede che un raggio pari a zero disabiliti il disegno dell'elemento: il plugin lo tratta
/// come dato non valido, perché una riga nell'elenco che non disegna niente è quasi sempre
/// una svista.
/// </para>
///
/// <para>
/// Come per tutti gli elementi, le coordinate non sono normalizzate rispetto alla cella: un
/// cerchio può sporgere oltre il bordo, ed è così che si ottengono i motivi continui.
/// </para>
/// </summary>
public sealed class CircleElement : VectorElement, IOverallOpacity
{
    /// <summary>
    /// Discriminatore scritto nel JSON. È una costante e non una stringa ripetuta perché
    /// lo stesso valore serve al plugin, al serializzatore e ai test: un refuso in uno dei
    /// tre posti produrrebbe documenti che si salvano e non si rileggono.
    /// </summary>
    public const string TypeName = "circle";

    /// <inheritdoc />
    public override string Type => TypeName;

    /// <summary>Centro del cerchio (asse X).</summary>
    public double Cx { get; set; }

    /// <summary>Centro del cerchio (asse Y).</summary>
    public double Cy { get; set; }

    /// <summary>Raggio. Un raggio pari a zero rende l'elemento invisibile: è considerato non valido.</summary>
    public double R { get; set; }

    /// <summary>Colore di riempimento in formato esadecimale (es. "#AEFAEF"), oppure null per nessun riempimento.</summary>
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
    public CircleElement(
        Guid id,
        double cx,
        double cy,
        double r,
        string? fill,
        double fillOpacity,
        string? stroke,
        double strokeWidth,
        double strokeOpacity,
        double opacity)
        : base(id)
    {
        Cx = cx;
        Cy = cy;
        R = r;
        Fill = fill;
        FillOpacity = fillOpacity;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        StrokeOpacity = strokeOpacity;
        Opacity = opacity;
    }

    /// <summary>Crea un nuovo CircleElement con i valori predefiniti definiti dal plugin.</summary>
    public static CircleElement CreateDefault() => new(
        id: Guid.CreateVersion7(),
        cx: 25,
        cy: 25,
        r: 15,
        fill: "#AEFAEF",
        fillOpacity: 1,
        stroke: null,
        strokeWidth: 1,
        strokeOpacity: 1,
        opacity: 1);
}
