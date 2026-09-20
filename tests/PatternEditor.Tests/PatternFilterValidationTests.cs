using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Models;
using PatternEditor.Core.Models.Filters;
using PatternEditor.Element.Rect;
using PatternEditor.Services;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// Verifica i controlli sul filtro.
///
/// <para>
/// La divisione fra errore e avviso è il punto: sono errori i numeri che la specifica
/// dichiara illegali, perché un browser di fronte a un raggio negativo non disegna un effetto
/// strano — disabilita l'intero filtro, e il pattern esce dall'editor con un aspetto e si
/// apre altrove con un altro. Sono avvisi i collegamenti che non portano da nessuna parte:
/// producono un passaggio che non fa niente, che è legittimo e quasi sempre un refuso.
/// </para>
/// </summary>
public class PatternFilterValidationTests
{
    /// <summary>
    /// Il validatore con il plugin del rettangolo registrato: senza, ogni pattern di prova
    /// porterebbe un avviso «nessun plugin per il tipo rect» che non c’entra niente con il
    /// filtro e coprirebbe proprio gli avvisi che questi test vogliono contare.
    /// </summary>
    private static PatternValidator Validatore()
    {
        var registro = new VectorElementPluginRegistry();
        registro.Register(new RectPlugin());
        return new PatternValidator(registro);
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

    [Fact]
    public void UnRaggioNegativoEUnErrore()
    {
        var esito = Validatore().Validate(ConFiltro(new GaussianBlurPrimitive { StdDeviationX = -1 }));

        Assert.False(esito.IsValid);
        Assert.Contains(esito.Errors, e => e.Contains("negative"));
    }

    [Fact]
    public void UnaMatriceIncompletaEUnErrore()
    {
        var colore = new ColorMatrixPrimitive { Kind = ColorMatrixKind.Matrix, Matrix = [1, 0, 0] };

        var esito = Validatore().Validate(ConFiltro(colore));

        Assert.False(esito.IsValid);
        Assert.Contains(esito.Errors, e => e.Contains("twenty coefficients"));
    }

    [Fact]
    public void UnNucleoDiMisuraSbagliataEUnErrore()
    {
        var nucleo = new ConvolveMatrixPrimitive { Order = ConvolveOrder.Cinque };

        var esito = Validatore().Validate(ConFiltro(nucleo));

        Assert.False(esito.IsValid);
        Assert.Contains(esito.Errors, e => e.Contains("25 coefficients"));
    }

    [Fact]
    public void UnAreaSenzaSuperficieEUnErrore()
    {
        var pattern = ConFiltro(new GaussianBlurPrimitive());
        pattern.Definition.Filter!.Width = 0;

        var esito = Validatore().Validate(pattern);

        Assert.False(esito.IsValid);
        Assert.Contains(esito.Errors, e => e.Contains("filter area", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Un ingresso che non esiste produce un passaggio che non fa niente. È legittimo per la
    /// specifica e quasi sempre un refuso: avviso, non errore.
    /// </summary>
    [Fact]
    public void UnIngressoInesistenteEUnAvviso()
    {
        var esito = Validatore().Validate(ConFiltro(new GaussianBlurPrimitive { In = "rumroe" }));

        Assert.True(esito.IsValid);
        Assert.Contains(esito.Warnings, w => w.Contains("rumroe"));
    }

    [Fact]
    public void LeSorgentiDellaSpecificaSonoSempreValide()
    {
        var esito = Validatore().Validate(ConFiltro(new GaussianBlurPrimitive { In = "SourceAlpha" }));

        Assert.Empty(esito.Warnings);
    }

    [Fact]
    public void UnNomeProdottoPrimaEValido()
    {
        var esito = Validatore().Validate(ConFiltro(
            new TurbulencePrimitive { Result = "rumore" },
            new DisplacementMapPrimitive { In = "SourceGraphic", In2 = "rumore" }));

        Assert.Empty(esito.Warnings);
    }

    /// <summary>
    /// Un nome prodotto DOPO non vale: è il difetto tipico di chi riordina i passaggi dopo
    /// averli collegati, e il risultato è un effetto che sparisce senza spiegazioni.
    /// </summary>
    [Fact]
    public void UnNomeProdottoDopoNonVale()
    {
        var esito = Validatore().Validate(ConFiltro(
            new DisplacementMapPrimitive { In2 = "rumore" },
            new TurbulencePrimitive { Result = "rumore" }));

        Assert.Contains(esito.Warnings, w => w.Contains("rumore"));
    }

    [Fact]
    public void UnPassaggioIgnotoEUnAvvisoENonUnErrore()
    {
        var esito = Validatore().Validate(ConFiltro(new UnknownFilterPrimitive("feDomani", default)));

        Assert.True(esito.IsValid);
        Assert.Contains(esito.Warnings, w => w.Contains("feDomani"));
    }

    [Fact]
    public void UnPatternSenzaFiltroNonProduceNullaDiNuovo()
    {
        var pattern = ConFiltro();
        pattern.Definition.Filter = null;

        var esito = Validatore().Validate(pattern);

        Assert.True(esito.IsValid);
        Assert.Empty(esito.Warnings);
    }
}
