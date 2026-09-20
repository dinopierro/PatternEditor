namespace PatternEditor.Core.Models;

/// <summary>
/// Entità Pattern: rappresenta un pattern SVG persistibile e riutilizzabile.
///
/// L'Id è un UUIDv7, assegnato alla creazione, immutabile e non modificabile dall'utente.
/// Name è invece descrittivo e modificabile. Definition contiene tutti i dati necessari
/// a rappresentare graficamente il pattern.
/// </summary>
public sealed class Pattern
{
    /// <summary>
    /// Versione del contratto dati del Pattern (non la versione dell'applicazione o dei plugin).
    /// Consente in futuro di introdurre modifiche incompatibili al formato senza dover
    /// interpretare tutti i documenti esistenti come appartenenti implicitamente
    /// alla versione più recente.
    /// </summary>
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>
    /// Identifica univocamente il Pattern. UUIDv7, assegnato alla creazione, immutabile.
    /// Non dipende dal nome del Pattern.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Nome descrittivo del Pattern, modificabile dall'utente e persistito nel JSON.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Data e ora di creazione (UTC). Impostata alla creazione e mai più modificata.
    ///
    /// Per i pattern salvati prima dell'introduzione di questo dato viene ricostruita dal
    /// timestamp contenuto nell'Id UUIDv7 (vedi <see cref="Uuid7"/>).
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Data e ora dell'ultimo salvataggio (UTC). È gestita dal livello di persistenza
    /// dell'applicazione host, non dall'editor: aprire un pattern, modificarlo e poi
    /// annullare non deve cambiarla. Coincide con <see cref="CreatedAt"/> finché il
    /// pattern non viene modificato dopo la creazione.
    /// </summary>
    public DateTimeOffset ModifiedAt { get; set; }

    /// <summary>
    /// Chi ha scritto questo pattern, se l'applicazione ospite tiene traccia degli autori.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// La libreria <b>trasporta</b> questo dato e non lo interpreta: non sa che cosa sia un
    /// utente, non verifica niente e non impedisce niente. A decidere chi può scrivere è
    /// l'ospite, ed è l'unico che può farlo davvero, perché è l'unico che sta su un server.
    /// </para>
    /// <para>
    /// Sta nel documento e non in un indice a parte perché l'autore è parte di ciò che il
    /// documento racconta: accompagna il file quando lo si esporta, lo si copia o lo si
    /// sposta, invece di restare indietro in una tabella che nessuno si ricorda di portarsi
    /// dietro. Un ospite che di utenti non sa niente lo lascia vuoto, e non se ne accorge.
    /// </para>
    /// </remarks>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Il nome dell'autore al momento del salvataggio.
    ///
    /// <para>
    /// Viaggia accanto all'identificativo per evitare una ricerca per ogni riga di un
    /// elenco. È una copia, e come ogni copia potrebbe invecchiare: regge perché il nome
    /// utente, per scelta, non si cambia.
    /// </para>
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    /// Chi può vedere questo pattern.
    ///
    /// <para>
    /// Vale la stessa regola dell’autore: la libreria lo trasporta e non lo interpreta. Nasce
    /// <see cref="PatternVisibility.Privata"/> perché un valore predefinito che non richiede
    /// approvazione renderebbe inutile l’approvazione: chi dimentica di scegliere pubblica, e
    /// un difetto che dimentica di scrivere il campo pubblica anche lui.
    /// </para>
    /// </summary>
    public PatternVisibility Visibility { get; set; } = PatternVisibility.Privata;

    /// <summary>
    /// Definizione grafica completa del Pattern.
    /// </summary>
    public PatternDefinition Definition { get; init; } = new();

    public Pattern()
    {
        Id = Guid.CreateVersion7();

        // La data di creazione viene dal timestamp dell'UUIDv7 appena generato, così che
        // Id e CreatedAt non possano mai raccontare due storie diverse.
        CreatedAt = Uuid7.TryGetCreationTime(Id, out var created) ? created : DateTimeOffset.UtcNow;
        ModifiedAt = CreatedAt;
    }

    /// <summary>
    /// Costruttore per la ricostruzione di un Pattern esistente (es. da deserializzazione),
    /// dove l'Id non deve essere rigenerato.
    /// </summary>
    public Pattern(
        Guid id,
        string name,
        PatternDefinition definition,
        int version = CurrentVersion,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? modifiedAt = null,
        Guid? authorId = null,
        string? authorName = null,
        PatternVisibility visibility = PatternVisibility.Privata)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'Id di un Pattern non può essere Guid.Empty.", nameof(id));
        }

        Id = id;
        Name = name;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Version = version;

        // Documento privo delle date (formato precedente): la creazione si ricava dall'Id.
        CreatedAt = createdAt ?? DefaultCreatedAtFor(id);
        ModifiedAt = modifiedAt ?? CreatedAt;

        AuthorId = authorId;
        AuthorName = authorName;
        Visibility = visibility;
    }

    /// <summary>
    /// Data di creazione da usare quando il documento non la contiene: quella incisa
    /// nell'UUIDv7 e, solo per un Id che non sia un UUIDv7 (caso possibile unicamente con
    /// un file scritto a mano), l'istante corrente.
    /// </summary>
    public static DateTimeOffset DefaultCreatedAtFor(Guid id) =>
        Uuid7.TryGetCreationTime(id, out var created) ? created : DateTimeOffset.UtcNow;
}
