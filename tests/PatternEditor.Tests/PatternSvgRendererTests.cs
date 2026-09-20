using System.Globalization;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Models;
using PatternEditor.Element.Line;
using PatternEditor.Element.Rect;
using PatternEditor.Services;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// Verifica la generazione del documento SVG.
///
/// <para>
/// Il renderer è l'unica sorgente del markup: lo stesso codice alimenta le anteprime, il
/// riquadro del sorgente, il file scaricato e la miniatura dell'elenco. Un difetto qui si
/// manifesta ovunque insieme, ed è il motivo per cui i test guardano il testo prodotto
/// carattere per carattere invece di limitarsi a controllare che non sollevi eccezioni.
/// </para>
///
/// <para>
/// Fra i casi c'è l'identificativo del nodo <c>&lt;pattern&gt;</c>: deve poter essere
/// diverso a ogni resa, perché <c>url(#id)</c> si risolve sull'intero documento HTML e due
/// anteprime dello stesso pattern nella stessa pagina si oscurerebbero a vicenda.
/// </para>
/// </summary>
public class PatternSvgRendererTests
{
    private static (PatternSvgRenderer renderer, VectorElementPluginRegistry registry) CreateRenderer()
    {
        var registry = new VectorElementPluginRegistry();
        registry.Register(new RectPlugin());
        registry.Register(new LinePlugin());
        return (new PatternSvgRenderer(registry, new SvgFilterRenderer()), registry);
    }

    private static Pattern CreateSamplePattern()
    {
        var pattern = new Pattern { Name = "Test" };
        pattern.Definition.Width = 50;
        pattern.Definition.Height = 50;
        pattern.Definition.Elements.Add(RectElement.CreateDefault());
        return pattern;
    }

    [Fact]
    public void Standalone_svg_contains_defs_pattern_and_fill_rect()
    {
        var (renderer, _) = CreateRenderer();
        var svg = renderer.RenderStandaloneSvg(CreateSamplePattern());

        Assert.Contains("<defs>", svg);
        Assert.Contains("<pattern id=\"p\"", svg);
        Assert.Contains("patternUnits=\"userSpaceOnUse\"", svg);
        Assert.Contains("fill=\"url(#p)\"", svg);
        Assert.Contains("<rect ", svg); // l'elemento generato dal plugin Rect
    }

    [Fact]
    public void Identity_transform_does_not_emit_patternTransform_attribute()
    {
        var (renderer, _) = CreateRenderer();
        var pattern = CreateSamplePattern(); // Scale=1, Rotation=0, Translate=0,0 di default

        var svg = renderer.RenderStandaloneSvg(pattern);

        Assert.DoesNotContain("patternTransform", svg);
    }

    [Fact]
    public void Non_identity_transform_is_reflected_in_patternTransform()
    {
        var (renderer, _) = CreateRenderer();
        var pattern = CreateSamplePattern();
        pattern.Definition.Scale = 0.5;
        pattern.Definition.Rotation = 45;

        var svg = renderer.RenderStandaloneSvg(pattern);

        Assert.Contains("patternTransform=\"scale(0.5) rotate(45) translate(0,0)\"", svg);
    }

    [Fact]
    public void Standalone_and_single_cell_svg_use_the_same_element_markup()
    {
        var (renderer, _) = CreateRenderer();
        var pattern = CreateSamplePattern();

        var elementsMarkup = renderer.RenderElementsMarkup(pattern);
        var standalone = renderer.RenderStandaloneSvg(pattern);
        var singleCell = renderer.RenderSingleCellSvg(pattern, 250);

        // Coerenza tra preview e SVG finale: stessa geometria, stesso renderer dei plugin.
        Assert.Contains(elementsMarkup.Trim(), standalone);
        Assert.Contains(elementsMarkup.Trim(), singleCell);
    }

    [Theory]
    // cella larga: il lato lungo prende tutto l'ingombro, l'altezza segue
    [InlineData(48, 24, 240, 120)]
    // cella alta: si inverte
    [InlineData(24, 48, 120, 240)]
    // cella quadrata: il riquadro resta quadrato
    [InlineData(50, 50, 240, 240)]
    // rapporti estremi: il riquadro non sfonda mai l'ingombro richiesto
    [InlineData(150, 20, 240, 32)]
    public void The_single_cell_box_has_the_shape_of_the_cell(
        double width, double height, double expectedWidth, double expectedHeight)
    {
        var (renderer, _) = CreateRenderer();
        var pattern = CreateSamplePattern();
        pattern.Definition.Width = width;
        pattern.Definition.Height = height;

        var (boxWidth, boxHeight) = renderer.SingleCellDisplaySize(pattern, 240);

        Assert.Equal(expectedWidth, boxWidth, 2);
        Assert.Equal(expectedHeight, boxHeight, 2);

        // Le stesse misure devono finire nel markup: il riquadro disegnato e quello
        // dichiarato al contenitore sono lo stesso riquadro.
        var svg = renderer.RenderSingleCellSvg(pattern, 240);
        Assert.Contains($"width=\"{boxWidth.ToString(CultureInfo.InvariantCulture)}\"", svg);
        Assert.Contains($"height=\"{boxHeight.ToString(CultureInfo.InvariantCulture)}\"", svg);
    }

