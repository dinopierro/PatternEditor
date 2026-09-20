using PatternEditor.Core.Models;

namespace PatternEditor.Core.Tests;

/// <summary>
/// Elemento fittizio per i test del livello comune.
///
/// <para>
/// Serve perché <c>VectorElement</c> è astratto e il progetto Core non conosce alcun tipo
/// concreto: usare qui un elemento vero — un rettangolo, poniamo — creerebbe una dipendenza
/// dal basso verso l'alto, esattamente quella che l'architettura vieta. Il tipo viene
/// passato come stringa proprio per poter simulare anche gli elementi sconosciuti.
/// </para>
/// </summary>
public sealed class FakeVectorElement : VectorElement
{
    public override string Type { get; }

    public FakeVectorElement(Guid id, string type) : base(id)
    {
        Type = type;
    }

    public FakeVectorElement(string type) : this(Guid.CreateVersion7(), type)
    {
    }
}
