using System.Text.Json;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using Xunit;

namespace PatternEditor.Abstractions.Tests;

/// <summary>
/// Il giro completo di scrittura e rilettura, per <b>ogni</b> tipo di elemento e per ogni
/// sua proprietà.
///
/// <para>
/// Il difetto che questi test cercano non è un'eccezione: è il silenzio. Un elemento che
/// perde una proprietà si salva senza lamentele e si riapre con un valore predefinito al
/// posto di quello scelto, e chi ha disegnato il pattern se ne accorge molto dopo. Le
/// proprietà si percorrono per riflessione proprio perché un campo aggiunto domani entri
/// nella verifica senza che nessuno si ricordi di aggiungerlo qui.
/// </para>
/// </summary>
public class AllElementTypesSerializationTests
{
    public static TheoryData<string> Types => AllPlugins.Types;

    [Theory]
    [MemberData(nameof(Types))]
    public void Every_element_type_survives_the_round_trip_with_all_its_properties(string type)
    {
        var serializer = new PatternSerializer(AllPlugins.Registry());
        var plugin = AllPlugins.Of(type);
        var originale = AllPlugins.Populated(plugin);

        var pattern = new Pattern { Name = "Prova" };
        pattern.Definition.Width = 40;
        pattern.Definition.Height = 60;
        pattern.Definition.Elements.Add(originale);

        var riletto = serializer.Deserialize(serializer.Serialize(pattern));
        var elemento = Assert.Single(riletto.Definition.Elements);

        Assert.Equal(plugin.ElementClrType, elemento.GetType());
        Assert.Equal(originale.Id, elemento.Id);
        Assert.Equal(originale.Type, elemento.Type);
        AllPlugins.AssertSameProperties(originale, elemento);
    }

    [Fact]
    public void A_document_with_every_element_type_keeps_them_all_and_in_order()
    {
        var serializer = new PatternSerializer(AllPlugins.Registry());

        var pattern = new Pattern { Name = "Tutti i tipi" };
        pattern.Definition.Width = 100;
        pattern.Definition.Height = 100;
        foreach (var plugin in AllPlugins.Instances)
        {
            pattern.Definition.Elements.Add(AllPlugins.Populated(plugin));
        }

        var riletto = serializer.Deserialize(serializer.Serialize(pattern));

        Assert.Equal(AllPlugins.Instances.Count, riletto.Definition.Elements.Count);

        // L'ordine è l'ordine di disegno: un documento che torna con gli elementi
        // rimescolati è un altro disegno, non lo stesso salvato male.
        Assert.Equal(
            pattern.Definition.Elements.Select(e => e.Id),
            riletto.Definition.Elements.Select(e => e.Id));

        Assert.Equal(
            pattern.Definition.Elements.Select(e => e.GetType()),
            riletto.Definition.Elements.Select(e => e.GetType()));
    }

    [Fact]
    public void The_written_document_declares_the_type_of_every_element()
    {
        var serializer = new PatternSerializer(AllPlugins.Registry());

        var pattern = new Pattern { Name = "Tutti i tipi" };
        foreach (var plugin in AllPlugins.Instances)
        {
            pattern.Definition.Elements.Add(plugin.Create());
        }

        using var documento = JsonDocument.Parse(serializer.Serialize(pattern));
        var elementi = documento.RootElement.GetProperty("definition").GetProperty("elements");

        // Il campo "type" è il discriminatore su cui si regge tutta la rilettura: senza,
        // il documento è illeggibile anche per l'installazione che lo ha scritto.
        Assert.Equal(
            AllPlugins.Instances.Select(p => p.Type),
            elementi.EnumerateArray().Select(e => e.GetProperty("type").GetString()));
    }

    [Fact]
    public void A_second_round_trip_produces_exactly_the_same_document()
    {
        var serializer = new PatternSerializer(AllPlugins.Registry());

        var pattern = new Pattern { Name = "Stabile" };
        foreach (var plugin in AllPlugins.Instances)
        {
            pattern.Definition.Elements.Add(AllPlugins.Populated(plugin));
        }

        var primo = serializer.Serialize(pattern);
        var secondo = serializer.Serialize(serializer.Deserialize(primo));

        // Aprire e richiudere un pattern senza toccarlo non deve cambiare il file: se lo
        // cambiasse, ogni apertura risulterebbe una modifica e il confronto fra due
        // versioni non direbbe più niente.
        Assert.Equal(primo, secondo);
    }
}
