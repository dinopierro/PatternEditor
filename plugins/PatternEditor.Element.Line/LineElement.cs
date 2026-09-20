using System.Text.Json.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Element.Line;

/// <summary>
/// Modello dell'elemento SVG &lt;line&gt;: un segmento fra due punti.
///
/// <para>
/// (<see cref="X1"/>, <see cref="Y1"/>) e (<see cref="X2"/>, <see cref="Y2"/>) sono gli
/// estremi. È l'unico elemento del catalogo senza riempimento: la specifica prevede
/// l'attributo <c>fill</c> anche su una linea, ma non ha alcun effetto visibile, e il modello
/// non lo espone per non offrire un comando che non fa nulla.
/// </para>
///
/// <para>
/// Ne segue che una linea con spessore zero è invisibile, ed è il motivo per cui il colore
/// del tratto qui non è annullabile: senza tratto non resterebbe niente.
/// </para>
/// </summary>
public sealed class LineElement : VectorElement, IOverallOpacity
{
    /// <summary>
    /// Discriminatore scritto nel JSON. È una costante e non una stringa ripetuta perché
    /// lo stesso valore serve al plugin, al serializzatore e ai test: un refuso in uno dei
    /// tre posti produrrebbe documenti che si salvano e non si rileggono.
    /// </summary>
    public const string TypeName = "line";

    /// <inheritdoc />
    public override string Type => TypeName;

    /// <summary>Primo estremo del segmento (asse X).</summary>
    public double X1 { get; set; }

    public double Y1 { get; set; }

    public double X2 { get; set; }

    /// <summary>Secondo estremo del segmento (asse Y).</summary>
    public double Y2 { get; set; }

    /// <summary>Colore del tratto in formato esadecimale (es. "#000000").</summary>
    public string Stroke { get; set; } = "#000000";

    public double StrokeWidth { get; set; }

    /// <summary>
    /// Opacità del solo bordo, da 0 a 1. Vale la stessa distinzione di
    /// <see cref="FillOpacity"/> rispetto all'opacità complessiva.
    /// </summary>
    public double StrokeOpacity { get; set; }

    public double Opacity { get; set; }

    /// <summary>
    /// Costruttore usato sia dal codice sia dal deserializzatore. Tutti i valori sono
    /// obbligatori e nessuno ha un predefinito implicito: un elemento a metà non deve poter
    /// esistere, e un documento che ne ometta uno è un documento malformato.
    /// </summary>
    [JsonConstructor]
    public LineElement(
        Guid id,
        double x1,
        double y1,
        double x2,
        double y2,
        string stroke,
        double strokeWidth,
        double strokeOpacity,
        double opacity)
        : base(id)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        StrokeOpacity = strokeOpacity;
        Opacity = opacity;
    }

    public static LineElement CreateDefault() => new(
        id: Guid.CreateVersion7(),
        x1: 0,
        y1: 0,
        x2: 30,
        y2: 30,
        stroke: "#000000",
        strokeWidth: 1,
        strokeOpacity: 1,
        opacity: 1);
}
