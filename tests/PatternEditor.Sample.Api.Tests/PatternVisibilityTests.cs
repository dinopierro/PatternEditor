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
/// Chi vede che cosa, e chi decide che lo si veda.
///
/// <para>
/// La regola sta in due frasi: <b>un pattern lo vede il suo autore</b>, e lo vedono tutti
/// gli altri soltanto dopo che qualcuno l'ha approvato. Quello che serve verificare sono i
/// modi di aggirarla — dichiararsi pubblici nel corpo della richiesta, farsi approvare una
/// cosa e salvarne un'altra, moderare senza essere moderatori — perché è per quelli che il
/// sistema esiste: senza, basterebbe un campo in un JSON.
/// </para>
/// </summary>
public class PatternVisibilityTests
{
    private static Pattern Campione(string nome = "Prova")
    {
        var pattern = new Pattern { Name = nome };
        pattern.Definition.Width = 50;
        pattern.Definition.Height = 50;
        pattern.Definition.Elements.Add(RectElement.CreateDefault());
        return pattern;
    }

    private static StringContent ComeJson(PatternApiFactory factory, Pattern pattern) =>
        new(factory.Services.GetRequiredService<IPatternSerializer>().Serialize(pattern),
            Encoding.UTF8, "application/json");

    private static async Task<PatternVisibility> StatoSuDiscoAsync(PatternApiFactory factory, Guid id)
    {
        var pattern = await factory.Services.GetRequiredService<IPatternRepository>().GetByIdAsync(id);
        return pattern!.Visibility;
    }

    private static async Task<HttpResponseMessage> ChiediAsync(HttpClient client, Guid id, string visibilita) =>
        await client.PutAsJsonAsync($"/api/patterns/{id}/visibilita", new { visibility = visibilita });

    // --------------------------------------------------------------- come nasce un pattern ---

    [Fact]
    public async Task A_new_pattern_is_private()
    {
        // È il valore predefinito, ed è quello che rende l'approvazione non aggirabile: se
        // nascesse pubblico, dimenticarsi di scegliere basterebbe a pubblicare.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));

        Assert.Equal(PatternVisibility.Privata, await StatoSuDiscoAsync(factory, pattern.Id));
    }