    [Fact]
    public void The_single_cell_box_never_exceeds_the_room_it_was_given()
    {
        var (renderer, _) = CreateRenderer();
        var pattern = CreateSamplePattern();

        foreach (var (width, height) in new[] { (1.0, 1000.0), (1000.0, 1.0), (7.0, 3.0), (3.0, 7.0) })
        {
            pattern.Definition.Width = width;
            pattern.Definition.Height = height;

            var (boxWidth, boxHeight) = renderer.SingleCellDisplaySize(pattern, 200);

            Assert.InRange(boxWidth, 0, 200);
            Assert.InRange(boxHeight, 0, 200);
            Assert.Equal(200, Math.Max(boxWidth, boxHeight), 2);

            // La forma è quella della cella, non un'approssimazione comoda.
            Assert.Equal(width / height, boxWidth / boxHeight, 2);
        }
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(50, 0)]
    [InlineData(-10, 50)]
    [InlineData(double.NaN, 50)]
    public void A_cell_without_a_usable_side_falls_back_to_a_square_instead_of_dividing_by_zero(
        double width, double height)
    {
        // Una cella così è un documento non valido, e il validatore lo dice. L'anteprima
        // però viene disegnata a ogni battuta, anche mentre il campo è vuoto perché lo si
        // sta riscrivendo: deve reggere il valore intermedio senza far saltare l'editor.
        var (renderer, _) = CreateRenderer();
        var pattern = CreateSamplePattern();
        pattern.Definition.Width = width;
        pattern.Definition.Height = height;

        var (boxWidth, boxHeight) = renderer.SingleCellDisplaySize(pattern, 240);

        Assert.Equal(240, boxWidth);
        Assert.Equal(240, boxHeight);
        Assert.Contains("<svg", renderer.RenderSingleCellSvg(pattern, 240));
    }

    [Fact]
    public void Unknown_element_is_skipped_in_rendering_but_not_lost_from_the_model()
    {
        var (renderer, _) = CreateRenderer();
        var pattern = CreateSamplePattern();
        var unknown = new UnknownVectorElement(Guid.CreateVersion7(), "tipo-senza-plugin",
            System.Text.Json.JsonDocument.Parse("{\"type\":\"tipo-senza-plugin\"}").RootElement);
        pattern.Definition.Elements.Add(unknown);

        var svg = renderer.RenderStandaloneSvg(pattern);

        Assert.DoesNotContain("tipo-senza-plugin", svg);
        Assert.Contains(unknown, pattern.Definition.Elements);
    }

    [Fact]
    public void Embedded_svg_uses_a_pattern_id_derived_from_the_pattern_identity()
    {
        // Due anteprime nella stessa pagina non devono condividere l'id del nodo <pattern>:
        // fill="url(#id)" viene risolto sull'intero documento HTML, quindi con id uguali
        // tutte le anteprime mostrerebbero la prima definizione incontrata.
        var (renderer, _) = CreateRenderer();
        var first = CreateSamplePattern();
        var second = CreateSamplePattern();

        var firstSvg = renderer.RenderStandaloneSvg(first, "100%", "100%");
        var secondSvg = renderer.RenderStandaloneSvg(second, "100%", "100%");

        Assert.Contains($"<pattern id=\"p-{first.Id:N}\"", firstSvg);
        Assert.Contains($"fill=\"url(#p-{first.Id:N})\"", firstSvg);
        Assert.Contains($"<pattern id=\"p-{second.Id:N}\"", secondSvg);
        Assert.DoesNotContain($"p-{second.Id:N}", firstSvg);
    }

    [Fact]
    public void Embedded_svg_honours_an_explicit_pattern_id()
    {
        // Caso dell'editor aperto sopra l'elenco: stesso Pattern (stesso Id) reso due volte
        // nella stessa pagina. L'anteprima dell'editor impone un id proprio.
        var (renderer, _) = CreateRenderer();
        var pattern = CreateSamplePattern();

        var svg = renderer.RenderStandaloneSvg(pattern, "100%", "100%", "pe-preview-abc");

        Assert.Contains("<pattern id=\"pe-preview-abc\"", svg);
        Assert.Contains("fill=\"url(#pe-preview-abc)\"", svg);
        Assert.DoesNotContain($"p-{pattern.Id:N}", svg);
    }

    [Fact]
    public void Downloaded_svg_keeps_the_default_pattern_id()
    {
        // Il file scaricato è un documento a se stante: nessuna collisione possibile.
        var (renderer, _) = CreateRenderer();

        var svg = renderer.RenderStandaloneSvg(CreateSamplePattern());

        Assert.Contains("<pattern id=\"p\"", svg);
    }
}
