using System.Globalization;
using System.Text;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Models;

namespace PatternEditor.Services;

/// <summary>
/// Genera la rappresentazione SVG del Pattern. Usa sempre lo stesso rendering prodotto dai
/// plugin sia per le preview sia per l'SVG finale scaricabile: non deve esistere una logica
/// di geometria duplicata.
/// </summary>
public interface IPatternSvgRenderer
{
    /// <summary>
    /// SVG completo e indipendente, con &lt;defs&gt;&lt;pattern&gt; e un &lt;rect&gt; che applica
    /// il pattern come riempimento. È il documento restituito dal comando "Download SVG":
    /// essendo un file a sé stante può usare l'identificativo predefinito del nodo pattern.
    /// </summary>
    string RenderStandaloneSvg(Pattern pattern);

    /// <summary>
    /// Come <see cref="RenderStandaloneSvg(Pattern)"/> ma con width/height espliciti, per
    /// incorporare l'SVG in un contenitore di dimensioni note (es. "300", "100%").
    /// L'identificativo del nodo &lt;pattern&gt; viene derivato dall'Id del Pattern, così che
    /// più anteprime presenti nella stessa pagina non collidano tra loro.
    /// </summary>
    string RenderStandaloneSvg(Pattern pattern, string width, string height);

    /// <summary>
    /// Come sopra, ma con l'identificativo del nodo &lt;pattern&gt; scelto dal chiamante.
    ///
    /// Serve quando nella stessa pagina possono coesistere due anteprime dello STESSO
    /// Pattern (tipicamente la riga dell'elenco e l'editor aperto sopra di essa): un
    /// riferimento <c>fill="url(#id)"</c> viene risolto sull'intero documento HTML, non
    /// all'interno del singolo &lt;svg&gt;, quindi due nodi con lo stesso id farebbero sì che
    /// entrambe le anteprime mostrino la prima definizione incontrata.
    /// </summary>
    string RenderStandaloneSvg(Pattern pattern, string width, string height, string patternId);

    /// <summary>
    /// SVG di una singola cella del pattern, utile per l'anteprima ingrandita. Non applica
    /// la ripetizione: mostra direttamente gli elementi con lo stesso rendering usato ovunque.
    ///
    /// <para>
    /// <paramref name="displaySize"/> è l'<b>ingombro</b> in pixel, non il lato: il riquadro
    /// prodotto ha la forma della cella, e il lato lungo misura <paramref name="displaySize"/>.
    /// Una cella 48 × 24 produce quindi un riquadro largo il doppio di quanto è alto.
    /// </para>
    /// </summary>
    /// <param name="filterId">
    /// L'identificativo del nodo &lt;filter&gt;, quando il pattern ne ha uno. Serve per la
    /// stessa ragione di quello del nodo &lt;pattern&gt;: due rese della stessa cella nella
    /// stessa pagina — l'anteprima ingrandita e quella ancorata dello schermo stretto —
    /// non devono dichiarare due nodi con lo stesso id. Assente, si ricava dall'Id del pattern.
    /// </param>
    string RenderSingleCellSvg(Pattern pattern, double displaySize, string? filterId = null);

    /// <summary>
    /// Le due misure in pixel del riquadro prodotto da <see cref="RenderSingleCellSvg"/>,
    /// a parità di ingombro.
    ///
    /// <para>
    /// Serve a chi deve dimensionare il contenitore attorno all'anteprima: il riquadro e la
    /// sua cornice devono avere la stessa forma del disegno che contengono, e ricavarla due
    /// volte — una qui e una nel componente — significherebbe vederle divergere alla prima
    /// modifica. La regola sta scritta in un punto solo.
    /// </para>
    /// </summary>
    (double Width, double Height) SingleCellDisplaySize(Pattern pattern, double displaySize);

    /// <summary>Genera esclusivamente il contenuto del nodo &lt;pattern&gt; (gli elementi, in ordine).</summary>
    string RenderElementsMarkup(Pattern pattern);
}

public sealed class PatternSvgRenderer : IPatternSvgRenderer
{
    /// <summary>Identificativo usato nell'SVG autonomo (file scaricato), dove non esistono collisioni possibili.</summary>
    private const string DefaultPatternId = "p";

    private readonly IVectorElementPluginRegistry _registry;
    private readonly ISvgFilterRenderer _filtri;

