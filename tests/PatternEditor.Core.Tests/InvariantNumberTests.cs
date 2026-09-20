using System.Globalization;
using PatternEditor.Core.Formatting;
using Xunit;

namespace PatternEditor.Core.Tests;

/// <summary>
/// Verifica la formattazione dei numeri.
///
/// <para>
/// Sembra un dettaglio ed è invece uno dei difetti più insidiosi di un'applicazione Blazor:
/// il codice gira nel browser e adotta la cultura di chi guarda. Con una cultura italiana,
/// un valore di 1,5 finisce nel markup con la virgola, e sia il lettore SVG sia il campo
/// numerico del browser lo rifiutano — l'utente vede il campo svuotarsi da solo mentre
/// scrive. I test forzano culture diverse proprio per intercettarlo.
/// </para>
/// </summary>
public class InvariantNumberTests
{
    [Fact]
    public void Format_uses_the_dot_as_decimal_separator_even_with_a_comma_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // Simula Blazor WebAssembly con cultura del browser it-IT: senza formattazione
            // invariante l'attributo value di un <input type="number"> conterrebbe "1,5"
            // e il campo risulterebbe vuoto.
            CultureInfo.CurrentCulture = new CultureInfo("it-IT");

            Assert.Equal("1.5", InvariantNumber.Format(1.5));
            Assert.Equal("40", InvariantNumber.Format(40));
            Assert.Equal("-0.25", InvariantNumber.Format(-0.25));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData("1,5", 1.5)]
    [InlineData("-3", -3)]
    [InlineData("0.05", 0.05)]
    public void Parse_accepts_both_decimal_separators(string input, double expected)
    {
        Assert.Equal(expected, InvariantNumber.Parse(input, fallback: 99));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void Parse_returns_the_fallback_for_a_non_numeric_value(string? input)
    {
        // Caso tipico: il campo viene svuotato durante la digitazione. Il valore
        // precedente deve essere conservato, non azzerato.
        Assert.Equal(7.5, InvariantNumber.Parse(input, fallback: 7.5));
    }
}
