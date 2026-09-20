using PatternEditor.Core.Models;

namespace PatternEditor.Services;

/// <summary>
/// Crea nuovi pattern già pronti all'uso.
///
/// <para>
/// Esiste come servizio, e non come costruttore o metodo statico, per due ragioni. La prima
/// è che i valori iniziali sono una decisione di prodotto e non del modello: un'applicazione
/// ospitante che lavori su celle da 200 unità può sostituire questa implementazione senza
/// toccare né il modello né l'editor. La seconda è che così l'editor riceve la fabbrica per
/// iniezione e resta verificabile: un test può fargli produrre un pattern noto.
/// </para>
/// </summary>
public interface IPatternFactory
{
    /// <summary>
    /// Nuovo pattern con identificativo appena generato, nome vuoto, nessun elemento e
    /// trasformazione neutra. È già valido: si può confermare così com'è.
    /// </summary>
    Pattern CreateNew();
}

/// <summary>
/// Implementazione predefinita: cella di 50×50, trasformazione neutra, nessun elemento.
///
/// <para>
/// La cella quadrata da 50 non è un numero magico: è grande abbastanza perché le coordinate
/// degli elementi restino cifre tonde e a una cifra o due, e piccola abbastanza perché la
/// ripetizione si veda subito nell'anteprima. Una cella da 500 mostrerebbe un solo motivo
/// nel riquadro, e non si capirebbe che si sta componendo un pattern.
/// </para>
///
/// <para>
/// La trasformazione parte neutra (scala 1, nessuna rotazione, nessuna traslazione) perché
/// il primo lavoro è disporre gli elementi nella cella: la trasformazione si regola dopo,
/// quando c'è qualcosa da trasformare.
/// </para>
/// </summary>
public sealed class PatternFactory : IPatternFactory
{
    public Pattern CreateNew() => new()
    {
        Name = string.Empty,
        Definition =
        {
            Width = 50,
            Height = 50,
            Scale = 1.0,
            Rotation = 0,
            TranslateX = 0,
            TranslateY = 0,
        },
    };
}
