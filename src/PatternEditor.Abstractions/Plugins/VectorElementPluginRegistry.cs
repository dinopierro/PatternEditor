namespace PatternEditor.Abstractions.Plugins;

/// <summary>
/// Registro dei plugin disponibili: l'unico posto in cui l'applicazione dichiara quali tipi
/// di elemento esistono.
///
/// <para>
/// Il ciclo di vita è nettamente diviso in due fasi. Durante l'avvio l'applicazione ospitante
/// chiama <see cref="Register"/> una volta per plugin; da lì in poi il registro viene solo
/// letto. Il dizionario non è sincronizzato ed è corretto così: scrivere dopo l'avvio non è
/// previsto, e proteggere ogni lettura con un lucchetto costerebbe a ogni rendering
/// dell'interfaccia per un'eventualità che non si verifica.
/// </para>
///
/// <para>
/// Il confronto fra identificativi è <see cref="StringComparer.Ordinal"/>, non quello
/// dipendente dalla cultura: "rect" deve valere "rect" ovunque, e le regole di confronto di
/// alcune lingue riservano sorprese (in turco, la I maiuscola non è la maiuscola di i).
/// </para>
/// </summary>
public sealed class VectorElementPluginRegistry : IVectorElementPluginRegistry
{
    private readonly Dictionary<string, IVectorElementPlugin> _plugins = new(StringComparer.Ordinal);

    /// <summary>
    /// Tutti i plugin registrati, nell'ordine in cui sono stati registrati. L'ordine conta:
    /// è quello in cui compaiono nel menu di inserimento, e lo decide l'applicazione
    /// ospitante scrivendo le righe di registrazione nell'ordine che preferisce.
    /// </summary>
    public IReadOnlyCollection<IVectorElementPlugin> All => _plugins.Values;

    /// <summary>
    /// Registra un plugin. Un identificativo duplicato è un errore di configurazione e viene
    /// segnalato subito, all'avvio: lasciar vincere l'ultimo registrato produrrebbe
    /// un'applicazione che funziona quasi, con un tipo di elemento gestito da un plugin che
    /// nessuno si aspetta.
    /// </summary>
    public void Register(IVectorElementPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (string.IsNullOrWhiteSpace(plugin.Type))
        {
            throw new ArgumentException("Il plugin deve dichiarare un Type non vuoto.", nameof(plugin));
        }

        if (!_plugins.TryAdd(plugin.Type, plugin))
        {
            throw new InvalidOperationException(
                $"Esiste già un plugin registrato con Type = \"{plugin.Type}\".");
        }
    }

    /// <summary>
    /// Cerca il plugin di un tipo. Restituisce <c>false</c> invece di sollevare un'eccezione
    /// perché il tipo sconosciuto **non è un errore**: è il caso normale di un documento
    /// scritto da un'installazione con più plugin di questa, e va gestito conservando
    /// l'elemento così com'è (vedi <c>UnknownVectorElement</c>).
    /// </summary>
    public bool TryGet(string type, out IVectorElementPlugin? plugin)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            plugin = null;
            return false;
        }

        return _plugins.TryGetValue(type, out plugin);
    }
}
