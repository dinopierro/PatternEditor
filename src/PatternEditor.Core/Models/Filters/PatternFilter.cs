namespace PatternEditor.Core.Models.Filters;

/// <summary>
/// Il filtro applicato all'intera superficie dipinta dal pattern.
///
/// <para>
/// <b>Dove si applica, e perché lì.</b> Il filtro non sta dentro la tessera: sta sul
/// rettangolo che la ripete. La differenza non è formale. Una primitiva che sconfina —
/// sfocatura, ombra, ispessimento, distorsione — ha bisogno di leggere i pixel appena fuori
/// da ciò che sta elaborando; dentro una tessera quei pixel non esistono, l'elaborazione
/// viene tagliata al bordo e il taglio si ripete identico a ogni ripetizione, disegnando una
/// griglia di cuciture che nel motivo non c'è. Applicandolo alla superficie, la sfocatura
/// attraversa le giunzioni come attraverserebbe qualunque altro punto.
/// </para>
///
/// <para>
/// La conseguenza da conoscere è che il filtro accompagna il <i>documento</i>, non il nodo
/// <c>&lt;pattern&gt;</c>: chi estraesse quel nodo da solo per riusarlo altrove si
/// porterebbe via la geometria e lascerebbe indietro il filtro. È il prezzo di non avere
/// cuciture, e vale la pena pagarlo.
/// </para>
/// </summary>
public sealed class PatternFilter
{
    /// <summary>
    /// Se falso il filtro resta scritto nel documento ma non viene applicato.
    ///
    /// <para>
    /// È l'interruttore generale: confrontare il prima e il dopo è il modo in cui si decide
    /// se un effetto serve, e per farlo non si deve essere costretti a smontarlo.
    /// </para>
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Lo spazio colore in cui le primitive fanno i conti.
    ///
    /// <para>
    /// La specifica predefinisce <see cref="FilterColorSpace.LinearRgb"/>; qui il valore
    /// predefinito è <see cref="FilterColorSpace.Srgb"/> e viene sempre scritto
    /// esplicitamente. La ragione è che l'altro sorprende: una sfocatura in spazio lineare
    /// schiarisce visibilmente i bordi, e chi ha appena mosso un cursore attribuisce quella
    /// schiarita al cursore. Chi sa che cosa sta cercando può cambiarlo.
    /// </para>
    /// </summary>
    public FilterColorSpace ColorSpace { get; set; } = FilterColorSpace.Srgb;

    /// <summary>
    /// L'area su cui il filtro lavora, in percentuale del riquadro dell'oggetto filtrato
    /// (<c>filterUnits="objectBoundingBox"</c>). I valori predefiniti sono quelli della
    /// specifica: si parte dal 10% prima e si arriva al 10% dopo.
    ///
    /// <para>
    /// Si regola quando un effetto risulta tagliato: un'ombra spostata di venti pixel su una
    /// superficie di duecento esce dal 110% e viene troncata di netto.
    /// </para>
    /// </summary>
    public double X { get; set; } = -10;

    /// <inheritdoc cref="X" />
    public double Y { get; set; } = -10;

    /// <inheritdoc cref="X" />
    public double Width { get; set; } = 120;

    /// <inheritdoc cref="X" />
    public double Height { get; set; } = 120;

    /// <summary>
    /// I passaggi, nell'ordine in cui si applicano. Il primo riceve il disegno, ciascuno dei
    /// successivi riceve — se non dice altrimenti — il risultato del precedente.
    /// </summary>
    public List<FilterPrimitive> Primitives { get; init; } = [];

    /// <summary>
    /// Vero se il filtro, così com'è, produrrebbe un effetto: acceso e con almeno un
    /// passaggio acceso. Un filtro vuoto non va scritto nell'SVG — sarebbe un
    /// <c>&lt;filter&gt;</c> senza primitive, che secondo la specifica rende l'oggetto
    /// completamente trasparente, cioè farebbe sparire il disegno.
    /// </summary>
    public bool ProduceEffetto => Enabled && Primitives.Any(p => p.Enabled);

    /// <summary>
    /// Vero se almeno un passaggio acceso si estende oltre i pixel di partenza. Serve
    /// all'interfaccia per spiegare l'area del filtro solo a chi ne ha bisogno.
    /// </summary>
    public bool QualcunoSconfina => Primitives.Any(p => p.Enabled && p.Sconfina);

    /// <summary>Copia profonda: due pattern non devono mai condividere lo stesso filtro.</summary>
    public PatternFilter Copia()
    {
        var copia = new PatternFilter
        {
            Enabled = Enabled,
            ColorSpace = ColorSpace,
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
        };

        foreach (var primitiva in Primitives)
        {
            copia.Primitives.Add(primitiva.Copia());
        }

        return copia;
    }
}
