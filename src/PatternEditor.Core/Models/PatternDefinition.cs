using PatternEditor.Core.Models.Filters;

namespace PatternEditor.Core.Models;

/// <summary>
/// Definizione grafica del Pattern: dimensioni della cella, trasformazione e collezione
/// ordinata degli elementi vettoriali che la compongono.
///
/// Il modello NON memorizza direttamente l'attributo SVG "patternTransform": la trasformazione
/// è rappresentata attraverso le singole proprietà Scale/Rotation/TranslateX/TranslateY.
/// La conversione verso patternTransform è responsabilità esclusiva del generatore SVG,
/// non di questo modello.
/// </summary>
public sealed class PatternDefinition
{
    public double Width { get; set; }

    public double Height { get; set; }

    public double Scale { get; set; } = 1.0;

    public double Rotation { get; set; }

    public double TranslateX { get; set; }

    public double TranslateY { get; set; }

    /// <summary>
    /// Collezione ORDINATA degli elementi vettoriali. L'ordine è significativo: l'elemento
    /// in posizione 0 viene generato per primo nell'SVG, e quelli successivi possono quindi
    /// risultare graficamente sopra i precedenti. L'ordine deve essere preservato durante
    /// editing, serializzazione, deserializzazione e generazione SVG.
    /// </summary>
    public List<VectorElement> Elements { get; init; } = new();

    /// <summary>
    /// Il filtro applicato alla superficie dipinta, oppure null quando non ce n'è nessuno.
    ///
    /// <para>
    /// È null e non un oggetto vuoto perché la distinzione conta nel documento salvato: un
    /// pattern senza filtro non deve portarsi dietro una sezione di valori predefiniti, e i
    /// quattrocento già in archivio devono continuare a leggersi e riscriversi identici.
    /// </para>
    ///
    /// <para>
    /// Come per la trasformazione, il modello non memorizza gli attributi SVG: descrive che
    /// cosa si vuole ottenere, e tradurlo in un nodo <c>&lt;filter&gt;</c> è compito
    /// esclusivo del generatore. Vedi <see cref="PatternFilter"/> per il motivo per cui il
    /// filtro si applica alla superficie e non alla singola tessera.
    /// </para>
    /// </summary>
    public PatternFilter? Filter { get; set; }
}
