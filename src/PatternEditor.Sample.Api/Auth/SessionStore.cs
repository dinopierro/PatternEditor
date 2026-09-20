using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PatternEditor.Sample.Api.Auth;

/// <summary>
/// Le sessioni aperte: da un gettone si risale a un utente, e da un utente si possono
/// chiudere tutte le sue.
/// </summary>
///
/// <remarks>
/// <para>
/// Il gettone è <b>opaco</b>: 256 bit casuali, senza alcun significato. È una scelta
/// deliberata rispetto a un JWT, che qui non porterebbe vantaggi e due svantaggi concreti.
/// Un JWT vale finché non scade e non si può revocare — un «esci» non potrebbe fare niente
/// di più che dimenticarlo dal lato del browser — e obbliga a custodire una chiave di
/// firma, che è un segreto in più da proteggere. Il gettone opaco si revoca togliendo una
/// riga, e non c'è nessuna chiave da custodire.
/// </para>
/// <para>
/// Sul server si conserva l'<b>impronta</b> del gettone, non il gettone. Vale lo stesso
/// ragionamento delle password: chi leggesse l'archivio delle sessioni non potrebbe
/// impersonare nessuno. Qui basta SHA-256 senza sale né iterazioni, e la differenza è che
/// un gettone di 256 bit casuali non si indovina provando: non c'è nessun dizionario dei
/// gettoni più usati.
/// </para>
/// </remarks>
public sealed class SessionStore
{
    /// <summary>
    /// Durata di una sessione. Due settimane: abbastanza da non richiedere l'accesso ogni
    /// giorno, poco da non lasciare un gettone valido per sempre su un computer prestato.
    /// </summary>
    public static readonly TimeSpan Durata = TimeSpan.FromDays(14);

    private readonly ConcurrentDictionary<string, Sessione> _sessioni = new(StringComparer.Ordinal);
    private readonly string? _percorso;
    private readonly Func<DateTimeOffset> _orologio;
    private readonly SemaphoreSlim _scrittura = new(1, 1);

    /// <param name="percorso">
    /// File in cui conservare le sessioni. Senza, vivono solo in memoria e ogni riavvio
    /// dell'API disconnette tutti — corretto ma scomodo in sviluppo, dove l'API riparte
    /// a ogni modifica.
    /// </param>
    public SessionStore(string? percorso = null, Func<DateTimeOffset>? orologio = null)
    {
        _percorso = percorso;
        _orologio = orologio ?? (() => DateTimeOffset.UtcNow);
        Carica();
    }

    private sealed record Sessione(Guid UtenteId, DateTimeOffset Scadenza);

    /// <summary>
    /// Apre una sessione e restituisce il gettone <b>in chiaro</b>. È l'unico momento in cui
    /// esiste: da qui in poi il server ne conosce solo l'impronta, e se il client lo perde
    /// non c'è modo di rimandarglielo — si rifà l'accesso.
    /// </summary>
    public string Apri(Guid utenteId)
    {
        var gettone = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        _sessioni[Impronta(gettone)] = new Sessione(utenteId, _orologio() + Durata);
        Salva();
        return gettone;
    }

    /// <summary>
    /// L'utente a cui appartiene il gettone, oppure <c>null</c> se non esiste o è scaduto.
    /// Una sessione scaduta viene tolta qui: è il momento in cui si scopre, e rimandare
    /// significherebbe tenerne in memoria per sempre.
    /// </summary>
    public Guid? Risolvi(string? gettone)
    {
        if (string.IsNullOrWhiteSpace(gettone))
        {
            return null;
        }

        var chiave = Impronta(gettone);
        if (!_sessioni.TryGetValue(chiave, out var sessione))
        {
            return null;
        }

        if (sessione.Scadenza <= _orologio())
        {
            _sessioni.TryRemove(chiave, out _);
            Salva();
            return null;
        }

        return sessione.UtenteId;
    }

    /// <summary>Chiude una sessione. Ripetuto su un gettone già chiuso non è un errore.</summary>
    public void Chiudi(string? gettone)
    {
        if (string.IsNullOrWhiteSpace(gettone))
        {
            return;
        }

        if (_sessioni.TryRemove(Impronta(gettone), out _))
        {
            Salva();
        }
    }

    /// <summary>
    /// Chiude <b>tutte</b> le sessioni di un utente. Si chiama quando cambia la password,
    /// compreso il cambio che segue un recupero: se qualcun altro era entrato, quella è
    /// l'unica mossa che lo butta fuori davvero.
    /// </summary>
    public void ChiudiTutteDi(Guid utenteId)
    {
        var toccato = false;
        foreach (var (chiave, sessione) in _sessioni)
        {
            if (sessione.UtenteId == utenteId && _sessioni.TryRemove(chiave, out _))
            {
                toccato = true;
            }
        }

        if (toccato)
        {
            Salva();
        }
    }

    /// <summary>Quante sessioni valide ci sono in questo momento. Serve ai test.</summary>
    public int Aperte
    {
        get
        {
            var ora = _orologio();
            return _sessioni.Count(s => s.Value.Scadenza > ora);
        }
    }

    private static string Impronta(string gettone) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(gettone)));

    // ------------------------------------------------------------------ persistenza ---

    private void Carica()
    {
        if (_percorso is null || !File.Exists(_percorso))
        {
            return;
        }

        try
        {
            var righe = JsonSerializer.Deserialize<Dictionary<string, Sessione>>(File.ReadAllText(_percorso));
            if (righe is null)
            {
                return;
            }

            var ora = _orologio();
            foreach (var (chiave, sessione) in righe)
            {
                // Le scadute non tornano in memoria: il file può essere vecchio di mesi.
                if (sessione.Scadenza > ora)
                {
                    _sessioni[chiave] = sessione;
                }
            }
        }
        catch (JsonException)
        {
            // File illeggibile: si riparte senza sessioni. Il costo è che tutti rifanno
            // l'accesso, che è esattamente ciò che si deve fare quando non si sa chi è chi.
        }
    }

    private void Salva()
    {
        if (_percorso is null)
        {
            return;
        }

        _scrittura.Wait();
        try
        {
            var temporaneo = _percorso + ".tmp";
            File.WriteAllText(temporaneo, JsonSerializer.Serialize(_sessioni));
            File.Move(temporaneo, _percorso, overwrite: true);
        }
        catch (IOException)
        {
            // Se il file non si scrive, le sessioni restano valide in memoria: il servizio
            // continua a funzionare e si perde solo la sopravvivenza al riavvio.
        }
        finally
        {
            _scrittura.Release();
        }
    }
}
