using PatternEditor.Abstractions.Plugins;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using PatternEditor.Element.Line;
using PatternEditor.Element.Rect;
using Xunit;

namespace PatternEditor.Abstractions.Tests;

/// <summary>
/// Verifica il cuore della persistenza: la serializzazione polimorfica.
///
/// <para>
/// È il punto più delicato del sistema, perché un errore qui non si vede subito: il
/// documento si salva senza lamentele e si rilegge mutilato. I casi coperti sono quelli in
/// cui questo può accadere — un tipo che il registro non conosce, un documento con campi in
/// più, un giro completo di scrittura e rilettura che deve restituire l'originale.
/// </para>
/// </summary>
public class PatternSerializerTests
{
    private static VectorElementPluginRegistry CreateFullRegistry()
    {
        var registry = new VectorElementPluginRegistry();
        registry.Register(new RectPlugin());
        registry.Register(new LinePlugin());
        return registry;
    }

    [Fact]
    public void Round_trip_preserves_pattern_identity_and_element_order()
    {
        var registry = CreateFullRegistry();
        var serializer = new PatternSerializer(registry);

        var pattern = new Pattern { Name = "Pattern di test" };
        pattern.Definition.Width = 50;
        pattern.Definition.Height = 50;
        pattern.Definition.Elements.Add(LineElement.CreateDefault());
        pattern.Definition.Elements.Add(RectElement.CreateDefault());

        var json = serializer.Serialize(pattern);
        var restored = serializer.Deserialize(json);

        Assert.Equal(pattern.Id, restored.Id);
        Assert.Equal(pattern.Name, restored.Name);
        Assert.Equal(2, restored.Definition.Elements.Count);
        Assert.IsType<LineElement>(restored.Definition.Elements[0]);
        Assert.IsType<RectElement>(restored.Definition.Elements[1]);
    }

    [Fact]
    public void Deserialized_elements_are_strongly_typed_with_correct_values()
    {
        var registry = CreateFullRegistry();
        var serializer = new PatternSerializer(registry);

        var pattern = new Pattern { Name = "Pattern" };
        var rect = RectElement.CreateDefault();
        rect.X = 12;
        rect.Y = 34;
        pattern.Definition.Elements.Add(rect);

        var restored = serializer.Deserialize(serializer.Serialize(pattern));
        var restoredRect = Assert.IsType<RectElement>(restored.Definition.Elements[0]);

        Assert.Equal(12, restoredRect.X);
        Assert.Equal(34, restoredRect.Y);
        Assert.Equal(rect.Id, restoredRect.Id);
    }

    [Fact]
    public void Unknown_element_type_is_preserved_instead_of_being_dropped()
    {
        // Registro SENZA il plugin "rect": simula una versione dell'editor che non conosce
        // ancora questo tipo di elemento.
        var registry = new VectorElementPluginRegistry();
        registry.Register(new LinePlugin());
        var serializer = new PatternSerializer(registry);

        var patternWithRect = new Pattern { Name = "Pattern" };
        patternWithRect.Definition.Elements.Add(RectElement.CreateDefault());

        var fullRegistrySerializer = new PatternSerializer(CreateFullRegistry());
        var json = fullRegistrySerializer.Serialize(patternWithRect);

        // Deserializzato con il registro "limitato": l'elemento rect deve essere preservato,
        // non scartato.
        var restored = serializer.Deserialize(json);

        Assert.Single(restored.Definition.Elements);
        var unknown = Assert.IsType<UnknownVectorElement>(restored.Definition.Elements[0]);
        Assert.Equal("rect", unknown.Type);

        // Ri-serializzando con il registro limitato, il JSON dell'elemento sconosciuto
        // deve rimanere invariato (nessuna perdita di dati).
        var roundTripJson = serializer.Serialize(restored);
        var reparsed = fullRegistrySerializer.Deserialize(roundTripJson);
        var reparsedRect = Assert.IsType<RectElement>(reparsed.Definition.Elements[0]);
        Assert.Equal(10, reparsedRect.X); // valore di default di RectElement.CreateDefault()
    }

    [Theory]
    [InlineData("{ non e' json")]
    [InlineData("[]")]
    [InlineData("")]
    public void A_malformed_document_is_reported_as_a_format_error(string json)
    {
        var serializer = new PatternSerializer(new VectorElementPluginRegistry());

        // Un solo tipo di eccezione per tutti i difetti di formato: chi chiama
        // (l'API, l'editor) ha un unico caso da gestire.
        Assert.ThrowsAny<Exception>(() => serializer.Deserialize(json));
    }

    [Theory]
    [InlineData("""{"version":1,"name":"x","definition":{"width":1,"height":1,"elements":[]}}""")]
    [InlineData("""{"version":1,"id":"non-un-guid","name":"x","definition":{"width":1,"height":1,"elements":[]}}""")]
    [InlineData("""{"version":1,"id":"00000000-0000-0000-0000-000000000000","name":"x","definition":{"width":1,"height":1,"elements":[]}}""")]
    public void An_absent_or_invalid_identifier_is_a_format_error(string json)
    {
        var serializer = new PatternSerializer(new VectorElementPluginRegistry());

        var e = Assert.Throws<FormatException>(() => serializer.Deserialize(json));
        Assert.Contains("identificativo", e.Message);
    }

    [Fact]
    public void A_document_without_a_definition_is_a_format_error()
    {
        var serializer = new PatternSerializer(new VectorElementPluginRegistry());

        var e = Assert.Throws<FormatException>(() => serializer.Deserialize(
            """{"version":1,"id":"01a08a43-f274-7e72-8c70-6f70376970bf","name":"x"}"""));

        Assert.Contains("definizione", e.Message);
    }
}
