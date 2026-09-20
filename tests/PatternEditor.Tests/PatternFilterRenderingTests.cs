using PatternEditor.Abstractions.Plugins;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using PatternEditor.Core.Models.Filters;
using PatternEditor.Element.Rect;
using PatternEditor.Services;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// Verifica la generazione del nodo <c>&lt;filter&gt;</c> e il punto in cui viene applicato.
///
/// <para>
/// I test guardano il testo prodotto carattere per carattere, come quelli del generatore
/// principale e per lo stesso motivo: un attributo scritto con il nome sbagliato non solleva
/// alcuna eccezione, il browser lo ignora in silenzio, e il difetto si manifesta come «il
/// cursore non fa niente» a settimane di distanza.
/// </para>
/// </summary>
public class PatternFilterRenderingTests
{
    private static SvgFilterRenderer Filtri() => new();

    private static PatternSvgRenderer Renderer()
    {
        var registro = new VectorElementPluginRegistry();
        registro.Register(new RectPlugin());
        return new PatternSvgRenderer(registro, Filtri());
    }

    private static Pattern ConFiltro(params FilterPrimitive[] passaggi)
    {
        var pattern = new Pattern { Name = "Prova" };
        pattern.Definition.Width = 50;
        pattern.Definition.Height = 50;
        pattern.Definition.Elements.Add(RectElement.CreateDefault());
        pattern.Definition.Filter = new PatternFilter();

        foreach (var passo in passaggi)
        {
            pattern.Definition.Filter.Primitives.Add(passo);
        }

        return pattern;
    }

    // ------------------------------------------------------------ quando non si scrive --

    [Fact]
    public void UnFiltroAssenteNonProduceNulla()
    {
        Assert.Null(Filtri().RenderFilterDefinition(null, "f"));
    }

    [Fact]
    public void UnFiltroSpentoNonProduceNulla()
    {
        var filtro = new PatternFilter { Enabled = false };
        filtro.Primitives.Add(new GaussianBlurPrimitive());

        Assert.Null(Filtri().RenderFilterDefinition(filtro, "f"));
    }

    /// <summary>
    /// Un <c>&lt;filter&gt;</c> senza primitive non è un filtro che non fa niente: secondo la
    /// specifica produce un'immagine completamente trasparente. Generarlo farebbe sparire il
    /// disegno, ed è il difetto più insidioso di tutta questa funzione.
    /// </summary>
    [Fact]
    public void UnFiltroSenzaPassaggiAccesiNonProduceNulla()
    {
        var filtro = new PatternFilter();
        filtro.Primitives.Add(new GaussianBlurPrimitive { Enabled = false });

        Assert.Null(Filtri().RenderFilterDefinition(filtro, "f"));
    }

    [Fact]
    public void UnFiltroDiSoliPassaggiIgnotiNonProduceNulla()
    {
        var filtro = new PatternFilter();
        filtro.Primitives.Add(new UnknownFilterPrimitive("feDomani", default));

        Assert.Null(Filtri().RenderFilterDefinition(filtro, "f"));
    }

    // ------------------------------------------------------------------ gli attributi ---

    [Fact]
    public void LAreaEIlSpazioColoreSonoSempreScritti()
    {
        var filtro = new PatternFilter();
        filtro.Primitives.Add(new GaussianBlurPrimitive());

        var svg = Filtri().RenderFilterDefinition(filtro, "f")!;

        Assert.Contains("x=\"-10%\" y=\"-10%\" width=\"120%\" height=\"120%\"", svg);
        Assert.Contains("color-interpolation-filters=\"sRGB\"", svg);
    }

    [Fact]
    public void LoSpazioLineareSiScriveComeNellaSpecifica()
    {
        var filtro = new PatternFilter { ColorSpace = FilterColorSpace.LinearRgb };
        filtro.Primitives.Add(new GaussianBlurPrimitive());

        Assert.Contains("color-interpolation-filters=\"linearRGB\"", Filtri().RenderFilterDefinition(filtro, "f"));
    }

