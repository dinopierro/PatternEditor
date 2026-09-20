namespace PatternEditor.Abstractions.Plugins;

/// <summary>
/// Registro dei plugin di elemento vettoriale disponibili nell'applicazione host.
///
/// Il componente principale (PatternEditor) deve risolvere sempre i plugin attraverso
/// questa astrazione, senza mai referenziare direttamente Line, Rect o altri plugin
/// concreti. L'aggiunta futura di un nuovo elemento richiede unicamente la registrazione
/// del relativo plugin, senza modifiche alla logica principale.
/// </summary>
public interface IVectorElementPluginRegistry
{
    /// <summary>Tutti i plugin attualmente registrati.</summary>
    IReadOnlyCollection<IVectorElementPlugin> All { get; }

    /// <summary>Registra un plugin. Il suo <see cref="IVectorElementPlugin.Type"/> deve essere univoco nel registro.</summary>
    void Register(IVectorElementPlugin plugin);

    /// <summary>
    /// Tenta di risolvere il plugin corrispondente al type tecnico indicato.
    /// Restituisce false se nessun plugin è disponibile per quel type (elemento sconosciuto):
    /// in questo caso il chiamante non deve eliminare o trasformare l'elemento, ma preservarlo.
    /// </summary>
    bool TryGet(string type, out IVectorElementPlugin? plugin);
}
