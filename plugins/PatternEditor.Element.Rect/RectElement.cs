using System.Text.Json.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Element.Rect;

/// <summary>
/// Modello dell'elemento SVG &lt;rect&gt;: un rettangolo con i lati paralleli agli assi.
///
/// <para>
/// La geometria è quella della specifica: (<see cref="X"/>, <see cref="Y"/>) è l'angolo
/// superiore sinistro, e da lì il rettangolo si estende verso destra e verso il basso — in
/// SVG l'asse Y cresce scendendo, al contrario della convenzione cartesiana.
/// </para>
///
/// <para>
/// Le coordinate sono espresse nello stesso sistema di riferimento della cella del pattern e
/// **non** vengono normalizzate rispetto alle sue dimensioni: un rettangolo può trovarsi
/// intenzionalmente a cavallo del bordo o del tutto fuori dalla cella. È il modo con cui si
/// costruiscono i motivi che si incastrano fra una ripetizione e l'altra, quindi ritagliare
/// gli elementi al bordo sarebbe un danno, non una comodità.
/// </para>
/// </summary>
public sealed class RectElement : VectorElement, IOverallOpacity
{
    /// <summary>
    /// Discriminatore scritto nel JSON. È una costante e non una stringa ripetuta perché
    /// lo stesso valore serve al plugin, al serializzatore e ai test: un refuso in uno dei
    /// tre posti produrrebbe documenti che si salvano e non si rileggono.
    /// </summary>
    public const string TypeName = "rect";

    /// <inheritdoc />
    public override string Type => TypeName;

    /// <summary>Angolo superiore sinistro (asse X).</summary>
    public double X { get; set; }

    /// <summary>Angolo superiore sinistro (asse Y).</summary>
    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

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
    public RectElement(
        Guid id,
        double x,
        double y,
        double width,
        double height,
        string? fill,
        double fillOpacity,
        string? stroke,
        double strokeWidth,
        double strokeOpacity,
        double opacity)
        : base(id)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Fill = fill;
        FillOpacity = fillOpacity;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        StrokeOpacity = strokeOpacity;
        Opacity = opacity;
    }

    /// <summary>Crea un nuovo RectElement con i valori predefiniti definiti dal plugin.</summary>
    public static RectElement CreateDefault() => new(
        id: Guid.CreateVersion7(),
        x: 10,
        y: 10,
        width: 30,
        height: 30,
        fill: "#AEFAEF",
        fillOpacity: 1,
        stroke: null,
        strokeWidth: 1,
        strokeOpacity: 1,
        opacity: 1);
}