    /// <summary>
    /// Due raggi uguali si scrivono una volta sola. Non è un'economia di caratteri:
    /// <c>stdDeviation="2"</c> dice «uguale nelle due direzioni», mentre <c>"2 2"</c>
    /// costringe chi legge a confrontare due numeri per scoprire la stessa cosa.
    /// </summary>
    [Fact]
    public void UnRaggioUgualeNelleDueDirezioniSiScriveUnaVoltaSola()
    {
        var filtro = new PatternFilter();
        filtro.Primitives.Add(new GaussianBlurPrimitive { StdDeviationX = 2, StdDeviationY = 2 });

        Assert.Contains("stdDeviation=\"2\"", Filtri().RenderFilterDefinition(filtro, "f"));
    }

    [Fact]
    public void DueRaggiDiversiSiScrivonoEntrambi()
    {
        var filtro = new PatternFilter();
        filtro.Primitives.Add(new GaussianBlurPrimitive { StdDeviationX = 2, StdDeviationY = 5 });

        Assert.Contains("stdDeviation=\"2 5\"", Filtri().RenderFilterDefinition(filtro, "f"));
    }

    /// <summary>
    /// I modi di fusione della specifica usano il trattino. Ricavarli dal nome
    /// dell'enumerazione produrrebbe <c>colorDodge</c>, che ogni browser ignora in silenzio.
    /// </summary>
    [Fact]
    public void IModiDiFusioneUsanoINomiDellaSpecifica()
    {
        var filtro = new PatternFilter();
        filtro.Primitives.Add(new BlendPrimitive { Mode = BlendMode.ColorDodge, In2 = "sotto" });

        var svg = Filtri().RenderFilterDefinition(filtro, "f")!;

        Assert.Contains("mode=\"color-dodge\"", svg);
        Assert.Contains("in2=\"sotto\"", svg);
    }

    [Fact]
    public void ICoefficientiDellaComposizioneSiScrivonoSoloSeServono()
    {
        var filtro = new PatternFilter();
        filtro.Primitives.Add(new CompositePrimitive { Operator = CompositeOperator.In });

        var sopra = Filtri().RenderFilterDefinition(filtro, "f")!;
        Assert.DoesNotContain("k1=", sopra);

        filtro.Primitives.Clear();
        filtro.Primitives.Add(new CompositePrimitive { Operator = CompositeOperator.Arithmetic, K1 = 0.5 });

        var aritmetica = Filtri().RenderFilterDefinition(filtro, "f")!;
        Assert.Contains("operator=\"arithmetic\"", aritmetica);
        Assert.Contains("k1=\"0.5\"", aritmetica);
    }

    /// <summary>
    /// <c>luminanceToAlpha</c> non prende <c>values</c>: scriverlo sarebbe un attributo che
    /// la specifica dichiara ignorato, cioè rumore che sembra una regolazione.
    /// </summary>
    [Fact]
    public void LaLuminositaInTrasparenzaNonScriveValori()
    {
        var filtro = new PatternFilter();
        filtro.Primitives.Add(new ColorMatrixPrimitive { Kind = ColorMatrixKind.LuminanceToAlpha, Value = 7 });

        var svg = Filtri().RenderFilterDefinition(filtro, "f")!;

        Assert.Contains("type=\"luminanceToAlpha\"", svg);
        Assert.DoesNotContain("values=", svg);
    }

    [Fact]
    public void SoloICanaliModificatiDiventanoUnNodo()
    {
        var livelli = new ComponentTransferPrimitive();
        livelli.R.Kind = TransferFunctionKind.Linear;
        livelli.R.Slope = 1.5;

        var filtro = new PatternFilter();
        filtro.Primitives.Add(livelli);

        var svg = Filtri().RenderFilterDefinition(filtro, "f")!;

        Assert.Contains("<feFuncR type=\"linear\" slope=\"1.5\" intercept=\"0\" />", svg);
        Assert.DoesNotContain("feFuncG", svg);
        Assert.DoesNotContain("feFuncA", svg);
    }

    [Fact]
    public void LaSovrapposizioneScriveUnNodoPerLivello()
    {
        var filtro = new PatternFilter();
        filtro.Primitives.Add(new MergePrimitive { Inputs = ["", "SourceGraphic"] });

        var svg = Filtri().RenderFilterDefinition(filtro, "f")!;

        Assert.Contains("<feMerge>", svg);
        Assert.Contains("<feMergeNode />", svg);
        Assert.Contains("<feMergeNode in=\"SourceGraphic\" />", svg);
        Assert.Contains("</feMerge>", svg);
    }

