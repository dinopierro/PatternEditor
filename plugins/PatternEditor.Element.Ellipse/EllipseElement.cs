using System.Text.Json.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Element.Ellipse;

/// <summary>
/// Modello dell'elemento SVG &lt;ellipse&gt;: un'ellisse con gli assi paralleli a quelli del
/// disegno.
///
/// <para>
/// (<see cref="Cx"/>, <see cref="Cy"/>) è il centro, <see cref="Rx"/> e <see cref="Ry"/> sono
/// i due semiassi. Con i due semiassi uguali si ottiene un cerchio: l'ellisse è la forma
/// generale, ma i due elementi restano distinti perché distinti sono i comandi che ci si
/// aspetta di trovare, e perché così li tiene separati anche la specifica.
/// </para>
///
/// <para>
/// La specifica non consente di ruotare un'ellisse con i suoi attributi: per un'ellisse
/// inclinata servono un arco in un tracciato o la rotazione dell'intero pattern.
/// </para>
/// </summary>
public sealed class EllipseElement : VectorElement, IOverallOpacity
{
    /// <summary>
    /// Discriminatore scritto nel JSON. È una costante e non una stringa ripetuta perché
    /// lo stesso valore serve al plugin, al serializzatore e ai test: un refuso in uno dei
    /// tre posti produrrebbe documenti che si salvano e non si rileggono.
    /// </summary>
    public const string TypeName = "ellipse";

    /// <inheritdoc />
    public override string Type => TypeName;

    /// <summary>Centro dell'ellisse (asse X).</summary>
    public double Cx { get; set; }

    /// <summary>Centro dell'ellisse (asse Y).</summary>
    public double Cy { get; set; }

    /// <summary>Semiasse orizzontale. Un valore nullo rende l'elemento invisibile: è considerato non valido.</summary>
    public double Rx { get; set; }

    /// <summary>Semiasse verticale.</summary>
    public double Ry { get; set; }

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
    public EllipseElement(
        Guid id,
        double cx,
        double cy,
        double rx,
        double ry,
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
        Rx = rx;
        Ry = ry;
        Fill = fill;
        FillOpacity = fillOpacity;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        StrokeOpacity = strokeOpacity;
        Opacity = opacity;
    }

    /// <summary>Crea un nuovo EllipseElement con i valori predefiniti definiti dal plugin.</summary>
    public static EllipseElement CreateDefault() => new(
        id: Guid.CreateVersion7(),
        cx: 25,
        cy: 25,
        rx: 20,
        ry: 12,
        fill: "#AEFAEF",
        fillOpacity: 1,
        stroke: null,
        strokeWidth: 1,
        strokeOpacity: 1,
        opacity: 1);
}
