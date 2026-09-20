using PatternEditor.Element.Polyline;
using Xunit;

namespace PatternEditor.Element.Polyline.Tests;

/// <summary>
/// Verifica il plugin dell'elemento «Polyline»: la spezzata, che di vertici ne richiede due e non viene chiusa
///
/// <para>
/// La struttura è la stessa per tutti i plugin, perché lo è il contratto: valori predefiniti
/// già validi, validazione che accumula gli errori invece di fermarsi al primo, e markup
/// generato con la cultura invariante e i caratteri speciali protetti. Sono i tre modi in cui
/// un plugin può sbagliare in silenzio.
/// </para>
/// </summary>
public class PolylinePluginTests
{
    private readonly PolylinePlugin _plugin = new();

    [Fact]
    public void Type_is_polyline()
    {
        Assert.Equal("polyline", _plugin.Type);
    }

    [Fact]
    public void Create_returns_a_valid_default_element_that_is_stroked_and_not_filled()
    {
        var element = Assert.IsType<PolylineElement>(_plugin.Create());

        Assert.True(_plugin.Validate(element).IsValid);
        Assert.Null(element.Fill);
        Assert.NotNull(element.Stroke);
    }

    [Fact]
    public void Two_vertices_are_enough_unlike_a_polygon()
    {
        // Una spezzata con due vertici è un segmento: legittima. Un poligono con due
        // vertici non racchiuderebbe alcuna superficie.
        var element = PolylineElement.CreateDefault();
        element.Points = "0,0 10,10";

        Assert.True(_plugin.Validate(element).IsValid);
    }

    [Fact]
    public void Validate_rejects_a_single_vertex()
    {
        var element = PolylineElement.CreateDefault();
        element.Points = "5,5";

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("at least 2 vertices"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0,0 10,0 10")]
    [InlineData("0,0 x,0")]
    public void Validate_rejects_a_malformed_list_of_points(string points)
    {
        var element = PolylineElement.CreateDefault();
        element.Points = points;

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("pairs of numbers"));
    }

    [Fact]
    public void Render_produces_the_expected_attributes()
    {
        var element = PolylineElement.CreateDefault();

        var svg = _plugin.Render(element);

        Assert.StartsWith("<polyline ", svg);
        Assert.EndsWith("/>", svg);
        Assert.Contains("points=\"2,38 14,20 26,30 38,8 48,20\"", svg);
        Assert.Contains("fill=\"none\"", svg);
        Assert.DoesNotContain("fill-opacity", svg);
        Assert.Contains("stroke=\"#000000\"", svg);
        Assert.Contains("stroke-width=\"2\"", svg);
    }

    [Fact]
    public void Render_escapes_the_points_so_that_invalid_input_cannot_break_the_markup()
    {
        var element = PolylineElement.CreateDefault();
        element.Points = "0,0\" /><script>alert('x')</script>";

        var svg = _plugin.Render(element);

        Assert.DoesNotContain("<script>", svg);
        Assert.Equal(1, svg.Split('<').Length - 1);
    }

    [Fact]
    public void Render_writes_decimals_with_a_dot_regardless_of_the_current_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("it-IT");
            var element = PolylineElement.CreateDefault();
            element.StrokeWidth = 1.5;

            Assert.Contains("stroke-width=\"1.5\"", _plugin.Render(element));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
