using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Sample.Client.Services;

/// <summary>
/// Riepilogo di un pattern per l'elenco: quanto basta a disegnare una scheda, senza scaricare
/// la definizione completa.
///
/// <para>
/// Comprende <c>PreviewSvg</c>, cioè l'anteprima **già generata dal server**. Potrebbe
/// sembrare un'anomalia — il rendering è compito del componente — ma l'alternativa sarebbe
/// scaricare la definizione integrale di ogni pattern per disegnarne una miniatura: con
/// quattrocento pattern in elenco significa quattrocento documenti completi al posto di
/// altrettante stringhe brevi.
/// </para>
/// </summary>
/// <param name="OwnerId">
/// Chi l'ha scritto, oppure <c>null</c> per un pattern più vecchio degli account. Serve a
/// sapere <b>prima</b> se il salvataggio avrà senso, invece di scoprirlo da un rifiuto del
/// server dopo che si è lavorato.
/// </param>
/// <param name="SizeBytes">
/// Quanto pesa il documento SVG, in byte. Lo conta il server, che il documento lo ha già
/// prodotto per l'anteprima.
/// </param>
/// <param name="Visibility">
/// Lo stato di pubblicazione per nome. Arriva come stringa e non come enumerazione del
/// client perché un valore che questa versione non conosce non deve far fallire la lettura
/// dell'intero elenco: si mostra com'è e non si tocca.
/// </param>
public sealed record PatternSummary(
    Guid Id,
    string Name,
    string PreviewSvg,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    double Width,
    double Height,
    int ElementCount,
    IReadOnlyList<string> ElementTypes,
    Guid? OwnerId,
    string? OwnerName,
    string? OwnerColor,
    bool OwnerHasPhoto,
    int OwnerPhotoVersion,
    string Visibility = "Pubblica",
    int SizeBytes = 0)
{
    /// <summary>Il peso del documento, come lo legge una persona.</summary>
    public string Peso => Dimensione.Leggibile(SizeBytes);

    public bool EPrivata => string.Equals(Visibility, "Privata", StringComparison.OrdinalIgnoreCase);

    public bool EInAttesa => string.Equals(Visibility, "InAttesa", StringComparison.OrdinalIgnoreCase);

    public bool EPubblica => string.Equals(Visibility, "Pubblica", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Il nome della voce di catalogo che dice questo stato a parole.
    /// </summary>
    ///
    /// <remarks>
    /// Il riepilogo restituisce il <b>nome della voce</b> e non la parola: un record che
    /// arriva dalla rete non ha modo di sapere in che lingua si sta leggendo, e nemmeno deve
    /// — a tradurre è chi disegna. La parola sta su una pastiglia larga quanto una scheda,
    /// quindi è corta; il resto della frase è nel suggerimento, dove c'è spazio.
    /// </remarks>
    public string VoceStato => EPrivata ? "stato.privato" : EInAttesa ? "stato.inAttesa" : "stato.pubblico";
}

/// <summary>
/// Un numero di byte scritto come lo si legge.
/// </summary>
///
/// <remarks>
/// Sta per conto suo perché lo stesso numero compare in quattro posti — le schede
/// dell'archivio, quelle della gestione, quelle della moderazione e il foglio del sorgente
/// nella presentazione — e quattro formattazioni leggermente diverse dello stesso dato si
/// notano, una accanto all'altra, molto più di quanto costi tenerle insieme.
/// </remarks>
public static class Dimensione
{
    /// <summary>
    /// Come si chiama l'unità più piccola: «bytes» in inglese, «byte» in italiano.
    /// </summary>
    ///
    /// <remarks>
    /// Lo imposta il servizio delle lingue, ed è un valore solo per tutta l'applicazione —
    /// come la cultura con cui si scrivono i numeri, che sta anch'essa in un posto solo.
    /// Passarlo a ogni chiamata vorrebbe dire darlo in mano anche ai riepiloghi che arrivano
    /// dalla rete, che di lingue non sanno niente e non devono saperne.
    /// </remarks>
    public static string NomeDelByte { get; set; } = "bytes";

    public static string Leggibile(int byteTotali) =>
        byteTotali < 1024
            ? $"{byteTotali} {NomeDelByte}"
            : $"{byteTotali / 1024.0:0.#} kB";
}

/// <summary>
/// Com'è andata una scrittura.
///
/// <para>
/// Non basta un booleano perché i modi di non riuscire non sono equivalenti: «non so chi
/// sei» porta alla finestra di accesso, «non è tuo» porta a un messaggio, e mandare alla
/// finestra di accesso chi è già entrato sarebbe incomprensibile.
/// </para>
/// </summary>
public sealed record EsitoApi(bool Riuscito, int Stato, string? Messaggio)
{
    public static readonly EsitoApi Ok = new(true, 200, null);

    public bool NonAutenticato => Stato == 401;

    public bool Vietato => Stato == 403;

    /// <summary>
    /// Il testo da mostrare, con una frase di riserva quando il server non ne ha scritto uno.
    /// </summary>
    ///
    /// <remarks>
    /// Il catalogo si passa invece di essere una proprietà del servizio: questo è un esito,
    /// cioè un dato, e un dato che si porti dietro il servizio delle lingue non si lascia più
    /// costruire in un test. Il messaggio scritto dal <b>server</b> vince e resta com'è: è già
    /// una frase, e tradurla qui significherebbe indovinare da che cosa è nata.
    /// </remarks>
    public string Testo(Lingua lingua) =>
        Messaggio is { Length: > 0 } ? Messaggio : lingua[Voce, Stato];

    private string Voce => Stato switch
    {
        0 => "api.nonRisponde",
        401 => "api.serveAccesso",
        403 => "api.diAltroUtente",
        404 => "api.nonEsiste",
        _ => "api.errore",
    };
}

/// <summary>
/// Client per l'API di persistenza dei pattern. Il body delle richieste/risposte contenenti
/// un Pattern completo viene sempre gestito tramite <see cref="IPatternSerializer"/> (basato
/// sul Plugin Registry), non tramite la (de)serializzazione JSON di default di HttpClient,
/// che non conoscerebbe il polimorfismo degli elementi vettoriali.
/// </summary>
public sealed class PatternApiClient
{
    private readonly HttpClient _http;
    private readonly IPatternSerializer _serializer;

    public PatternApiClient(HttpClient http, IPatternSerializer serializer)
    {
        _http = http;
        _serializer = serializer;
    }

    /// <summary>
    /// Elenco dei riepiloghi. Qui la deserializzazione JSON predefinita va benissimo: il
    /// riepilogo è un record piatto, senza polimorfismo da gestire.
    /// </summary>
    public async Task<IReadOnlyList<PatternSummary>> GetSummariesAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<PatternSummary>>("/api/patterns", ct) ?? new List<PatternSummary>();

    /// <summary>
    /// Pattern completo, oppure null se non esiste. Un pattern cancellato da qualcun altro
    /// mentre lo si stava per aprire non è un guasto: è una situazione ordinaria, e il
    /// chiamante la gestisce meglio con un null che con un'eccezione.
    /// </summary>
    public async Task<Pattern?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/patterns/{id}", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return _serializer.Deserialize(json);
    }

    /// <summary>
    /// Crea un pattern. Il corpo viene composto **a mano** con il serializzatore del progetto
    /// e non con i comodi metodi JSON di HttpClient: quelli userebbero il serializzatore
    /// predefinito, che non sa ricostruire gli elementi polimorfici e produrrebbe un
    /// documento con i soli campi comuni.
    /// </summary>
    public async Task<EsitoApi> CreateAsync(Pattern pattern, CancellationToken ct = default)
    {
        var content = new StringContent(_serializer.Serialize(pattern), Encoding.UTF8, "application/json");
        return await InviaAsync(() => _http.PostAsync("/api/patterns", content, ct));
    }

    /// <summary>Aggiorna un pattern esistente. Vale la stessa nota di <see cref="CreateAsync"/>.</summary>
    public async Task<EsitoApi> UpdateAsync(Pattern pattern, CancellationToken ct = default)
    {
        var content = new StringContent(_serializer.Serialize(pattern), Encoding.UTF8, "application/json");
        return await InviaAsync(() => _http.PutAsync($"/api/patterns/{pattern.Id}", content, ct));
    }

    /// <summary>
    /// Chiede di pubblicare un pattern, o di tornare a tenerlo per sé.
    /// </summary>
    ///
    /// <remarks>
    /// Restituisce lo stato in cui il pattern si è trovato davvero, che per una richiesta di
    /// pubblicazione non è quello chiesto: è «InAttesa». Chi chiama lo mostra invece di
    /// dedurlo, perché dedurlo significherebbe riscrivere qui la regola del server e doverla
    /// correggere in due posti.
    /// </remarks>
    public async Task<(EsitoApi Esito, string? Stato)> ChiediVisibilitaAsync(
        Guid id, string visibilita, CancellationToken ct = default)
    {
        HttpResponseMessage risposta;
        try
        {
            risposta = await _http.PutAsJsonAsync(
                $"/api/patterns/{id}/visibilita", new { visibility = visibilita }, ct);
        }
        catch (HttpRequestException)
        {
            return (new EsitoApi(false, 0, null), null);
        }

        if (!risposta.IsSuccessStatusCode)
        {
            return (await TraduciAsync(risposta), null);
        }

        var stato = await risposta.Content.ReadFromJsonAsync<VisibilitaDto>(ct);
        return (EsitoApi.Ok, stato?.Visibility);
    }

    /// <summary>
    /// Le richieste di pubblicazione da esaminare. Un <c>null</c> significa che il server ha
    /// detto di no — sessione scaduta, oppure questo account non modera — ed è diverso da un
    /// elenco vuoto, che significa che non c'è niente da fare.
    /// </summary>
    public async Task<IReadOnlyList<PatternSummary>?> GetInAttesaAsync(CancellationToken ct = default)
    {
        try
        {
            var risposta = await _http.GetAsync("/api/moderation/pending", ct);
            return risposta.IsSuccessStatusCode
                ? await risposta.Content.ReadFromJsonAsync<List<PatternSummary>>(ct)
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Approva una richiesta: da qui, e solo da qui, un pattern diventa pubblico.</summary>
    public async Task<EsitoApi> ApprovaAsync(Guid id, CancellationToken ct = default) =>
        await InviaAsync(() => _http.PostAsync($"/api/moderation/{id}/approve", null, ct));

    /// <summary>Rifiuta una richiesta: il pattern torna privato e resta al suo autore.</summary>
    public async Task<EsitoApi> RifiutaAsync(Guid id, CancellationToken ct = default) =>
        await InviaAsync(() => _http.PostAsync($"/api/moderation/{id}/reject", null, ct));

    /// <summary>Elimina un pattern.</summary>
    public async Task<EsitoApi> DeleteAsync(Guid id, CancellationToken ct = default) =>
        await InviaAsync(() => _http.DeleteAsync($"/api/patterns/{id}", ct));

    /// <summary>
    /// Elimina più pattern in una sola richiesta. Serve un
    /// <see cref="HttpRequestMessage"/> costruito a mano perché DELETE con un corpo non ha
    /// una scorciatoia in HttpClient: la specifica HTTP lo consente ma lo sconsiglia, e le
    /// librerie si adeguano.
    /// </summary>
    public async Task<EsitoApi> DeleteManyAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/patterns")
        {
            Content = JsonContent.Create(ids.ToArray()),
        };

        // L'esito va riferito al chiamante e non ignorato: un'eliminazione che fallisce in
        // silenzio lascia l'utente davanti a un elenco immutato senza capire il perche'.
        return await InviaAsync(() => _http.SendAsync(request, ct));
    }

    /// <summary>
    /// Manda una richiesta e ne traduce l'esito.
    ///
    /// <para>
    /// Un'API che non risponde affatto diventa lo stato 0, non un'eccezione: per chi chiama
    /// è un esito come gli altri — il salvataggio non è andato — e trattarlo come un guasto
    /// del programma costringerebbe ogni pulsante ad avere il proprio try/catch.
    /// </para>
    /// </summary>
    private static async Task<EsitoApi> InviaAsync(Func<Task<HttpResponseMessage>> invio)
    {
        HttpResponseMessage risposta;
        try
        {
            risposta = await invio();
        }
        catch (HttpRequestException)
        {
            return new EsitoApi(false, 0, null);
        }

        return risposta.IsSuccessStatusCode ? EsitoApi.Ok : await TraduciAsync(risposta);
    }

    /// <summary>Il testo dell'errore, quando il server ne ha scritto uno.</summary>
    private static async Task<EsitoApi> TraduciAsync(HttpResponseMessage risposta)
    {
        string? messaggio = null;
        try
        {
            var errore = await risposta.Content.ReadFromJsonAsync<ErroreDto>();
            messaggio = errore?.Message;
        }
        catch (JsonException)
        {
            // Il corpo non è nella forma attesa: resta il messaggio di riserva.
        }
        catch (NotSupportedException)
        {
            // Tipo di contenuto inatteso: idem.
        }

        return new EsitoApi(false, (int)risposta.StatusCode, messaggio);
    }

    private sealed record ErroreDto(string? Message);

    private sealed record VisibilitaDto(string? Visibility);
}
