using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PatternEditor.Sample.Api.Tests;

/// <summary>
/// Registrazione, accesso, recupero e profilo, passando davvero dall'HTTP.
///
/// <para>
/// Quello che si verifica qui non è soltanto «funziona»: è soprattutto che <b>non</b>
/// funzioni quello che non deve. Un accesso che accetta una password sbagliata si nota
/// subito; uno che dice quali nomi utente esistono, o che lascia valido un gettone dopo il
/// cambio di password, non si nota mai — finché non è troppo tardi.
/// </para>
/// </summary>
public class AuthEndpointsTests
{
    private const string PasswordBuona = "una frase lunga che va bene";

    private static object Registrazione(string nome, string? password = null) => new
    {
        username = nome,
        password = password ?? PasswordBuona,
        securityQuestion = "La via in cui sono cresciuto",
        securityAnswer = "Via Garibaldi",
    };

    private static async Task<string> MessaggioAsync(HttpResponseMessage risposta)
    {
        var documento = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    // ------------------------------------------------------------------ registrazione ---

    [Fact]
    public async Task Registering_opens_a_session_straight_away()
    {
        // Chi si registra è appena riuscito a dimostrare chi è: chiedergli di rifarlo
        // subito dopo sarebbe una formalità senza contropartita.
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        var risposta = await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));

        Assert.Equal(HttpStatusCode.Created, risposta.StatusCode);

