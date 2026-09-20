using PatternEditor.Services;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// Verifica la riformattazione del sorgente mostrata a schermo.
///
/// <para>
/// Riguarda la sola lettura: il file scaricato e quello copiato restano quelli prodotti dal
/// renderer. È una distinzione che i test devono difendere, perché la tentazione di usare
/// il testo «bello» anche per il download è forte e introdurrebbe una seconda sorgente di
/// verità per il markup.
/// </para>
/// </summary>
public class SvgSourceFormatterTests
{
    [Fact]
    public void Every_attribute_goes_on_its_own_line()
    {
        var formatted = SvgSourceFormatter.Format(
            """<rect x="-25" y="0" width="50" height="25" fill="#dc1822" />""");

        Assert.Equal(
            """
            <rect
              x="-25"
              y="0"
              width="50"
              height="25"
              fill="#dc1822"
            />
            """.ReplaceLineEndings("\n"),
            formatted);
    }

    [Fact]
    public void Nested_tags_are_indented_by_depth()
    {
        var formatted = SvgSourceFormatter.Format(
            """<svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%"><defs><pattern id="p"><rect /></pattern></defs></svg>""");

        Assert.Equal(
            """
            <svg
              xmlns="http://www.w3.org/2000/svg"
              width="100%"
              height="100%"
            >
              <defs>
                <pattern id="p">
                  <rect />
                </pattern>
              </defs>
            </svg>
            """.ReplaceLineEndings("\n"),
            formatted);
    }

    [Fact]
    public void An_attribute_value_containing_spaces_is_not_split()
    {
        var formatted = SvgSourceFormatter.Format(
            """<pattern id="p" patternTransform="scale(2) rotate(45) translate(0,0)" />""");

        Assert.Contains("""  patternTransform="scale(2) rotate(45) translate(0,0)" """.TrimEnd(), formatted);
        Assert.Contains("""  id="p" """.TrimEnd(), formatted);
    }

    [Fact]
    public void A_tag_with_at_most_one_attribute_stays_on_a_single_line()
    {
        Assert.Equal("<defs>", SvgSourceFormatter.Format("<defs>"));
        Assert.Equal("""<pattern id="p">""", SvgSourceFormatter.Format("""<pattern id="p">"""));
    }

    [Fact]
    public void No_formatted_line_is_longer_than_the_original_markup_single_line()
    {
        // Obiettivo della formattazione: nessuna riga lunghissima da leggere scorrendo
        // in orizzontale.
        const string svg = """<svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%"><defs><pattern id="p" width="50" height="50" patternUnits="userSpaceOnUse" patternTransform="scale(2) rotate(45) translate(0,0)"><rect x="-25" y="0" width="50" height="25" fill="#dc1822" fill-opacity="1" stroke="#ffffff" stroke-width="1" /></pattern></defs><rect width="100%" height="100%" fill="url(#p)" /></svg>""";

        var lines = SvgSourceFormatter.Format(svg).Split('\n');

        Assert.All(lines, line => Assert.True(line.Length <= 60, $"Riga troppo lunga ({line.Length}): {line}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void An_empty_input_produces_an_empty_result(string? svg)
    {
        Assert.Equal(string.Empty, SvgSourceFormatter.Format(svg!));
    }
}
