using System.Text.Json;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using PatternEditor.Element.Line;
using PatternEditor.Element.Rect;
using PatternEditor.Services;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// Verifica la duplicazione di un elemento.
///
/// <para>
/// La copia deve essere profonda e agnostica: profonda perché due elementi non devono
/// condividere niente, agnostica perché il duplicatore non conosce i tipi concreti e non
/// deve conoscerli. Il test che conta è quello sull'identificativo: la copia ne riceve uno
/// nuovo, altrimenti due elementi indistinguibili convivrebbero nello stesso pattern.
/// </para>
/// </summary>
public class VectorElementClonerTests
{
    private static VectorElementCloner CreateCloner(bool registerRect = true)
    {
        var registry = new VectorElementPluginRegistry();
        registry.Register(new LinePlugin());
        if (registerRect)
        {
            registry.Register(new RectPlugin());
        }

        return new VectorElementCloner(new PatternSerializer(registry));
    }

    [Fact]
    public void Clone_returns_a_new_instance_with_a_new_id()
    {
        var original = RectElement.CreateDefault();

        var clone = CreateCloner().Clone(original);

        Assert.NotSame(original, clone);
        Assert.NotEqual(original.Id, clone.Id);
        Assert.Equal(original.Type, clone.Type);
    }

    [Fact]
    public void Clone_preserves_all_the_properties_of_the_concrete_element()
    {
        var original = RectElement.CreateDefault();
        original.X = 3.5;
        original.Y = -2;
        original.Width = 11;
        original.Height = 7;
        original.Fill = "#123456";
        original.FillOpacity = 0.4;
        original.Stroke = "#654321";
        original.StrokeWidth = 2.5;
        original.StrokeOpacity = 0.6;
        original.Opacity = 0.8;

        var clone = Assert.IsType<RectElement>(CreateCloner().Clone(original));

        Assert.Equal(original.X, clone.X);
        Assert.Equal(original.Y, clone.Y);
        Assert.Equal(original.Width, clone.Width);
        Assert.Equal(original.Height, clone.Height);
        Assert.Equal(original.Fill, clone.Fill);
        Assert.Equal(original.FillOpacity, clone.FillOpacity);
        Assert.Equal(original.Stroke, clone.Stroke);
        Assert.Equal(original.StrokeWidth, clone.StrokeWidth);
        Assert.Equal(original.StrokeOpacity, clone.StrokeOpacity);
        Assert.Equal(original.Opacity, clone.Opacity);
    }

    [Fact]
    public void Modifying_the_clone_does_not_affect_the_original()
    {
        var original = LineElement.CreateDefault();
        var clone = Assert.IsType<LineElement>(CreateCloner().Clone(original));

        clone.X2 = 999;

        Assert.NotEqual(999, original.X2);
    }

    [Fact]
    public void Unknown_elements_are_cloned_without_losing_their_original_json()
    {
        // Elemento il cui plugin non è registrato: deve restare duplicabile e integro.
        using var document = JsonDocument.Parse(
            """{"type":"tipo-senza-plugin","id":"0192f4d0-0000-7000-8000-000000000001","cx":5,"cy":6,"r":7}""");
        var unknown = new UnknownVectorElement(
            Guid.Parse("0192f4d0-0000-7000-8000-000000000001"),
            "tipo-senza-plugin",
            document.RootElement.Clone());

        var clone = Assert.IsType<UnknownVectorElement>(CreateCloner().Clone(unknown));

        Assert.NotEqual(unknown.Id, clone.Id);
        Assert.Equal("tipo-senza-plugin", clone.Type);
        Assert.Equal(7, clone.RawJson.GetProperty("r").GetDouble());
    }
}
