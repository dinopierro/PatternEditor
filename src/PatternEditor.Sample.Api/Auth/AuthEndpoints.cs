using System.Globalization;

namespace PatternEditor.Sample.Api.Auth;

/// <summary>
/// Gli endpoint di registrazione, accesso, recupero e profilo.
/// </summary>
///
/// <remarks>
/// <para>
/// Il sistema è dichiaratamente leggero: nessuna posta elettronica, nessun secondo fattore,
/// il rientro affidato a una domanda scelta dall'utente. Quello che <b>non</b> è leggero è
/// il modo in cui i dati vengono trattati — password e risposta non esistono in chiaro da
/// nessuna parte, i gettoni si revocano, i tentativi si contano — perché quella parte non
/// costa quasi niente farla bene e costa moltissimo rifarla dopo.
/// </para>
/// <para>
/// Un limite noto, da tenere presente: <b>i nomi utente si possono scoprire</b>. La
/// registrazione deve dire che un nome è già preso e il recupero deve mostrare la domanda
/// giusta, e nessuna delle due cose si può fare senza ammettere che quell'utente esiste. Lo
/// si attenua con il limite di frequenza, non lo si elimina. L'accesso invece non lo rivela:
/// nome sbagliato e password sbagliata danno la stessa identica risposta.
/// </para>
/// </remarks>
public static class AuthEndpoints
{
    /// <summary>
    /// Dopo quanti tentativi falliti l'account si blocca, e per quanto. Il limite di
    /// frequenza difende il server da chi prova molto; questo difende il singolo utente da
    /// chi prova con calma soltanto su di lui.
    /// </summary>
    private const int TentativiPrimaDelBlocco = 8;

