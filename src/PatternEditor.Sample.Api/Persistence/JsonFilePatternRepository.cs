using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Sample.Api.Persistence;

/// <summary>
/// Storage di test basato su filesystem: un file "{PatternId}.json" per ciascun pattern,
/// nella directory configurata. Ha esclusivamente finalità dimostrativa: non introduce
/// alcuna dipendenza da database o servizi di persistenza esterni.
/// </summary>
public sealed class JsonFilePatternRepository : IPatternRepository
{
    private readonly string _storageDirectory;
    private readonly IPatternSerializer _serializer;
    private readonly Func<DateTimeOffset> _clock;

    /// <param name="clock">
    /// Sorgente dell'ora corrente. Iniettabile per rendere verificabili nei test le date di
    /// creazione e ultima modifica; in esercizio è semplicemente l'orologio di sistema.
    /// </param>
    public JsonFilePatternRepository(
        string storageDirectory,
        IPatternSerializer serializer,
        Func<DateTimeOffset>? clock = null)
    {
        _storageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        Directory.CreateDirectory(_storageDirectory);
    }

    private string PathFor(Guid id) => Path.Combine(_storageDirectory, $"{id}.json");

    public async Task<IReadOnlyList<Pattern>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var patterns = new List<Pattern>();

        // Ordina per nome file (= Id UUIDv7) con confronto ordinale: è la modalità
        // corretta per sfruttare l'ordinamento temporale di UUIDv7 (Guid.CompareTo non è
        // affidabile a questo scopo, perché confronta alcuni campi come interi con segno).
        var files = Directory.EnumerateFiles(_storageDirectory, "*.json")
            .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.Ordinal);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            patterns.Add(_serializer.Deserialize(json));
        }

        return patterns;
    }

    public async Task<Pattern?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
        {
            return null;
        }

        // Legge esclusivamente il file corrispondente all'Id richiesto, senza caricare
        // l'intero archivio dei pattern.
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return _serializer.Deserialize(json);
    }

    public async Task<bool> CreateAsync(Pattern pattern, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var path = PathFor(pattern.Id);

        if (File.Exists(path))
        {
            return false;
        }

        // Le date sono responsabilità del livello di persistenza, non dell'editor: aprire
        // un pattern e annullare non deve alterarle. Alla creazione le due date coincidono.
        pattern.CreatedAt = _clock();
        pattern.ModifiedAt = pattern.CreatedAt;

        await File.WriteAllTextAsync(path, _serializer.Serialize(pattern), cancellationToken);
        return true;
    }

    public async Task<bool> UpdateAsync(Pattern pattern, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var path = PathFor(pattern.Id);

        if (!File.Exists(path))
        {
            return false;
        }

        // La data di creazione appartiene al documento già salvato: il client potrebbe
        // inviarne una diversa (o non inviarla affatto), ma non deve poterla riscrivere.
        var existing = await GetByIdAsync(pattern.Id, cancellationToken);
        if (existing is not null)
        {
            pattern.CreatedAt = existing.CreatedAt;
        }

        pattern.ModifiedAt = _clock();

        // Aggiorna esclusivamente il file corrispondente: nessun altro pattern viene toccato.
        await File.WriteAllTextAsync(path, _serializer.Serialize(pattern), cancellationToken);
        return true;
    }

    public async Task<bool> ReplaceAsync(Pattern pattern, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var path = PathFor(pattern.Id);
        if (!File.Exists(path))
        {
            return false;
        }

        // Nessun orologio, nessuna data riscritta: il documento va su disco esattamente
        // come lo si è ricevuto.
        await File.WriteAllTextAsync(path, _serializer.Serialize(pattern), cancellationToken);
        return true;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        return Task.FromResult(true);
    }
}
