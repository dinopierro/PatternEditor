using System.Text.Json;
using System.Text.Json.Nodes;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Abstractions.Tests;
using PatternEditor.Core.Models;
using PatternEditor.Services;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// «Domani stacco un plugin dal componente, e il giorno dopo mi arriva un documento
/// scritto quando c'era ancora.»
///
/// <para>
/// È lo scenario che il sistema deve attraversare senza rompere niente e senza perdere
/// niente, ed è anche quello che non si verifica da solo usando l'applicazione: per vederlo
/// servirebbe togliere una riga dal <c>Program.cs</c>, riaprire un vecchio documento e
/// guardare bene. Qui la riga la si toglie a ogni caso di prova, una per volta, per tutti e
/// nove i tipi.
/// </para>
///
/// <para>
/// Le promesse verificate sono quattro, e valgono insieme: il documento <b>si apre</b>,
/// l'elemento orfano <b>resta nell'elenco</b>, il salvataggio <b>non lo tocca</b>, e il
/// giorno in cui il plugin torna l'elemento <b>ridiventa quello di prima</b>, con tutti i
/// suoi valori. Le prime tre senza la quarta non servirebbero a niente: conservare un
/// elemento che poi non si riesce più a leggere è come perderlo.
/// </para>
/// </summary>
public class DetachedPluginTests
{
    public static TheoryData<string> Types => AllPlugins.Types;

    /// <summary>Un pattern che contiene un elemento per ogni tipo, tutti con valori propri.</summary>
    private static Pattern PatternWithEveryType()
    {
        var pattern = new Pattern { Name = "Scritto da chi aveva tutto" };
        pattern.Definition.Width = 80;
        pattern.Definition.Height = 60;

        foreach (var plugin in AllPlugins.Instances)
        {
            pattern.Definition.Elements.Add(AllPlugins.Populated(plugin));
        }

        return pattern;
    }

    private static IPatternSerializer SerializerWithEveryPlugin() =>
        new PatternSerializer(AllPlugins.Registry());

    private static IPatternSerializer SerializerWithout(string type) =>
        new PatternSerializer(AllPlugins.RegistryWithout(type));

    /// <summary>Il nodo JSON di un elemento dentro un documento, cercato per identificativo.</summary>
    private static JsonObject ElementNode(string json, Guid id) =>
        JsonNode.Parse(json)!["definition"]!["elements"]!.AsArray()
            .Select(n => n!.AsObject())
            .Single(n => n["id"]!.GetValue<string>() == id.ToString());

    // ------------------------------------------------------------------ si apre