    public PatternSvgRenderer(IVectorElementPluginRegistry registry, ISvgFilterRenderer filtri)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _filtri = filtri ?? throw new ArgumentNullException(nameof(filtri));
    }

    public string RenderElementsMarkup(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var sb = new StringBuilder();
        foreach (var element in pattern.Definition.Elements)
        {
            if (element is UnknownVectorElement)
            {
                // Un elemento sconosciuto non può essere renderizzato (nessun plugin):
                // viene semplicemente omesso dalla resa grafica, ma resta preservato nel
                // modello e verrà riscritto identico in fase di serializzazione JSON.
                continue;
            }

            if (_registry.TryGet(element.Type, out var plugin) && plugin is not null)
            {
                sb.Append("  ").Append(Trasformato(element, plugin.Render(element))).Append('\n');
            }
        }

        return sb.ToString();
    }

    public string RenderStandaloneSvg(Pattern pattern) =>
        RenderStandaloneSvg(pattern, "100vw", "100vh", DefaultPatternId);

    public string RenderStandaloneSvg(Pattern pattern, string width, string height)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return RenderStandaloneSvg(pattern, width, height, BuildPatternIdFor(pattern));
    }

    public string RenderStandaloneSvg(Pattern pattern, string width, string height, string patternId)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(width);
        ArgumentException.ThrowIfNullOrWhiteSpace(height);
        ArgumentException.ThrowIfNullOrWhiteSpace(patternId);

        var inv = CultureInfo.InvariantCulture;
        var definition = pattern.Definition;
        var transform = BuildPatternTransform(definition);
        var elementsMarkup = RenderElementsMarkup(pattern);

        // Il filtro si applica al rettangolo dipinto, non al contenuto della tessera. Il
        // motivo sta per esteso su PatternFilter, e in breve è questo: una sfocatura dentro
        // una tessera viene tagliata al bordo della tessera, e il taglio si ripete identico a
        // ogni ripetizione disegnando una griglia di cuciture che nel motivo non c'è.
        var filterId = $"f-{patternId}";
        var filtro = _filtri.RenderFilterDefinition(definition.Filter, filterId);

        // Con un filtro attivo il rettangolo si allarga oltre la vista. Una primitiva che
        // sconfina legge i pixel appena fuori dall'oggetto, e appena fuori da un rettangolo
        // grande quanto la pagina non c'è niente: il risultato sarebbe una sfumatura verso
        // il trasparente lungo i quattro bordi, cioè un alone che il motivo non ha. Con il
        // rettangolo più grande l'alone cade fuori dalla finestra di visualizzazione.
        //
        // Senza filtro il rettangolo resta esattamente quello di prima: un documento non
        // deve cambiare per una funzione che non usa.
        var rect = filtro is null
            ? $"  <rect width=\"100%\" height=\"100%\" fill=\"url(#{patternId})\" />\n"
            : $"  <rect x=\"-25%\" y=\"-25%\" width=\"150%\" height=\"150%\" "
              + $"fill=\"url(#{patternId})\" filter=\"url(#{filterId})\" />\n";

        return new StringBuilder()
            .Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\">\n")
            .Append("  <defs>\n")
            .Append($"    <pattern id=\"{patternId}\" width=\"{definition.Width.ToString(inv)}\" height=\"{definition.Height.ToString(inv)}\" ")
            .Append("patternUnits=\"userSpaceOnUse\"")
            .Append(transform is null ? "" : $" patternTransform=\"{transform}\"")
            .Append(">\n")
            .Append(elementsMarkup)
            .Append("    </pattern>\n")
            .Append(filtro ?? "")
            .Append("  </defs>\n")
            .Append(rect)
            .Append("</svg>")
            .ToString();
    }

    public string RenderSingleCellSvg(Pattern pattern, double displaySize, string? filterId = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var inv = CultureInfo.InvariantCulture;
        var definition = pattern.Definition;
        var elementsMarkup = RenderElementsMarkup(pattern);
        var (width, height) = SingleCellDisplaySize(pattern, displaySize);

        // Il filtro si vede anche qui. Sarebbe difendibile lasciarlo fuori da questa vista —
        // dichiara di mostrare la geometria, e la trasformazione infatti non la applica — ma
        // il filtro cambia soprattutto i colori, e una cella che resta blu mentre la
        // ripetizione accanto è diventata grigia non sembra una scelta: sembra un guasto.
        //
        // Ciò che qui si vede diverso, e va saputo, è il bordo: dentro un riquadro grande
        // quanto la cella una sfocatura viene tagliata ai lati. Sulla superficie vera non
        // succede, ed è proprio per questo che lì il filtro si applica al rettangolo.
        var idFiltro = filterId ?? $"fc-{pattern.Id:N}";
        var filtro = _filtri.RenderFilterDefinition(definition.Filter, idFiltro, rientro: 2);

        // Il fattore di zoom è puramente grafico (viewBox + dimensioni visualizzate):
        // non modifica in alcun modo il valore Scale effettivo del pattern.
        var sb = new StringBuilder()
            .Append("<svg xmlns=\"http://www.w3.org/2000/svg\" ")
            .Append($"viewBox=\"0 0 {definition.Width.ToString(inv)} {definition.Height.ToString(inv)}\" ")
            .Append($"width=\"{width.ToString(inv)}\" height=\"{height.ToString(inv)}\">\n");

        if (filtro is null)
        {
            sb.Append(elementsMarkup);
        }
        else
        {
            sb.Append("  <defs>\n").Append(filtro).Append("  </defs>\n")
              .Append($"  <g filter=\"url(#{idFiltro})\">\n")
              .Append(elementsMarkup)
              .Append("  </g>\n");
        }

        return sb.Append("</svg>").ToString();
    }

    /// <inheritdoc />
    public (double Width, double Height) SingleCellDisplaySize(Pattern pattern, double displaySize)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var width = pattern.Definition.Width;
        var height = pattern.Definition.Height;

        // Una cella con un lato nullo, negativo o non finito non ha una forma da rispettare:
        // è un documento che il validatore segnala come errore. Qui si restituisce un
        // quadrato invece di dividere per zero — l'anteprima resterà vuota, ma il riquadro
        // c'è e l'editor continua a funzionare mentre si corregge la misura.
        if (!IsUsableSide(width) || !IsUsableSide(height))
        {
            return (displaySize, displaySize);
        }

        // displaySize è l'ingombro, non il lato: il maggiore dei due se lo prende tutto e
        // l'altro segue la proporzione. Così il riquadro ha la forma della cella senza mai
        // sfondare lo spazio che il chiamante gli ha riservato, qualunque sia il rapporto.
        return width >= height
            ? (displaySize, Math.Round(displaySize * height / width, 2))
            : (Math.Round(displaySize * width / height, 2), displaySize);
    }

    private static bool IsUsableSide(double value) =>
        value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);

    /// <summary>
    /// Identificativo stabile e univoco per Pattern, valido come id XML (non può iniziare
    /// con una cifra, da cui il prefisso).
    /// </summary>
    /// <summary>
    /// Il tag del plugin, avvolto nella trasformazione dell'elemento quando ce n'è una.
    ///
    /// <para>
    /// L'involucro sta qui e non nel plugin per una ragione di contratto: il plugin produce
    /// la propria forma e nient'altro, e ruotarla è un'operazione che vale per tutti i tipi
    /// allo stesso modo. Metterla nei plugin significherebbe scriverla nove volte e
    /// dimenticarla alla decima.
    /// </para>
    ///
    /// <para>
    /// L'ordine dei fattori non è indifferente: si porta l'origine sul punto scelto, si
    /// ruota, si specchia, e si torna indietro. Scritto al contrario, l'elemento ruoterebbe
    /// attorno all'angolo della cella invece che attorno al proprio punto.
    /// </para>
    /// </summary>
    private static string Trasformato(VectorElement element, string markup)
    {
        if (!element.HasTransform)
        {
            return markup;
        }

        var inv = CultureInfo.InvariantCulture;
        var ox = element.OriginX;
        var oy = element.OriginY;
        var sx = element.FlipX ? -1 : 1;
        var sy = element.FlipY ? -1 : 1;
        var spostata = ox != 0 || oy != 0;

        var pezzi = new List<string>();

        if (spostata)
        {
            pezzi.Add($"translate({ox.ToString(inv)},{oy.ToString(inv)})");
        }

        if (element.Rotation % 360 != 0)
        {
            pezzi.Add($"rotate({element.Rotation.ToString(inv)})");
        }

        if (sx != 1 || sy != 1)
        {
            pezzi.Add($"scale({sx.ToString(inv)},{sy.ToString(inv)})");
        }

        if (spostata)
        {
            // Il segno meno davanti a zero produce «-0», che è un numero valido e un
            // documento brutto: la stessa traslazione scritta in due modi diversi a seconda
            // di quale coordinata sia nulla. Si riporta a zero e basta.
            pezzi.Add($"translate({Senza(-ox).ToString(inv)},{Senza(-oy).ToString(inv)})");
        }

        return $"<g transform=\"{string.Join(' ', pezzi)}\">{markup}</g>";
    }

    /// <summary>Zero senza segno: «-0» è valido ma sporca il documento.</summary>
    private static double Senza(double valore) => valore == 0 ? 0 : valore;

    private static string BuildPatternIdFor(Pattern pattern) => $"p-{pattern.Id:N}";

    /// <summary>
    /// Converte Scale/Rotation/TranslateX/TranslateY nell'attributo SVG patternTransform.
    /// Restituisce null quando la trasformazione risultante sarebbe l'identità, per non
    /// generare un attributo superfluo.
    /// </summary>
    private static string? BuildPatternTransform(PatternDefinition definition)
    {
        var inv = CultureInfo.InvariantCulture;
        var isIdentity =
            definition.Scale == 1.0 &&
            definition.Rotation == 0 &&
            definition.TranslateX == 0 &&
            definition.TranslateY == 0;

        if (isIdentity)
        {
            return null;
        }

        return $"scale({definition.Scale.ToString(inv)}) "
             + $"rotate({definition.Rotation.ToString(inv)}) "
             + $"translate({definition.TranslateX.ToString(inv)},{definition.TranslateY.ToString(inv)})";
    }
}
