using PatternEditor.Element.Circle;
using Xunit;

namespace PatternEditor.Element.Circle.Tests;

/// <summary>
/// Verifica il plugin dell'elemento «Circle»: il cerchio, il cui raggio nullo la specifica tratterebbe come elemento invisibile
///
/// <para>
/// La struttura è la stessa per tutti i plugin, perché lo è il contratto: valori predefiniti
/// già validi, validazione che accumula gli errori invece di fermarsi al primo, e markup
/// generato con la cultura invariante e i caratteri speciali protetti. Sono i tre modi in cui
/// un plugin può sbagliare in silenzio.
/// </para>
/// </summary>
public class CirclePluginTests
{
    private readonly CirclePlugin _plugin = new();

    [Fact]
    public void Type_is_circle()
    {
        Assert.Equal("circle", _plugin.Type);
    }

    [Fact]
    public void Create_returns_a_valid_default_element()
    {
        var element = Assert.IsType<CircleElement>(_plugin.Create());

        Assert.True(_plugin.Validate(element).IsValid);
        Assert.True(element.R > 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_rejects_a_non_positive_radius(double invalidRadius)
    {
        var element = CircleElement.CreateDefault();
        element.R = invalidRadius;

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("radius"));
    }

    [Fact]
    public void Validate_rejects_a_negative_stroke_width()
    {
        var element = CircleElement.CreateDefault();
        element.Stroke = "#000000";
        element.StrokeWidth = -1;

        Assert.False(_plugin.Validate(element).IsValid);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_rejects_out_of_range_opacity(double invalidOpacity)
    {
        var element = CircleElement.CreateDefault();
        element.Opacity = invalidOpacity;

        Assert.False(_plugin.Validate(element).IsValid);
    }

    [Fact]
    public void Validate_accepts_a_centre_outside_the_cell()
    {
        // Le coordinate non sono normalizzate rispetto alla cella: un cerchio può
        // sporgere o essere centrato fuori, per ottenere motivi che continuano oltre
        // il bordo della cella.
        var element = CircleElement.CreateDefault();
        element.Cx = -10;
        element.Cy = 120;

        Assert.True(_plugin.Validate(element).IsValid);
    }

    [Fact]
    public void Render_produces_the_expected_attributes_when_filled_without_stroke()
    {
        var element = CircleElement.CreateDefault();
        element.Cx = 25;
        element.Cy = 25;
        element.R = 15;
        element.Fill = "#AEFAEF";
        element.FillOpacity = 1;
        element.Stroke = null;
        element.Opacity = 1;

        var svg = _plugin.Render(element);

        Assert.StartsWith("<circle ", svg);
        Assert.EndsWith("/>", svg);
        Assert.Contains("cx=\"25\"", svg);
        Assert.Contains("cy=\"25\"", svg);
        Assert.Contains("r=\"15\"", svg);
        Assert.Contains("fill=\"#AEFAEF\"", svg);
        Assert.Contains("fill-opacity=\"1\"", svg);
        Assert.Contains("stroke=\"none\"", svg);
        Assert.DoesNotContain("stroke-width", svg);
        Assert.Contains("opacity=\"1\"", svg);
    }

    [Fact]
    public void Render_omits_fill_opacity_when_there_is_no_fill()
    {
        var element = CircleElement.CreateDefault();
        element.Fill = null;

        var svg = _plugin.Render(element);

        Assert.Contains("fill=\"none\"", svg);
        Assert.DoesNotContain("fill-opacity", svg);
    }

    [Fact]
    public void Render_writes_decimals_with_a_dot_regardless_of_the_current_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("it-IT");
            var element = CircleElement.CreateDefault();
            element.R = 12.5;

            Assert.Contains("r=\"12.5\"", _plugin.Render(element));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
