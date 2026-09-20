using PatternEditor.Core.Models;
using Xunit;

namespace PatternEditor.Core.Tests;

/// <summary>
/// Verifica i valori iniziali e il comportamento della definizione di un pattern: la cella
/// e la sua trasformazione.
/// </summary>
public class PatternDefinitionTests
{
    [Fact]
    public void Elements_preserve_insertion_order()
    {
        var definition = new PatternDefinition();
        var first = new FakeVectorElement("a");
        var second = new FakeVectorElement("b");
        var third = new FakeVectorElement("c");

        definition.Elements.Add(first);
        definition.Elements.Add(second);
        definition.Elements.Add(third);

        Assert.Equal(new VectorElement[] { first, second, third }, definition.Elements);
    }

    [Fact]
    public void Moving_an_element_changes_its_rendering_order()
    {
        var definition = new PatternDefinition();
        var first = new FakeVectorElement("a");
        var second = new FakeVectorElement("b");
        definition.Elements.Add(first);
        definition.Elements.Add(second);

        // Sposta "second" in cima, simulando l'operazione "sposta verso l'alto".
        definition.Elements.Remove(second);
        definition.Elements.Insert(0, second);

        Assert.Equal(new VectorElement[] { second, first }, definition.Elements);
    }

    [Fact]
    public void Default_scale_is_one_and_default_rotation_and_translation_are_zero()
    {
        var definition = new PatternDefinition();

        Assert.Equal(1.0, definition.Scale);
        Assert.Equal(0.0, definition.Rotation);
        Assert.Equal(0.0, definition.TranslateX);
        Assert.Equal(0.0, definition.TranslateY);
    }
}
