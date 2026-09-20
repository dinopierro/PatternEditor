using System.Globalization;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Models;
using Xunit;

namespace PatternEditor.Abstractions.Tests;

/// <summary>
/// Verifica che <b>ogni</b> plugin rispetti il contratto, non solo quelli che capitava di
/// avere sotto mano.
///
/// <para>
/// Il contratto di <see cref="IVectorElementPlugin"/> è fatto per lo più di promesse che il
/// compilatore non può controllare: che il <c>Type</c> sia stabile e univoco, che
/// <c>Render</c> produca un tag e non un documento, che <c>Create</c> restituisca un
/// elemento già valido, che i numeri escano con il punto decimale in qualunque lingua.
/// Sono esattamente le cose che un decimo plugin scritto in fretta sbaglia, e sono
/// verificate qui una volta per tutti invece che in nove progetti di test distinti.
/// </para>
/// </summary>
public class PluginContractTests
{
    [Fact]
    public void The_solution_provides_the_nine_declared_element_types()
    {
        // Il numero è scritto qui apposta: un plugin che sparisce dalla soluzione, o che
        // viene aggiunto senza aggiornare i test, deve far fallire qualcosa.
        Assert.Equal(9, AllPlugins.Instances.Count);

        Assert.Equal(
            new[] { "line", "rect", "circle", "ellipse", "path", "polygon", "polyline", "text", "image" },
            AllPlugins.Instances.Select(p => p.Type));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Every_plugin_declares_a_type_and_a_display_name(string type)
    {
        var plugin = AllPlugins.Of(type);

        Assert.False(string.IsNullOrWhiteSpace(plugin.Type));
        Assert.False(string.IsNullOrWhiteSpace(plugin.DisplayName));

        // Il Type è un identificativo tecnico che finisce nel JSON e ci resta per sempre:
        // niente maiuscole, niente spazi, niente accenti. Il nome visibile è un'altra cosa.
        Assert.Equal(plugin.Type.Trim().ToLowerInvariant(), plugin.Type);
        Assert.DoesNotContain(' ', plugin.Type);
    }

    [Fact]
    public void No_two_plugins_share_a_type_or_a_display_name()
    {
        // Un Type ripetuto è un errore di avvio (lo verifica il registro); un nome visibile
        // ripetuto no, ma renderebbe il menù di inserimento indecifrabile.
        Assert.Equal(
            AllPlugins.Instances.Count,
            AllPlugins.Instances.Select(p => p.Type).Distinct(StringComparer.Ordinal).Count());

        Assert.Equal(
            AllPlugins.Instances.Count,
            AllPlugins.Instances.Select(p => p.DisplayName).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Every_plugin_creates_an_element_of_its_own_type_with_a_fresh_identifier(string type)
    {
        var plugin = AllPlugins.Of(type);

        var first = plugin.Create();
        var second = plugin.Create();

        Assert.Equal(plugin.Type, first.Type);
        Assert.Equal(plugin.ElementClrType, first.GetType());
        Assert.NotEqual(Guid.Empty, first.Id);

        // Due inserimenti di fila non devono produrre lo stesso identificativo: è il difetto
        // che manda in crisi il rendering dell'elenco e che il validatore segnala come errore.
        Assert.NotEqual(first.Id, second.Id);
        Assert.True(Uuid7.TryGetCreationTime(first.Id, out _), "L'identificativo non è un UUIDv7.");
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Every_plugin_creates_an_element_that_its_own_validation_accepts(string type)
    {
        var plugin = AllPlugins.Of(type);

        var result = plugin.Validate(plugin.Create());

        // Un elemento appena inserito che nasce già in errore costringerebbe a correggere
        // qualcosa prima ancora di aver deciso che cosa farne.
        Assert.True(result.IsValid, string.Join(" · ", result.Errors));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void The_element_that_the_tests_build_is_a_plausible_element(string type)
    {
        // Gli altri test costruiscono elementi con ogni proprietà spostata dal proprio
        // valore predefinito, per accorgersi se una si perde per strada. Quei valori devono
        // però restare valori possibili: un elemento assurdo farebbe fallire le verifiche
        // che passano dal validatore per un motivo che non c'entra niente con ciò che
        // stanno controllando.
        var plugin = AllPlugins.Of(type);

        var result = plugin.Validate(AllPlugins.Populated(plugin));

        Assert.True(result.IsValid, string.Join(" · ", result.Errors));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Every_plugin_renders_a_tag_and_not_a_document(string type)
    {
        var plugin = AllPlugins.Of(type);

        var markup = plugin.Render(AllPlugins.Populated(plugin)).Trim();

        Assert.StartsWith("<", markup);
        Assert.EndsWith(">", markup);

        // Il contorno del documento appartiene al componente: se lo mettesse il plugin, la
        // stessa forma non potrebbe essere disegnata sia nella cella singola sia nella
        // ripetizione, che sono due contenitori diversi.
        Assert.DoesNotContain("<svg", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<defs", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<pattern", markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patternTransform", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Every_plugin_renders_the_same_markup_in_any_language(string type)
    {
        var plugin = AllPlugins.Of(type);
        var element = AllPlugins.Populated(plugin);

        var invariante = plugin.Render(element);

        // Italiano e arabo saudita: il primo usa la virgola come separatore decimale, il
        // secondo usa cifre proprie in alcune configurazioni. In SVG la virgola separa due
        // numeri, quindi «10,5» non è un numero con la virgola: sono due numeri.
        foreach (var cultura in new[] { "it-IT", "ar-SA", "de-DE" })
        {
            var precedente = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(cultura);
                Assert.Equal(invariante, plugin.Render(element));
            }
            finally
            {
                CultureInfo.CurrentCulture = precedente;
            }
        }
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Every_plugin_declares_types_that_are_what_they_claim(string type)
    {
        var plugin = AllPlugins.Of(type);

        Assert.True(
            typeof(VectorElement).IsAssignableFrom(plugin.ElementClrType),
            $"{plugin.ElementClrType.Name} non è un VectorElement.");

        // Il componente di modifica si verifica per nome dell'interfaccia e non con
        // typeof(IComponent): questo progetto di test non referenzia Blazor, e non deve —
        // il contratto dei plugin vive apposta in un progetto che non lo conosce.
        Assert.Contains(
            plugin.EditorComponentType.GetInterfaces(),
            i => i.FullName == "Microsoft.AspNetCore.Components.IComponent");
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Every_plugin_provides_the_content_of_an_icon_not_the_icon_itself(string type)
    {
        var plugin = AllPlugins.Of(type);

        Assert.False(string.IsNullOrWhiteSpace(plugin.IconSvg));

        // Il tag <svg> lo mette chi mostra l'icona, che è l'unico a sapere quanto grande
        // deve essere e di che colore.
        Assert.DoesNotContain("<svg", plugin.IconSvg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_plugin_has_an_icon_of_its_own()
    {
        // L'icona ha un'implementazione predefinita nel contratto: se due plugin la
        // condividono, uno dei due si è dimenticato di disegnare la propria e nel menù di
        // inserimento compaiono due voci indistinguibili.
        Assert.Equal(
            AllPlugins.Instances.Count,
            AllPlugins.Instances.Select(p => p.IconSvg).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_registry_resolves_every_type_and_keeps_the_registration_order()
    {
        var registry = AllPlugins.Registry();

        foreach (var plugin in AllPlugins.Instances)
        {
            Assert.True(registry.TryGet(plugin.Type, out var trovato));
            Assert.Same(plugin, trovato);
        }

        // L'ordine è quello delle righe di registrazione, ed è l'ordine delle voci nel menù:
        // chi compone l'applicazione decide anche come si presenta la scelta.
        Assert.Equal(
            AllPlugins.Instances.Select(p => p.Type),
            registry.All.Select(p => p.Type));
    }

    [Fact]
    public void A_type_that_no_longer_has_a_plugin_is_simply_not_found()
    {
        var registry = AllPlugins.RegistryWithout("text");

        Assert.False(registry.TryGet("text", out var plugin));
        Assert.Null(plugin);

        // Gli altri otto continuano a rispondere: staccare un plugin non ne disturba nessun altro.
        Assert.Equal(8, registry.All.Count);
        Assert.True(registry.TryGet("rect", out _));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Every_plugin_hands_its_overall_opacity_to_the_common_section(string type)
    {
        // La sezione «Generale» dell'editor è una sola, comune a tutti i tipi, e compare
        // solo per i modelli che dichiarano IOverallOpacity. Un plugin che possiede
        // un'opacità complessiva ma non lo dichiara la perderebbe dall'interfaccia senza
        // che niente se ne accorga: non è un errore di compilazione, è un campo che non
        // c'è più.
        var elemento = AllPlugins.Of(type).Create();

        Assert.IsAssignableFrom<IOverallOpacity>(elemento);

        var opacita = (IOverallOpacity)elemento;
        opacita.Opacity = 0.5;
        Assert.Equal(0.5, opacita.Opacity);
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void The_overall_opacity_is_declared_once_and_not_twice(string type)
    {
        // Se un giorno la proprietà venisse spostata sulla classe base senza toglierla dai
        // modelli, il serializzatore troverebbe due «Opacity» e solleverebbe un'eccezione
        // all'apertura di qualunque documento. Qui costa una riga scoprirlo prima.
        var proprieta = AllPlugins.Of(type).ElementClrType
            .GetProperties()
            .Where(p => p.Name == "Opacity")
            .ToList();

        Assert.Single(proprieta);
    }

    public static TheoryData<string> Types => AllPlugins.Types;
}
