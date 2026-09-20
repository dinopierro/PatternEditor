using PatternEditor.Element.Text;
using Xunit;

namespace PatternEditor.Element.Text.Tests;

/// <summary>
/// Verifica il plugin dell'elemento «Text»: il testo, l'unico elemento con un contenuto da proteggere oltre agli attributi
///
/// <para>
/// La struttura è la stessa per tutti i plugin, perché lo è il contratto: valori predefiniti
/// già validi, validazione che accumula gli errori invece di fermarsi al primo, e markup
/// generato con la cultura invariante e i caratteri speciali protetti. Sono i tre modi in cui
/// un plugin può sbagliare in silenzio.
/// </para>
/// </summary>
public class TextPluginTests
{
    private readonly TextPlugin _plugin = new();

    [Fact]
    public void Type_is_text()
    {
        Assert.Equal("text", _plugin.Type);
    }

    [Fact]
    public void Create_returns_a_valid_default_element()
    {
        var element = Assert.IsType<TextElement>(_plugin.Create());

        Assert.True(_plugin.Validate(element).IsValid);
        Assert.False(string.IsNullOrWhiteSpace(element.Content));
        Assert.True(element.FontSize > 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_an_empty_text(string content)
    {
        var element = TextElement.CreateDefault();
        element.Content = content;

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Validate_rejects_a_non_positive_font_size(double fontSize)
    {
        var element = TextElement.CreateDefault();
        element.FontSize = fontSize;

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("font size"));
    }

    [Fact]
    public void Validate_rejects_an_unknown_anchor()
    {
        var element = TextElement.CreateDefault();
        element.TextAnchor = "centro";

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("alignment"));
    }

    [Theory]
    [InlineData("sans-serif")]
    [InlineData("serif")]
    [InlineData("monospace")]
    public void A_generic_family_produces_no_warning(string family)
    {
        var element = TextElement.CreateDefault();
        element.FontFamily = family;

        var result = _plugin.Validate(element);

        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void A_named_font_is_a_warning_not_an_error()
    {
        // Il carattere non viaggia dentro l'SVG: usarne uno specifico è legittimo,
        // ma chi apre il file potrebbe non averlo.
        var element = TextElement.CreateDefault();
        element.FontFamily = "Segoe UI";

        var result = _plugin.Validate(element);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("Segoe UI"));
    }

    [Fact]
    public void Render_puts_the_text_inside_the_node_not_in_an_attribute()
    {
        var element = TextElement.CreateDefault();
        element.Content = "Ciao";

        var svg = _plugin.Render(element);

        Assert.StartsWith("<text ", svg);
        Assert.EndsWith(">Ciao</text>", svg);
        Assert.Contains("x=\"25\"", svg);
        Assert.Contains("y=\"30\"", svg);
        Assert.Contains("font-family=\"sans-serif\"", svg);
        Assert.Contains("font-size=\"12\"", svg);
        Assert.Contains("text-anchor=\"middle\"", svg);
        Assert.Contains("fill=\"#000000\"", svg);
    }

    [Fact]
    public void The_content_is_escaped_but_apostrophes_are_left_alone()
    {
        // Dentro il contenuto di un nodo gli apici non hanno significato speciale:
        // sostituirli renderebbe il sorgente illeggibile senza cambiare il risultato.
        var element = TextElement.CreateDefault();
        element.Content = "Pane & Companatico <l'ottimo>";

        var svg = _plugin.Render(element);

        Assert.Contains(">Pane &amp; Companatico &lt;l'ottimo&gt;</text>", svg);
    }

    [Fact]
    public void A_hostile_content_cannot_break_the_markup()
    {
        var element = TextElement.CreateDefault();
        element.Content = "</text><script>alert('x')</script>";

        var svg = _plugin.Render(element);

        Assert.DoesNotContain("<script>", svg);

        // Restano soltanto il tag di apertura e quello di chiusura generati dal plugin.
        Assert.Equal(2, svg.Split('<').Length - 1);
    }

    [Fact]
    public void A_font_name_containing_quotes_cannot_break_the_attribute()
    {
        var element = TextElement.CreateDefault();
        element.FontFamily = "\" onload=\"alert(1)";

        var svg = _plugin.Render(element);

        Assert.Contains("&quot;", svg);
        Assert.DoesNotContain("onload=\"alert", svg);
    }

    [Fact]
    public void Render_writes_decimals_with_a_dot_regardless_of_the_current_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("it-IT");
            var element = TextElement.CreateDefault();
            element.FontSize = 10.5;

            Assert.Contains("font-size=\"10.5\"", _plugin.Render(element));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
