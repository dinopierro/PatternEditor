using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using PatternEditor.Element.Rect;
using PatternEditor.Sample.Api.Persistence;
using Xunit;

namespace PatternEditor.Sample.Api.Tests;

/// <summary>
/// Chi può scrivere che cosa.
///
/// <para>
/// La regola è una sola e non ha eccezioni: <b>un pattern lo modifica e lo elimina chi lo ha
/// scritto</b>. Quello che serve verificare sono i bordi — l'utente anonimo, l'utente
/// sbagliato, il pattern senza autore, e chi prova a firmare un documento con il nome di un
/// altro — perché è lì che una regola semplice smette di esserlo.
/// </para>
/// </summary>
public class PatternOwnershipTests
{
    private static Pattern Campione(string nome = "Prova")
    {
        var pattern = new Pattern { Name = nome };
        pattern.Definition.Width = 50;
        pattern.Definition.Height = 50;
        pattern.Definition.Elements.Add(RectElement.CreateDefault());
        return pattern;
    }

    /// <summary>
    /// Un pattern come quelli che c'erano prima: pubblico, perché prima della moderazione
    /// tutto lo era. È quello che dà la rilettura di un documento senza il campo
    /// <c>visibility</c>, e i test che scrivono direttamente nell'archivio — saltando
    /// l'endpoint — devono partire da lì, altrimenti verificherebbero una situazione che in
    /// quell'archivio non esiste.
    /// </summary>
    private static Pattern Vecchio(string nome = "Prova")
    {
        var pattern = Campione(nome);
        pattern.Visibility = PatternVisibility.Pubblica;
        return pattern;
    }

    /// <summary>
    /// Approva un pattern scrivendo nell'archivio, cioè senza passare dalla moderazione.
    /// I test che la riguardano la esercitano per intero (ModerationTests); qui serve solo
    /// che il pattern <b>sia</b> pubblico, e farlo in tre righe di preparazione invece che
    /// in dieci di HTTP tiene il test su quello che sta verificando.
    /// </summary>
    private static async Task PubblicaAsync(PatternApiFactory factory, Guid id)
    {
        var archivio = factory.Services.GetRequiredService<IPatternRepository>();
        var pattern = await archivio.GetByIdAsync(id);
        pattern!.Visibility = PatternVisibility.Pubblica;
        await archivio.ReplaceAsync(pattern);
    }

    private static StringContent ComeJson(PatternApiFactory factory, Pattern pattern) =>
        new(factory.Services.GetRequiredService<IPatternSerializer>().Serialize(pattern),
            Encoding.UTF8, "application/json");

    private static async Task<Pattern> RileggiAsync(PatternApiFactory factory, HttpClient client, Guid id)
    {
        var json = await client.GetStringAsync($"/api/patterns/{id}");
        return factory.Services.GetRequiredService<IPatternSerializer>().Deserialize(json);
    }

    // ------------------------------------------------------------------------ anonimo ---

