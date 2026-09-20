using PatternEditor.Core.Models;

namespace PatternEditor.Sample.Api.Persistence;

/// <summary>
/// Astrazione della persistenza dei pattern. Isola l'accesso allo storage (in questo
/// progetto di test: un file JSON per pattern) dai minimal API endpoints e dal resto
/// dell'applicazione, così da poter sostituire lo storage (database, cloud, ecc.) senza
/// impatti sul resto della soluzione.
/// </summary>
public interface IPatternRepository
{
    Task<IReadOnlyList<Pattern>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Pattern?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <returns>false se esiste già un pattern con lo stesso Id.</returns>
    Task<bool> CreateAsync(Pattern pattern, CancellationToken cancellationToken = default);

    /// <returns>false se non esiste alcun pattern con l'Id indicato.</returns>
    Task<bool> UpdateAsync(Pattern pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Riscrive un documento <b>senza</b> segnare una modifica: le date restano quelle che
    /// erano.
    /// </summary>
    ///
    /// <remarks>
    /// Non è un secondo <c>UpdateAsync</c> ed è bene che si chiami in un altro modo: serve
    /// alla manutenzione, dove si corregge un campo di servizio su un archivio intero.
    /// Passare da <c>UpdateAsync</c> segnerebbe quattrocento pattern come modificati oggi,
    /// distruggendo l'ordinamento per ultima modifica e un'informazione che non si recupera.
    /// </remarks>
    /// <returns>false se non esiste alcun pattern con l'Id indicato.</returns>
    Task<bool> ReplaceAsync(Pattern pattern, CancellationToken cancellationToken = default);

    /// <returns>false se il file non esisteva.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
