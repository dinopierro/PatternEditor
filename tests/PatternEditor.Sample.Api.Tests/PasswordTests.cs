using PatternEditor.Sample.Api.Auth;
using Xunit;

namespace PatternEditor.Sample.Api.Tests;

/// <summary>
/// Come si conserva una password senza conservarla.
///
/// <para>
/// Sono test su poche righe di codice, e sono fra i più importanti della soluzione: qui un
/// difetto non si manifesta come un guasto: si manifesta come un archivio che sembra
/// funzionare e che, il giorno in cui finisce nelle mani sbagliate, consegna le credenziali
/// di tutti.
/// </para>
/// </summary>
public class PasswordHasherTests
{
    // Un costo basso: questi test verificano la correttezza, non la lentezza. Che il costo
    // vero sia alto lo dice il valore predefinito, verificato a parte.
    private static PasswordHasher Hasher(int iterazioni = 1000) => new(iterazioni);

    [Fact]
    public void The_stored_form_never_contains_the_password()
    {
        // La verifica più ovvia e quella che non si può dare per scontata: se la password
        // comparisse nell'hash, tutto il resto sarebbe teatro.
        var hash = Hasher().Hash("una frase lunga che va bene");

        Assert.DoesNotContain("una frase", hash, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("frase lunga", hash, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_same_password_hashed_twice_gives_two_different_strings()
    {
        // È il sale a farlo. Senza, guardando l'archivio si vedrebbe a colpo d'occhio quali
        // utenti hanno scelto la stessa password — e indovinarne una le aprirebbe tutte.
        var hasher = Hasher();

        var primo = hasher.Hash("una frase lunga che va bene");
        var secondo = hasher.Hash("una frase lunga che va bene");

        Assert.NotEqual(primo, secondo);
        Assert.True(hasher.Verifica("una frase lunga che va bene", primo));
        Assert.True(hasher.Verifica("una frase lunga che va bene", secondo));
    }

    [Fact]
    public void The_right_password_verifies_and_the_wrong_one_does_not()
    {
        var hasher = Hasher();
        var hash = hasher.Hash("una frase lunga che va bene");

        Assert.True(hasher.Verifica("una frase lunga che va bene", hash));
        Assert.False(hasher.Verifica("una frase lunga che va Bene", hash));
        Assert.False(hasher.Verifica("una frase lunga che va bene ", hash));
        Assert.False(hasher.Verifica(string.Empty, hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("non e' un hash")]
    [InlineData("pbkdf2-sha256$1000$soloTrePezzi")]
    [InlineData("pbkdf2-sha256$zero$c2FsZQ==$aGFzaA==")]
    [InlineData("bcrypt$1000$c2FsZQ==$aGFzaA==")]
    [InlineData("pbkdf2-sha256$1000$non-base64!$aGFzaA==")]
    public void A_hash_that_cannot_be_read_is_a_no_and_not_a_crash(string? conservato)
    {
        // Un dato corrotto non è un guasto del programma, ed è la differenza fra un accesso
        // negato e un errore 500 che rivela la struttura interna a chi lo sta provocando.
        Assert.False(Hasher().Verifica("una frase lunga che va bene", conservato));
    }

    [Fact]
    public void An_old_hash_still_verifies_but_asks_to_be_rewritten()
    {
        // È il motivo per cui il numero di iterazioni sta scritto dentro l'hash: alzare il
        // costo non deve invalidare le credenziali di chi si era registrato prima.
        var vecchio = Hasher(1000).Hash("una frase lunga che va bene");
        var oggi = Hasher(5000);

        Assert.True(oggi.Verifica("una frase lunga che va bene", vecchio));
        Assert.True(oggi.NeedsUpgrade(vecchio));

        var riscritto = oggi.Hash("una frase lunga che va bene");
        Assert.False(oggi.NeedsUpgrade(riscritto));
    }

    [Fact]
    public void The_default_cost_is_the_one_the_guidelines_ask_for()
    {
        // Il valore non è un dettaglio di gusto: è la difesa. Abbassarlo per fare prima
        // renderebbe l'intero meccanismo un ornamento, e lo si farebbe senza accorgersene.
        Assert.Equal(600_000, PasswordHasher.IterazioniPredefinite);
    }

    [Fact]
    public void A_cost_too_low_to_protect_anything_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PasswordHasher(10));
    }
}

/// <summary>Che cosa si accetta come password, come nome utente e come risposta.</summary>
public class PasswordPolicyTests
{
    [Theory]
    [InlineData("corta")]
    [InlineData("undici.cara")]
    public void A_password_shorter_than_twelve_characters_is_refused(string password)
    {
        Assert.NotNull(PasswordPolicy.Controlla(password));
    }

    [Fact]
    public void Twelve_characters_are_enough_with_no_strange_symbols()
    {
        // È la regola moderna e sorprende ancora: conta la lunghezza, non la composizione.
        // «Password1!» soddisfa tutte le vecchie regole ed è fra le prime che si provano.
        Assert.Null(PasswordPolicy.Controlla("cavallo blu marino"));
        Assert.Null(PasswordPolicy.Controlla("dodicilettere"));
    }

    [Fact]
    public void A_password_among_the_most_used_in_the_world_is_refused_however_long()
    {
        Assert.NotNull(PasswordPolicy.Controlla("password1234"));
        Assert.NotNull(PasswordPolicy.Controlla("amministratore"));
    }

    [Fact]
    public void A_password_that_contains_the_username_is_refused()
    {
        // Chi conosce il nome utente avrebbe già mezza password, e il nome utente non è un
        // segreto: compare accanto a ogni pattern.
        Assert.NotNull(PasswordPolicy.Controlla("mariorossi e poi altro", "mariorossi"));
        Assert.Null(PasswordPolicy.Controlla("cavallo blu marino", "mariorossi"));
    }

    [Fact]
    public void A_password_that_starts_or_ends_with_a_space_is_refused()
    {
        // Uno spazio ai bordi non si vede, e alla seconda volta non si riscrive uguale:
        // sarebbe una password che smette di funzionare senza un motivo visibile.
        Assert.NotNull(PasswordPolicy.Controlla(" cavallo blu marino"));
        Assert.NotNull(PasswordPolicy.Controlla("cavallo blu marino "));
    }

    [Fact]
    public void Length_alone_does_not_save_a_repeated_character()
    {
        Assert.NotNull(PasswordPolicy.Controlla("aaaaaaaaaaaaaaaa"));
        Assert.NotNull(PasswordPolicy.Controlla("ababababababab"));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("con spazio")]
    [InlineData("con@chiocciola")]
    [InlineData(".puntoIniziale")]
    [InlineData("")]
    public void A_username_outside_the_allowed_shape_is_refused(string nome)
    {
        Assert.NotNull(PasswordPolicy.ControllaNomeUtente(nome));
    }

    [Theory]
    [InlineData("mario")]
    [InlineData("mario.rossi")]
    [InlineData("mario_rossi-2")]
    [InlineData("M4rio")]
    public void A_username_in_the_allowed_shape_is_accepted(string nome)
    {
        Assert.Null(PasswordPolicy.ControllaNomeUtente(nome));
    }

    [Fact]
    public void Two_names_that_look_identical_cannot_both_exist()
    {
        // «а» cirillica e «a» latina sono due caratteri diversi che sullo schermo sono lo
        // stesso segno. Con un alfabeto aperto si registrerebbero due account
        // indistinguibili a vista, e uno dei due firmerebbe pattern spacciandosi per
        // l'altro senza indovinare nemmeno una password.
        //
        // A chiudere la porta è la regola sull'alfabeto e non il confronto fra i nomi: il
        // confronto fa quello che deve, cioè distinguere caratteri diversi.
        var latino = "collaudo";
        var conCirillica = "collaudо";

        Assert.NotEqual(latino, conCirillica);
        Assert.Null(PasswordPolicy.ControllaNomeUtente(latino));
        Assert.NotNull(PasswordPolicy.ControllaNomeUtente(conCirillica));
    }

    [Theory]
    [InlineData("mariò")]
    [InlineData("mаrio")]
    [InlineData("μάριο")]
    [InlineData("马里奥")]
    [InlineData("mario​rossi")]
    public void Letters_from_other_alphabets_are_refused(string nome)
    {
        // Compreso lo spazio a larghezza zero dell'ultimo caso, che non si vede affatto.
        Assert.NotNull(PasswordPolicy.ControllaNomeUtente(nome));
    }

    [Fact]
    public void The_recovery_answer_is_compared_without_case_and_without_extra_spaces()
    {
        // A distanza di mesi nessuno riscrive «Via Garibaldi» esattamente come l'aveva
        // scritta. Far fallire il recupero per una maiuscola significherebbe non averlo fatto.
        Assert.Equal("via garibaldi", PasswordPolicy.Normalizza("Via Garibaldi"));
        Assert.Equal("via garibaldi", PasswordPolicy.Normalizza("  via   GARIBALDI  "));
        Assert.Equal("via garibaldi", PasswordPolicy.Normalizza("Via\tGaribaldi"));
    }

    [Fact]
    public void An_answer_too_short_to_be_a_secret_is_refused()
    {
        // È l'unica strada per rientrare: una parola di tre lettere la si indovina.
        Assert.NotNull(PasswordPolicy.ControllaRisposta("blu"));
        Assert.NotNull(PasswordPolicy.ControllaRisposta("   "));
        Assert.Null(PasswordPolicy.ControllaRisposta("Via Garibaldi"));
    }

    [Fact]
    public void A_recovery_question_must_say_something()
    {
        Assert.NotNull(PasswordPolicy.ControllaDomanda("?"));
        Assert.NotNull(PasswordPolicy.ControllaDomanda(null));
        Assert.Null(PasswordPolicy.ControllaDomanda("La via in cui sono cresciuto"));
    }
}

/// <summary>Le sessioni aperte: aprire, risolvere, revocare.</summary>
public class SessionStoreTests
{
    [Fact]
    public void A_token_that_was_just_opened_resolves_to_its_user()
    {
        var sessioni = new SessionStore();
        var utente = Guid.CreateVersion7();

        var gettone = sessioni.Apri(utente);

        Assert.Equal(utente, sessioni.Risolvi(gettone));
    }

    [Fact]
    public void Two_sessions_never_get_the_same_token()
    {
        var sessioni = new SessionStore();

        var primo = sessioni.Apri(Guid.CreateVersion7());
        var secondo = sessioni.Apri(Guid.CreateVersion7());

        Assert.NotEqual(primo, secondo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("un-gettone-inventato")]
    public void A_token_nobody_issued_resolves_to_nobody(string? gettone)
    {
        var sessioni = new SessionStore();
        sessioni.Apri(Guid.CreateVersion7());

        Assert.Null(sessioni.Risolvi(gettone));
    }

    [Fact]
    public void Closing_a_session_makes_its_token_useless()
    {
        var sessioni = new SessionStore();
        var gettone = sessioni.Apri(Guid.CreateVersion7());

        sessioni.Chiudi(gettone);

        Assert.Null(sessioni.Risolvi(gettone));

        // Chiuderla due volte non è un errore: chi esce due volte è uscito.
        sessioni.Chiudi(gettone);
    }

    [Fact]
    public void Closing_every_session_of_one_user_leaves_the_others_alone()
    {
        // È quello che deve succedere a un cambio di password: chi conosceva la vecchia
        // viene buttato fuori, e chi non c'entra niente non se ne accorge.
        var sessioni = new SessionStore();
        var mario = Guid.CreateVersion7();
        var lucia = Guid.CreateVersion7();

        var telefono = sessioni.Apri(mario);
        var portatile = sessioni.Apri(mario);
        var altra = sessioni.Apri(lucia);

        sessioni.ChiudiTutteDi(mario);

        Assert.Null(sessioni.Risolvi(telefono));
        Assert.Null(sessioni.Risolvi(portatile));
        Assert.Equal(lucia, sessioni.Risolvi(altra));
    }

    [Fact]
    public void An_expired_session_stops_working_and_stops_taking_up_room()
    {
        var adesso = new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
        var sessioni = new SessionStore(orologio: () => adesso);

        var gettone = sessioni.Apri(Guid.CreateVersion7());
        Assert.Equal(1, sessioni.Aperte);

        adesso += SessionStore.Durata + TimeSpan.FromMinutes(1);

        Assert.Null(sessioni.Risolvi(gettone));
        Assert.Equal(0, sessioni.Aperte);
    }

    [Fact]
    public void The_file_on_disk_does_not_contain_the_token()
    {
        // Sul server si conserva l'impronta, non il gettone: chi leggesse il file delle
        // sessioni non potrebbe impersonare nessuno. È lo stesso ragionamento delle password.
        var percorso = Path.Combine(Path.GetTempPath(), $"sessioni-{Guid.NewGuid()}.json");

        try
        {
            var sessioni = new SessionStore(percorso);
            var gettone = sessioni.Apri(Guid.CreateVersion7());

            var contenuto = File.ReadAllText(percorso);

            Assert.DoesNotContain(gettone, contenuto, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(percorso))
            {
                File.Delete(percorso);
            }
        }
    }

    [Fact]
    public void A_restart_does_not_throw_everyone_out()
    {
        // Le sessioni sopravvivono al riavvio dell'API. In sviluppo l'API riparte a ogni
        // modifica, e dover rifare l'accesso ogni volta renderebbe l'accesso stesso il
        // primo ostacolo al lavoro.
        var percorso = Path.Combine(Path.GetTempPath(), $"sessioni-{Guid.NewGuid()}.json");

        try
        {
            var utente = Guid.CreateVersion7();
            var gettone = new SessionStore(percorso).Apri(utente);

            var dopoIlRiavvio = new SessionStore(percorso);

            Assert.Equal(utente, dopoIlRiavvio.Risolvi(gettone));
        }
        finally
        {
            if (File.Exists(percorso))
            {
                File.Delete(percorso);
            }
        }
    }
}

/// <summary>Il riconoscimento delle immagini caricate come foto del profilo.</summary>
public class ImmagineCaricataTests
{
    private static byte[] Png() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
    ];

    [Fact]
    public void A_png_is_recognised_by_its_first_bytes()
    {
        Assert.Equal("image/png", ImmagineCaricata.Riconosci(Png()));
    }

    [Fact]
    public void A_jpeg_and_a_webp_are_recognised_too()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0];
        byte[] webp = [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50];

        Assert.Equal("image/jpeg", ImmagineCaricata.Riconosci(jpeg));
        Assert.Equal("image/webp", ImmagineCaricata.Riconosci(webp));
    }

    [Fact]
    public void An_svg_is_refused_even_though_this_project_is_made_of_svg()
    {
        // Un SVG è un documento che può contenere script: è l'unico formato d'immagine per
        // cui «mostra questo file» significa anche «esegui questo codice». Servito dal
        // nostro dominio, avrebbe accanto i gettoni dei nostri utenti.
        var svg = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

        Assert.Null(ImmagineCaricata.Riconosci(svg));
    }

    [Fact]
    public void A_file_that_only_claims_to_be_an_image_is_refused()
    {
        var html = System.Text.Encoding.UTF8.GetBytes("<!doctype html><script>rubaTutto()</script>");

        Assert.Null(ImmagineCaricata.Riconosci(html));
    }

    [Fact]
    public void Something_too_short_to_say_what_it_is_is_refused()
    {
        Assert.Null(ImmagineCaricata.Riconosci([0x89, 0x50]));
        Assert.Null(ImmagineCaricata.Riconosci([]));
    }
}
