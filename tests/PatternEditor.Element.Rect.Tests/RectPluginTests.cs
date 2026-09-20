using PatternEditor.Element.Rect;
using Xunit;

namespace PatternEditor.Element.Rect.Tests;

/// <summary>
/// Verifica il plugin dell'elemento «Rect»: il rettangolo, con i suoi lati che devono essere positivi
///
/// <para>
/// La struttura è la stessa per tutti i plugin, perché lo è il contratto: valori predefiniti
/// già validi, validazione che accumula gli errori invece di fermarsi al primo, e markup
/// generato con la cultura invariante e i caratteri speciali protetti. Sono i tre modi in cui
/// un plugin può sbagliare in silenzio.
/// </para>
/// </summary>
public class RectPluginTests
{
    private readonly RectPlugin _plugin = new();

    [Fact]
    public void Type_is_rect()
    {
        Assert.Equal("rect", _plugin.Type);
    }

    [Fact]
    public void Create_returns_a_valid_default_element()
    {
        var element = Assert.IsType<RectElement>(_plugin.Create());

        Assert.True(_plugin.Validate(element).IsValid);
        Assert.True(element.Width > 0);
        Assert.True(element.Height > 0);
    }

    [Fact]
    public void Validate_rejects_non_positive_width_or_height()
    {
        var element = RectElement.CreateDefault();
        element.Width = 0;

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("width"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_rejects_out_of_range_opacity(double invalidOpacity)
    {
        var element = RectElement.CreateDefault();
        element.Opacity = invalidOpacity;

        Assert.False(_plugin.Validate(element).IsValid);
    }

    [Fact]
    public void Render_produces_expected_attributes_when_filled_and_stroked()
    {
        var element = RectElement.CreateDefault();
        element.X = 10;
        element.Y = 10;
        element.Width = 30;
        element.Height = 30;
        element.Fill = "#AEFAEF";
        element.FillOpacity = 1;
        element.Stroke = null;
        element.Opacity = 1;

        var svg = _plugin.Render(element);

        Assert.Contains("x=\"10\"", svg);
        Assert.Contains("y=\"10\"", svg);
        Assert.Contains("width=\"30\"", svg);
        Assert.Contains("height=\"30\"", svg);
        Assert.Contains("fill=\"#AEFAEF\"", svg);
        Assert.Contains("fill-opacity=\"1\"", svg);
        Assert.Contains("stroke=\"none\"", svg);
        Assert.DoesNotContain("stroke-width", svg);
        Assert.Contains("opacity=\"1\"", svg);
    }

    [Fact]
    public void Render_omits_fill_opacity_when_no_fill()
    {
        var element = RectElement.CreateDefault();
        element.Fill = null;

        var svg = _plugin.Render(element);

        Assert.Contains("fill=\"none\"", svg);
        Assert.DoesNotContain("fill-opacity", svg);
    }
}
