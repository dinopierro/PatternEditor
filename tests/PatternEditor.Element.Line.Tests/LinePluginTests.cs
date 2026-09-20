using PatternEditor.Element.Line;
using Xunit;

namespace PatternEditor.Element.Line.Tests;

/// <summary>
/// Verifica il plugin dell'elemento «Line»: la linea, unico elemento senza riempimento
///
/// <para>
/// La struttura è la stessa per tutti i plugin, perché lo è il contratto: valori predefiniti
/// già validi, validazione che accumula gli errori invece di fermarsi al primo, e markup
/// generato con la cultura invariante e i caratteri speciali protetti. Sono i tre modi in cui
/// un plugin può sbagliare in silenzio.
/// </para>
/// </summary>
public class LinePluginTests
{
    private readonly LinePlugin _plugin = new();

    [Fact]
    public void Type_is_line()
    {
        Assert.Equal("line", _plugin.Type);
    }

    [Fact]
    public void Create_returns_a_valid_default_element()
    {
        var element = Assert.IsType<LineElement>(_plugin.Create());

        Assert.True(_plugin.Validate(element).IsValid);
    }

    [Fact]
    public void Validate_allows_negative_coordinates()
    {
        var element = LineElement.CreateDefault();
        element.X1 = -100;
        element.Y2 = -50;

        Assert.True(_plugin.Validate(element).IsValid);
    }

    [Fact]
    public void Validate_rejects_negative_stroke_width()
    {
        var element = LineElement.CreateDefault();
        element.StrokeWidth = -1;

        Assert.False(_plugin.Validate(element).IsValid);
    }

    [Fact]
    public void Render_produces_the_expected_svg_line_element()
    {
        var element = LineElement.CreateDefault();
        element.X1 = 0;
        element.Y1 = 0;
        element.X2 = 50;
        element.Y2 = 50;
        element.Stroke = "#000000";
        element.StrokeWidth = 1;
        element.StrokeOpacity = 1;
        element.Opacity = 1;

        var svg = _plugin.Render(element);

        Assert.Contains("x1=\"0\"", svg);
        Assert.Contains("y1=\"0\"", svg);
        Assert.Contains("x2=\"50\"", svg);
        Assert.Contains("y2=\"50\"", svg);
        Assert.Contains("stroke=\"#000000\"", svg);
        Assert.Contains("stroke-width=\"1\"", svg);
        Assert.Contains("stroke-opacity=\"1\"", svg);
        Assert.Contains("opacity=\"1\"", svg);
        Assert.DoesNotContain("fill", svg);
    }
}
