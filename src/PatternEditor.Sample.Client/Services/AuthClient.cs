using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace PatternEditor.Sample.Client.Services;

/// <summary>Chi è l'utente, per quel poco che il client ha bisogno di sapere.</summary>
/// <param name="IsAdmin">
/// Se questo utente modera le pubblicazioni. Serve a <b>mostrare</b> la voce di menu e non a
/// concedere niente: chi modificasse questo valore nel browser si ritroverebbe una pagina
/// che il server riempie di rifiuti.
/// </param>
public sealed record Account(
    Guid Id,
    string Username,
    string AvatarColor,
    bool HasPhoto,
    int PhotoVersion,
    DateTimeOffset CreatedAt,
    bool IsAdmin = false)
{
    /// <summary>
    /// Le due lettere del cerchio. Si prendono dall'inizio e non dalle iniziali di parole
    /// diverse: un nome utente è una parola sola per costruzione, e «MA» di «mario» è più
    /// riconoscibile di qualunque regola più furba.
    /// </summary>
    public string Iniziali =>
        Username.Length >= 2
            ? Username[..2].ToUpperInvariant()
            : Username.ToUpperInvariant();
}

/// <summary>
/// Il gettone della sessione, in un oggetto per conto suo.
/// </summary>
///
/// <remarks>
/// Sembra un'inezia ma risolve un nodo: il gestore che aggiunge l'intestazione alle
/// richieste ha bisogno del gettone, e chi ottiene il gettone ha bisogno di fare richieste.
/// Con un riferimento diretto fra i due si otterrebbe un anello che il contenitore non sa
/// costruire. Un contenitore del solo gettone lo spezza.
/// </remarks>
public sealed class SessioneCorrente
{
    public string? Token { get; set; }
}

/// <summary>
/// Attacca il gettone a ogni richiesta verso l'API.
///
/// <para>
/// In un punto solo, e non nei singoli metodi: una chiamata a cui ci si dimentica di
/// aggiungerlo non fallisce in modo evidente — risponde come se l'utente fosse anonimo, che
/// è il modo peggiore di sbagliare perché somiglia a un comportamento corretto.
/// </para>
/// </summary>
public sealed class GettoneHandler : DelegatingHandler
{
    private readonly SessioneCorrente _sessione;

    public GettoneHandler(SessioneCorrente sessione) => _sessione = sessione;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_sessione.Token is { Length: > 0 } gettone)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gettone);
        }

        return base.SendAsync(request, ct);
    }
}

/// <summary>Esito di un'operazione che può fallire per un motivo da mostrare a chi l'ha chiesta.</summary>
public sealed record Esito(bool Riuscito, string? Errore)
{
    public static readonly Esito Ok = new(true, null);

    public static Esito No(string errore) => new(false, errore);
}

/// <summary>
/// Registrazione, accesso, recupero e profilo, visti dal client.
/// </summary>
///
/// <remarks>
/// <para>
/// Conserva il gettone in <c>localStorage</c>. È la scelta comoda e non la più sicura, e
/// vale la pena dire perché: un cookie <c>HttpOnly</c> sarebbe fuori portata di qualunque
/// script, e quindi al riparo anche da uno script iniettato nella pagina. Richiede però che
/// client e API stiano sullo stesso sito, oppure HTTPS con credenziali fra origini diverse,
/// e questa applicazione di riferimento gira su due porte in chiaro. Il gettone in
/// <c>localStorage</c> è la scelta onesta per il contesto; in esercizio si passerebbe al
/// cookie.
/// </para>
/// </remarks>
public sealed class AuthClient
{
    private const string ChiaveArchivio = "pattern-editor.sessione";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly SessioneCorrente _sessione;
    private readonly IJSRuntime _js;
    private readonly Lingua _lingua;

    public AuthClient(HttpClient http, SessioneCorrente sessione, IJSRuntime js, Lingua lingua)
    {
        _http = http;
        _sessione = sessione;
        _js = js;
        _lingua = lingua;
    }

