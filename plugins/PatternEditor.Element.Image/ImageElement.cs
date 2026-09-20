using System.Text.Json.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Element.Image;

/// <summary>
/// Modello dell'elemento SVG &lt;image&gt;: un'immagine esterna dentro il disegno vettoriale.
///
/// <para>
/// L'immagine viene collocata nel rettangolo definito da (<see cref="X"/>, <see cref="Y"/>) e
/// (<see cref="Width"/>, <see cref="Height"/>). Quando le proporzioni dell'immagine non
/// coincidono con quelle del rettangolo interviene <see cref="PreserveAspectRatio"/>:
/// <c>meet</c> la contiene per intero lasciando spazio vuoto, <c>slice</c> riempie il
/// rettangolo tagliando ciò che avanza, <c>none</c> la deforma per farla combaciare.
/// </para>
///
/// <para>
/// <see cref="Href"/> accetta due forme, ed è un compromesso senza soluzione perfetta: un
/// indirizzo esterno tiene il documento leggero ma lo lega a un server che potrebbe non
/// rispondere; un data URI lo rende autonomo ma ne moltiplica il peso, e un'immagine di pochi
/// kilobyte diventa un documento di parecchi. Il validatore segnala entrambe le conseguenze
/// come avvisi, lasciando la scelta a chi la sta facendo.
/// </para>
/// </summary>
public sealed class ImageElement : VectorElement, IOverallOpacity
{
    /// <summary>
    /// Discriminatore scritto nel JSON. È una costante e non una stringa ripetuta perché
    /// lo stesso valore serve al plugin, al serializzatore e ai test: un refuso in uno dei
    /// tre posti produrrebbe documenti che si salvano e non si rileggono.
    /// </summary>
    public const string TypeName = "image";

    /// <summary>
    /// Segnaposto mostrato da un elemento appena inserito, prima che si scelga
    /// un'immagine. Volutamente NON quadrato (32×20): dentro un riquadro quadrato i tre
    /// valori di preserveAspectRatio danno esiti visibilmente diversi, mentre con un
    /// segnaposto quadrato sembrerebbero equivalenti e l'impostazione parrebbe inerte.
    /// </summary>
    public const string PlaceholderHref =
        "data:image/svg+xml;base64," +
        "PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAzMiAyMCI+PHJlY3Qg" +
        "d2lkdGg9IjMyIiBoZWlnaHQ9IjIwIiBmaWxsPSIjZGZlM2U4Ii8+PGNpcmNsZSBjeD0iOCIgY3k9IjYuNSIgcj0i" +
        "Mi42IiBmaWxsPSIjOWFhNGIxIi8+PHBhdGggZD0iTTIgMTggTDExIDkgTDE2IDE0IEwyMSAxMCBMMzAgMTggWiIg" +
        "ZmlsbD0iIzlhYTRiMSIvPjxyZWN0IHg9IjAuNSIgeT0iMC41IiB3aWR0aD0iMzEiIGhlaWdodD0iMTkiIGZpbGw9" +
        "Im5vbmUiIHN0cm9rZT0iIzlhYTRiMSIgc3Ryb2tlLWRhc2hhcnJheT0iMiAyIi8+PC9zdmc+";

    /// <summary>Adatta l'immagine al riquadro conservandone le proporzioni (comportamento predefinito SVG).</summary>
    public const string FitContain = "xMidYMid meet";

    /// <summary>Riempie il riquadro conservando le proporzioni e ritagliando l'eccedenza.</summary>
    public const string FitCover = "xMidYMid slice";

    /// <summary>Deforma l'immagine per farla combaciare esattamente con il riquadro.</summary>
    public const string FitStretch = "none";

    /// <inheritdoc />
    public override string Type => TypeName;

    /// <summary>
    /// Sorgente dell'immagine: data URI oppure indirizzo http/https.
    /// Viene conservata testualmente, senza reinterpretazioni.
    /// </summary>
    public string Href { get; set; } = string.Empty;

    /// <summary>Angolo superiore sinistro del riquadro (asse X).</summary>
    public double X { get; set; }

    /// <summary>Angolo superiore sinistro del riquadro (asse Y).</summary>
    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    /// <summary>
    /// Attributo SVG preserveAspectRatio: decide come l'immagine si adatta al riquadro
    /// X/Y/Width/Height. Valori usati dall'editor: <see cref="FitContain"/>,
    /// <see cref="FitCover"/>, <see cref="FitStretch"/>.
    /// </summary>
    public string PreserveAspectRatio { get; set; } = FitContain;

    public double Opacity { get; set; }

    /// <summary>
    /// Costruttore usato sia dal codice sia dal deserializzatore. Tutti i valori sono
    /// obbligatori e nessuno ha un predefinito implicito: un elemento a metà non deve poter
    /// esistere, e un documento che ne ometta uno è un documento malformato.
    /// </summary>
    [JsonConstructor]
    public ImageElement(
        Guid id,
        string href,
        double x,
        double y,
        double width,
        double height,
        string preserveAspectRatio,
        double opacity)
        : base(id)
    {
        Href = href ?? string.Empty;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        PreserveAspectRatio = string.IsNullOrWhiteSpace(preserveAspectRatio) ? FitContain : preserveAspectRatio;
        Opacity = opacity;
    }

    /// <summary>Crea un nuovo ImageElement con i valori predefiniti definiti dal plugin.</summary>
    public static ImageElement CreateDefault() => new(
        id: Guid.CreateVersion7(),
        href: PlaceholderHref,
        x: 5,
        y: 5,
        width: 40,
        height: 40,
        preserveAspectRatio: FitContain,
        opacity: 1);
}
