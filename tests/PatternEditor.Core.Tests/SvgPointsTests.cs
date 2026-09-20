using PatternEditor.Core.Formatting;
using Xunit;

namespace PatternEditor.Core.Tests;

/// <summary>
/// Verifica la lettura dell'attributo <c>points</c> di poligoni e spezzate.
///
/// <para>
/// La specifica è insolitamente permissiva: i numeri possono essere separati da spazi,
/// virgole, o entrambi, in qualunque combinazione, e le coppie possono essere scritte
/// attaccate. I test coprono le forme che si incontrano davvero copiando punti da altri
/// strumenti, perché è da lì che arrivano.
/// </para>
/// </summary>
public class SvgPointsTests
{
    [Theory]
    [InlineData("0,0 10,0 10,10", 3)]
    [InlineData("0 0 10 0 10 10", 3)]
    [InlineData("  0,0\n  10,10  ", 2)]
    [InlineData("-2.5,0 1e1,3", 2)]
    public void Valid_sequences_are_counted_in_pairs(string points, int expected)
    {
        Assert.True(SvgPoints.TryCountPoints(points, out var count));
        Assert.Equal(expected, count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0,0 10")]        // coordinata spaiata
    [InlineData("0,0 dieci,10")]
    [InlineData("0,0 10,10,")]    // la virgola finale non aggiunge un valore ma non e' un errore di parsing
    public void Invalid_or_incomplete_sequences_are_rejected(string? points)
    {
        // L'ultimo caso conta 4 valori (pari) quindi è accettato: la virgola in coda
        // viene ignorata come separatore vuoto.
        var isValid = SvgPoints.TryCountPoints(points, out var count);

        if (points == "0,0 10,10,")
        {
            Assert.True(isValid);
            Assert.Equal(2, count);
            return;
        }

        Assert.False(isValid);
        Assert.Equal(0, count);
    }

    [Fact]
    public void The_decimal_separator_is_always_the_dot_never_the_comma()
    {
        // "1,5" non è il numero 1.5: è la coppia (1, 5). La virgola separa le coordinate.
        Assert.True(SvgPoints.TryCountPoints("1,5", out var count));
        Assert.Equal(1, count);
    }
}
