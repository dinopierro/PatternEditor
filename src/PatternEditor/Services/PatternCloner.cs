using PatternEditor.Core.Models;

namespace PatternEditor.Services;

/// <summary>
/// Duplica un pattern intero.
///
/// <para>
/// Sta accanto a <see cref="IVectorElementCloner"/>, che duplica un singolo elemento, e ne
/// riusa il meccanismo: la copia degli elementi resta quindi agnostica rispetto ai tipi
/// concreti, e anche gli elementi non gestiti da alcun plugin
/// (<see cref="UnknownVectorElement"/>) vengono duplicati senza perdere nulla.
/// </para>
///
/// <para>
/// Il servizio vive nella libreria e non nell'applicazione di esempio perché duplicare un
/// pattern non è una funzione di quella pagina: è un'operazione sul modello, utile a
/// qualunque applicazione ospitante, e replicarla in ognuna significherebbe replicarne anche
/// le insidie — dimenticare un nuovo identificativo agli elementi, o condividere la stessa
/// istanza di definizione fra originale e copia.
/// </para>
/// </summary>
public interface IPatternCloner
{
    /// <summary>
    /// Copia profonda del pattern, con identificativi nuovi e il nome indicato.
    ///
    /// <para>
    /// Le date **non** vengono copiate: la copia è un pattern nuovo, e le sue date le
    /// assegna il livello di persistenza al momento della creazione. Copiarle produrrebbe
    /// un pattern che dichiara di esistere da prima di essere stato creato.
    /// </para>
    /// </summary>
    /// <param name="source">Pattern da duplicare. Non viene modificato.</param>
    /// <param name="name">Nome della copia.</param>
    Pattern Clone(Pattern source, string name);
}

/// <inheritdoc cref="IPatternCloner" />
public sealed class PatternCloner : IPatternCloner
{
    private readonly IVectorElementCloner _elementCloner;

    public PatternCloner(IVectorElementCloner elementCloner)
    {
        _elementCloner = elementCloner ?? throw new ArgumentNullException(nameof(elementCloner));
    }

    /// <inheritdoc />
    public Pattern Clone(Pattern source, string name)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(name);

        // Il costruttore senza argomenti genera un identificativo nuovo e allinea le date:
        // è il modo con cui il modello dichiara che questo è un pattern appena nato.
        //
        // L'autore non si copia, ed è la stessa ragione: la copia è un documento nuovo, e
        // chi la fa non eredita la paternità di chi l'ha disegnato. Chi salva lo deciderà.
        //
        // Per la stessa ragione la copia nasce privata: una
        // copia non eredita l'approvazione dell'originale. Sarebbe il modo più comodo per
        // aggirare la moderazione — far approvare un disegno e poi duplicarlo cambiandolo.
        var copy = new Pattern { Name = name };

        // I sei valori della definizione si copiano uno per uno invece di riusare l'istanza:
        // Definition è una proprietà "init" e assegnarla condividerebbe l'oggetto, con il
        // risultato che modificare la copia cambierebbe anche l'originale.
        copy.Definition.Width = source.Definition.Width;
        copy.Definition.Height = source.Definition.Height;
        copy.Definition.Scale = source.Definition.Scale;
        copy.Definition.Rotation = source.Definition.Rotation;
        copy.Definition.TranslateX = source.Definition.TranslateX;
        copy.Definition.TranslateY = source.Definition.TranslateY;

        // Il filtro si copia in profondità per lo stesso motivo della definizione: condividere
        // l'istanza farebbe sì che spostare un cursore sulla copia cambi anche l'originale, e
        // il difetto si scoprirebbe solo aprendo l'altro pattern.
        copy.Definition.Filter = source.Definition.Filter?.Copia();

        // L'ordine degli elementi è l'ordine di disegno e va conservato: un duplicato con
        // gli elementi rimescolati sarebbe un altro disegno.
        foreach (var element in source.Definition.Elements)
        {
            copy.Definition.Elements.Add(_elementCloner.Clone(element));
        }

        return copy;
    }
}
