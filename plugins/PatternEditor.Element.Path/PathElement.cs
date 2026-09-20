using System.Text.Json.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Element.Path;

/// <summary>
/// Modello dell'elemento SVG &lt;path&gt;: la forma libera.
///
/// <para>
/// L'intera geometria sta nell'attributo <c>d</c> (<see cref="D"/>), una sequenza di comandi
/// definita dalla specifica: <c>M</c> sposta la penna senza disegnare, <c>L</c> traccia una
/// linea, <c>H</c> e <c>V</c> linee orizzontali e verticali, <c>C</c> e <c>S</c> curve
/// cubiche di Bézier, <c>Q</c> e <c>T</c> curve quadratiche, <c>A</c> archi di ellisse,
/// <c>Z</c> chiude la figura ricongiungendosi al punto di partenza. Ogni comando esiste in
/// versione maiuscola (coordinate assolute) e minuscola (relative al punto corrente).
/// </para>
///
/// <para>
/// Il comando viene conservato **testualmente**, senza interpretarlo né normalizzarlo. Il
/// Pattern Editor non è un editor di curve: un tracciato incollato da un altro strumento deve
/// tornare indietro identico, e anche il solo riordino degli spazi sarebbe una modifica che
/// nessuno ha chiesto.
/// </para>
/// </summary>
public sealed class PathElement : VectorElement, IOverallOpacity
{
    /// <summary>
    /// Discriminatore scritto nel JSON. È una costante e non una stringa ripetuta perché
    /// lo stesso valore serve al plugin, al serializzatore e ai test: un refuso in uno dei
    /// tre posti produrrebbe documenti che si salvano e non si rileggono.
    /// </summary>
    public const string TypeName = "path";

    /// <inheritdoc />
    public override string Type => TypeName;

    /// <summary>
    /// Comando di tracciato SVG (attributo "d"), es. "M 0 25 L 25 0 L 50 25".
    /// Conservato testualmente così come inserito, senza normalizzazioni.
    /// </summary>
    public string D { get; set; } = string.Empty;

    /// <summary>Colore di riempimento in formato esadecimale, oppure null per nessun riempimento.</summary>
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
    public PathElement(
        Guid id,
        string d,
        string? fill,
        double fillOpacity,
        string? stroke,
        double strokeWidth,
        double strokeOpacity,
        double opacity)
        : base(id)
    {
        D = d ?? string.Empty;
        Fill = fill;
        FillOpacity = fillOpacity;
        Stroke = stroke;
        StrokeWidth = strokeWidth;
        StrokeOpacity = strokeOpacity;
        Opacity = opacity;
    }

    /// <summary>
    /// Crea un nuovo PathElement con i valori predefiniti definiti dal plugin: una spezzata
    /// a "V" rovesciata che attraversa una cella di 50×50, tracciata e non riempita
    /// (il riempimento predefinito di un path aperto risulterebbe quasi sempre indesiderato).
    /// </summary>
    public static PathElement CreateDefault() => new(
        id: Guid.CreateVersion7(),
        d: "M 0 25 L 25 0 L 50 25",
        fill: null,
        fillOpacity: 1,
        stroke: "#000000",
        strokeWidth: 2,
        strokeOpacity: 1,
        opacity: 1);
}
