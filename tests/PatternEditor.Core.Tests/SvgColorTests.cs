using PatternEditor.Core.Formatting;
using Xunit;

namespace PatternEditor.Core.Tests;

/// <summary>
/// Verifica la lettura di un colore scritto a mano.
///
/// <para>
/// Il campo di testo accanto al selettore accetta forme che il selettore non conosce — senza
/// cancelletto, abbreviata a tre cifre — e deve rifiutare tutto il resto senza mai far
/// arrivare al modello un valore a metà. I test coprono le tre famiglie: ciò che si accetta,
/// ciò che si espande, ciò che si rifiuta.
/// </para>
/// </summary>
public class SvgColorTests
{
    [Theory]
    [InlineData("#3b6ef5", "#3b6ef5")]
    [InlineData("#3B6EF5", "#3B6EF5")]
    [InlineData("3b6ef5", "#3b6ef5")]
    [InlineData("  #3b6ef5  ", "#3b6ef5")]
    public void Normalize_accepts_the_forms_one_writes_by_hand(string input, string expected)
    {
        Assert.Equal(expected, SvgColor.Normalize(input));
    }

    [Theory]
    [InlineData("#abc", "#aabbcc")]
    [InlineData("abc", "#aabbcc")]
    [InlineData("#0F8", "#00FF88")]
    public void Normalize_expands_the_three_digit_form_by_doubling_each_digit(string input, string expected)
    {
        // È la regola della specifica CSS: #abc e #aabbcc sono lo stesso colore.
        Assert.Equal(expected, SvgColor.Normalize(input));
    }

    [Theory]
    [InlineData("#3b6e")]      // quattro cifre: non è né la forma breve né quella lunga
    [InlineData("#3b6ef")]
    [InlineData("#3b6ef55")]
    [InlineData("#ggg")]
    [InlineData("rosso")]
    [InlineData("red")]        // i nomi della specifica SVG non sono ammessi: vedi la classe
    [InlineData("rgb(0,0,0)")]
    [InlineData("#")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Normalize_returns_null_for_anything_that_is_not_a_colour(string? input)
    {
        Assert.Null(SvgColor.Normalize(input));
        Assert.False(SvgColor.IsValid(input));
    }

    [Fact]
    public void Normalize_leaves_the_case_of_the_digits_alone()
    {
        // Riscriverle darebbe l'impressione che il valore sia stato cambiato.
        Assert.Equal("#AEFAEF", SvgColor.Normalize("#AEFAEF"));
        Assert.Equal("#aefaef", SvgColor.Normalize("#aefaef"));
    }

    [Theory]
    [InlineData("#3b6ef5", true)]
    [InlineData("#3B6EF5", true)]
    [InlineData("3b6ef5", false)]   // manca il cancelletto: il selettore non lo accetterebbe
    [InlineData("#abc", false)]     // forma breve: valida, ma non canonica
    [InlineData(null, false)]
    public void IsCanonical_recognises_only_the_form_the_picker_uses(string? input, bool expected)
    {
        Assert.Equal(expected, SvgColor.IsCanonical(input));
    }

    [Theory]
    [InlineData("#3b6ef5", "#3b6ef5")]
    [InlineData("abc", "#aabbcc")]
    [InlineData("mezzo colore", SvgColor.Fallback)]
    [InlineData(null, SvgColor.Fallback)]
    public void ForPicker_always_returns_something_the_picker_can_show(string? input, string expected)
    {
        // Un valore incompleto non deve far apparire il selettore vuoto.
        Assert.Equal(expected, SvgColor.ForPicker(input));
    }
}