    /// <summary>Chi è entrato, oppure <c>null</c> per un visitatore anonimo.</summary>
    public Account? Utente { get; private set; }

    public bool Autenticato => Utente is not null;

    /// <summary>
    /// Si è già guardato se c'era una sessione da riprendere. Prima che sia vero non si può
    /// dire che l'utente è anonimo: si può solo dire che non si sa ancora.
    /// </summary>
    public bool Pronto { get; private set; }

    /// <summary>Qualcosa è cambiato: chi disegna l'avatar o i pulsanti deve ridisegnarsi.</summary>
    public event Action? Cambiato;

    /// <summary>
    /// Qualcuno ha chiesto di aprire la finestra di accesso.
    ///
    /// <para>
    /// La finestra vive nell'intestazione, che è sempre a schermo; le pagine però hanno
    /// pulsanti che senza un accesso non possono funzionare. Un evento lascia che la
    /// richiesta parta da dove nasce senza che la pagina debba conoscere la finestra.
    /// </para>
    /// </summary>
    public event Action? AccessoRichiesto;

    /// <summary>Qualcuno ha chiesto di aprire la finestra del profilo.</summary>
    public event Action? ProfiloRichiesto;

    /// <summary>Apre la finestra di accesso da qualunque punto dell'applicazione.</summary>
    public void ChiediAccesso() => AccessoRichiesto?.Invoke();

    /// <summary>Apre la finestra del profilo.</summary>
    public void ChiediProfilo() => ProfiloRichiesto?.Invoke();

    /// <summary>
    /// Riprende la sessione dell'ultima volta, se c'è ed è ancora valida.
    ///
    /// <para>
    /// Il gettone letto dall'archivio non si prende per buono: si chiede al server chi sia.
    /// Può essere scaduto, revocato da un cambio di password, o appartenere a un account che
    /// nel frattempo non c'è più — e in tutti e tre i casi ripartire come autenticati
    /// mostrerebbe un'interfaccia che poi non funziona.
    /// </para>
    /// </summary>
    public async Task InizializzaAsync()
    {
        if (Pronto)
        {
            return;
        }

        try
        {
            var gettone = await _js.InvokeAsync<string?>("localStorage.getItem", ChiaveArchivio);
            if (!string.IsNullOrWhiteSpace(gettone))
            {
                _sessione.Token = gettone;

                var risposta = await _http.GetAsync("/api/auth/me");
                if (risposta.IsSuccessStatusCode)
                {
                    Utente = await risposta.Content.ReadFromJsonAsync<Account>(Json);
                }
                else
                {
                    await DimenticaAsync();
                }
            }
        }
        catch (HttpRequestException)
        {
            // L'API non risponde: si resta anonimi e la pagina funziona lo stesso in sola
            // lettura. Cancellare il gettone qui sarebbe sbagliato — non è scaduto, è il
            // server a non esserci.
            _sessione.Token = null;
        }
        catch (JSException)
        {
            // Archivio del browser non disponibile (finestra anonima con i dati bloccati):
            // si resta anonimi, senza rumore.
        }

        Pronto = true;
        Cambiato?.Invoke();
    }

    public Task<Esito> RegistraAsync(string utente, string password, string domanda, string risposta, string? colore) =>
        ApriSessioneAsync(HttpMethod.Post, "/api/auth/register", new
        {
            username = utente,
            password,
            securityQuestion = domanda,
            securityAnswer = risposta,
            avatarColor = colore,
        });

    public Task<Esito> AccediAsync(string utente, string password) =>
        ApriSessioneAsync(HttpMethod.Post, "/api/auth/login", new { username = utente, password });

