using PatternEditor.Element.Polygon;
using Xunit;

namespace PatternEditor.Element.Polygon.Tests;

/// <summary>
/// Verifica il plugin dell'elemento «Polygon»: il poligono, che richiede almeno tre vertici per racchiudere una superficie
///
/// <para>
/// La struttura è la stessa per tutti i plugin, perché lo è il contratto: valori predefiniti
/// già validi, validazione che accumula gli errori invece di fermarsi al primo, e markup
/// generato con la cultura invariante e i caratteri speciali protetti. Sono i tre modi in cui
/// un plugin può sbagliare in silenzio.
/// </para>
/// </summary>
public class PolygonPluginTests
{
    private readonly PolygonPlugin _plugin = new();

    [Fact]
    public void Type_is_polygon()
    {
        Assert.Equal("polygon", _plugin.Type);
    }

    [Fact]
    public void Create_returns_a_valid_default_element()
    {
        var element = Assert.IsType<PolygonElement>(_plugin.Create());

        Assert.True(_plugin.Validate(element).IsValid);
        Assert.False(string.IsNullOrWhiteSpace(element.Points));
    }

    [Theory]
    [InlineData("0,0 10,0 10,10")]
    [InlineData("0 0 10 0 10 10")]
    [InlineData("0,0  10,0\n10,10")]
    [InlineData("-2.5,0 10,0 10,1e1")]
    public void Validate_accepts_the_formats_allowed_for_points(string points)
    {
        var element = PolygonElement.CreateDefault();
        element.Points = points;

        var result = _plugin.Validate(element);

        Assert.True(result.IsValid, string.Join(" / ", result.Errors));
    }

    [Fact]
    public void Validate_rejects_fewer_than_three_vertices()
    {
        var element = PolygonElement.CreateDefault();
        element.Points = "0,0 10,10";

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("at least 3 vertices"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0,0 10,0 10")]      // coordinata spaiata
    [InlineData("0,0 dieci,0 10,10")]
    public void Validate_rejects_a_malformed_list_of_points(string points)
    {
        var element = PolygonElement.CreateDefault();
        element.Points = points;

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("pairs of numbers"));
    }

    [Fact]
    public void Render_produces_the_expected_attributes()
    {
        var element = PolygonElement.CreateDefault();

        var svg = _plugin.Render(element);

        Assert.StartsWith("<polygon ", svg);
        Assert.EndsWith("/>", svg);
        Assert.Contains("points=\"25,4 46,19 38,44 12,44 4,19\"", svg);
        Assert.Contains("fill=\"#AEFAEF\"", svg);
        Assert.Contains("stroke=\"none\"", svg);
        Assert.Contains("opacity=\"1\"", svg);
    }

    [Fact]
    public void Render_escapes_the_points_so_that_invalid_input_cannot_break_the_markup()
    {
        var element = PolygonElement.CreateDefault();
        element.Points = "0,0\" /><script>alert('x')</script>";

        var svg = _plugin.Render(element);

        Assert.DoesNotContain("<script>", svg);
        Assert.Equal(1, svg.Split('<').Length - 1);
    }

    [Fact]
    public void The_icon_is_specific_to_the_plugin()
    {
        // Il contratto ha un'icona predefinita, raggiungibile solo attraverso l'interfaccia:
        // è un membro di default, quindi un plugin che non la ridefinisce resta valido.
        var predefinita = ((Abstractions.Plugins.IVectorElementPlugin)new FallbackPlugin()).IconSvg;

        Assert.NotEqual(predefinita, _plugin.IconSvg);
        Assert.Contains("path", _plugin.IconSvg);
    }

    /// <summary>Plugin minimo che non ridefinisce l'icona: serve a leggere quella predefinita del contratto.</summary>
    private sealed class FallbackPlugin : Abstractions.Plugins.IVectorElementPlugin
    {
        public string Type => "fallback";
        public string DisplayName => "Fallback";
        public Type ElementClrType => typeof(object);
        public Type EditorComponentType => typeof(object);
        public Core.Models.VectorElement Create() => throw new NotSupportedException();
        public Core.Validation.ValidationResult Validate(Core.Models.VectorElement element) => throw new NotSupportedException();
        public string Render(Core.Models.VectorElement element) => throw new NotSupportedException();
    }
}
