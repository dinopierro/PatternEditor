using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using PatternEditor.Element.Circle;
using PatternEditor.Element.Ellipse;
using PatternEditor.Element.Image;
using PatternEditor.Element.Line;
using PatternEditor.Element.Path;
using PatternEditor.Element.Polygon;
using PatternEditor.Element.Polyline;
using PatternEditor.Element.Rect;
using PatternEditor.Element.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using PatternEditor.Extensions;
using PatternEditor.Sample.Api;
using PatternEditor.Sample.Api.Auth;
using PatternEditor.Sample.Api.Persistence;
using PatternEditor.Services;

// ---------------------------------------------------------------------------------------
// API di persistenza dell'applicazione di riferimento.
//
// Finalità dimostrativa: mostra come si integra il componente, non come si costruisce un
// servizio di produzione. Archivia su file, e il modello di accesso è volutamente semplice —
// un solo ruolo, nessuna posta elettronica, il rientro affidato a una domanda di recupero.
//
// Il trattamento dei dati, invece, è quello che sarebbe corretto ovunque: le password e le
// risposte di recupero non esistono in chiaro da nessuna parte, i gettoni di sessione si
// conservano per impronta e si revocano, i tentativi falliti si contano, le richieste di
// autenticazione hanno un limite di frequenza. Vedi la cartella Auth.
//
// Quello che qui manca per essere un servizio vero, e che è giusto dire: il trasporto è in
// chiaro (in esercizio servirebbe HTTPS, senza il quale ogni difesa qui sotto è inutile) e
// il gettone viaggia in un'intestazione invece che in un cookie HttpOnly.
//
// Chi vede che cosa è deciso qui e in nessun altro posto. Un pattern nasce privato, diventa
// «in attesa» quando l'autore chiede di pubblicarlo e pubblico solo quando un amministratore
// approva: il client mostra questi stati, ma è l'API che li assegna e li filtra, perché è
// l'unica che un browser non può contraddire. Vedi VisibileA e Concessa in fondo al file.
//
// Un punto merita attenzione più di tutti. Il corpo delle richieste che trasportano un
// Pattern viene letto **come testo** e passato al serializzatore del progetto, invece di
// affidarsi al binding predefinito: quest'ultimo, vedendo il tipo dichiarato VectorElement,
// costruirebbe elementi privi di tutte le proprietà specifiche. Il prezzo è che gli errori
// di formato arrivano come eccezioni e vanno tradotti in 400 a mano (PatternRequestReader).
// ---------------------------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

// Libreria Pattern Editor + plugin concreti. L'API (applicazione host) è il punto in cui
// si decide quali tipi di elemento sono disponibili: la libreria stessa non li conosce.
builder.Services.AddPatternEditor();
builder.Services.AddPatternEditorPlugin<LinePlugin>();
builder.Services.AddPatternEditorPlugin<RectPlugin>();
builder.Services.AddPatternEditorPlugin<CirclePlugin>();
builder.Services.AddPatternEditorPlugin<EllipsePlugin>();
builder.Services.AddPatternEditorPlugin<PathPlugin>();
builder.Services.AddPatternEditorPlugin<PolygonPlugin>();
builder.Services.AddPatternEditorPlugin<PolylinePlugin>();
builder.Services.AddPatternEditorPlugin<TextPlugin>();
builder.Services.AddPatternEditorPlugin<ImagePlugin>();

builder.Services.AddSingleton<IPatternRepository>(sp =>
{
    // Il percorso configurato, se relativo, viene risolto sulla content root e non sulla
    // directory di lavoro del processo: quest'ultima cambia a seconda di come si avvia
    // l'applicazione (dotnet run, exe compilato, IDE) e farebbe leggere e scrivere i
    // pattern in cartelle diverse, dando l'impressione che i dati "tornino indietro".
    // La content root resta invece la stessa cartella in tutti i casi.
    var configured = builder.Configuration["Storage:Directory"] ?? Path.Combine("App_Data", "patterns");
    var storageDirectory = Path.IsPathRooted(configured)
        ? configured
        : Path.Combine(builder.Environment.ContentRootPath, configured);

    return new JsonFilePatternRepository(storageDirectory, sp.GetRequiredService<IPatternSerializer>());
});