    [Fact]
    public async Task An_anonymous_visitor_can_read_what_is_public()
    {
        // Consultare non richiede un accesso, e non deve: quello che è pubblico si legge da
        // chiunque. Quello che non lo è ancora, invece, non esiste — e risponde 404 e non
        // 403, perché 403 confermerebbe che c'è.
        using var factory = new PatternApiFactory();
        using var autore = await factory.ClientAutenticatoAsync("mario");
        using var chiunque = factory.CreateClient();

        var pattern = Campione();
        await autore.PostAsync("/api/patterns", ComeJson(factory, pattern));

        Assert.Equal(HttpStatusCode.OK, (await chiunque.GetAsync("/api/patterns")).StatusCode);
        Assert.Empty((await chiunque.GetFromJsonAsync<List<JsonElement>>("/api/patterns"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await chiunque.GetAsync($"/api/patterns/{pattern.Id}")).StatusCode);

        await PubblicaAsync(factory, pattern.Id);

        Assert.Single((await chiunque.GetFromJsonAsync<List<JsonElement>>("/api/patterns"))!);
        Assert.Equal(HttpStatusCode.OK, (await chiunque.GetAsync($"/api/patterns/{pattern.Id}")).StatusCode);
    }

    [Fact]
    public async Task An_anonymous_visitor_cannot_write_anything()
    {
        using var factory = new PatternApiFactory();
        using var chiunque = factory.CreateClient();

        var pattern = Campione();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await chiunque.PostAsync("/api/patterns", ComeJson(factory, pattern))).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await chiunque.PutAsync($"/api/patterns/{pattern.Id}", ComeJson(factory, pattern))).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await chiunque.DeleteAsync($"/api/patterns/{pattern.Id}")).StatusCode);
    }

    [Fact]
    public async Task Refusing_an_anonymous_write_says_who_it_does_not_know_and_not_what_is_forbidden()
    {
        // 401 e 403 dicono due cose diverse e il client se ne serve: il primo porta alla
        // finestra di accesso, il secondo a un messaggio. Confonderli manderebbe alla
        // finestra di accesso chi è già entrato.
        using var factory = new PatternApiFactory();
        using var chiunque = factory.CreateClient();

        var risposta = await chiunque.PostAsync("/api/patterns", ComeJson(factory, Campione()));

        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);

        var documento = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.Contains("accesso", documento.RootElement.GetProperty("message").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------- l'autore nel documento ---

    [Fact]
    public async Task The_author_is_written_inside_the_document()
    {
        // Non in un indice a parte: l'autore accompagna il file quando lo si esporta, lo si
        // copia o lo si sposta, invece di restare indietro in una tabella che nessuno si
        // ricorda di portarsi dietro.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");

        var pattern = Campione("Il mio");
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));

        var salvato = await File.ReadAllTextAsync(Path.Combine(factory.StorageDirectory, $"{pattern.Id}.json"));

        Assert.Contains("\"authorName\": \"mario\"", salvato, StringComparison.Ordinal);
        Assert.Contains("\"authorId\"", salvato, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_list_carries_the_author_and_how_to_draw_it()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");

        await mario.PostAsync("/api/patterns", ComeJson(factory, Campione("Il mio")));

        var elenco = await mario.GetFromJsonAsync<List<JsonElement>>("/api/patterns");
        var riga = Assert.Single(elenco!);

        Assert.Equal("mario", riga.GetProperty("ownerName").GetString());
        Assert.NotEqual(Guid.Empty, riga.GetProperty("ownerId").GetGuid());

        // Colore e foto non possono stare nel documento: appartengono all'account e
        // cambiano quando l'utente li cambia.
        Assert.StartsWith("#", riga.GetProperty("ownerColor").GetString()!, StringComparison.Ordinal);
        Assert.False(riga.GetProperty("ownerHasPhoto").GetBoolean());
    }

    [Fact]
    public async Task Nobody_can_sign_a_document_with_somebody_elses_name()
    {
        // È la ragione per cui l'autore lo decide il server e non il corpo della richiesta:
        // altrimenti basterebbero due righe di JSON per attribuirsi — o attribuire ad altri —
        // qualunque pattern.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var lucia = await factory.ClientDiUnAltroAsync("lucia");

        var falso = Campione("Firmato male");
        falso.AuthorId = Guid.CreateVersion7();
        falso.AuthorName = "lucia";

        await mario.PostAsync("/api/patterns", ComeJson(factory, falso));

        var salvato = await RileggiAsync(factory, mario, falso.Id);

        Assert.Equal("mario", salvato.AuthorName);
        Assert.NotEqual(falso.AuthorId, salvato.AuthorId);

        // E infatti Lucia non lo tocca.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await lucia.PutAsync($"/api/patterns/{falso.Id}", ComeJson(factory, falso))).StatusCode);
    }

    [Fact]
    public async Task Saving_again_does_not_let_the_author_be_rewritten()
    {
        // La paternità di un documento non è una proprietà che si modifica salvandolo.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));

        pattern.AuthorId = Guid.CreateVersion7();
        pattern.AuthorName = "qualcun altro";
        await mario.PutAsync($"/api/patterns/{pattern.Id}", ComeJson(factory, pattern));

        var riletto = await RileggiAsync(factory, mario, pattern.Id);
        Assert.Equal("mario", riletto.AuthorName);
    }

    // -------------------------------------------------------------------- fra due utenti ---

    [Fact]
    public async Task Somebody_else_cannot_overwrite_it()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var lucia = await factory.ClientDiUnAltroAsync("lucia");

        var pattern = Campione("Di Mario");
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));

        pattern.Name = "Adesso di Lucia";
        var risposta = await lucia.PutAsync($"/api/patterns/{pattern.Id}", ComeJson(factory, pattern));

        Assert.Equal(HttpStatusCode.Forbidden, risposta.StatusCode);

        // E il nome è rimasto quello: il rifiuto dev'essere anche un non-fatto, non solo un
        // codice di stato.
        var riletto = await mario.GetStringAsync($"/api/patterns/{pattern.Id}");
        Assert.Contains("Di Mario", riletto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_refusal_says_whose_it_is()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var lucia = await factory.ClientDiUnAltroAsync("lucia");

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));

        var risposta = await lucia.PutAsync($"/api/patterns/{pattern.Id}", ComeJson(factory, pattern));
        var documento = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());

        Assert.Contains("mario", documento.RootElement.GetProperty("message").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Somebody_else_cannot_delete_it()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var lucia = await factory.ClientDiUnAltroAsync("lucia");

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));
        await PubblicaAsync(factory, pattern.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await lucia.DeleteAsync($"/api/patterns/{pattern.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lucia.GetAsync($"/api/patterns/{pattern.Id}")).StatusCode);
    }

    [Fact]
    public async Task The_author_can_do_both()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");

        var pattern = Campione("Prima");
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));

        pattern.Name = "Dopo";
        Assert.Equal(HttpStatusCode.OK,
            (await mario.PutAsync($"/api/patterns/{pattern.Id}", ComeJson(factory, pattern))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await mario.DeleteAsync($"/api/patterns/{pattern.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_bulk_delete_takes_what_is_mine_and_leaves_what_is_not()
    {
        // Un pattern altrui nel lotto vale come «non eliminato» e non fa fallire gli altri:
        // è la stessa regola dell'identificativo inesistente, ed è quella che permette a chi
        // chiama di sapere esattamente che cosa è successo riga per riga.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var lucia = await factory.ClientDiUnAltroAsync("lucia");

        var mio = Campione("Mio");
        var suo = Campione("Suo");
        await mario.PostAsync("/api/patterns", ComeJson(factory, mio));
        await lucia.PostAsync("/api/patterns", ComeJson(factory, suo));

        // Pubblico perché il test finisce guardando se è ancora lì: privato, Mario non lo
        // vedrebbe comunque, e il controllo direbbe «eliminato» anche se non lo fosse.
        await PubblicaAsync(factory, suo.Id);

        var richiesta = new HttpRequestMessage(HttpMethod.Delete, "/api/patterns")
        {
            Content = JsonContent.Create(new[] { mio.Id, suo.Id }),
        };

        var risposta = await mario.SendAsync(richiesta);
        var esiti = await risposta.Content.ReadFromJsonAsync<Dictionary<Guid, bool>>();

        Assert.True(esiti![mio.Id]);
        Assert.False(esiti[suo.Id]);

        Assert.Equal(HttpStatusCode.NotFound, (await mario.GetAsync($"/api/patterns/{mio.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await mario.GetAsync($"/api/patterns/{suo.Id}")).StatusCode);
    }

    // ------------------------------------------------------------------- senza autore ---

    [Fact]
    public async Task A_pattern_without_an_author_belongs_to_nobody_and_nobody_can_change_it()
    {
        // «Senza autore» non significa «di chiunque abbia fatto l'accesso»: se non risulta
        // chi l'ha scritto, non risulta nemmeno chi ha il diritto di riscriverlo.
        using var factory = new PatternApiFactory();

        // Scritto passando dal repository e non dall'endpoint: nessun autore, come i
        // pattern che c'erano prima che gli account esistessero.
        var orfano = Campione("Senza autore");
        await factory.Services.GetRequiredService<IPatternRepository>().CreateAsync(orfano);

        using var mario = await factory.ClientAutenticatoAsync("mario");

        orfano.Name = "Provo a prenderlo";

        Assert.Equal(HttpStatusCode.Forbidden,
            (await mario.PutAsync($"/api/patterns/{orfano.Id}", ComeJson(factory, orfano))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await mario.DeleteAsync($"/api/patterns/{orfano.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_pattern_without_an_author_can_still_be_read_and_downloaded()
    {
        // Intoccabile non vuol dire invisibile: aprirlo, guardarlo e scaricarlo resta
        // possibile a chiunque, ed è quasi sempre quello che serve.
        using var factory = new PatternApiFactory();

        var orfano = Vecchio("Senza autore");
        await factory.Services.GetRequiredService<IPatternRepository>().CreateAsync(orfano);

        using var chiunque = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await chiunque.GetAsync($"/api/patterns/{orfano.Id}")).StatusCode);
    }

    [Fact]
    public async Task The_refusal_on_a_pattern_without_an_author_explains_that_it_has_none()
    {
        using var factory = new PatternApiFactory();

        var orfano = Campione();
        await factory.Services.GetRequiredService<IPatternRepository>().CreateAsync(orfano);

        using var mario = await factory.ClientAutenticatoAsync("mario");

        var risposta = await mario.PutAsync($"/api/patterns/{orfano.Id}", ComeJson(factory, orfano));
        var documento = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());

        Assert.Contains("autore", documento.RootElement.GetProperty("message").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_pattern_without_an_author_says_so_instead_of_inventing_one()
    {
        using var factory = new PatternApiFactory();

        var orfano = Vecchio("Senza autore");
        await factory.Services.GetRequiredService<IPatternRepository>().CreateAsync(orfano);

        using var chiunque = factory.CreateClient();
        var elenco = await chiunque.GetFromJsonAsync<List<JsonElement>>("/api/patterns");
        var riga = Assert.Single(elenco!);

        Assert.Equal(JsonValueKind.Null, riga.GetProperty("ownerId").ValueKind);
        Assert.Equal(JsonValueKind.Null, riga.GetProperty("ownerName").ValueKind);
    }

    [Fact]
    public async Task An_author_that_no_longer_exists_keeps_the_name_and_loses_only_the_colours()
    {
        // Succede copiando a mano un file nella cartella, o cancellando un account: il nome
        // sta nel documento e resta leggibile, mentre colore e foto stanno nell'account e
        // spariscono con lui. Il cerchio ripiega sul colore ricavato dal nome, che il client
        // sa calcolare da sé.
        using var factory = new PatternApiFactory();

        var orfano = Vecchio("Di un fantasma");
        orfano.AuthorId = Guid.CreateVersion7();
        orfano.AuthorName = "utente-che-non-esiste";
        await factory.Services.GetRequiredService<IPatternRepository>().CreateAsync(orfano);

        using var chiunque = factory.CreateClient();
        var elenco = await chiunque.GetFromJsonAsync<List<JsonElement>>("/api/patterns");
        var riga = Assert.Single(elenco!);

        Assert.Equal("utente-che-non-esiste", riga.GetProperty("ownerName").GetString());
        Assert.Equal(JsonValueKind.Null, riga.GetProperty("ownerColor").ValueKind);
        Assert.False(riga.GetProperty("ownerHasPhoto").GetBoolean());
    }

    [Fact]
    public async Task A_pattern_signed_by_somebody_who_does_not_exist_cannot_be_touched_by_anyone()
    {
        // Non è un pattern «di nessuno» ma «di un altro», e la regola è la stessa: scrive
        // chi ha scritto. Nessun utente ha quell'identificativo, quindi nessuno scrive.
        using var factory = new PatternApiFactory();

        var orfano = Campione();
        orfano.AuthorId = Guid.CreateVersion7();
        orfano.AuthorName = "utente-che-non-esiste";
        await factory.Services.GetRequiredService<IPatternRepository>().CreateAsync(orfano);

        using var mario = await factory.ClientAutenticatoAsync("mario");

        var risposta = await mario.PutAsync($"/api/patterns/{orfano.Id}", ComeJson(factory, orfano));
        Assert.Equal(HttpStatusCode.Forbidden, risposta.StatusCode);

        var documento = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.Contains("utente-che-non-esiste",
            documento.RootElement.GetProperty("message").GetString()!, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await mario.DeleteAsync($"/api/patterns/{orfano.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_file_copied_by_hand_shows_up_twice_with_the_same_identifier()
    {
        // Non è un difetto dell'API ma un fatto dell'archivio: due file, due righe, e
        // l'identificativo è quello scritto dentro. Vale la pena fissarlo in un test perché
        // è la situazione che il client deve saper disegnare senza morire — le chiavi
        // ripetute fra fratelli, in Blazor, sono un'eccezione che non si recupera.
        using var factory = new PatternApiFactory();

        var originale = Vecchio("Originale");
        await factory.Services.GetRequiredService<IPatternRepository>().CreateAsync(originale);

        // La copia: stesso contenuto, nome di file diverso. È quello che succede
        // trascinando un file nella cartella.
        var percorso = Path.Combine(factory.StorageDirectory, $"{originale.Id}.json");
        File.Copy(percorso, Path.Combine(factory.StorageDirectory, $"{Guid.CreateVersion7()}.json"));

        using var chiunque = factory.CreateClient();
        var elenco = await chiunque.GetFromJsonAsync<List<JsonElement>>("/api/patterns");

        Assert.Equal(2, elenco!.Count);
        Assert.Equal(2, elenco.Count(r => r.GetProperty("id").GetGuid() == originale.Id));
    }

    [Fact]
    public async Task A_file_whose_name_disagrees_with_its_identifier_is_listed_but_not_reachable()
    {
        // L'elenco legge l'identificativo dentro il documento, la lettura singola il nome
        // del file: se i due non coincidono la scheda compare ma non si apre. Il client
        // deve dirlo, non aprire un editor vuoto.
        using var factory = new PatternApiFactory();

        var pattern = Vecchio("Smarrito");
        await factory.Services.GetRequiredService<IPatternRepository>().CreateAsync(pattern);

        var percorso = Path.Combine(factory.StorageDirectory, $"{pattern.Id}.json");
        File.Move(percorso, Path.Combine(factory.StorageDirectory, $"{Guid.CreateVersion7()}.json"));

        using var chiunque = factory.CreateClient();

        var elenco = await chiunque.GetFromJsonAsync<List<JsonElement>>("/api/patterns");
        Assert.Equal(pattern.Id, Assert.Single(elenco!).GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.NotFound, (await chiunque.GetAsync($"/api/patterns/{pattern.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_copy_belongs_to_whoever_made_it_and_not_to_whoever_drew_it()
    {
        // Duplicare crea un documento nuovo: chi lo crea ne è l'autore, anche se il disegno
        // viene da un altro. È il motivo per cui duplicare richiede un accesso.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var lucia = await factory.ClientDiUnAltroAsync("lucia");

        var originale = Campione("Originale");
        await mario.PostAsync("/api/patterns", ComeJson(factory, originale));

        // La copia come la farebbe il client: stesso disegno, identificativo nuovo.
        var copia = Campione("Copia di Originale");
        await lucia.PostAsync("/api/patterns", ComeJson(factory, copia));

        var riletta = await RileggiAsync(factory, lucia, copia.Id);
        Assert.Equal("lucia", riletta.AuthorName);

        // E l'originale non si è mosso.
        var intatto = await RileggiAsync(factory, mario, originale.Id);
        Assert.Equal("mario", intatto.AuthorName);
    }
}