    [Fact]
    public async Task Asking_for_public_in_the_body_only_gets_you_into_the_queue()
    {
        // Il tentativo più ovvio: scriverlo nel documento. Se funzionasse, tutto il resto
        // sarebbe decorazione.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");

        var pattern = Campione();
        pattern.Visibility = PatternVisibility.Pubblica;
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));

        Assert.Equal(PatternVisibility.InAttesa, await StatoSuDiscoAsync(factory, pattern.Id));

        using var chiunque = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await chiunque.GetAsync($"/api/patterns/{pattern.Id}")).StatusCode);
    }

    [Fact]
    public async Task The_list_says_which_state_each_pattern_is_in()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));

        var elenco = await mario.GetFromJsonAsync<List<JsonElement>>("/api/patterns");

        Assert.Equal("Privata", Assert.Single(elenco!).GetProperty("visibility").GetString());
    }

    // ------------------------------------------------------------------ chiedere e ritirare ---

    [Fact]
    public async Task The_author_asks_to_publish_and_gets_told_it_is_waiting()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));

        var risposta = await ChiediAsync(mario, pattern.Id, "Pubblica");
        var esito = await risposta.Content.ReadFromJsonAsync<JsonElement>();

        // La risposta dice lo stato reale e non l'eco della domanda: è l'informazione che
        // serve all'interfaccia per scrivere «in attesa» invece di «pubblicato».
        Assert.Equal("InAttesa", esito.GetProperty("visibility").GetString());
        Assert.Equal(PatternVisibility.InAttesa, await StatoSuDiscoAsync(factory, pattern.Id));
    }

    [Fact]
    public async Task The_author_can_withdraw_the_request()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));
        await ChiediAsync(mario, pattern.Id, "Pubblica");
        await ChiediAsync(mario, pattern.Id, "Privata");

        Assert.Equal(PatternVisibility.Privata, await StatoSuDiscoAsync(factory, pattern.Id));
    }

    [Fact]
    public async Task Asking_twice_does_not_send_an_approved_pattern_back_to_the_queue()
    {
        // Premere due volte non è un errore da punire: qui non è cambiato niente da
        // guardare, e rimandarlo in coda toglierebbe dal portale un pattern già approvato.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var moderatore = await factory.ClientAmministratoreAsync();

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));
        await ChiediAsync(mario, pattern.Id, "Pubblica");
        await moderatore.PostAsync($"/api/moderation/{pattern.Id}/approve", null);

        await ChiediAsync(mario, pattern.Id, "Pubblica");

        Assert.Equal(PatternVisibility.Pubblica, await StatoSuDiscoAsync(factory, pattern.Id));
    }

    [Fact]
    public async Task Nobody_else_decides_whether_my_pattern_is_visible()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var lucia = await factory.ClientDiUnAltroAsync("lucia");

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));
        await ChiediAsync(mario, pattern.Id, "Pubblica");

        // Per Lucia quel pattern non esiste — è in coda e lei non modera — quindi la risposta
        // è 404 e non 403: dire «non è tuo» confermerebbe che c'è.
        Assert.Equal(HttpStatusCode.NotFound, (await ChiediAsync(lucia, pattern.Id, "Privata")).StatusCode);

        Assert.Equal(PatternVisibility.InAttesa, await StatoSuDiscoAsync(factory, pattern.Id));
    }

    [Fact]
    public async Task Changing_the_state_does_not_count_as_changing_the_drawing()
    {
        // Le date sono un'informazione che non si recupera, e l'elenco si ordina per ultima
        // modifica: far risalire in cima un pattern perché ne è cambiata la visibilità
        // sarebbe un riordino silenzioso dell'archivio.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));

        var archivio = factory.Services.GetRequiredService<IPatternRepository>();
        var prima = (await archivio.GetByIdAsync(pattern.Id))!.ModifiedAt;

        await ChiediAsync(mario, pattern.Id, "Pubblica");

        Assert.Equal(prima, (await archivio.GetByIdAsync(pattern.Id))!.ModifiedAt);
    }

    // ------------------------------------------------------------------------- moderazione ---

    [Fact]
    public async Task Only_an_administrator_sees_the_queue()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var chiunque = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await chiunque.GetAsync("/api/moderation/pending")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await mario.GetAsync("/api/moderation/pending")).StatusCode);

        using var moderatore = await factory.ClientAmministratoreAsync();
        Assert.Equal(HttpStatusCode.OK, (await moderatore.GetAsync("/api/moderation/pending")).StatusCode);
    }

    [Fact]
    public async Task The_queue_holds_what_is_waiting_and_nothing_else()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var moderatore = await factory.ClientAmministratoreAsync();

        var privato = Campione("Resta mio");
        var chiesto = Campione("Vorrei pubblicarlo");
        await mario.PostAsync("/api/patterns", ComeJson(factory, privato));
        await mario.PostAsync("/api/patterns", ComeJson(factory, chiesto));
        await ChiediAsync(mario, chiesto.Id, "Pubblica");

        var coda = await moderatore.GetFromJsonAsync<List<JsonElement>>("/api/moderation/pending");

        Assert.Equal(chiesto.Id, Assert.Single(coda!).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Approving_is_the_only_way_a_pattern_becomes_visible_to_everybody()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var moderatore = await factory.ClientAmministratoreAsync();
        using var chiunque = factory.CreateClient();

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));
        await ChiediAsync(mario, pattern.Id, "Pubblica");

        Assert.Equal(HttpStatusCode.NotFound, (await chiunque.GetAsync($"/api/patterns/{pattern.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await moderatore.PostAsync($"/api/moderation/{pattern.Id}/approve", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await chiunque.GetAsync($"/api/patterns/{pattern.Id}")).StatusCode);
    }

    [Fact]
    public async Task Rejecting_gives_it_back_to_its_author_instead_of_destroying_it()
    {
        // Decidere che una cosa non si pubblica non è decidere che non esiste: il disegno
        // resta di chi l'ha fatto, che può correggerlo e riproporlo.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var moderatore = await factory.ClientAmministratoreAsync();

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));
        await ChiediAsync(mario, pattern.Id, "Pubblica");

        await moderatore.PostAsync($"/api/moderation/{pattern.Id}/reject", null);

        Assert.Equal(PatternVisibility.Privata, await StatoSuDiscoAsync(factory, pattern.Id));
        Assert.Equal(HttpStatusCode.OK, (await mario.GetAsync($"/api/patterns/{pattern.Id}")).StatusCode);
    }

    [Fact]
    public async Task An_ordinary_user_cannot_approve_anything()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var lucia = await factory.ClientDiUnAltroAsync("lucia");

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));
        await ChiediAsync(mario, pattern.Id, "Pubblica");

        // Nemmeno il suo autore: chiedere e concedere sono due gesti di due persone diverse,
        // ed è tutto ciò che questo sistema fa.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await mario.PostAsync($"/api/moderation/{pattern.Id}/approve", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await lucia.PostAsync($"/api/moderation/{pattern.Id}/approve", null)).StatusCode);

        Assert.Equal(PatternVisibility.InAttesa, await StatoSuDiscoAsync(factory, pattern.Id));
    }

    [Fact]
    public async Task A_request_withdrawn_in_the_meantime_cannot_be_approved()
    {
        // Due pagine aperte: l'autore ritira mentre il moderatore guarda. Approvare lo stesso
        // renderebbe pubblico qualcosa che nessuno stava più proponendo.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var moderatore = await factory.ClientAmministratoreAsync();

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));
        await ChiediAsync(mario, pattern.Id, "Pubblica");
        await ChiediAsync(mario, pattern.Id, "Privata");

        Assert.Equal(HttpStatusCode.Conflict,
            (await moderatore.PostAsync($"/api/moderation/{pattern.Id}/approve", null)).StatusCode);

        Assert.Equal(PatternVisibility.Privata, await StatoSuDiscoAsync(factory, pattern.Id));
    }

    [Fact]
    public async Task Approving_does_not_count_as_changing_the_drawing_either()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var moderatore = await factory.ClientAmministratoreAsync();

        var pattern = Campione();
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));
        await ChiediAsync(mario, pattern.Id, "Pubblica");

        var archivio = factory.Services.GetRequiredService<IPatternRepository>();
        var prima = (await archivio.GetByIdAsync(pattern.Id))!.ModifiedAt;

        await moderatore.PostAsync($"/api/moderation/{pattern.Id}/approve", null);

        Assert.Equal(prima, (await archivio.GetByIdAsync(pattern.Id))!.ModifiedAt);
    }

    // ------------------------------------------------------- quello che l'approvazione vale ---

    [Fact]
    public async Task Saving_over_an_approved_pattern_sends_it_back_to_the_queue()
    {
        // È la falla che il sistema esisterebbe per niente se lasciasse aperta: far approvare
        // un disegno innocuo e poi salvarci sopra quello che si voleva pubblicare davvero.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var moderatore = await factory.ClientAmministratoreAsync();
        using var chiunque = factory.CreateClient();

        var pattern = Campione("Innocuo");
        await mario.PostAsync("/api/patterns", ComeJson(factory, pattern));
        await ChiediAsync(mario, pattern.Id, "Pubblica");
        await moderatore.PostAsync($"/api/moderation/{pattern.Id}/approve", null);

        pattern.Name = "Tutt'altro";
        pattern.Visibility = PatternVisibility.Pubblica;
        Assert.Equal(HttpStatusCode.OK,
            (await mario.PutAsync($"/api/patterns/{pattern.Id}", ComeJson(factory, pattern))).StatusCode);

        Assert.Equal(PatternVisibility.InAttesa, await StatoSuDiscoAsync(factory, pattern.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await chiunque.GetAsync($"/api/patterns/{pattern.Id}")).StatusCode);

        // L'autore continua a vederlo: non gli è stato tolto, è uscito dalla vetrina.
        Assert.Equal(HttpStatusCode.OK, (await mario.GetAsync($"/api/patterns/{pattern.Id}")).StatusCode);

        // E torna in coda da sé: senza questo, un pattern modificato sparirebbe dal portale
        // senza che nessuno si trovi davanti la richiesta di rimetterlo.
        var coda = await moderatore.GetFromJsonAsync<List<JsonElement>>("/api/moderation/pending");
        Assert.Equal(pattern.Id, Assert.Single(coda!).GetProperty("id").GetGuid());

        // Approvata la seconda volta, si rivede.
        await moderatore.PostAsync($"/api/moderation/{pattern.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, (await chiunque.GetAsync($"/api/patterns/{pattern.Id}")).StatusCode);
    }

    [Fact]
    public async Task The_author_keeps_seeing_his_own_in_any_state()
    {
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");

        var privato = Campione("Privato");
        var chiesto = Campione("Chiesto");
        await mario.PostAsync("/api/patterns", ComeJson(factory, privato));
        await mario.PostAsync("/api/patterns", ComeJson(factory, chiesto));
        await ChiediAsync(mario, chiesto.Id, "Pubblica");

        var elenco = await mario.GetFromJsonAsync<List<JsonElement>>("/api/patterns");

        Assert.Equal(2, elenco!.Count);
    }

    [Fact]
    public async Task A_moderator_sees_what_is_submitted_and_not_what_is_private()
    {
        // Moderare significa giudicare quello che qualcuno ha chiesto di mostrare, non
        // guardare nei cassetti: il permesso più alto del sistema non è un passe-partout.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var moderatore = await factory.ClientAmministratoreAsync();

        var privato = Campione("Mio e basta");
        var chiesto = Campione("Proposto");
        await mario.PostAsync("/api/patterns", ComeJson(factory, privato));
        await mario.PostAsync("/api/patterns", ComeJson(factory, chiesto));
        await ChiediAsync(mario, chiesto.Id, "Pubblica");

        Assert.Equal(HttpStatusCode.NotFound, (await moderatore.GetAsync($"/api/patterns/{privato.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await moderatore.GetAsync($"/api/patterns/{chiesto.Id}")).StatusCode);
    }

    [Fact]
    public async Task Being_an_administrator_is_something_the_client_is_told()
    {
        // Serve a mostrare la voce di menu: chi non modera non deve vedersi offrire una
        // pagina che gli verrebbe poi negata.
        using var factory = new PatternApiFactory();
        using var mario = await factory.ClientAutenticatoAsync("mario");
        using var moderatore = await factory.ClientAmministratoreAsync();

        var normale = await mario.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var amministratore = await moderatore.GetFromJsonAsync<JsonElement>("/api/auth/me");

        Assert.False(normale.GetProperty("isAdmin").GetBoolean());
        Assert.True(amministratore.GetProperty("isAdmin").GetBoolean());
    }
}