    /// <summary>
    /// I nomi dei risultati li scrive l'utente: un apice chiuderebbe l'attributo e
    /// produrrebbe un documento malformato.
    /// </summary>
    [Fact]
    public void INomiScrittiDallUtenteVengonoProtetti()
    {
        var filtro = new PatternFilter();
        filtro.Primitives.Add(new GaussianBlurPrimitive { Result = "il \"mio\" risultato" });

        var svg = Filtri().RenderFilterDefinition(filtro, "f")!;

        Assert.Contains("result=\"il &quot;mio&quot; risultato\"", svg);
    }

    [Fact]
    public void UnPassaggioSpentoNonVieneGenerato()
    {
        var filtro = new PatternFilter();
        filtro.Primitives.Add(new GaussianBlurPrimitive());
        filtro.Primitives.Add(new MorphologyPrimitive { Enabled = false });

        var svg = Filtri().RenderFilterDefinition(filtro, "f")!;

        Assert.Contains("feGaussianBlur", svg);
        Assert.DoesNotContain("feMorphology", svg);
    }

    // ------------------------------------------------------- dove il filtro si applica --

    /// <summary>
    /// Il filtro sta sul rettangolo dipinto e non dentro la tessera. Dentro, una sfocatura
    /// verrebbe tagliata al bordo della tessera e il taglio si ripeterebbe identico a ogni
    /// ripetizione, disegnando una griglia di cuciture che nel motivo non c'è.
    /// </summary>
    [Fact]
    public void IlFiltroSiApplicaAllaSuperficieENonAllaTessera()
    {
        var svg = Renderer().RenderStandaloneSvg(ConFiltro(new GaussianBlurPrimitive()), "100%", "100%", "p");

        var apertura = svg.IndexOf("<pattern", StringComparison.Ordinal);
        var chiusura = svg.IndexOf("</pattern>", StringComparison.Ordinal);
        var tessera = svg[apertura..chiusura];

        Assert.DoesNotContain("filter=", tessera);
        Assert.Contains("<filter id=\"f-p\"", svg);
        Assert.Contains("filter=\"url(#f-p)\"", svg);
    }

    /// <summary>
    /// Con un filtro attivo il rettangolo si allarga: una primitiva che sconfina legge i
    /// pixel appena fuori dall'oggetto, e su un rettangolo grande quanto la pagina là non
    /// c'è niente — il risultato sarebbe una sfumatura verso il trasparente lungo i quattro
    /// bordi, cioè un alone che il motivo non ha.
    /// </summary>
    [Fact]
    public void ConUnFiltroIlRettangoloSuperaLaFinestra()
    {
        var svg = Renderer().RenderStandaloneSvg(ConFiltro(new GaussianBlurPrimitive()), "100%", "100%", "p");

        Assert.Contains("<rect x=\"-25%\" y=\"-25%\" width=\"150%\" height=\"150%\"", svg);
    }

    /// <summary>
    /// Senza filtro non cambia una virgola. Vale per i quattrocento documenti già in
    /// archivio: una funzione che non si usa non deve riscrivere ciò che c'era.
    /// </summary>
    [Fact]
    public void SenzaFiltroIlDocumentoEQuelloDiPrima()
    {
        var pattern = ConFiltro();
        pattern.Definition.Filter = null;

        var svg = Renderer().RenderStandaloneSvg(pattern, "100%", "100%", "p");

        Assert.Contains("<rect width=\"100%\" height=\"100%\" fill=\"url(#p)\" />", svg);
        Assert.DoesNotContain("<filter", svg);
    }

    [Fact]
    public void LaCellaSingolaMostraIlFiltroSuUnGruppo()
    {
        var svg = Renderer().RenderSingleCellSvg(ConFiltro(new GaussianBlurPrimitive()), 120, "fc");

        Assert.Contains("<filter id=\"fc\"", svg);
        Assert.Contains("<g filter=\"url(#fc)\">", svg);
        Assert.Contains("</g>", svg);
    }

