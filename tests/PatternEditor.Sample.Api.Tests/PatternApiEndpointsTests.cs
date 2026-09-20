using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using PatternEditor.Element.Circle;
using PatternEditor.Element.Line;
using PatternEditor.Element.Rect;
using PatternEditor.Element.Text;
using Xunit;

namespace PatternEditor.Sample.Api.Tests;

/// <summary>
/// Test di integrazione degli endpoint minimal API: l'applicazione viene avviata davvero,
/// le richieste passano dal binding, dal serializzatore polimorfico e dal repository.
///
/// Ogni test usa una propria istanza della factory, quindi una propria cartella di
/// storage: l'elenco restituito da GET è deterministico e i test non si influenzano.
/// </summary>
public class PatternApiEndpointsTests
{
    /// <summary>
    /// Serializzatore configurato come quello dell'API, prelevato dal contenitore di
    /// dipendenze dell'applicazione avviata: i test non ricostruiscono un registry
    /// proprio, che potrebbe divergere da quello reale.
    /// </summary>
    private static IPatternSerializer SerializerOf(PatternApiFactory factory) =>
        factory.Services.GetRequiredService<IPatternSerializer>();

    private static Pattern CreateSamplePattern(string name = "Prova")
    {
        var pattern = new Pattern { Name = name };
        pattern.Definition.Width = 50;
        pattern.Definition.Height = 50;
        pattern.Definition.Elements.Add(RectElement.CreateDefault());
        return pattern;
    }

    private static StringContent AsJson(IPatternSerializer serializer, Pattern pattern) =>
        new(serializer.Serialize(pattern), Encoding.UTF8, "application/json");

    private static StringContent RawJson(string json) =>
        new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task The_list_is_empty_when_no_pattern_has_been_saved()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();

        var summaries = await client.GetFromJsonAsync<List<JsonElement>>("/api/patterns");