    private static readonly TimeSpan DurataBlocco = TimeSpan.FromMinutes(15);

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app, string politicaFrequenza)
    {
        var gruppo = app.MapGroup("/api/auth").RequireRateLimiting(politicaFrequenza);

        // ---------------------------------------------------------------- registrazione ---
        gruppo.MapPost("/register", async (
            RegistrazioneRichiesta richiesta,
            IUserRepository utenti,
            PasswordHasher hasher,
            SessionStore sessioni,
            CancellationToken ct) =>
        {
            var nome = richiesta.Username?.Trim() ?? string.Empty;

            if (PasswordPolicy.ControllaNomeUtente(nome) is { } erroreNome)
            {
                return Errore(erroreNome);
            }

            if (PasswordPolicy.Controlla(richiesta.Password, nome) is { } errorePassword)
            {
                return Errore(errorePassword);
            }

            if (PasswordPolicy.ControllaDomanda(richiesta.SecurityQuestion) is { } erroreDomanda)
            {
                return Errore(erroreDomanda);
            }

            if (PasswordPolicy.ControllaRisposta(richiesta.SecurityAnswer) is { } erroreRisposta)
            {
                return Errore(erroreRisposta);
            }

            var account = new UserAccount
            {
                Id = Guid.CreateVersion7(),
                Username = nome,
                PasswordHash = hasher.Hash(richiesta.Password!),
                SecurityQuestion = richiesta.SecurityQuestion!.Trim(),

                // La risposta si normalizza PRIMA di calcolarne l'hash: chi rientra fra sei
                // mesi non riscriverà «Via Garibaldi» con le stesse maiuscole, e far
                // fallire il recupero per questo significherebbe non averlo previsto.
                SecurityAnswerHash = hasher.Hash(PasswordPolicy.Normalizza(richiesta.SecurityAnswer!)),
                AvatarColor = ColoreValido(richiesta.AvatarColor) ?? ColoreDa(nome),
                CreatedAt = DateTimeOffset.UtcNow,
            };

            if (!await utenti.CreaAsync(account, ct))
            {
                return Results.Conflict(new ErroreRisposta("Questo nome utente è già stato preso."));
            }

            var gettone = sessioni.Apri(account.Id);
            return Results.Created(
                $"/api/users/{account.Id}",
                new SessioneApertaRisposta(gettone, DateTimeOffset.UtcNow + SessionStore.Durata, account.Pubblico()));
        });

        // ---------------------------------------------------------------------- accesso ---
        gruppo.MapPost("/login", async (
            AccessoRichiesta richiesta,
            IUserRepository utenti,
            PasswordHasher hasher,
            SessionStore sessioni,
            CancellationToken ct) =>
        {
            var account = await utenti.TrovaPerNomeAsync(richiesta.Username ?? string.Empty, ct);

            if (account is not null && account.BloccatoFinoA > DateTimeOffset.UtcNow)
            {
                var minuti = Math.Max(1, (int)Math.Ceiling((account.BloccatoFinoA.Value - DateTimeOffset.UtcNow).TotalMinutes));
                return Results.Json(
                    new ErroreRisposta($"Troppi tentativi falliti. Riprova fra {minuti} minuti."),
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            // L'hash si verifica anche quando l'utente non esiste, contro un hash finto.
            // Senza, un nome inesistente risponderebbe in un millesimo di secondo e uno
            // esistente in mezzo secondo: la differenza è visibile, e basta per compilare
            // l'elenco degli iscritti senza indovinare nemmeno una password.
            var corretta = account is not null
                ? hasher.Verifica(richiesta.Password ?? string.Empty, account.PasswordHash)
                : BruciaIlTempo(hasher, richiesta.Password);

            if (account is null || !corretta)
            {
                if (account is not null)
                {
                    account.TentativiFalliti++;
                    if (account.TentativiFalliti >= TentativiPrimaDelBlocco)
                    {
                        account.BloccatoFinoA = DateTimeOffset.UtcNow + DurataBlocco;
                        account.TentativiFalliti = 0;
                    }

                    await utenti.AggiornaAsync(account, ct);
                }

                // Un messaggio solo per entrambi i casi: dire «questo utente non esiste»
                // regalerebbe l'elenco degli iscritti a chiunque lo chieda.
                return Results.Json(
                    new ErroreRisposta("Nome utente o password non corretti."),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            account.TentativiFalliti = 0;
            account.BloccatoFinoA = null;
            account.LastLoginAt = DateTimeOffset.UtcNow;

            // L'accesso riuscito è l'unico momento in cui la password è disponibile in
            // chiaro: se l'hash è stato scritto con un costo ormai superato, è adesso o mai più.
            if (hasher.NeedsUpgrade(account.PasswordHash))
            {
                account.PasswordHash = hasher.Hash(richiesta.Password!);
            }

            await utenti.AggiornaAsync(account, ct);

            var gettone = sessioni.Apri(account.Id);
            return Results.Ok(new SessioneApertaRisposta(
                gettone, DateTimeOffset.UtcNow + SessionStore.Durata, account.Pubblico()));
        });

        // ----------------------------------------------------------------------- uscita ---
        gruppo.MapPost("/logout", (HttpRequest richiesta, SessionStore sessioni) =>
        {
            sessioni.Chiudi(Gettone(richiesta));
            return Results.NoContent();
        });

        // ------------------------------------------------------------------- chi sono io ---
        gruppo.MapGet("/me", async (HttpRequest richiesta, SessionStore sessioni, IUserRepository utenti, CancellationToken ct) =>
        {
            if (sessioni.Risolvi(Gettone(richiesta)) is not { } id)
            {
                return Results.Unauthorized();
            }

            var account = await utenti.TrovaPerIdAsync(id, ct);
            return account is null ? Results.Unauthorized() : Results.Ok(account.Pubblico());
        });

        // --------------------------------------------------------- recupero: la domanda ---
        gruppo.MapPost("/recovery/question", async (DomandaRichiesta richiesta, IUserRepository utenti, CancellationToken ct) =>
        {
            var account = await utenti.TrovaPerNomeAsync(richiesta.Username ?? string.Empty, ct);

            return account is null
                ? Results.NotFound(new ErroreRisposta("Non risulta nessun utente con questo nome."))
                : Results.Ok(new DomandaRisposta(account.SecurityQuestion));
        });

        // --------------------------------------------------- recupero: la nuova password ---
        gruppo.MapPost("/recovery/reset", async (
            RecuperoRichiesta richiesta,
            IUserRepository utenti,
            PasswordHasher hasher,
            SessionStore sessioni,
            CancellationToken ct) =>
        {
            var account = await utenti.TrovaPerNomeAsync(richiesta.Username ?? string.Empty, ct);

            if (account is not null && account.BloccatoFinoA > DateTimeOffset.UtcNow)
            {
                return Results.Json(
                    new ErroreRisposta("Troppi tentativi falliti. Riprova fra qualche minuto."),
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            // La nuova password si controlla PRIMA di verificare la risposta: altrimenti chi
            // risponde bene e sceglie una password debole si sente dire «risposta accettata,
            // ma ricomincia», e alla seconda volta la risposta potrebbe non bastare più
            // perché nel frattempo i tentativi si sono consumati.
            if (PasswordPolicy.Controlla(richiesta.NewPassword, account?.Username) is { } errorePassword)
            {
                return Errore(errorePassword);
            }

            var giusta = account is not null
                && hasher.Verifica(PasswordPolicy.Normalizza(richiesta.Answer ?? string.Empty), account.SecurityAnswerHash);

            if (account is null || !giusta)
            {
                if (account is not null)
                {
                    account.TentativiFalliti++;
                    if (account.TentativiFalliti >= TentativiPrimaDelBlocco)
                    {
                        account.BloccatoFinoA = DateTimeOffset.UtcNow + DurataBlocco;
                        account.TentativiFalliti = 0;
                    }

                    await utenti.AggiornaAsync(account, ct);
                }

                return Results.Json(
                    new ErroreRisposta("La risposta non corrisponde."),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            account.PasswordHash = hasher.Hash(richiesta.NewPassword!);
            account.TentativiFalliti = 0;
            account.BloccatoFinoA = null;
            account.LastLoginAt = DateTimeOffset.UtcNow;
            await utenti.AggiornaAsync(account, ct);

            // Chi conosceva la vecchia password viene buttato fuori: se il recupero è
            // servito perché qualcun altro era entrato, lasciargli la sessione aperta
            // renderebbe il cambio una formalità.
            sessioni.ChiudiTutteDi(account.Id);

            var gettone = sessioni.Apri(account.Id);
            return Results.Ok(new SessioneApertaRisposta(
                gettone, DateTimeOffset.UtcNow + SessionStore.Durata, account.Pubblico()));
        });

        // ------------------------------------------------------------- cambio password ---
        gruppo.MapPut("/password", async (
            CambioPasswordRichiesta richiesta,
            HttpRequest http,
            SessionStore sessioni,
            IUserRepository utenti,
            PasswordHasher hasher,
            CancellationToken ct) =>
        {
            var gettone = Gettone(http);
            if (sessioni.Risolvi(gettone) is not { } id || await utenti.TrovaPerIdAsync(id, ct) is not { } account)
            {
                return Results.Unauthorized();
            }

            // La password attuale si richiede anche a chi è già dentro: protegge dal
            // computer lasciato aperto, che è il caso in cui serve davvero.
            if (!hasher.Verifica(richiesta.CurrentPassword ?? string.Empty, account.PasswordHash))
            {
                return Results.Json(
                    new ErroreRisposta("La password attuale non è corretta."),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (PasswordPolicy.Controlla(richiesta.NewPassword, account.Username) is { } errore)
            {
                return Errore(errore);
            }

            account.PasswordHash = hasher.Hash(richiesta.NewPassword!);
            await utenti.AggiornaAsync(account, ct);

            // Tutte le altre sessioni cadono; questa resta, perché è quella di chi ha appena
            // cambiato la password e farlo uscire sarebbe punirlo per aver fatto la cosa giusta.
            sessioni.ChiudiTutteDi(account.Id);
            var nuovo = sessioni.Apri(account.Id);

            return Results.Ok(new SessioneApertaRisposta(
                nuovo, DateTimeOffset.UtcNow + SessionStore.Durata, account.Pubblico()));
        });

        // -------------------------------------------------------------------- profilo ---
        gruppo.MapPut("/profile", async (
            ProfiloRichiesta richiesta,
            HttpRequest http,
            SessionStore sessioni,
            IUserRepository utenti,
            CancellationToken ct) =>
        {
            if (sessioni.Risolvi(Gettone(http)) is not { } id || await utenti.TrovaPerIdAsync(id, ct) is not { } account)
            {
                return Results.Unauthorized();
            }

            if (ColoreValido(richiesta.AvatarColor) is not { } colore)
            {
                return Errore("Il colore deve essere scritto nella forma #rrggbb.");
            }

            account.AvatarColor = colore;
            await utenti.AggiornaAsync(account, ct);
            return Results.Ok(account.Pubblico());
        });

        // ---------------------------------------------------------------------- foto ---
        gruppo.MapPut("/photo", async (
            HttpRequest http,
            SessionStore sessioni,
            IUserRepository utenti,
            CancellationToken ct) =>
        {
            if (sessioni.Risolvi(Gettone(http)) is not { } id || await utenti.TrovaPerIdAsync(id, ct) is not { } account)
            {
                return Results.Unauthorized();
            }

            // Si legge con un tetto invece di leggere tutto e misurare dopo: «leggi e poi
            // controlla» significa aver già accettato in memoria qualunque cosa sia arrivata.
            using var memoria = new MemoryStream();
            var buffer = new byte[81920];
            int letti;
            while ((letti = await http.Body.ReadAsync(buffer, ct)) > 0)
            {
                if (memoria.Length + letti > ImmagineCaricata.ByteMassimi)
                {
                    return Results.Json(
                        new ErroreRisposta($"L'immagine supera {ImmagineCaricata.ByteMassimi / (1024 * 1024)} MB."),
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                }

                memoria.Write(buffer, 0, letti);
            }

            var contenuto = memoria.ToArray();
            if (ImmagineCaricata.Riconosci(contenuto) is not { } tipo)
            {
                return Errore("Il file non è un'immagine PNG, JPEG o WebP.");
            }

            await utenti.ScriviFotoAsync(account.Id, contenuto, ct);
            account.AvatarContentType = tipo;
            account.AvatarVersion++;
            await utenti.AggiornaAsync(account, ct);

            return Results.Ok(account.Pubblico());
        });

        gruppo.MapDelete("/photo", async (
            HttpRequest http,
            SessionStore sessioni,
            IUserRepository utenti,
            CancellationToken ct) =>
        {
            if (sessioni.Risolvi(Gettone(http)) is not { } id || await utenti.TrovaPerIdAsync(id, ct) is not { } account)
            {
                return Results.Unauthorized();
            }

            await utenti.EliminaFotoAsync(account.Id);
            account.AvatarContentType = null;

            // Anche togliere la foto è un cambio di revisione: chi aveva in cache la vecchia
            // deve smettere di vederla, e il cerchio con le iniziali deve prendere il posto.
            account.AvatarVersion++;
            await utenti.AggiornaAsync(account, ct);

            return Results.Ok(account.Pubblico());
        });

        // La foto è pubblica: compare accanto ai pattern, che chiunque può vedere. Sta
        // fuori dal gruppo con il limite di frequenza perché la scarica anche il browser
        // di chi si limita a guardare l'elenco.
        app.MapGet("/api/users/{id:guid}/photo", async (
            Guid id,
            IUserRepository utenti,
            HttpResponse risposta,
            CancellationToken ct) =>
        {
            var account = await utenti.TrovaPerIdAsync(id, ct);
            if (account?.AvatarContentType is null)
            {
                return Results.NotFound();
            }

            var contenuto = await utenti.LeggiFotoAsync(id, ct);
            if (contenuto is null)
            {
                return Results.NotFound();
            }

            // Il tipo è quello riconosciuto dai byte al caricamento, e nosniff impedisce al
            // browser di cercarne uno diverso: insieme, chiudono la strada a un file
            // caricato come immagine e interpretato come qualcos'altro.
            risposta.Headers["X-Content-Type-Options"] = "nosniff";

            // Cache lunga: l'indirizzo porta la revisione (vedi AvatarVersion), quindi una
            // foto nuova è un indirizzo nuovo e non c'è niente da invalidare.
            risposta.Headers.CacheControl = "private, max-age=86400";
            return Results.File(contenuto, account.AvatarContentType);
        });

        return app;
    }

    /// <summary>Il gettone dall'intestazione <c>Authorization: Bearer …</c>.</summary>
    public static string? Gettone(HttpRequest richiesta)
    {
        var intestazione = richiesta.Headers.Authorization.ToString();
        return intestazione.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? intestazione["Bearer ".Length..].Trim()
            : null;
    }

    private static IResult Errore(string messaggio) => Results.BadRequest(new ErroreRisposta(messaggio));

    /// <summary>
    /// Spende lo stesso tempo che sarebbe servito a verificare una password vera, e
    /// restituisce sempre <c>false</c>. Serve a rendere indistinguibili «non esiste» e
    /// «password sbagliata» anche col cronometro in mano.
    /// </summary>
    private static bool BruciaIlTempo(PasswordHasher hasher, string? password)
    {
        hasher.Verifica(password ?? string.Empty, HashFinto.Value);
        return false;
    }

    /// <summary>
    /// Un hash di una password che nessuno conosce, calcolato una volta all'avvio. Serve
    /// solo a far lavorare il verificatore per il tempo giusto.
    /// </summary>
    private static readonly Lazy<string> HashFinto = new(() =>
        new PasswordHasher().Hash(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))));

    /// <summary>Il colore se è scritto come #rrggbb, altrimenti <c>null</c>.</summary>
    private static string? ColoreValido(string? colore)
    {
        if (colore is null || colore.Length != 7 || colore[0] != '#')
        {
            return null;
        }

        for (var i = 1; i < 7; i++)
        {
            if (!Uri.IsHexDigit(colore[i]))
            {
                return null;
            }
        }

        return colore.ToLowerInvariant();
    }

    /// <summary>
    /// Un colore ricavato dal nome utente.
    ///
    /// <para>
    /// Non è casuale ma <b>determinato dal nome</b>, e la differenza conta: lo stesso utente
    /// ha sempre lo stesso colore, su qualunque macchina e dopo qualunque riavvio. Un colore
    /// davvero casuale renderebbe l'avatar irriconoscibile ogni volta, che è l'opposto di
    /// ciò che serve. La tinta viene dal nome, saturazione e luminosità sono fisse: così
    /// nessuna estrazione produce un grigio spento o un giallo illeggibile.
    /// </para>
    /// </summary>
    public static string ColoreDa(string nomeUtente)
    {
        var somma = 0;
        foreach (var c in nomeUtente.ToLowerInvariant())
        {
            somma = (somma * 31 + c) % 360;
        }

        return DaHsl(somma, 0.62, 0.48);
    }

    private static string DaHsl(double gradi, double saturazione, double luminosita)
    {
        var c = (1 - Math.Abs(2 * luminosita - 1)) * saturazione;
        var x = c * (1 - Math.Abs(gradi / 60 % 2 - 1));
        var m = luminosita - c / 2;

        var (r, g, b) = gradi switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };

        return "#" + Componente(r + m) + Componente(g + m) + Componente(b + m);
    }

    private static string Componente(double valore) =>
        ((int)Math.Round(Math.Clamp(valore, 0, 1) * 255)).ToString("x2", CultureInfo.InvariantCulture);
}