    [Theory]
    [MemberData(nameof(Types))]
    public void A_document_that_uses_a_detached_plugin_still_opens(string detached)
    {
        var pattern = PatternWithEveryType();
        var json = SerializerWithEveryPlugin().Serialize(pattern);

        var riletto = SerializerWithout(detached).Deserialize(json);

        // Nessun elemento perso per strada: nove erano, nove sono.
        Assert.Equal(AllPlugins.Instances.Count, riletto.Definition.Elements.Count);
        Assert.Equal(
            pattern.Definition.Elements.Select(e => e.Id),
            riletto.Definition.Elements.Select(e => e.Id));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Only_the_detached_element_loses_its_type(string detached)
    {
        var pattern = PatternWithEveryType();
        var json = SerializerWithEveryPlugin().Serialize(pattern);

        var riletto = SerializerWithout(detached).Deserialize(json);

        foreach (var elemento in riletto.Definition.Elements)
        {
            if (elemento.Type == detached)
            {
                var sconosciuto = Assert.IsType<UnknownVectorElement>(elemento);
                Assert.Equal(detached, sconosciuto.Type);
            }
            else
            {
                // Gli altri otto non si accorgono di niente: staccare un plugin non
                // degrada il documento, degrada un elemento solo.
                Assert.Equal(AllPlugins.Of(elemento.Type).ElementClrType, elemento.GetType());
            }
        }
    }

    // ------------------------------------------------------------------ non lo tocca

    [Theory]
    [MemberData(nameof(Types))]
    public void Re_saving_does_not_change_a_single_field_of_the_detached_element(string detached)
    {
        var pattern = PatternWithEveryType();
        var originale = pattern.Definition.Elements.Single(e => e.Type == detached);
        var json = SerializerWithEveryPlugin().Serialize(pattern);

        var serializer = SerializerWithout(detached);
        var risalvato = serializer.Serialize(serializer.Deserialize(json));

        // Confronto sul nodo JSON e non sulle proprietà: l'installazione che non ha il
        // plugin non sa nemmeno quali proprietà esistano, e deve restituire il pezzo di
        // documento esattamente com'è arrivato — comprese eventuali chiavi che nessuna
        // versione del modello ha mai avuto.
        Assert.True(
            JsonNode.DeepEquals(ElementNode(json, originale.Id), ElementNode(risalvato, originale.Id)),
            $"Il documento risalvato ha cambiato l'elemento di tipo \"{detached}\".");
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void A_field_that_no_version_of_the_model_knows_is_kept_as_well(string detached)
    {
        var pattern = PatternWithEveryType();
        var originale = pattern.Definition.Elements.Single(e => e.Type == detached);

        // Si aggiunge a mano una proprietà che il modello non ha: è il caso di un documento
        // scritto da una versione più recente del plugin, quella che verrà.
        var documento = JsonNode.Parse(SerializerWithEveryPlugin().Serialize(pattern))!;
        ElementNodeOf(documento, originale.Id)["proprietaDelFuturo"] = "arrivata dal domani";
        var json = documento.ToJsonString();

        var serializer = SerializerWithout(detached);
        var risalvato = serializer.Serialize(serializer.Deserialize(json));

        Assert.Equal(
            "arrivata dal domani",
            ElementNode(risalvato, originale.Id)["proprietaDelFuturo"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ ritorna quello di prima

    [Theory]
    [MemberData(nameof(Types))]
    public void When_the_plugin_comes_back_the_element_is_exactly_the_one_of_before(string detached)
    {
        var pattern = PatternWithEveryType();
        var originale = pattern.Definition.Elements.Single(e => e.Type == detached);
        var json = SerializerWithEveryPlugin().Serialize(pattern);

        // Il documento passa da un'installazione senza il plugin, che lo riscrive...
        var senza = SerializerWithout(detached);
        var risalvato = senza.Serialize(senza.Deserialize(json));

        // ...e torna su un'installazione che ce l'ha.
        var elemento = SerializerWithEveryPlugin()
            .Deserialize(risalvato).Definition.Elements
            .Single(e => e.Id == originale.Id);

        // È questa la verifica che conta: non «l'elemento c'è ancora», ma «l'elemento è
        // di nuovo quello, con tutti i suoi valori».
        AllPlugins.AssertSameProperties(originale, elemento);
    }

    [Fact]
    public void A_document_that_passes_through_nine_incomplete_installations_arrives_intact()
    {
        var pattern = PatternWithEveryType();
        var json = SerializerWithEveryPlugin().Serialize(pattern);

        // Ogni tappa è un'installazione a cui manca un plugin diverso: apre il documento,
        // lo salva e lo passa alla successiva. Alla fine ogni elemento è stato «sconosciuto»
        // almeno una volta.
        foreach (var plugin in AllPlugins.Instances)
        {
            var tappa = SerializerWithout(plugin.Type);
            json = tappa.Serialize(tappa.Deserialize(json));
        }

        var arrivato = SerializerWithEveryPlugin().Deserialize(json);

        Assert.Equal(AllPlugins.Instances.Count, arrivato.Definition.Elements.Count);
        for (var i = 0; i < pattern.Definition.Elements.Count; i++)
        {
            AllPlugins.AssertSameProperties(pattern.Definition.Elements[i], arrivato.Definition.Elements[i]);
        }
    }

    // ------------------------------------------------------------------ il resto continua a funzionare

    [Theory]
    [MemberData(nameof(Types))]
    public void The_renderer_skips_the_detached_element_and_draws_the_others(string detached)
    {
        var pattern = PatternWithEveryType();
        var json = SerializerWithEveryPlugin().Serialize(pattern);
        var riletto = SerializerWithout(detached).Deserialize(json);

        var renderer = new PatternSvgRenderer(AllPlugins.RegistryWithout(detached), new SvgFilterRenderer());
        var markup = renderer.RenderElementsMarkup(riletto);

        foreach (var elemento in pattern.Definition.Elements)
        {
            var suo = AllPlugins.Of(elemento.Type).Render(elemento);

            if (elemento.Type == detached)
            {
                // Nessun segnaposto al suo posto: mostrare una forma che nel documento non
                // c'è sarebbe peggio che non mostrare niente.
                Assert.DoesNotContain(suo, markup);
            }
            else
            {
                Assert.Contains(suo, markup);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void The_whole_svg_is_still_well_formed_around_the_hole(string detached)
    {
        var pattern = PatternWithEveryType();
        var json = SerializerWithEveryPlugin().Serialize(pattern);
        var riletto = SerializerWithout(detached).Deserialize(json);

        var svg = new PatternSvgRenderer(AllPlugins.RegistryWithout(detached), new SvgFilterRenderer())
            .RenderStandaloneSvg(riletto, "100%", "100%");

        Assert.Contains("<defs>", svg);
        Assert.Contains("<pattern", svg);
        Assert.Contains("</pattern>", svg);
        Assert.Contains("fill=\"url(#", svg);
        Assert.EndsWith("</svg>", svg);
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void The_validator_warns_about_the_detached_element_but_does_not_block_the_work(string detached)
    {
        var pattern = PatternWithEveryType();
        var json = SerializerWithEveryPlugin().Serialize(pattern);
        var riletto = SerializerWithout(detached).Deserialize(json);

        var esito = new PatternValidator(AllPlugins.RegistryWithout(detached)).Validate(riletto);

        // Avviso, non errore: il documento non è sbagliato, è questa installazione a essere
        // incompleta. Bloccare la conferma impedirebbe di lavorare sugli altri otto elementi.
        Assert.True(esito.IsValid, string.Join(" · ", esito.Errors));
        Assert.Contains(esito.Warnings, w => w.Contains(detached, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void The_detached_element_can_be_duplicated_without_leaving_two_twins_behind(string detached)
    {
        var pattern = PatternWithEveryType();
        var json = SerializerWithEveryPlugin().Serialize(pattern);
        var serializer = SerializerWithout(detached);
        var riletto = serializer.Deserialize(json);

        var originale = riletto.Definition.Elements.Single(e => e.Type == detached);
        var copia = new VectorElementCloner(serializer).Clone(originale);

        Assert.NotEqual(originale.Id, copia.Id);

        // La trappola: l'identificativo nuovo deve stare ANCHE dentro il JSON conservato,
        // non solo nell'oggetto. Se restasse quello vecchio, il duplicato si salverebbe con
        // l'identificativo dell'originale e il documento uscirebbe con due elementi gemelli.
        var grezzo = Assert.IsType<UnknownVectorElement>(copia);
        Assert.Equal(copia.Id.ToString(), grezzo.RawJson.GetProperty("id").GetString());

        // E il documento che ne esce deve essere accettabile: due identificativi diversi.
        riletto.Definition.Elements.Add(copia);
        var esito = new PatternValidator(AllPlugins.RegistryWithout(detached)).Validate(riletto);
        Assert.True(esito.IsValid, string.Join(" · ", esito.Errors));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Opening_the_editor_copies_the_pattern_without_losing_the_detached_element(string detached)
    {
        // Il componente, all'apertura, fa esattamente questo: serializza e rideserializza il
        // pattern ricevuto, per lavorare su una copia. Il giro passa dal registro, quindi
        // deve reggere anche un elemento che il registro non conosce.
        var pattern = PatternWithEveryType();
        var serializer = SerializerWithout(detached);
        var daArchivio = serializer.Deserialize(SerializerWithEveryPlugin().Serialize(pattern));

        var copiaDiLavoro = serializer.Deserialize(serializer.Serialize(daArchivio));

        Assert.Equal(daArchivio.Definition.Elements.Count, copiaDiLavoro.Definition.Elements.Count);
        Assert.Equal(serializer.Serialize(daArchivio), serializer.Serialize(copiaDiLavoro));
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Duplicating_the_whole_pattern_keeps_the_detached_element(string detached)
    {
        var pattern = PatternWithEveryType();
        var serializer = SerializerWithout(detached);
        var riletto = serializer.Deserialize(SerializerWithEveryPlugin().Serialize(pattern));

        var copia = new PatternCloner(new VectorElementCloner(serializer))
            .Clone(riletto, "Copia di " + riletto.Name);

        Assert.Equal(riletto.Definition.Elements.Count, copia.Definition.Elements.Count);
        Assert.Contains(copia.Definition.Elements, e => e.Type == detached);

        // Nessun identificativo in comune fra originale e copia, nemmeno quello
        // dell'elemento che nessuno sa leggere.
        Assert.Empty(copia.Definition.Elements
            .Select(e => e.Id)
            .Intersect(riletto.Definition.Elements.Select(e => e.Id)));
    }

    // ------------------------------------------------------------------ il caso estremo

    [Fact]
    public void A_document_opened_with_no_plugin_at_all_does_not_explode()
    {
        // L'installazione vuota non è un'ipotesi di scuola: è l'applicazione ospitante che
        // registra il componente e dimentica le righe dei plugin.
        var pattern = PatternWithEveryType();
        var json = SerializerWithEveryPlugin().Serialize(pattern);

        var vuoto = new VectorElementPluginRegistry();
        var serializer = new PatternSerializer(vuoto);
        var riletto = serializer.Deserialize(json);

        Assert.Equal(AllPlugins.Instances.Count, riletto.Definition.Elements.Count);
        Assert.All(riletto.Definition.Elements, e => Assert.IsType<UnknownVectorElement>(e));

        // Si disegna il contorno del documento e nient'altro: una cella vuota, non un errore.
        var svg = new PatternSvgRenderer(vuoto, new SvgFilterRenderer()).RenderStandaloneSvg(riletto, "100%", "100%");
        Assert.Contains("<pattern", svg);
        Assert.EndsWith("</svg>", svg);

        // Nove avvisi, nessun errore.
        var esito = new PatternValidator(vuoto).Validate(riletto);
        Assert.True(esito.IsValid);
        Assert.Equal(AllPlugins.Instances.Count, esito.Warnings.Count);

        // E il documento risalvato è identico a quello ricevuto.
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(serializer.Serialize(riletto))),
            "Un'installazione senza plugin ha modificato il documento che ha solo riletto.");
    }

    [Fact]
    public void The_kept_json_survives_the_document_it_was_read_from()
    {
        // Un elemento sconosciuto conserva un pezzo del documento JSON da cui è stato
        // letto, e quel documento viene chiuso appena finita la lettura. Il pezzo
        // conservato deve quindi essere una copia autonoma e non una finestra sulla
        // memoria del documento, che nel frattempo è tornata in un serbatoio condiviso.
        //
        // Il test rilegge il pezzo dopo aver fatto passare altre cinquanta letture, che
        // quella memoria la ripescano. Non è una dimostrazione — un riferimento a memoria
        // riciclata può anche continuare a funzionare per un po' — ma è il controllo che
        // resta a guardia del comportamento visibile: dopo, quei valori devono esserci ancora.
        var pattern = PatternWithEveryType();
        var originale = pattern.Definition.Elements.Single(e => e.Type == "text");
        var json = SerializerWithEveryPlugin().Serialize(pattern);

        var serializer = SerializerWithout("text");
        var sconosciuto = Assert.IsType<UnknownVectorElement>(
            serializer.Deserialize(json).Definition.Elements.Single(e => e.Id == originale.Id));

        for (var i = 0; i < 50; i++)
        {
            using var altro = JsonDocument.Parse(json);
            _ = altro.RootElement.GetProperty("definition").GetProperty("elements").GetArrayLength();
        }

        Assert.Equal("text", sconosciuto.RawJson.GetProperty("type").GetString());
        Assert.Equal(originale.Id.ToString(), sconosciuto.RawJson.GetProperty("id").GetString());
    }

    private static JsonObject ElementNodeOf(JsonNode documento, Guid id) =>
        documento["definition"]!["elements"]!.AsArray()
            .Select(n => n!.AsObject())
            .Single(n => n["id"]!.GetValue<string>() == id.ToString());
}