    [Fact]
    public void LaCellaSingolaSenzaFiltroNonHaDefs()
    {
        var pattern = ConFiltro();
        pattern.Definition.Filter = null;

        Assert.DoesNotContain("<defs>", Renderer().RenderSingleCellSvg(pattern, 120));
    }

    // -------------------------------------------------------------------- la copia ------

    /// <summary>
    /// Duplicare un pattern non deve lasciare i due filtri collegati: il difetto che ne
    /// nascerebbe è muovere un cursore su una copia e vedere cambiare l'originale, e si
    /// scopre solo riaprendo l'altro.
    /// </summary>
    [Fact]
    public void LaCopiaDiUnPatternHaUnFiltroTuttoSuo()
    {
        var originale = ConFiltro(new GaussianBlurPrimitive { StdDeviationX = 3 });
        var copia = new PatternCloner(new VectorElementCloner(new PatternSerializer(new VectorElementPluginRegistry())))
            .Clone(originale, "Copia");

        Assert.NotNull(copia.Definition.Filter);
        Assert.NotSame(originale.Definition.Filter, copia.Definition.Filter);

        ((GaussianBlurPrimitive)copia.Definition.Filter!.Primitives[0]).StdDeviationX = 9;

        Assert.Equal(3, ((GaussianBlurPrimitive)originale.Definition.Filter!.Primitives[0]).StdDeviationX);
    }

    /// <summary>
    /// Anche le liste dentro una primitiva vanno sdoppiate: una matrice condivisa è il caso
    /// in cui il difetto si nota di meno e fa più danno.
    /// </summary>
    [Fact]
    public void AncheLeListeDentroLePrimitiveVengonoSdoppiate()
    {
        var originale = new ColorMatrixPrimitive { Kind = ColorMatrixKind.Matrix };
        var copia = (ColorMatrixPrimitive)originale.Copia();

        copia.Matrix[0] = 42;

        Assert.Equal(1, originale.Matrix[0]);
    }

    // ------------------------------------------------------------------- gli effetti ----

    /// <summary>
    /// Ogni effetto pronto deve produrre un filtro che il generatore sa scrivere. È il test
    /// che protegge dal caso in cui si aggiunge un preset e ci si dimentica di provarlo.
    /// </summary>
    [Fact]
    public void OgniEffettoProntoProduceUnFiltroGenerabile()
    {
        foreach (var preset in FilterPresets.Tutti)
        {
            var filtro = new PatternFilter();
            foreach (var passo in preset.Passaggi())
            {
                filtro.Primitives.Add(passo);
            }

            var svg = Filtri().RenderFilterDefinition(filtro, "f");

            Assert.False(string.IsNullOrWhiteSpace(svg), $"L'effetto «{preset.Nome}» non produce alcun filtro.");
            Assert.StartsWith("    <filter", svg);
            Assert.EndsWith("</filter>\n", svg);
        }
    }

    /// <summary>
    /// Un effetto pronto non deve produrre né errori né avvisi.
    ///
    /// <para>
    /// Gli errori perché sono il punto di partenza consigliato, e uno che facesse comparire un
    /// messaggio rosso appena scelto sarebbe peggio di niente. Gli avvisi perché questo
    /// controllo ha già pagato: «Carta invecchiata» metteva una fusione subito dopo un rumore
    /// senza dichiararne l'ingresso, e il risultato a schermo era rumore e basta — il disegno
    /// non c'era più. Un difetto che si vede solo guardando, e che da qui in poi si vede anche
    /// senza guardare.
    /// </para>
    /// </summary>
    [Fact]
    public void NessunEffettoProntoContieneErroriOAvvisi()
    {
        var registro = new VectorElementPluginRegistry();
        registro.Register(new RectPlugin());
        var validatore = new PatternValidator(registro);

        foreach (var preset in FilterPresets.Tutti)
        {
            var pattern = ConFiltro([.. preset.Passaggi()]);
            var esito = validatore.Validate(pattern);

            Assert.True(esito.IsValid, $"L'effetto «{preset.Nome}»: {string.Join(" / ", esito.Errors)}");
            Assert.True(
                esito.Warnings.Count == 0,
                $"L'effetto «{preset.Nome}»: {string.Join(" / ", esito.Warnings)}");
        }
    }
}
