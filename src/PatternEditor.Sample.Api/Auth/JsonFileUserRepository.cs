using System.Collections.Concurrent;
using System.Text.Json;

namespace PatternEditor.Sample.Api.Auth;

/// <summary>Archivio degli utenti registrati.</summary>
public interface IUserRepository
{
    Task<UserAccount?> TrovaPerNomeAsync(string nomeUtente, CancellationToken ct = default);

    Task<UserAccount?> TrovaPerIdAsync(Guid id, CancellationToken ct = default);

    /// <returns><c>false</c> se il nome utente è già preso.</returns>
    Task<bool> CreaAsync(UserAccount account, CancellationToken ct = default);

    Task AggiornaAsync(UserAccount account, CancellationToken ct = default);

    Task ScriviFotoAsync(Guid id, byte[] contenuto, CancellationToken ct = default);

    Task<byte[]?> LeggiFotoAsync(Guid id, CancellationToken ct = default);

    Task EliminaFotoAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Un file JSON per utente, nella stessa filosofia dell'archivio dei pattern.
/// </summary>
///
/// <remarks>
/// <para>
/// I file si chiamano con l'<b>identificativo</b> e non con il nome utente, e la ragione è
/// di sicurezza prima ancora che di ordine: un nome scelto dall'utente che diventa un nome
/// di file è la strada più corta verso un percorso che esce dalla cartella
/// (<c>../../qualcosa</c>) o verso un nome che il sistema operativo riserva a sé. Un GUID
/// non ha questi problemi per costruzione.
/// </para>
/// <para>
/// Il nome utente resta comunque unico, e a garantirlo è un indice in memoria costruito una
/// volta all'avvio. Con qualche decina di utenti non esiste ragione di rileggere la cartella
/// a ogni accesso.
/// </para>
/// </remarks>
public sealed class JsonFileUserRepository : IUserRepository
{
    private static readonly JsonSerializerOptions Opzioni = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _cartella;
    private readonly ConcurrentDictionary<string, Guid> _perNome = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _scrittura = new(1, 1);

    public JsonFileUserRepository(string cartella)
    {
        _cartella = cartella ?? throw new ArgumentNullException(nameof(cartella));
        Directory.CreateDirectory(_cartella);
        CostruisciIndice();
    }

    private string PercorsoAccount(Guid id) => Path.Combine(_cartella, $"{id}.json");

    private string PercorsoFoto(Guid id) => Path.Combine(_cartella, $"{id}.foto");

    private void CostruisciIndice()
    {
        foreach (var file in Directory.EnumerateFiles(_cartella, "*.json"))
        {
            try
            {
                var account = JsonSerializer.Deserialize<UserAccount>(File.ReadAllText(file), Opzioni);
                if (account is { Id: var id } && !string.IsNullOrEmpty(account.UsernameNormalizzato))
                {
                    _perNome[account.UsernameNormalizzato] = id;
                }
            }
            catch (JsonException)
            {
                // Un file illeggibile non deve impedire l'avvio dell'applicazione: gli altri
                // utenti esistono ancora, e un archivio che non parte è un guasto peggiore
                // di un account che manca.
            }
        }
    }

    public async Task<UserAccount?> TrovaPerNomeAsync(string nomeUtente, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nomeUtente))
        {
            return null;
        }

        return _perNome.TryGetValue(Normalizza(nomeUtente), out var id)
            ? await TrovaPerIdAsync(id, ct)
            : null;
    }

    public async Task<UserAccount?> TrovaPerIdAsync(Guid id, CancellationToken ct = default)
    {
        var percorso = PercorsoAccount(id);
        if (!File.Exists(percorso))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UserAccount>(await File.ReadAllTextAsync(percorso, ct), Opzioni);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<bool> CreaAsync(UserAccount account, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var normalizzato = Normalizza(account.Username);
        account.UsernameNormalizzato = normalizzato;

        // Prenota il nome prima di scrivere: fra il controllo e la scrittura c'è un
        // intervallo, e due registrazioni con lo stesso nome arrivate insieme finirebbero
        // entrambe dentro. TryAdd decide in un'operazione sola chi dei due arriva primo.
        if (!_perNome.TryAdd(normalizzato, account.Id))
        {
            return false;
        }

        try
        {
            await ScriviAsync(account, ct);
            return true;
        }
        catch
        {
            // La scrittura è fallita: il nome torna libero, altrimenti resterebbe occupato
            // da un account che non esiste su disco.
            _perNome.TryRemove(normalizzato, out _);
            throw;
        }
    }

    public Task AggiornaAsync(UserAccount account, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        return ScriviAsync(account, ct);
    }

    private async Task ScriviAsync(UserAccount account, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(account, Opzioni);

        await _scrittura.WaitAsync(ct);
        try
        {
            // Scrittura in due tempi: un file temporaneo e poi lo spostamento, che sul
            // filesystem è atomico. Scrivendo direttamente sul file definitivo, un arresto
            // a metà lascerebbe un account troncato — cioè un utente che non può più entrare.
            var definitivo = PercorsoAccount(account.Id);
            var temporaneo = definitivo + ".tmp";

            await File.WriteAllTextAsync(temporaneo, json, ct);
            File.Move(temporaneo, definitivo, overwrite: true);
        }
        finally
        {
            _scrittura.Release();
        }
    }

    public async Task ScriviFotoAsync(Guid id, byte[] contenuto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contenuto);

        var definitivo = PercorsoFoto(id);
        var temporaneo = definitivo + ".tmp";

        await File.WriteAllBytesAsync(temporaneo, contenuto, ct);
        File.Move(temporaneo, definitivo, overwrite: true);
    }

    public async Task<byte[]?> LeggiFotoAsync(Guid id, CancellationToken ct = default)
    {
        var percorso = PercorsoFoto(id);
        return File.Exists(percorso) ? await File.ReadAllBytesAsync(percorso, ct) : null;
    }

    public Task EliminaFotoAsync(Guid id, CancellationToken ct = default)
    {
        var percorso = PercorsoFoto(id);
        if (File.Exists(percorso))
        {
            File.Delete(percorso);
        }

        return Task.CompletedTask;
    }

    private static string Normalizza(string nomeUtente) => nomeUtente.Trim().ToLowerInvariant();
}
