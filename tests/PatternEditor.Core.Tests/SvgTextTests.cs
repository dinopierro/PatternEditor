using PatternEditor.Core.Formatting;
using Xunit;

namespace PatternEditor.Core.Tests;

/// <summary>
/// Verifica la protezione dei caratteri speciali nel markup.
///
/// <para>
/// Due regole diverse per due posti diversi: dentro un attributo vanno protetti anche gli
/// apici, che altrimenti chiuderebbero il valore in anticipo; dentro il contenuto di un nodo
/// no, e sostituirli renderebbe il sorgente illeggibile senza cambiare nulla di ciò che si
/// vede. C'è poi un ordine obbligatorio: la e commerciale va sostituita per prima, o le
/// entità prodotte dopo verrebbero a loro volta protette e il testo risulterebbe corrotto.
/// </para>
/// </summary>
public class SvgTextTests
{
    [Theory]
    [InlineData("M 0 0 L 10 10", "M 0 0 L 10 10")]
    [InlineData("a\"b", "a&quot;b")]
    [InlineData("a<b>c", "a&lt;b&gt;c")]
    [InlineData("a&b", "a&amp;b")]
    [InlineData("a'b", "a&apos;b")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void Escape_protects_the_characters_that_would_break_an_attribute(string? input, string expected)
    {
        Assert.Equal(expected, SvgText.Escape(input));
    }

    [Fact]
    public void The_ampersand_is_escaped_first_so_that_entities_are_not_double_encoded()
    {
        // Sostituendo & per ultimo si otterrebbe "&amp;lt;": il testo verrebbe corrotto.
        Assert.Equal("&amp;lt;", SvgText.Escape("&lt;"));
    }

    [Theory]
    [InlineData("Pane & Companatico", "Pane &amp; Companatico")]
    [InlineData("a<b>c", "a&lt;b&gt;c")]
    [InlineData("l'ottimo", "l'ottimo")]
    [InlineData("dice \"ciao\"", "dice \"ciao\"")]
    [InlineData(null, "")]
    public void EscapeContent_leaves_quotes_alone(string? input, string expected)
    {
        // Dentro il contenuto di un nodo gli apici non chiudono nulla: sostituirli
        // renderebbe il sorgente illeggibile senza cambiare ciò che viene mostrato.
        Assert.Equal(expected, SvgText.EscapeContent(input));
    }

    [Fact]
    public void Escape_protects_quotes_because_an_attribute_value_would_be_closed()
    {
        Assert.Equal("&quot;x&quot;", SvgText.Escape("\"x\""));
        Assert.Equal("l&apos;ottimo", SvgText.Escape("l'ottimo"));
    }
}