        Assert.NotNull(summaries);
        Assert.Empty(summaries);
    }

    [Fact]
    public async Task A_created_pattern_appears_in_the_list_with_preview_and_dates()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var serializer = SerializerOf(factory);
        var pattern = CreateSamplePattern("Mattoncini");

        var created = await client.PostAsync("/api/patterns", AsJson(serializer, pattern));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var summaries = await client.GetFromJsonAsync<List<JsonElement>>("/api/patterns");

        var summary = Assert.Single(summaries!);
        Assert.Equal("Mattoncini", summary.GetProperty("name").GetString());
        Assert.Equal(pattern.Id, summary.GetProperty("id").GetGuid());
        Assert.Contains("<pattern", summary.GetProperty("previewSvg").GetString());
        Assert.Equal(
            summary.GetProperty("createdAt").GetDateTimeOffset(),
            summary.GetProperty("modifiedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Creating_the_same_id_twice_is_a_conflict()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var serializer = SerializerOf(factory);
        var pattern = CreateSamplePattern();

        await client.PostAsync("/api/patterns", AsJson(serializer, pattern));
        var second = await client.PostAsync("/api/patterns", AsJson(serializer, pattern));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Creating_a_pattern_without_a_valid_id_is_rejected()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();

        var response = await client.PostAsync("/api/patterns", RawJson(
            """
            {"version":1,"id":"00000000-0000-0000-0000-000000000000","name":"Senza id",
             "definition":{"width":50,"height":50,"elements":[]}}
            """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{ questo non e' json")]
    [InlineData("[]")]
    [InlineData("")]
    public async Task A_malformed_body_is_a_client_error_not_a_server_failure(string body)
    {
        // Il corpo arriva come testo e viene interpretato dal serializzatore: un documento
        // rotto deve produrre 400, non un'eccezione non gestita e quindi un 500.
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();

        var response = await client.PostAsync("/api/patterns", RawJson(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_body_without_an_identifier_is_rejected()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();

        var response = await client.PostAsync("/api/patterns", RawJson(
            """
            {"version":1,"name":"Senza id","definition":{"width":50,"height":50,"elements":[]}}
            """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("identificativo", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_malformed_body_is_rejected_by_the_update_endpoint_too()
    {
        // Su un pattern che esiste ed è suo: da quando c'è la proprietà, il permesso si
        // verifica PRIMA di leggere il corpo — non si lavora per conto di chi non può
        // chiedere — e su un identificativo inventato la risposta sarebbe «non esiste».
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var pattern = CreateSamplePattern();
        await client.PostAsync("/api/patterns", AsJson(SerializerOf(factory), pattern));

        var response = await client.PutAsync($"/api/patterns/{pattern.Id}", RawJson("{ rotto"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Updating_something_that_does_not_exist_says_so_before_looking_at_the_body()
    {
        // Non c'è nessun autore da proteggere e nessun documento da riscrivere: la risposta
        // giusta è «non esiste», qualunque cosa contenga il corpo.
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();

        var response = await client.PutAsync(
            $"/api/patterns/{Guid.CreateVersion7()}", RawJson("{ rotto"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reading_a_pattern_that_does_not_exist_returns_not_found()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();

        var response = await client.GetAsync($"/api/patterns/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_concrete_element_types_survive_the_round_trip_over_http()
    {
        // È il motivo per cui gli endpoint leggono il corpo come testo e usano
        // IPatternSerializer: il binding JSON predefinito, basato sul tipo dichiarato
        // VectorElement, appiattirebbe il polimorfismo degli elementi.
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var serializer = SerializerOf(factory);

        var pattern = CreateSamplePattern("Misto");
        pattern.Definition.Elements.Add(LineElement.CreateDefault());
        pattern.Definition.Elements.Add(CircleElement.CreateDefault());
        pattern.Definition.Elements.Add(TextElement.CreateDefault());

        await client.PostAsync("/api/patterns", AsJson(serializer, pattern));
        var json = await client.GetStringAsync($"/api/patterns/{pattern.Id}");
        var reloaded = serializer.Deserialize(json);

        Assert.Collection(
            reloaded.Definition.Elements,
            e => Assert.IsType<RectElement>(e),
            e => Assert.IsType<LineElement>(e),
            e => Assert.IsType<CircleElement>(e),
            e => Assert.IsType<TextElement>(e));

        // L'ordine è significativo per la sovrapposizione grafica: va preservato.
        Assert.Equal(
            pattern.Definition.Elements.Select(e => e.Id),
            reloaded.Definition.Elements.Select(e => e.Id));
    }

    [Fact]
    public async Task An_element_without_a_plugin_survives_the_round_trip_untouched()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var id = Guid.CreateVersion7();
        var elementId = Guid.CreateVersion7();

        // JSON con segnaposto sostituiti a mano: l'interpolazione mal si concilia con le
        // graffe del JSON, e il documento resta leggibile così come verrà inviato.
        var body = """
            {"version":1,"id":"ID-PATTERN","name":"Con ignoto",
             "definition":{"width":50,"height":50,"elements":[
               {"type":"tipo-senza-plugin","id":"ID-ELEMENTO","misura":42}]}}
            """
            .Replace("ID-PATTERN", id.ToString())
            .Replace("ID-ELEMENTO", elementId.ToString());

        var response = await client.PostAsync("/api/patterns", RawJson(body));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var reloaded = JsonDocument.Parse(await client.GetStringAsync($"/api/patterns/{id}"));
        var element = reloaded.RootElement
            .GetProperty("definition").GetProperty("elements")[0];

        Assert.Equal("tipo-senza-plugin", element.GetProperty("type").GetString());
        Assert.Equal(42, element.GetProperty("misura").GetInt32());
    }

    [Fact]
    public async Task A_pattern_with_an_orphan_element_can_still_be_listed_and_updated()
    {
        // Lo scenario vero: un documento scritto quando il plugin c'era arriva a
        // un'installazione in cui non c'è più. Non basta che si legga — deve attraversare
        // tutti gli endpoint, compreso quello che ne disegna l'anteprima, senza che il
        // servizio risponda 500 e senza che il pezzo che non sa leggere si perda.
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var id = Guid.CreateVersion7();
        var noto = Guid.CreateVersion7();
        var orfano = Guid.CreateVersion7();

        var body = """
            {"version":1,"id":"ID-PATTERN","name":"Con un orfano",
             "definition":{"width":50,"height":50,"elements":[
               {"type":"rect","id":"ID-NOTO","x":0,"y":0,"width":50,"height":50,
                "fill":"#9f2828","fillOpacity":1,"strokeWidth":0,"strokeOpacity":1,"opacity":1},
               {"type":"runa","id":"ID-ORFANO","incisione":"ᚦ","profondita":3.5}]}}
            """
            .Replace("ID-PATTERN", id.ToString())
            .Replace("ID-NOTO", noto.ToString())
            .Replace("ID-ORFANO", orfano.ToString());

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync("/api/patterns", RawJson(body))).StatusCode);

        // 1. L'elenco: l'anteprima si disegna con gli elementi che si sanno disegnare.
        var elenco = JsonDocument.Parse(await client.GetStringAsync("/api/patterns"));
        var scheda = elenco.RootElement.EnumerateArray().Single();
        var anteprima = scheda.GetProperty("previewSvg").GetString();
        Assert.Contains("<rect", anteprima);
        Assert.DoesNotContain("runa", anteprima);

        // 2. La modifica: si cambia il nome, come farebbe l'editor dopo una conferma.
        var modificato = body.Replace("Con un orfano", "Rinominato");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync($"/api/patterns/{id}", RawJson(modificato))).StatusCode);

        // 3. Il documento riletto: l'elemento orfano è ancora lì, con le sue proprietà
        //    sconosciute, al suo posto nell'ordine di disegno.
        var riletto = JsonDocument.Parse(await client.GetStringAsync($"/api/patterns/{id}"));
        var elementi = riletto.RootElement.GetProperty("definition").GetProperty("elements");

        Assert.Equal("Rinominato", riletto.RootElement.GetProperty("name").GetString());
        Assert.Equal(2, elementi.GetArrayLength());
        Assert.Equal("rect", elementi[0].GetProperty("type").GetString());
        Assert.Equal("runa", elementi[1].GetProperty("type").GetString());
        Assert.Equal("ᚦ", elementi[1].GetProperty("incisione").GetString());
        Assert.Equal(3.5, elementi[1].GetProperty("profondita").GetDouble());
    }

    [Fact]
    public async Task Updating_a_pattern_changes_only_the_modification_date()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var serializer = SerializerOf(factory);
        var pattern = CreateSamplePattern("Prima");

        var created = serializer.Deserialize(
            await (await client.PostAsync("/api/patterns", AsJson(serializer, pattern))).Content.ReadAsStringAsync());

        await Task.Delay(20);
        pattern.Name = "Dopo";
        var updated = await client.PutAsync($"/api/patterns/{pattern.Id}", AsJson(serializer, pattern));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var reloaded = serializer.Deserialize(await client.GetStringAsync($"/api/patterns/{pattern.Id}"));

        Assert.Equal("Dopo", reloaded.Name);
        Assert.Equal(created.CreatedAt, reloaded.CreatedAt);
        Assert.True(reloaded.ModifiedAt > created.ModifiedAt);
    }

    [Fact]
    public async Task Updating_a_pattern_that_does_not_exist_returns_not_found()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var serializer = SerializerOf(factory);
        var pattern = CreateSamplePattern();

        var response = await client.PutAsync($"/api/patterns/{pattern.Id}", AsJson(serializer, pattern));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_id_in_the_url_that_differs_from_the_body_is_rejected()
    {
        // Due pattern veri, entrambi suoi: il corpo dell'uno spedito all'indirizzo
        // dell'altro. È il modo in cui si proverebbe a sovrascrivere un pattern con il
        // contenuto di un altro, e va rifiutato anche quando sono tutti e due di chi chiede.
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var serializer = SerializerOf(factory);

        var uno = CreateSamplePattern("Uno");
        var altro = CreateSamplePattern("Altro");
        await client.PostAsync("/api/patterns", AsJson(serializer, uno));
        await client.PostAsync("/api/patterns", AsJson(serializer, altro));

        var response = await client.PutAsync($"/api/patterns/{altro.Id}", AsJson(serializer, uno));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_pattern_removes_it_from_the_list()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var serializer = SerializerOf(factory);
        var pattern = CreateSamplePattern();
        await client.PostAsync("/api/patterns", AsJson(serializer, pattern));

        var deleted = await client.DeleteAsync($"/api/patterns/{pattern.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<JsonElement>>("/api/patterns"))!);
    }

    [Fact]
    public async Task Deleting_a_pattern_that_does_not_exist_returns_not_found()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();

        var response = await client.DeleteAsync($"/api/patterns/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_bulk_delete_reports_each_id_separately_and_a_missing_one_does_not_stop_the_others()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var serializer = SerializerOf(factory);

        var first = CreateSamplePattern("Primo");
        var second = CreateSamplePattern("Secondo");
        var missing = Guid.CreateVersion7();
        await client.PostAsync("/api/patterns", AsJson(serializer, first));
        await client.PostAsync("/api/patterns", AsJson(serializer, second));

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/patterns")
        {
            Content = JsonContent.Create(new[] { first.Id, missing, second.Id }),
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<Dictionary<Guid, bool>>();

        Assert.True(results![first.Id]);
        Assert.True(results[second.Id]);
        Assert.False(results[missing]);
        Assert.Empty((await client.GetFromJsonAsync<List<JsonElement>>("/api/patterns"))!);
    }

    [Fact]
    public async Task The_patterns_are_listed_in_creation_order()
    {
        // L'ordinamento sfrutta l'UUIDv7 nel nome del file: i pattern più vecchi prima.
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var serializer = SerializerOf(factory);

        var names = new[] { "Uno", "Due", "Tre" };
        foreach (var name in names)
        {
            await client.PostAsync("/api/patterns", AsJson(serializer, CreateSamplePattern(name)));
            await Task.Delay(5);
        }

        var summaries = await client.GetFromJsonAsync<List<JsonElement>>("/api/patterns");

        Assert.Equal(names, summaries!.Select(s => s.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task The_tests_never_touch_the_real_storage_directory()
    {
        // Verifica esplicita della sicurezza della factory: se l'override della
        // configurazione smettesse di funzionare, questi test cancellerebbero i pattern
        // veri dell'applicazione.
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync();
        var serializer = SerializerOf(factory);

        await client.PostAsync("/api/patterns", AsJson(serializer, CreateSamplePattern()));

        Assert.True(Directory.Exists(factory.StorageDirectory));
        Assert.Single(Directory.GetFiles(factory.StorageDirectory, "*.json"));
        Assert.Contains(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), factory.StorageDirectory);
    }
}
