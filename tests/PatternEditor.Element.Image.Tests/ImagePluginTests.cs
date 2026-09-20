using PatternEditor.Element.Image;
using Xunit;

namespace PatternEditor.Element.Image.Tests;

/// <summary>
/// Verifica il plugin dell'elemento «Image»: l'immagine, con i tre modi di adattamento e gli avvisi sulla sorgente
///
/// <para>
/// La struttura è la stessa per tutti i plugin, perché lo è il contratto: valori predefiniti
/// già validi, validazione che accumula gli errori invece di fermarsi al primo, e markup
/// generato con la cultura invariante e i caratteri speciali protetti. Sono i tre modi in cui
/// un plugin può sbagliare in silenzio.
/// </para>
/// </summary>
public class ImagePluginTests
{
    private readonly ImagePlugin _plugin = new();

    [Fact]
    public void Type_is_image()
    {
        Assert.Equal("image", _plugin.Type);
    }

    [Fact]
    public void Create_returns_a_valid_default_element()
    {
        var element = Assert.IsType<ImageElement>(_plugin.Create());

        Assert.True(_plugin.Validate(element).IsValid);
        Assert.Equal(ImageElement.PlaceholderHref, element.Href);
        Assert.True(element.Width > 0);
        Assert.True(element.Height > 0);
    }

    [Theory]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("DATA:IMAGE/SVG+XML;base64,PHN2Zy8+")]
    [InlineData("https://esempio.it/trama.png")]
    [InlineData("http://esempio.it/trama.png")]
    public void Validate_accepts_data_uris_and_http_addresses(string href)
    {
        var element = ImageElement.CreateDefault();
        element.Href = href;

        Assert.True(_plugin.Validate(element).IsValid);
    }

    [Theory]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/foto.png")]
    [InlineData("/percorso/locale/foto.png")]
    public void Validate_rejects_sources_that_are_not_images_over_a_supported_scheme(string href)
    {
        var element = ImageElement.CreateDefault();
        element.Href = href;

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("data URI"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_a_missing_source(string href)
    {
        var element = ImageElement.CreateDefault();
        element.Href = href;

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("No image"));
    }

    [Fact]
    public void Validate_rejects_a_non_positive_size()
    {
        var element = ImageElement.CreateDefault();
        element.Width = 0;

        var result = _plugin.Validate(element);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("width"));
    }

    [Fact]
    public void A_large_embedded_image_is_a_warning_not_an_error()
    {
        // Incorporare un'immagine pesante è una scelta legittima: va segnalata,
        // non impedita.
        var element = ImageElement.CreateDefault();
        element.Href = "data:image/png;base64," + new string('A', ImagePlugin.LargeHrefWarningBytes);

        var result = _plugin.Validate(element);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("kB"));
    }

    [Fact]
    public void Render_produces_the_expected_attributes()
    {
        var element = ImageElement.CreateDefault();
        element.Href = "https://esempio.it/trama.png";
        element.X = 5;
        element.Y = 5;
        element.Width = 40;
        element.Height = 40;
        element.Opacity = 1;

        var svg = _plugin.Render(element);

        Assert.StartsWith("<image ", svg);
        Assert.EndsWith("/>", svg);
        Assert.Contains("x=\"5\"", svg);
        Assert.Contains("y=\"5\"", svg);
        Assert.Contains("width=\"40\"", svg);
        Assert.Contains("height=\"40\"", svg);
        Assert.Contains("href=\"https://esempio.it/trama.png\"", svg);
        Assert.Contains("preserveAspectRatio=\"xMidYMid meet\"", svg);
        Assert.Contains("opacity=\"1\"", svg);
    }

    [Fact]
    public void Render_escapes_the_source_so_that_it_cannot_break_the_markup()
    {
        var element = ImageElement.CreateDefault();
        element.Href = "https://esempio.it/a.png\" /><script>alert('x')</script>";

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
            var element = ImageElement.CreateDefault();
            element.Width = 12.5;

            Assert.Contains("width=\"12.5\"", _plugin.Render(element));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
