using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PatternEditor.Sample.Api.Auth;

namespace PatternEditor.Sample.Api.Tests;

/// <summary>
/// Avvia l'API vera e propria in memoria, con tutti i suoi endpoint, i plugin registrati e
/// il repository su filesystem: i test che la usano esercitano il percorso completo
/// richiesta HTTP → serializzatore → repository → risposta, non le singole classi isolate.
///
/// Lo storage viene rediretto su una directory temporanea per ogni istanza. È il punto
/// più importante di questa classe: senza l'override, i test scriverebbero e
/// cancellerebbero nella cartella App_Data reale dell'applicazione. Lo stesso vale per gli
/// utenti e per l'indice degli autori, che dalla versione con l'accesso sono altri due
/// posti in cui l'API scrive.
/// </summary>
internal sealed class PatternApiFactory : WebApplicationFactory<Program>
{
    private readonly string _radice =
        Path.Combine(Path.GetTempPath(), "pattern-editor-api-tests-" + Guid.NewGuid());

    public string StorageDirectory => Path.Combine(_radice, "patterns");

    private string UsersDirectory => Path.Combine(_radice, "users");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Percorsi assoluti: l'API li usa così come sono, senza risolverli sulla content root.
        builder.UseSetting("Storage:Directory", StorageDirectory);
        builder.UseSetting("Auth:Directory", UsersDirectory);

        // Il costo dell'hash scende a un millesimo. In esercizio quel costo è la difesa —
        // rende cara ogni prova a chi ne tenta milioni — ma qui ogni registrazione lo
        // pagherebbe per intero, e una suite che impiega un minuto a contare le password
        // non la esegue più nessuno.
        builder.UseSetting("Auth:Pbkdf2Iterations", "1000");
    }

    /// <summary>
    /// Un client con un utente registrato e la sessione già aperta.
    ///
    /// <para>
    /// Scrivere richiede un accesso, e quasi tutti i test scrivono: senza questa scorciatoia
    /// ognuno di loro comincerebbe con dieci righe di registrazione che non hanno niente a
    /// che vedere con quello che sta verificando.
    /// </para>
    /// </summary>
    public async Task<HttpClient> ClientAutenticatoAsync(string nomeUtente = "collaudatore")
    {
        var client = CreateClient();
        var gettone = await RegistraAsync(client, nomeUtente);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", gettone);
        return client;
    }

    /// <summary>
    /// Registra un utente e restituisce il gettone della sua sessione. Serve ai test che
    /// hanno bisogno di <b>due</b> utenti per verificare che uno non possa toccare le cose
    /// dell'altro.
    /// </summary>
    public static async Task<string> RegistraAsync(HttpClient client, string nomeUtente)
    {
        var risposta = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = nomeUtente,
            password = "una frase lunga che va bene",
            securityQuestion = "La via in cui sono cresciuto",
            securityAnswer = "Via Garibaldi",
        });

        risposta.EnsureSuccessStatusCode();

        var sessione = await risposta.Content.ReadFromJsonAsync<GettoneDto>();
        return sessione!.Token;
    }

    /// <summary>Un secondo client, autenticato come un altro utente.</summary>
    public async Task<HttpClient> ClientDiUnAltroAsync(string nomeUtente = "estraneo")
    {
        var client = CreateClient();
        var gettone = await RegistraAsync(client, nomeUtente);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", gettone);
        return client;
    }

    /// <summary>
    /// Un client autenticato come amministratore.
    /// </summary>
    ///
    /// <remarks>
    /// Il permesso si assegna dall'archivio degli utenti e non da una richiesta, perché da una
    /// richiesta non si può assegnare: è esattamente quello che i test devono confermare. Qui
    /// si fa quello che in esercizio fa la riga di comando.
    /// </remarks>
    public async Task<HttpClient> ClientAmministratoreAsync(string nomeUtente = "moderatore")
    {
        var client = await ClientAutenticatoAsync(nomeUtente);

        var utenti = Services.GetRequiredService<IUserRepository>();
        var account = await utenti.TrovaPerNomeAsync(nomeUtente);
        account!.IsAdmin = true;
        await utenti.AggiornaAsync(account);

        return client;
    }

    private sealed record GettoneDto(string Token);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_radice))
        {
            Directory.Delete(_radice, recursive: true);
        }
    }
}