    /// <summary>La domanda di recupero di un utente, oppure il motivo per cui non si può avere.</summary>
    public async Task<(string? Domanda, string? Errore)> DomandaDiRecuperoAsync(string utente)
    {
        try
        {
            var risposta = await _http.PostAsJsonAsync("/api/auth/recovery/question", new { username = utente });
            if (risposta.IsSuccessStatusCode)
            {
                var corpo = await risposta.Content.ReadFromJsonAsync<DomandaDto>(Json);
                return (corpo?.Question, null);
            }

            return (null, await MessaggioAsync(risposta));
        }
        catch (HttpRequestException)
        {
            return (null, NonRaggiungibile);
        }
    }

    public Task<Esito> RecuperaAsync(string utente, string risposta, string nuovaPassword) =>
        ApriSessioneAsync(HttpMethod.Post, "/api/auth/recovery/reset", new
        {
            username = utente,
            answer = risposta,
            newPassword = nuovaPassword,
        });

    public Task<Esito> CambiaPasswordAsync(string attuale, string nuova) =>
        ApriSessioneAsync(HttpMethod.Put, "/api/auth/password", new
        {
            currentPassword = attuale,
            newPassword = nuova,
        });

    public async Task<Esito> CambiaColoreAsync(string colore)
    {
        try
        {
            var risposta = await _http.PutAsJsonAsync("/api/auth/profile", new { avatarColor = colore });
            if (!risposta.IsSuccessStatusCode)
            {
                return Esito.No(await MessaggioAsync(risposta));
            }

            Utente = await risposta.Content.ReadFromJsonAsync<Account>(Json);
            Cambiato?.Invoke();
            return Esito.Ok;
        }
        catch (HttpRequestException)
        {
            return Esito.No(NonRaggiungibile);
        }
    }

    public async Task<Esito> CaricaFotoAsync(Stream contenuto, string tipo)
    {
        try
        {
            using var corpo = new StreamContent(contenuto);
            corpo.Headers.ContentType = MediaTypeHeaderValue.Parse(tipo);

            var risposta = await _http.PutAsync("/api/auth/photo", corpo);
            if (!risposta.IsSuccessStatusCode)
            {
                return Esito.No(await MessaggioAsync(risposta));
            }

            Utente = await risposta.Content.ReadFromJsonAsync<Account>(Json);
            Cambiato?.Invoke();
            return Esito.Ok;
        }
        catch (HttpRequestException)
        {
            return Esito.No(NonRaggiungibile);
        }
    }

    public async Task<Esito> RimuoviFotoAsync()
    {
        try
        {
            var risposta = await _http.DeleteAsync("/api/auth/photo");
            if (!risposta.IsSuccessStatusCode)
            {
                return Esito.No(await MessaggioAsync(risposta));
            }

            Utente = await risposta.Content.ReadFromJsonAsync<Account>(Json);
            Cambiato?.Invoke();
            return Esito.Ok;
        }
        catch (HttpRequestException)
        {
            return Esito.No(NonRaggiungibile);
        }
    }

    /// <summary>
    /// Esce. La sessione si chiude sul server <b>e</b> si dimentica qui: fermarsi al primo
    /// lascerebbe l'interfaccia convinta di essere ancora dentro, fermarsi al secondo
    /// lascerebbe valido un gettone che qualcuno potrebbe avere copiato.
    /// </summary>
    public async Task EsciAsync()
    {
        try
        {
            await _http.PostAsync("/api/auth/logout", null);
        }
        catch (HttpRequestException)
        {
            // Il server non risponde: si esce comunque da questa parte. Il gettone scadrà.
        }

        await DimenticaAsync();
        Cambiato?.Invoke();
    }

    /// <summary>
    /// Da chiamare quando l'API risponde 401 a una richiesta qualsiasi: vuol dire che la
    /// sessione non vale più, e continuare a mostrare l'avatar sarebbe una bugia.
    /// </summary>
    public async Task SessioneScadutaAsync()
    {
        if (Utente is null)
        {
            return;
        }

        await DimenticaAsync();
        Cambiato?.Invoke();
    }

