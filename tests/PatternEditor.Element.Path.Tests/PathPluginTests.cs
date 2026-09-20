using PatternEditor.Element.Path;
using Xunit;

namespace PatternEditor.Element.Path.Tests;

/// <summary>
/// Verifica il plugin dell'elemento «Path»: il tracciato, di cui si verifica che il comando non venga riscritto
///
/// <para>
/// La struttura è la stessa per tutti i plugin, perché lo è il contratto: valori predefiniti
/// già validi, validazione che accumula gli errori invece di fermarsi al primo, e markup
/// generato con la cultura invariante e i caratteri speciali protetti. Sono i tre modi in cui
/// un plugin può sbagliare in silenzio.
/// </para>
/// </summary>
public class PathPluginTests
{
    private readonly PathPlugin _plugin = new();

    [Fact]
    public void Type_is_path()
    {
        Assert.Equal("path", _plugin.Type);
    }

    [Fact]
    public void Create_returns_a_valid_default_element()
    {
        var element = Assert.IsType<PathElement>(_plugin.Create());

        Assert.True(_plugin.Validate(element).IsValid);
        Assert.False(string.IsNullOrWhiteSpace(element.D));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_an_empty_path(string emptyPath)
    {
        var element = PathElement.CreateDefault();
        element.D = emptyPath;

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty"));
    }

    [Fact]
    public void Validate_requires_the_path_to_start_with_a_moveto()
    {
        var element = PathElement.CreateDefault();
        element.D = "L 10 10";

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("move command"));
    }

    [Theory]
    [InlineData("M 0 25 L 25 0 L 50 25")]
    [InlineData("m0 0l10 10z")]
    [InlineData("M 0,0 C 10,20 30,20 40,0 Z")]
    [InlineData("M 0 0 A 25 25 0 1 1 50 0")]
    [InlineData("M 1e-3 0 H 50 V 50 Z")]
    public void Validate_accepts_the_svg_path_commands(string d)
    {
        var element = PathElement.CreateDefault();
        element.D = d;

        var result = _plugin.Validate(element);

        Assert.True(result.IsValid, string.Join(" / ", result.Errors));
    }

    [Fact]
    public void Validate_rejects_characters_that_are_not_path_data()
    {
        var element = PathElement.CreateDefault();
        element.D = "M 0 0 L <script> 10";

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not allowed"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_rejects_out_of_range_opacity(double invalidOpacity)
    {
        var element = PathElement.CreateDefault();
        element.Opacity = invalidOpacity;

        Assert.False(_plugin.Validate(element).IsValid);
    }

    [Fact]
    public void Render_produces_the_expected_attributes_when_stroked_without_fill()
    {
        var element = PathElement.CreateDefault();

        var svg = _plugin.Render(element);

        Assert.StartsWith("<path ", svg);
        Assert.EndsWith("/>", svg);
        Assert.Contains("d=\"M 0 25 L 25 0 L 50 25\"", svg);
        Assert.Contains("fill=\"none\"", svg);
        Assert.DoesNotContain("fill-opacity", svg);
        Assert.Contains("stroke=\"#000000\"", svg);
        Assert.Contains("stroke-width=\"2\"", svg);
        Assert.Contains("opacity=\"1\"", svg);
    }

    [Fact]
    public void Render_escapes_the_path_so_that_invalid_input_cannot_break_the_markup()
    {
        // La validazione segnala l'errore ma l'anteprima viene comunque generata mentre
        // si scrive: il markup prodotto deve restare ben formato in ogni caso.
        var element = PathElement.CreateDefault();
        element.D = "M 0 0\" /><script>alert('x')</script>";

        var svg = _plugin.Render(element);

        Assert.DoesNotContain("<script>", svg);
        Assert.Equal(1, svg.Split('<').Length - 1);
        Assert.Contains("&quot;", svg);
    }

    [Fact]
    public void Render_writes_decimals_with_a_dot_regardless_of_the_current_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("it-IT");
            var element = PathElement.CreateDefault();
            element.StrokeWidth = 1.5;

            Assert.Contains("stroke-width=\"1.5\"", _plugin.Render(element));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
