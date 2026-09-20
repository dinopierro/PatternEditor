namespace PatternEditor.Core.Models;

/// <summary>
/// Elemento che possiede un'opacità complessiva, distinta da quelle del riempimento e del
/// bordo.
/// </summary>
///
/// <remarks>
/// <para>
/// È un'interfaccia e non una proprietà della classe base per una ragione precisa: ogni
/// modello concreto <b>dichiara già</b> la propria <c>Opacity</c>. Spostarla sulla base
/// farebbe sì che un plugin compilato prima — che la dichiara ancora — ne nasconda una
/// omonima ereditata, e il serializzatore, trovandone due con lo stesso nome, solleverebbe
/// un'eccezione all'apertura del documento. La promessa del progetto è che un plugin
/// staccato continui a funzionare: un'interfaccia la mantiene, una proprietà sulla base no.
/// </para>
/// <para>
/// Chi la implementa dichiara: «la mia opacità complessiva la mostra l'editor principale,
/// nella sezione comune a tutti i tipi». Un plugin che non la implementa continua a
/// mostrarla per conto proprio, e nessuna delle due cose disturba l'altra.
/// </para>
/// </remarks>
public interface IOverallOpacity
{
    /// <summary>Opacità dell'elemento intero, da 0 (invisibile) a 1 (pieno).</summary>
    double Opacity { get; set; }
}