    /// <summary>
    /// L'indirizzo della foto di un utente.
    ///
    /// <para>
    /// La <b>revisione</b> fa parte dell'indirizzo, e non è un dettaglio: la foto si serve con
    /// una cache lunga — è la stessa per giorni — e con un indirizzo fisso chi ne carica una
    /// nuova continuerebbe a vedere la vecchia finché la cache non scade. Cambiando la
    /// revisione, per il browser è un'altra risorsa e la scarica subito.
    /// </para>
    /// </summary>
    public string UrlFoto(Guid id, int revisione) =>
        $"{_http.BaseAddress}api/users/{id}/photo?v={revisione}";

    // ------------------------------------------------------------------ appoggio ------

    private sealed record DomandaDto(string Question);

    private sealed record SessioneDto(string Token, DateTimeOffset Scadenza, Account User);

    private sealed record ErroreDto(string? Message);

    /// <summary>
    /// Quando il servizio non risponde affatto. Non è una costante perché la frase dipende
    /// dalla lingua, e la lingua si sceglie dopo l'avvio.
    /// </summary>
    private string NonRaggiungibile => _lingua["api.nonRispondeRiprova"];

    /// <summary>
    /// Le quattro operazioni che aprono una sessione — registrazione, accesso, recupero e
    /// cambio password — rispondono tutte allo stesso modo, e qui finiscono nello stesso
    /// posto: gettone da conservare, utente da mostrare, errore da riferire.
    /// </summary>
    private async Task<Esito> ApriSessioneAsync(HttpMethod metodo, string indirizzo, object corpo)
    {
        try
        {
            var richiesta = new HttpRequestMessage(metodo, indirizzo)
            {
                Content = JsonContent.Create(corpo),
            };

            var risposta = await _http.SendAsync(richiesta);
            if (!risposta.IsSuccessStatusCode)
            {
                return Esito.No(await MessaggioAsync(risposta));
            }

            var sessione = await risposta.Content.ReadFromJsonAsync<SessioneDto>(Json);
            if (sessione is null)
            {
                return Esito.No(_lingua["api.rispostaIllegibile"]);
            }

            _sessione.Token = sessione.Token;
            Utente = sessione.User;
            await RicordaAsync(sessione.Token);

            Cambiato?.Invoke();
            return Esito.Ok;
        }
        catch (HttpRequestException)
        {
            return Esito.No(NonRaggiungibile);
        }
    }

    /// <summary>
    /// Il messaggio che il server ha scritto per l'utente. Se non ne ha scritto uno — un
    /// errore inatteso, una pagina di errore del server — si ripiega su una frase che almeno
    /// dice il codice, invece di lasciare la finestra muta.
    /// </summary>
    private async Task<string> MessaggioAsync(HttpResponseMessage risposta)
    {
        try
        {
            var errore = await risposta.Content.ReadFromJsonAsync<ErroreDto>(Json);
            if (errore?.Message is { Length: > 0 } messaggio)
            {
                return messaggio;
            }
        }
        catch (JsonException)
        {
            // Corpo non JSON: si passa al messaggio generico qui sotto.
        }
        catch (NotSupportedException)
        {
            // Tipo di contenuto inatteso: idem.
        }

        return risposta.StatusCode switch
        {
            HttpStatusCode.TooManyRequests => _lingua["api.troppeRichieste"],
            HttpStatusCode.Unauthorized => _lingua["api.credenzialiNonValide"],
            HttpStatusCode.Forbidden => _lingua["api.nonConsentita"],
            _ => _lingua["api.errore", (int)risposta.StatusCode],
        };
    }

    private async Task RicordaAsync(string gettone)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", ChiaveArchivio, gettone);
        }
        catch (JSException)
        {
            // Archivio non disponibile: la sessione vale per questa scheda e basta.
        }
    }

    private async Task DimenticaAsync()
    {
        _sessione.Token = null;
        Utente = null;

        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", ChiaveArchivio);
        }
        catch (JSException)
        {
            // Niente archivio, niente da cancellare.
        }
    }
}