// --------------------------------------------------------------------- autenticazione ---

// Il costo dell'hash è configurabile perché i test ne calcolano a decine: il valore pensato
// per rendere cara ogni prova a un attaccante renderebbe cara anche ogni asserzione.
builder.Services.AddSingleton(new PasswordHasher(
    builder.Configuration.GetValue("Auth:Pbkdf2Iterations", PasswordHasher.IterazioniPredefinite)));

builder.Services.AddSingleton<IUserRepository>(_ =>
    new JsonFileUserRepository(CartellaDati(builder, "Auth:Directory", Path.Combine("App_Data", "users"))));

builder.Services.AddSingleton(_ => new SessionStore(
    Path.Combine(CartellaDati(builder, "Auth:Directory", Path.Combine("App_Data", "users")), "sessions.json")));

// Chi ha scritto che cosa. Sta qui e non dentro il documento del pattern: il formato è
// quello della libreria, che di utenti non sa niente e non deve saperne.
// Limite di frequenza sulle richieste di autenticazione. Senza, provare centomila password
// costa quanto provarne una: è la difesa che rende inutile la forza bruta, e nessuna
// lunghezza minima la sostituisce.
const string PoliticaAuth = "auth";
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(PoliticaAuth, http => RateLimitPartition.GetFixedWindowLimiter(
        // Per indirizzo di provenienza: una finestra globale lascerebbe che un solo
        // attaccante chiuda fuori tutti gli altri utenti.
        partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "sconosciuto",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

// CORS: l'API di riferimento risponde al client locale. Non è più "qualunque origine" da
// quando esistono i gettoni — un'origine qualsiasi potrebbe leggere le risposte di un
// utente autenticato che si trovasse a visitarla.
const string CorsPolicy = "SampleClient";
var origini = builder.Configuration.GetSection("Cors:Origins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy => policy
        .SetIsOriginAllowed(origine => OrigineAmmessa(origine, origini))
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// ------------------------------------------------------------------------ manutenzione ---
//
// L'API sa fare una cosa oltre a rispondere: assegnare un autore ai pattern che non ne
// hanno. Serve una volta sola, sugli archivi nati prima che gli account esistessero — che
// altrimenti resterebbero leggibili da tutti e modificabili da nessuno.
//
//     dotnet run --project src/PatternEditor.Sample.Api -- assegna-autore <nome utente>
//
if (args is ["assegna-autore", var nomeUtente, ..])
{
    return await AssegnaAutoreAsync(app, nomeUtente);
}

// E nominare chi approva le pubblicazioni. Non esiste un endpoint che lo faccia, e non è
// una dimenticanza: il primo amministratore non può essere nominato da un amministratore,
// e qualunque altra strada via rete sarebbe una porta aperta sul permesso più alto.
//
//     dotnet run --project src/PatternEditor.Sample.Api -- amministratore <nome utente>
//     dotnet run --project src/PatternEditor.Sample.Api -- amministratore <nome utente> revoca
//
if (args is ["amministratore", var nomeAmministratore, ..])
{
    return await NominaAmministratoreAsync(app, nomeAmministratore, args is [_, _, "revoca", ..]);
}

app.UseRateLimiter();
app.UseCors(CorsPolicy);

// ------------------------------------------------------------------ il client, da qui ---
//
// L'applicazione e' una sola: questa serve sia le proprie risposte sia il client
// WebAssembly che le consuma. Un indirizzo, un certificato, una pubblicazione.
//
// L'ordine conta. UseBlazorFrameworkFiles insegna a servire /_framework con i tipi MIME
// giusti — senza, il browser scarica il .wasm come testo e l'applicazione non parte.
// UseStaticFiles serve il resto del wwwroot del client: i fogli di stile, i moduli
// JavaScript, i cataloghi delle lingue, i manuali in PDF.
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapAuth(PoliticaAuth);

var api = app.MapGroup("/api/patterns");

// GET /api/patterns — elenco dei pattern (nome + anteprima SVG).
api.MapGet("/", async (
    HttpRequest request,
    IPatternRepository repository,
    IPatternSvgRenderer renderer,
    SessionStore sessioni,
    IUserRepository utenti,
    CancellationToken ct) =>
{
    // L'accesso qui non è richiesto ma conta: l'elenco di un anonimo e quello di chi è
    // entrato non sono lo stesso elenco. Un gettone assente o scaduto dà semplicemente la
    // vista pubblica, che è la risposta giusta e non un errore.
    var chi = await ChiEAsync(request, sessioni, utenti, ct);

    var patterns = await repository.GetAllAsync(ct);
    var visibili = patterns.Where(p => VisibileA(p, chi)).ToList();

    return Results.Ok(await RiepiloghiAsync(visibili, renderer, utenti, ct));
});

// GET /api/patterns/{id} — recupero di un singolo pattern.
api.MapGet("/{id:guid}", async (
    Guid id,
    HttpRequest request,
    IPatternRepository repository,
    IPatternSerializer serializer,
    SessionStore sessioni,
    IUserRepository utenti,
    CancellationToken ct) =>
{
    var pattern = await repository.GetByIdAsync(id, ct);

    // Un pattern che non si può vedere risponde 404 e non 403, ed è voluto: 403 direbbe
    // «esiste, ma non è per te», e ripetuto su una serie di identificativi permetterebbe di
    // ricostruire l'archivio privato di qualcun altro senza vederne mai uno.
    if (pattern is null || !VisibileA(pattern, await ChiEAsync(request, sessioni, utenti, ct)))
    {
        return Results.NotFound();
    }

    return Results.Content(serializer.Serialize(pattern), "application/json");
});

// POST /api/patterns — creazione di un nuovo pattern. L'Id (UUIDv7) è già stato assegnato
// dal client al momento della creazione nell'editor: l'API lo verifica e lo persiste.
//
// Il corpo della richiesta viene letto come testo grezzo e deserializzato tramite
// IPatternSerializer (basato sul Plugin Registry): il model binding automatico di
// ASP.NET Core, basato sul tipo dichiarato VectorElement, non è in grado di gestire
// correttamente il polimorfismo degli elementi.
api.MapPost("/", async (
    HttpRequest request,
    IPatternRepository repository,
    IPatternSerializer serializer,
    SessionStore sessioni,
    IUserRepository utenti,
    CancellationToken ct) =>
{
    if (await ChiEAsync(request, sessioni, utenti, ct) is not { } autore)
    {
        return Senza("Per salvare un pattern serve un accesso.");
    }

    using var reader = new StreamReader(request.Body);
    var json = await reader.ReadToEndAsync(ct);

    if (!PatternRequestReader.TryRead(serializer, json, out var pattern, out var error))
    {
        return Results.BadRequest(error);
    }

    // L'autore lo decide il server, sempre, e sovrascrive quello che il corpo dichiarava.
    // Prenderlo dalla richiesta significherebbe lasciare che chiunque firmi un documento
    // con il nome di un altro scrivendo due righe di JSON.
    pattern.AuthorId = autore.Id;
    pattern.AuthorName = autore.Username;

    // E la visibilità allo stesso modo: «pubblica» non è uno stato che un client possa
    // assegnare: chiederla significa metterlo in attesa. Se bastasse scriverlo nel corpo,
    // l'approvazione sarebbe una formalità aggirabile da chiunque sappia cos'è un JSON.
    pattern.Visibility = Concessa(pattern.Visibility);

    var created = await repository.CreateAsync(pattern, ct);

    return created
        ? Results.Content(serializer.Serialize(pattern), "application/json", statusCode: StatusCodes.Status201Created)
        : Results.Conflict($"Esiste già un pattern con Id {pattern.Id}.");
});

// PUT /api/patterns/{id} — modifica di un pattern esistente. Il file mantiene sempre il
// proprio nome ({PatternId}.json), anche quando cambia il nome o qualsiasi altra proprietà.
api.MapPut("/{id:guid}", async (
    Guid id,
    HttpRequest request,
    IPatternRepository repository,
    IPatternSerializer serializer,
    SessionStore sessioni,
    IUserRepository utenti,
    CancellationToken ct) =>
{
    if (await ChiEAsync(request, sessioni, utenti, ct) is not { } utente)
    {
        return Senza("Per salvare le modifiche serve un accesso.");
    }

    var esistente = await repository.GetByIdAsync(id, ct);
    if (esistente is null)
    {
        return Results.NotFound();
    }

    if (esistente.AuthorId != utente.Id)
    {
        return Vietato(MotivoDelRifiuto(esistente));
    }

    using var reader = new StreamReader(request.Body);
    var json = await reader.ReadToEndAsync(ct);

    if (!PatternRequestReader.TryRead(serializer, json, out var pattern, out var error))
    {
        return Results.BadRequest(error);
    }

    if (id != pattern.Id)
    {
        return Results.BadRequest("L'identificativo nell'URL non corrisponde a quello del pattern.");
    }

    // L'autore resta quello scritto sul disco e non quello che arriva nel corpo: la
    // paternità di un documento non è una proprietà che si modifica salvandolo.
    pattern.AuthorId = esistente.AuthorId;
    pattern.AuthorName = esistente.AuthorName;

    // Una modifica al disegno annulla l'approvazione: quello che era stato approvato è il
    // contenuto, non il nome del file. Senza questa riga basterebbe far approvare una cosa
    // qualsiasi e poi salvarci sopra quello che si voleva pubblicare davvero — cioè la
    // moderazione servirebbe a niente. Il prezzo è che correggere un dettaglio in un pattern
    // già pubblico lo rimette in coda, e il client lo dice prima di salvare.
    pattern.Visibility = Concessa(pattern.Visibility);

    var updated = await repository.UpdateAsync(pattern, ct);

    return updated
        ? Results.Content(serializer.Serialize(pattern), "application/json")
        : Results.NotFound();
});

// PUT /api/patterns/{id}/visibilita — l'autore chiede la pubblicazione, o la ritira.
//
// Sta per conto suo e non dentro il PUT del pattern perché è un gesto diverso: non cambia il
// disegno, e non deve costringere a scaricare e rimandare un documento intero per spostare
// una parola. Per la stessa ragione non tocca la data di modifica.
api.MapPut("/{id:guid}/visibilita", async (
    Guid id,
    VisibilitaRichiesta corpo,
    HttpRequest request,
    IPatternRepository repository,
    SessionStore sessioni,
    IUserRepository utenti,
    CancellationToken ct) =>
{
    if (await ChiEAsync(request, sessioni, utenti, ct) is not { } utente)
    {
        return Senza("Per cambiare la visibilità serve un accesso.");
    }

    var esistente = await repository.GetByIdAsync(id, ct);
    if (esistente is null || !VisibileA(esistente, utente))
    {
        return Results.NotFound();
    }

    if (esistente.AuthorId != utente.Id)
    {
        return Vietato("Solo l'autore decide se un pattern si vede.");
    }

    if (!Enum.TryParse<PatternVisibility>(corpo?.Visibility, ignoreCase: true, out var chiesta))
    {
        return Results.BadRequest(new ErroreRisposta("Visibilità non riconosciuta."));
    }

    // Un pattern già approvato che si ri-chiede pubblico resta pubblico: qui non è cambiato
    // niente da guardare, e rimandarlo in coda sarebbe una punizione per aver premuto due
    // volte. Diverso è il salvataggio, che il contenuto lo cambia davvero.
    esistente.Visibility = chiesta == PatternVisibility.Privata
        ? PatternVisibility.Privata
        : esistente.Visibility == PatternVisibility.Pubblica
            ? PatternVisibility.Pubblica
            : PatternVisibility.InAttesa;

    // ReplaceAsync: cambiare chi lo vede non è modificare il disegno, e segnarlo come
    // modificato oggi lo farebbe risalire in cima a un elenco ordinato per ultima modifica.
    await repository.ReplaceAsync(esistente, ct);

    return Results.Ok(new VisibilitaRisposta(esistente.Visibility.ToString()));
});

// DELETE /api/patterns/{id} — eliminazione di un singolo pattern.
api.MapDelete("/{id:guid}", async (
    Guid id,
    HttpRequest request,
    IPatternRepository repository,
    SessionStore sessioni,
    IUserRepository utenti,
    CancellationToken ct) =>
{
    if (await ChiEAsync(request, sessioni, utenti, ct) is not { } utente)
    {
        return Senza("Per eliminare un pattern serve un accesso.");
    }

    var esistente = await repository.GetByIdAsync(id, ct);
    if (esistente is null)
    {
        return Results.NotFound();
    }

    if (esistente.AuthorId != utente.Id)
    {
        return Vietato(MotivoDelRifiuto(esistente));
    }

    var deleted = await repository.DeleteAsync(id, ct);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// DELETE /api/patterns — eliminazione multipla in un'unica richiesta. Ogni identificativo
// viene gestito indipendentemente: un id non trovato non deve impedire l'eliminazione degli
// altri, ed è comunque segnalato nel risultato.
api.MapDelete("/", async (
    [Microsoft.AspNetCore.Mvc.FromBody] Guid[] ids,
    HttpRequest request,
    IPatternRepository repository,
    SessionStore sessioni,
    IUserRepository utenti,
    CancellationToken ct) =>
{
    if (await ChiEAsync(request, sessioni, utenti, ct) is not { } utente)
    {
        return Senza("Per eliminare dei pattern serve un accesso.");
    }

    var results = new Dictionary<Guid, bool>();
    foreach (var id in ids)
    {
        // Un pattern che non è suo vale come "non eliminato" e non fa fallire gli altri: è
        // la stessa regola dell'id inesistente, ed è quella che permette al chiamante di
        // sapere esattamente che cosa è successo riga per riga.
        var esistente = await repository.GetByIdAsync(id, ct);
        if (esistente is null || esistente.AuthorId != utente.Id)
        {
            results[id] = false;
            continue;
        }

        results[id] = await repository.DeleteAsync(id, ct);
    }

    return Results.Ok(results);
});

// ------------------------------------------------------------------------ moderazione ---
//
// Tre richieste sole: che cosa aspetta, sì, no. Tutte e tre riservate a chi ha il permesso,
// e il permesso si verifica qui a ogni richiesta e non una volta all'entrata nella pagina:
// una pagina si raggiunge scrivendone l'indirizzo, un endpoint no.
var moderazione = app.MapGroup("/api/moderation");

// GET /api/moderation/pending — la coda, nello stesso formato dell'elenco: chi modera deve
// vedere il disegno, non il nome di un file.
moderazione.MapGet("/pending", async (
    HttpRequest request,
    IPatternRepository repository,
    IPatternSvgRenderer renderer,
    SessionStore sessioni,
    IUserRepository utenti,
    CancellationToken ct) =>
{
    if (await ChiEAsync(request, sessioni, utenti, ct) is not { } utente)
    {
        return Senza("Questa pagina richiede un accesso.");
    }

    if (!utente.IsAdmin)
    {
        return Vietato("Solo gli amministratori vedono le richieste di pubblicazione.");
    }

    var patterns = await repository.GetAllAsync(ct);
    var inCoda = patterns
        .Where(p => p.Visibility == PatternVisibility.InAttesa)
        // Dalla più vecchia: chi ha chiesto prima aspetta da più tempo, e una coda che parte
        // dalle ultime arrivate lascia in fondo per sempre quelle che nessuno guarda.
        .OrderBy(p => p.ModifiedAt)
        .ToList();

    return Results.Ok(await RiepiloghiAsync(inCoda, renderer, utenti, ct));
});

// POST /api/moderation/{id}/approve — da qui, e solo da qui, un pattern diventa pubblico.
moderazione.MapPost("/{id:guid}/approve", (
    Guid id, HttpRequest request, IPatternRepository repository,
    SessionStore sessioni, IUserRepository utenti, CancellationToken ct) =>
    DecidiAsync(id, PatternVisibility.Pubblica, request, repository, sessioni, utenti, ct));

// POST /api/moderation/{id}/reject — il rifiuto riporta il pattern privato e non lo cancella:
// il disegno è di chi l'ha fatto, e decidere che non si pubblica non è decidere che non esiste.
moderazione.MapPost("/{id:guid}/reject", (
    Guid id, HttpRequest request, IPatternRepository repository,
    SessionStore sessioni, IUserRepository utenti, CancellationToken ct) =>
    DecidiAsync(id, PatternVisibility.Privata, request, repository, sessioni, utenti, ct));

// Tutto quello che non e' un'API e non e' un file statico e' una pagina del client, e va
// servita con index.html perche' sia il client a decidere che cosa mostrare: /gestione e
// /moderazione esistono solo nel suo instradamento, e senza questa riga un aggiornamento
// della pagina su uno di quegli indirizzi risponderebbe 404.
app.MapFallbackToFile("index.html");

app.Run();

// Le istruzioni di primo livello restituiscono un codice di uscita perche' la modalita' di
// manutenzione qui sopra ne restituisce uno: senza questa riga il compilatore osserva, a
// ragione, che un percorso del codice non ne produce nessuno.
return 0;

// ------------------------------------------------------------------ funzioni di appoggio ---

/// <summary>Chi può vedere questo pattern.</summary>
///
/// <remarks>
/// Tre casi e nient'altro: quello che è pubblico lo vedono tutti, il proprio lo vede l'autore
/// in qualunque stato, e chi modera vede ciò che gli è stato sottoposto. Non c'è un quarto
/// caso in cui l'amministratore vede anche i privati altrui, ed è deliberato: moderare
/// significa giudicare quello che qualcuno ha chiesto di mostrare, non guardare nei cassetti.
/// </remarks>
static bool VisibileA(PatternEditor.Core.Models.Pattern pattern, UserAccount? chi) =>
    pattern.Visibility == PatternVisibility.Pubblica
    || (chi is not null && pattern.AuthorId == chi.Id)
    || (chi is { IsAdmin: true } && pattern.Visibility == PatternVisibility.InAttesa);

/// <summary>
/// La visibilità che si concede a chi ne chiede una. È la regola dell'intero sistema in due
/// righe: privato si ottiene chiedendolo, pubblico no — si ottiene aspettando che qualcuno
/// guardi. Uno stato sconosciuto ricade sull'attesa, che è il lato prudente.
/// </summary>
static PatternVisibility Concessa(PatternVisibility chiesta) =>
    chiesta == PatternVisibility.Privata ? PatternVisibility.Privata : PatternVisibility.InAttesa;

/// <summary>
/// Approvazione e rifiuto sono la stessa richiesta con un esito diverso, e scriverle due
/// volte significherebbe correggerne una sola il giorno in cui il controllo cambia.
/// </summary>
static async Task<IResult> DecidiAsync(
    Guid id,
    PatternVisibility esito,
    HttpRequest request,
    IPatternRepository repository,
    SessionStore sessioni,
    IUserRepository utenti,
    CancellationToken ct)
{
    if (await ChiEAsync(request, sessioni, utenti, ct) is not { } utente)
    {
        return Senza("Questa operazione richiede un accesso.");
    }

    if (!utente.IsAdmin)
    {
        return Vietato("Solo gli amministratori decidono le pubblicazioni.");
    }

    var pattern = await repository.GetByIdAsync(id, ct);
    if (pattern is null)
    {
        return Results.NotFound();
    }

    // Si decide su ciò che è in attesa, e basta. Fuori da questo stato la richiesta è già
    // stata evasa da qualcun altro, o ritirata dall'autore mentre la pagina era aperta:
    // eseguirla lo stesso renderebbe pubblico qualcosa che nessuno stava più proponendo.
    if (pattern.Visibility != PatternVisibility.InAttesa)
    {
        return Results.Conflict(new ErroreRisposta(
            "Questa richiesta non è più in attesa: l'autore l'ha ritirata o è già stata decisa."));
    }

    pattern.Visibility = esito;

    // ReplaceAsync, come per la visibilità: approvare non è modificare il disegno.
    await repository.ReplaceAsync(pattern, ct);

    return Results.Ok(new VisibilitaRisposta(pattern.Visibility.ToString()));
}

/// <summary>
/// I riepiloghi di un insieme di pattern. Serve in due posti — l'elenco e la coda di
/// moderazione — e sono lo stesso oggetto: chi modera guarda le stesse schede degli altri.
/// </summary>
static async Task<List<PatternSummaryDto>> RiepiloghiAsync(
    IReadOnlyList<PatternEditor.Core.Models.Pattern> patterns,
    IPatternSvgRenderer renderer,
    IUserRepository utenti,
    CancellationToken ct)
{
    // Il nome dell'autore sta già nel documento; quello che manca è come disegnarlo — il
    // colore e se ha una foto — e sta nell'account. Si risolve una volta per autore e non
    // una per riga: l'elenco ha quattrocento righe e una manciata di autori distinti.
    var aspetto = new Dictionary<Guid, (string Colore, bool ConFoto, int Revisione)>();
    foreach (var autore in patterns.Select(p => p.AuthorId).OfType<Guid>().Distinct())
    {
        if (await utenti.TrovaPerIdAsync(autore, ct) is { } account)
        {
            aspetto[autore] = (account.AvatarColor, account.AvatarContentType is not null, account.AvatarVersion);
        }
    }

    return patterns.Select(p =>
    {
        // Un autore che non si trova più — account cancellato a mano, archivio spostato —
        // lascia il nome scritto nel documento e perde solo il modo di disegnarlo: il
        // cerchio ripiega sul colore ricavato dal nome, che il client sa calcolare da sé.
        var come = p.AuthorId is { } autore && aspetto.TryGetValue(autore, out var trovato)
            ? trovato
            : default((string Colore, bool ConFoto, int Revisione)?);

        // Reso una volta e usato due: come anteprima e come misura. Renderlo due volte per
        // contare dei byte che si hanno già sotto mano sarebbe lavoro pagato due volte su
        // quattrocento righe.
        var svg = renderer.RenderStandaloneSvg(p, "100%", "100%");

        return new PatternSummaryDto(
            p.Id,
            p.Name,
            svg,
            p.CreatedAt,
            p.ModifiedAt,
            p.Definition.Width,
            p.Definition.Height,
            p.Definition.Elements.Count,
            p.Definition.Elements.Select(e => e.Type).Distinct().ToList(),
            p.AuthorId,
            p.AuthorName,
            come?.Colore,
            come?.ConFoto ?? false,
            come?.Revisione ?? 0,
            p.Visibility.ToString(),
            System.Text.Encoding.UTF8.GetByteCount(svg));
    }).ToList();
}

// Nomina (o revoca) un amministratore. Dalla riga di comando e non da un endpoint: chi può
// avviare il processo ha già pieno accesso ai dati, e non si guadagna niente a difendersi da
// lui; una rotta HTTP invece sarebbe una superficie in più sul permesso più alto che esista.
static async Task<int> NominaAmministratoreAsync(WebApplication app, string nomeUtente, bool revoca)
{
    var utenti = app.Services.GetRequiredService<IUserRepository>();

    if (await utenti.TrovaPerNomeAsync(nomeUtente) is not { } account)
    {
        await Console.Error.WriteLineAsync($"Nessun utente si chiama «{nomeUtente}». Registralo prima dall'applicazione.");
        return 1;
    }

    if (account.IsAdmin == !revoca)
    {
        Console.WriteLine(revoca
            ? $"«{account.Username}» non era amministratore: niente da fare."
            : $"«{account.Username}» era già amministratore.");
        return 0;
    }

    account.IsAdmin = !revoca;
    await utenti.AggiornaAsync(account);

    Console.WriteLine(revoca
        ? $"«{account.Username}» non modera più le pubblicazioni."
        : $"«{account.Username}» può approvare le pubblicazioni. Chi ha già la pagina "
          + "aperta vede la voce «Moderazione» ricaricandola: il permesso si rilegge "
          + "dall'account a ogni ripresa della sessione.");

    return 0;
}

// Assegna un autore a tutti i pattern che non ne hanno. Non tocca quelli che ce l'hanno
// già: la manutenzione riempie un vuoto, non riscrive una firma.
static async Task<int> AssegnaAutoreAsync(WebApplication app, string nomeUtente)
{
    var utenti = app.Services.GetRequiredService<IUserRepository>();
    var archivio = app.Services.GetRequiredService<IPatternRepository>();

    if (await utenti.TrovaPerNomeAsync(nomeUtente) is not { } account)
    {
        await Console.Error.WriteLineAsync($"Nessun utente si chiama «{nomeUtente}». Registralo prima dall'applicazione.");
        return 1;
    }

    var patterns = await archivio.GetAllAsync();
    var senzaAutore = patterns.Where(p => p.AuthorId is null).ToList();

    foreach (var pattern in senzaAutore)
    {
        pattern.AuthorId = account.Id;
        pattern.AuthorName = account.Username;

        // ReplaceAsync e non UpdateAsync: assegnare un autore è manutenzione, non una
        // modifica al disegno, e segnare quattrocento pattern come modificati oggi
        // distruggerebbe l'ordinamento per ultima modifica.
        await archivio.ReplaceAsync(pattern);
    }

    Console.WriteLine(senzaAutore.Count == 0
        ? $"Nessun pattern era senza autore: {patterns.Count} già assegnati."
        : $"Assegnati a «{account.Username}» {senzaAutore.Count} pattern su {patterns.Count}.");

    return 0;
}

// Un percorso relativo si risolve sulla content root e non sulla directory di lavoro del
// processo: quest'ultima cambia a seconda di come si avvia l'applicazione, e farebbe
// leggere e scrivere in cartelle diverse.
static string CartellaDati(WebApplicationBuilder builder, string chiave, string predefinito)
{
    var configurato = builder.Configuration[chiave] ?? predefinito;
    return Path.GetFullPath(Path.IsPathRooted(configurato)
        ? configurato
        : Path.Combine(builder.Environment.ContentRootPath, configurato));
}

// 401 e 403 dicono due cose diverse e il client se ne serve: il primo significa «non so chi
// sei» e porta alla finestra di accesso, il secondo «so chi sei e non è tuo» e porta a un
// messaggio. Confonderli manderebbe alla finestra di accesso chi è già entrato.
static IResult Senza(string messaggio) =>
    Results.Json(new ErroreRisposta(messaggio), statusCode: StatusCodes.Status401Unauthorized);

static IResult Vietato(string messaggio) =>
    Results.Json(new ErroreRisposta(messaggio), statusCode: StatusCodes.Status403Forbidden);

// Chi sta scrivendo, risolto dal gettone. Restituisce l'account intero e non il solo
// identificativo perché il nome serve a firmare il documento, e chiederlo due volte
// significherebbe rileggere lo stesso file due volte nella stessa richiesta.
static async Task<UserAccount?> ChiEAsync(
    HttpRequest richiesta, SessionStore sessioni, IUserRepository utenti, CancellationToken ct) =>
    sessioni.Risolvi(AuthEndpoints.Gettone(richiesta)) is { } id
        ? await utenti.TrovaPerIdAsync(id, ct)
        : null;

// Perché il pattern non si tocca. Sono due casi diversi e vale la pena distinguerli: uno si
// risolve chiedendo all'autore, l'altro non si risolve affatto.
static string MotivoDelRifiuto(PatternEditor.Core.Models.Pattern pattern) =>
    pattern.AuthorId is null
        ? "Questo pattern non ha un autore: si può aprire e scaricare, non salvare né eliminare."
        : $"Questo pattern è di {pattern.AuthorName ?? "un altro utente"}: puoi aprirlo e scaricarlo, non salvarlo.";

// Le origini ammesse dal CORS. In assenza di configurazione valgono quelle locali: in
// sviluppo la porta del client cambia spesso, e un elenco fisso costringerebbe a
// ricompilare l'API per cambiarla.
static bool OrigineAmmessa(string origine, string[]? configurate)
{
    if (configurate is { Length: > 0 })
    {
        return Array.Exists(configurate, o => string.Equals(o, origine, StringComparison.OrdinalIgnoreCase));
    }

    return Uri.TryCreate(origine, UriKind.Absolute, out var uri)
        && (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Dichiarazione esplicita della classe generata dalle istruzioni di primo livello.
///
/// <para>
/// Serve ai test di integrazione: <c>WebApplicationFactory&lt;Program&gt;</c> ha bisogno di
/// un tipo pubblico da referenziare, e quello generato dal compilatore per un file di
/// istruzioni di primo livello è interno. Senza questa riga i test dovrebbero avviare un
/// processo vero e parlarci via rete, invece di ospitare l'applicazione in memoria.
/// </para>
/// </summary>
public partial class Program;
