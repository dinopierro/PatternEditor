using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Models;
using PatternEditor.Element.Rect;
using PatternEditor.Services;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// Verifica la validazione a due livelli.
///
/// <para>
/// Il punto da proteggere è la delega: il validatore generale non deve contenere una sola
/// regola specifica di un tipo concreto, e deve invece interrogare il plugin. I test
/// verificano anche il trattamento degli elementi sconosciuti, che producono un avviso e
/// non un errore: sono conservati, non scartati.
/// </para>
/// </summary>
public class PatternValidatorTests
{
    private static PatternValidator CreateValidator(out VectorElementPluginRegistry registry)
    {
        registry = new VectorElementPluginRegistry();
        registry.Register(new RectPlugin());
        return new PatternValidator(registry);
    }

    [Fact]
    public void Valid_pattern_has_no_errors()
    {
        var validator = CreateValidator(out _);
        var pattern = new Pattern();
        pattern.Definition.Width = 50;
        pattern.Definition.Height = 50;
        pattern.Definition.Scale = 1;
        pattern.Definition.Rotation = 0;

        var result = validator.Validate(pattern);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 50, 1, 0)]
    [InlineData(50, 0, 1, 0)]
    [InlineData(50, 50, 0, 0)]
    [InlineData(50, 50, 1, 400)]
    [InlineData(50, 50, 1, -10)]
    public void Invalid_general_properties_produce_errors(double width, double height, double scale, double rotation)
    {
        var validator = CreateValidator(out _);
        var pattern = new Pattern();
        pattern.Definition.Width = width;
        pattern.Definition.Height = height;
        pattern.Definition.Scale = scale;
        pattern.Definition.Rotation = rotation;

        var result = validator.Validate(pattern);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_element_produces_a_blocking_error()
    {
        var validator = CreateValidator(out _);
        var pattern = new Pattern();
        pattern.Definition.Width = 50;
        pattern.Definition.Height = 50;

        var rect = RectElement.CreateDefault();
        rect.Width = -5;
        pattern.Definition.Elements.Add(rect);

        var result = validator.Validate(pattern);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Element_with_unregistered_plugin_produces_a_warning_not_an_error()
    {
        var validator = CreateValidator(out _);
        var pattern = new Pattern();
        pattern.Definition.Width = 50;
        pattern.Definition.Height = 50;

        var unknown = new UnknownVectorElement(Guid.CreateVersion7(), "tipo-senza-plugin",
            System.Text.Json.JsonDocument.Parse("{}").RootElement);
        pattern.Definition.Elements.Add(unknown);

        var result = validator.Validate(pattern);

        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Two_elements_with_the_same_identifier_are_an_error()
    {
        // Non è una stranezza innocua: l'identificativo distingue un elemento dall'altro in
        // ogni operazione che li tratta singolarmente, e prima di questo controllo un
        // documento così faceva morire il rendering dell'elenco con un'eccezione.
        var registry = new VectorElementPluginRegistry();
        registry.Register(new RectPlugin());

        var condiviso = Guid.CreateVersion7();
        var pattern = new Pattern();
        pattern.Definition.Elements.Add(new RectElement(
            condiviso, 0, 0, 10, 10, "#ffffff", 1, null, 0, 1, 1));
        pattern.Definition.Elements.Add(new RectElement(
            condiviso, 5, 5, 10, 10, "#000000", 1, null, 0, 1, 1));

        var esito = new PatternValidator(registry).Validate(pattern);

        Assert.False(esito.IsValid);
        Assert.Contains(esito.Errors, e => e.Contains(condiviso.ToString()) && e.Contains("unique"));
    }

    [Fact]
    public void Distinct_identifiers_raise_nothing()
    {
        var registry = new VectorElementPluginRegistry();
        registry.Register(new RectPlugin());

        var pattern = new Pattern();
        pattern.Definition.Elements.Add(RectElement.CreateDefault());
        pattern.Definition.Elements.Add(RectElement.CreateDefault());

        var esito = new PatternValidator(registry).Validate(pattern);

        Assert.DoesNotContain(esito.Errors, e => e.Contains("unique"));
    }
}