        var documento = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(documento.RootElement.GetProperty("token").GetString()));
        Assert.Equal("mario", documento.RootElement.GetProperty("user").GetProperty("username").GetString());
    }

    [Fact]
    public async Task The_answer_never_carries_a_hash_or_a_recovery_question()
    {
        // L'account che esce verso il client è un tipo diverso da quello conservato, e la
        // ragione è esattamente questa: rendere impossibile spedire per sbaglio un segreto.
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        var risposta = await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));
        var testo = await risposta.Content.ReadAsStringAsync();

        Assert.DoesNotContain("pbkdf2", testo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Garibaldi", testo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityQuestion", testo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PasswordBuona, testo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_same_name_cannot_be_taken_twice_not_even_with_other_capitals()
    {
        // «Mario» e «mario» che convivono come due account distinti sarebbero un invito a
        // spacciarsi per qualcun altro senza nemmeno dover indovinare una password.
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));
        var secondo = await client.PostAsJsonAsync("/api/auth/register", Registrazione("MARIO"));

        Assert.Equal(HttpStatusCode.Conflict, secondo.StatusCode);
    }

    [Fact]
    public async Task A_name_that_only_looks_like_a_free_one_is_refused()
    {
        // Il caso che conta davvero: «collaudo» con la «o» cirillica passa il controllo di
        // unicità — perché è un nome diverso — e creerebbe un secondo account identico a
        // vederlo. A fermarlo è l'alfabeto ammesso, prima che l'unicità entri in gioco.
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/auth/register", Registrazione("collaudo"))).StatusCode);

        var gemello = await client.PostAsJsonAsync("/api/auth/register", Registrazione("collaudо"));

        Assert.Equal(HttpStatusCode.BadRequest, gemello.StatusCode);
    }

    [Fact]
    public async Task The_name_is_taken_whatever_spacing_was_typed_around_it()
    {
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));

        foreach (var variante in new[] { "MARIO", "  Mario  ", "mArIo" })
        {
            var risposta = await client.PostAsJsonAsync("/api/auth/register", Registrazione(variante));
            Assert.Equal(HttpStatusCode.Conflict, risposta.StatusCode);
        }
    }

    [Fact]
    public async Task A_weak_password_does_not_create_an_account()
    {
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        var risposta = await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario", "corta"));

        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
        Assert.Contains("12", await MessaggioAsync(risposta));

        // E il nome resta libero: un tentativo rifiutato non deve consumare nulla.
        var secondo = await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));
        Assert.Equal(HttpStatusCode.Created, secondo.StatusCode);
    }

    [Fact]
    public async Task A_registration_without_a_recovery_question_is_refused()
    {
        // Senza domanda non c'è modo di rientrare, e un account da cui si resta chiusi
        // fuori per sempre è peggio di un account che non si è creato.
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        var risposta = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "mario",
            password = PasswordBuona,
            securityQuestion = "",
            securityAnswer = "Via Garibaldi",
        });

        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
    }

    // ------------------------------------------------------------------------ accesso ---

    [Fact]
    public async Task A_registered_user_can_come_back_in()
    {
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));

        var risposta = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "mario",
            password = PasswordBuona,
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
    }

    [Fact]
    public async Task A_wrong_password_and_a_name_that_does_not_exist_say_exactly_the_same_thing()
    {
        // Se le due risposte differissero, chiunque potrebbe compilare l'elenco degli
        // iscritti senza indovinare nemmeno una password.
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));

        var sbagliata = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "mario",
            password = "un'altra frase lunga",
        });

        var inesistente = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "nessuno",
            password = "un'altra frase lunga",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, sbagliata.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, inesistente.StatusCode);
        Assert.Equal(await MessaggioAsync(sbagliata), await MessaggioAsync(inesistente));
    }

    [Fact]
    public async Task The_name_is_recognised_whatever_the_capitals()
    {
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", Registrazione("Mario"));

        var risposta = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "  mArIo ",
            password = PasswordBuona,
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
    }

    [Fact]
    public async Task Too_many_wrong_attempts_lock_the_account_for_a_while()
    {
        // Il limite di frequenza difende il server da chi prova molto; questo difende il
        // singolo utente da chi prova con calma soltanto su di lui.
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));

        HttpResponseMessage? ultima = null;
        for (var i = 0; i < 9; i++)
        {
            ultima = await client.PostAsJsonAsync("/api/auth/login", new
            {
                username = "mario",
                password = $"tentativo numero {i} sbagliato",
            });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, ultima!.StatusCode);

        // E adesso nemmeno la password giusta passa: è il punto del blocco.
        var conLaGiusta = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "mario",
            password = PasswordBuona,
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, conLaGiusta.StatusCode);
    }

    // --------------------------------------------------------------------- chi sono io ---

    [Fact]
    public async Task Without_a_token_nobody_is_anybody()
    {
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        var risposta = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
    }

    [Fact]
    public async Task Logging_out_makes_the_token_useless_right_away()
    {
        // È il vantaggio del gettone opaco sul JWT: un «esci» che esce davvero, invece di
        // limitarsi a dimenticare dal lato del browser qualcosa che resta valido.
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync("mario");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_token_somebody_made_up_is_not_a_session()
    {
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "inventato");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    // ------------------------------------------------------------------------ recupero ---

    [Fact]
    public async Task The_recovery_question_comes_back_as_the_user_wrote_it()
    {
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));

        var risposta = await client.PostAsJsonAsync("/api/auth/recovery/question", new { username = "mario" });
        var documento = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());

        Assert.Equal("La via in cui sono cresciuto", documento.RootElement.GetProperty("question").GetString());
    }

    [Fact]
    public async Task The_right_answer_sets_a_new_password_and_lets_the_user_in()
    {
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));

        // Maiuscole e spazi diversi: a distanza di mesi si riscrive così, e deve bastare.
        var risposta = await client.PostAsJsonAsync("/api/auth/recovery/reset", new
        {
            username = "mario",
            answer = "  via   GARIBALDI ",
            newPassword = "un'altra frase lunga",
        });

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);

        var nuovoAccesso = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "mario",
            password = "un'altra frase lunga",
        });

        Assert.Equal(HttpStatusCode.OK, nuovoAccesso.StatusCode);
    }

    [Fact]
    public async Task The_old_password_stops_working_after_a_recovery()
    {
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));
        await client.PostAsJsonAsync("/api/auth/recovery/reset", new
        {
            username = "mario",
            answer = "Via Garibaldi",
            newPassword = "un'altra frase lunga",
        });

        var conLaVecchia = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "mario",
            password = PasswordBuona,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, conLaVecchia.StatusCode);
    }

    [Fact]
    public async Task A_recovery_throws_out_whoever_was_already_inside()
    {
        // Se il recupero è servito perché qualcun altro era entrato, lasciargli la sessione
        // aperta renderebbe il cambio di password una formalità.
        using var factory = new PatternApiFactory();
        using var intruso = await factory.ClientAutenticatoAsync("mario");
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await intruso.GetAsync("/api/auth/me")).StatusCode);

        await client.PostAsJsonAsync("/api/auth/recovery/reset", new
        {
            username = "mario",
            answer = "Via Garibaldi",
            newPassword = "un'altra frase lunga",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await intruso.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_wrong_answer_changes_nothing()
    {
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));

        var risposta = await client.PostAsJsonAsync("/api/auth/recovery/reset", new
        {
            username = "mario",
            answer = "Via Mazzini",
            newPassword = "un'altra frase lunga",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);

        var conLaVecchia = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "mario",
            password = PasswordBuona,
        });

        Assert.Equal(HttpStatusCode.OK, conLaVecchia.StatusCode);
    }

    [Fact]
    public async Task A_weak_new_password_is_refused_before_the_answer_is_even_looked_at()
    {
        // Altrimenti chi risponde bene e sceglie una password corta si sentirebbe dire
        // «risposta accettata, ma ricomincia», e il secondo giro consumerebbe un tentativo
        // per un errore che con la risposta non c'entrava niente.
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));

        var risposta = await client.PostAsJsonAsync("/api/auth/recovery/reset", new
        {
            username = "mario",
            answer = "Via Garibaldi",
            newPassword = "corta",
        });

        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
    }

    // ------------------------------------------------------------- cambio della password ---

    [Fact]
    public async Task Changing_the_password_needs_the_current_one()
    {
        // Protegge dal computer lasciato aperto, che è il caso in cui serve davvero.
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync("mario");

        var risposta = await client.PutAsJsonAsync("/api/auth/password", new
        {
            currentPassword = "non la so",
            newPassword = "un'altra frase lunga",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_closes_the_other_sessions_but_not_this_one()
    {
        using var factory = new PatternApiFactory();
        using var primo = await factory.ClientAutenticatoAsync("mario");

        // Una seconda sessione dello stesso utente, come un altro dispositivo.
        using var secondo = factory.CreateClient();
        var risposta = await secondo.PostAsJsonAsync("/api/auth/login", new
        {
            username = "mario",
            password = "una frase lunga che va bene",
        });
        var documento = JsonDocument.Parse(await risposta.Content.ReadAsStringAsync());
        secondo.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", documento.RootElement.GetProperty("token").GetString());

        var cambio = await secondo.PutAsJsonAsync("/api/auth/password", new
        {
            currentPassword = "una frase lunga che va bene",
            newPassword = "un'altra frase lunga",
        });

        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);

        // Il gettone nuovo arriva nella risposta: chi ha cambiato la password resta dentro.
        var nuovo = JsonDocument.Parse(await cambio.Content.ReadAsStringAsync());
        secondo.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", nuovo.RootElement.GetProperty("token").GetString());

        Assert.Equal(HttpStatusCode.OK, (await secondo.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await primo.GetAsync("/api/auth/me")).StatusCode);
    }

    // ------------------------------------------------------------------------- profilo ---

    [Fact]
    public async Task The_colour_of_the_initials_can_be_changed_but_only_to_a_colour()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync("mario");

        var buono = await client.PutAsJsonAsync("/api/auth/profile", new { avatarColor = "#2E9E63" });
        Assert.Equal(HttpStatusCode.OK, buono.StatusCode);

        var documento = JsonDocument.Parse(await buono.Content.ReadAsStringAsync());
        Assert.Equal("#2e9e63", documento.RootElement.GetProperty("avatarColor").GetString());

        // Un valore libero finirebbe dentro un attributo di stile della pagina.
        var cattivo = await client.PutAsJsonAsync("/api/auth/profile", new { avatarColor = "rosso; background:url(x)" });
        Assert.Equal(HttpStatusCode.BadRequest, cattivo.StatusCode);
    }

    [Fact]
    public async Task Two_users_without_a_chosen_colour_do_not_get_the_same_one()
    {
        // Il colore nasce dal nome: è sempre lo stesso per lo stesso utente, e diverso fra
        // utenti diversi. Un colore casuale renderebbe l'avatar irriconoscibile ogni volta.
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        var mario = await client.PostAsJsonAsync("/api/auth/register", Registrazione("mario"));
        var lucia = await client.PostAsJsonAsync("/api/auth/register", Registrazione("lucia"));

        var unColore = JsonDocument.Parse(await mario.Content.ReadAsStringAsync())
            .RootElement.GetProperty("user").GetProperty("avatarColor").GetString();
        var altroColore = JsonDocument.Parse(await lucia.Content.ReadAsStringAsync())
            .RootElement.GetProperty("user").GetProperty("avatarColor").GetString();

        Assert.NotEqual(unColore, altroColore);
    }

    [Fact]
    public async Task A_photo_that_is_not_an_image_is_refused_whatever_it_claims_to_be()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync("mario");

        var contenuto = new ByteArrayContent(Encoding.UTF8.GetBytes("<script>rubaTutto()</script>"));
        contenuto.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

        var risposta = await client.PutAsync("/api/auth/photo", contenuto);

        Assert.Equal(HttpStatusCode.BadRequest, risposta.StatusCode);
    }

    [Fact]
    public async Task A_real_image_is_stored_and_served_with_the_type_it_really_has()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync("mario");

        var me = JsonDocument.Parse(await (await client.GetAsync("/api/auth/me")).Content.ReadAsStringAsync());
        var id = me.RootElement.GetProperty("id").GetGuid();

        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];
        var contenuto = new ByteArrayContent(png);

        // Dichiarato male apposta: a decidere sono i byte, non chi manda il file.
        contenuto.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync("/api/auth/photo", contenuto)).StatusCode);

        var foto = await client.GetAsync($"/api/users/{id}/photo");

        Assert.Equal(HttpStatusCode.OK, foto.StatusCode);
        Assert.Equal("image/png", foto.Content.Headers.ContentType?.MediaType);

        // nosniff: senza, un browser potrebbe decidere da sé che quel file è qualcos'altro.
        Assert.Contains("nosniff", foto.Headers.GetValues("X-Content-Type-Options"));
    }

    [Fact]
    public async Task Changing_the_photo_changes_its_address()
    {
        // È il difetto che si nasconde meglio: la foto nuova arriva sul server, ma chi
        // guarda continua a vedere la vecchia perché il browser ha in cache quell'indirizzo.
        // La revisione fa parte dell'indirizzo, quindi una foto nuova è una risorsa nuova.
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync("mario");

        var iniziale = await LeggiAccountAsync(client);
        Assert.False(iniziale.GetProperty("hasPhoto").GetBoolean());
        var revisioneIniziale = iniziale.GetProperty("photoVersion").GetInt32();

        await CaricaPngAsync(client);
        var conFoto = await LeggiAccountAsync(client);

        Assert.True(conFoto.GetProperty("hasPhoto").GetBoolean());
        Assert.True(conFoto.GetProperty("photoVersion").GetInt32() > revisioneIniziale);

        // Una seconda foto avanza ancora: due immagini diverse non possono condividere
        // l'indirizzo.
        await CaricaPngAsync(client);
        var seconda = await LeggiAccountAsync(client);

        Assert.True(seconda.GetProperty("photoVersion").GetInt32()
                    > conFoto.GetProperty("photoVersion").GetInt32());
    }

    [Fact]
    public async Task Removing_the_photo_changes_the_address_too()
    {
        // Togliere la foto è un cambio quanto sostituirla: senza avanzare la revisione, chi
        // l'aveva in cache continuerebbe a vederla al posto delle iniziali.
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync("mario");

        await CaricaPngAsync(client);
        var conFoto = await LeggiAccountAsync(client);

        await client.DeleteAsync("/api/auth/photo");
        var senzaFoto = await LeggiAccountAsync(client);

        Assert.False(senzaFoto.GetProperty("hasPhoto").GetBoolean());
        Assert.True(senzaFoto.GetProperty("photoVersion").GetInt32()
                    > conFoto.GetProperty("photoVersion").GetInt32());
    }

    [Fact]
    public async Task The_list_of_patterns_carries_the_photo_revision_of_each_author()
    {
        // Serve alle miniature: senza la revisione, la scheda di un pattern mostrerebbe la
        // foto che l'autore aveva quando l'elenco è stato scaricato.
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync("mario");

        var pattern = new PatternEditor.Core.Models.Pattern { Name = "Prova" };
        pattern.Definition.Width = 50;
        pattern.Definition.Height = 50;
        var serializer = factory.Services.GetRequiredService<PatternEditor.Abstractions.Serialization.IPatternSerializer>();
        await client.PostAsync("/api/patterns",
            new StringContent(serializer.Serialize(pattern), Encoding.UTF8, "application/json"));

        await CaricaPngAsync(client);

        var elenco = await client.GetFromJsonAsync<List<JsonElement>>("/api/patterns");
        var riga = Assert.Single(elenco!);

        Assert.True(riga.GetProperty("ownerHasPhoto").GetBoolean());
        Assert.True(riga.GetProperty("ownerPhotoVersion").GetInt32() > 0);
    }

    private static async Task<JsonElement> LeggiAccountAsync(HttpClient client) =>
        JsonDocument.Parse(await (await client.GetAsync("/api/auth/me")).Content.ReadAsStringAsync()).RootElement;

    private static async Task CaricaPngAsync(HttpClient client)
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];
        var contenuto = new ByteArrayContent(png);
        contenuto.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

        (await client.PutAsync("/api/auth/photo", contenuto)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Without_a_photo_there_is_nothing_to_serve()
    {
        using var factory = new PatternApiFactory();
        using var client = await factory.ClientAutenticatoAsync("mario");

        var me = JsonDocument.Parse(await (await client.GetAsync("/api/auth/me")).Content.ReadAsStringAsync());
        var id = me.RootElement.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/users/{id}/photo")).StatusCode);
    }

    [Fact]
    public async Task The_profile_belongs_to_whoever_is_holding_the_token()
    {
        using var factory = new PatternApiFactory();
        using var client = factory.CreateClient();

        var risposta = await client.PutAsJsonAsync("/api/auth/profile", new { avatarColor = "#2e9e63" });

        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
    }
}
