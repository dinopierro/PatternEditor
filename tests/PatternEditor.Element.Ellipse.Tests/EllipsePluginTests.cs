using PatternEditor.Element.Ellipse;
using Xunit;

namespace PatternEditor.Element.Ellipse.Tests;

/// <summary>
/// Verifica il plugin dell'elemento «Ellipse»: l'ellisse, con i due semiassi da verificare separatamente
///
/// <para>
/// La struttura è la stessa per tutti i plugin, perché lo è il contratto: valori predefiniti
/// già validi, validazione che accumula gli errori invece di fermarsi al primo, e markup
/// generato con la cultura invariante e i caratteri speciali protetti. Sono i tre modi in cui
/// un plugin può sbagliare in silenzio.
/// </para>
/// </summary>
public class EllipsePluginTests
{
    private readonly EllipsePlugin _plugin = new();

    [Fact]
    public void Type_is_ellipse()
    {
        Assert.Equal("ellipse", _plugin.Type);
    }

    [Fact]
    public void Create_returns_a_valid_default_element_with_two_different_radii()
    {
        var element = Assert.IsType<EllipseElement>(_plugin.Create());

        Assert.True(_plugin.Validate(element).IsValid);
        Assert.NotEqual(element.Rx, element.Ry);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    public void Validate_rejects_a_non_positive_semi_axis(double rx, double ry)
    {
        var element = EllipseElement.CreateDefault();
        element.Rx = rx;
        element.Ry = ry;

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("semi-axis"));
    }

    [Fact]
    public void Equal_radii_are_valid_and_simply_describe_a_circle()
    {
        var element = EllipseElement.CreateDefault();
        element.Rx = 15;
        element.Ry = 15;

        Assert.True(_plugin.Validate(element).IsValid);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_rejects_out_of_range_opacity(double invalidOpacity)
    {
        var element = EllipseElement.CreateDefault();
        element.Opacity = invalidOpacity;

        Assert.False(_plugin.Validate(element).IsValid);
    }

    [Fact]
    public void Render_produces_the_expected_attributes()
    {
        var element = EllipseElement.CreateDefault();

        var svg = _plugin.Render(element);

        Assert.StartsWith("<ellipse ", svg);
        Assert.EndsWith("/>", svg);
        Assert.Contains("cx=\"25\"", svg);
        Assert.Contains("cy=\"25\"", svg);
        Assert.Contains("rx=\"20\"", svg);
        Assert.Contains("ry=\"12\"", svg);
        Assert.Contains("fill=\"#AEFAEF\"", svg);
        Assert.Contains("stroke=\"none\"", svg);
        Assert.DoesNotContain("stroke-width", svg);
    }

    [Fact]
    public void Render_writes_decimals_with_a_dot_regardless_of_the_current_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("it-IT");
            var element = EllipseElement.CreateDefault();
            element.Ry = 7.5;

            Assert.Contains("ry=\"7.5\"", _plugin.Render(element));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
