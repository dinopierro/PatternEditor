namespace PatternEditor.Core.Models;

/// <summary>
/// Contratto base comune a tutti gli elementi vettoriali contenuti in un <see cref="PatternDefinition"/>.
///
/// Contiene esclusivamente le informazioni necessarie alla gestione generica dell'elemento
/// (identità dell'istanza e tipo tecnico). Le proprietà geometriche e grafiche specifiche
/// (X, Y, Width, Stroke, ecc.) NON devono comparire qui: appartengono ai modelli concreti
/// definiti da ciascun plugin (es. LineElement, RectElement).
/// </summary>
public abstract class VectorElement
{
    /// <summary>
    /// Identificativo univoco dell'istanza dell'elemento all'interno del Pattern.
    /// Distinto da <see cref="Type"/>, che identifica invece la tipologia dell'elemento.
    /// Immutabile per la durata della sessione di editing.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Identificativo tecnico stabile del tipo di elemento (es. "line", "rect").
    /// Utilizzato come discriminatore nel JSON persistito e per la risoluzione del plugin
    /// tramite il Plugin Registry. Deve rimanere indipendente dal nome della classe C#,
    /// dal namespace e dall'assembly che implementa l'elemento.
    /// </summary>
    public abstract string Type { get; }

    /// <summary>
    /// Rotazione dell'elemento in gradi, attorno al punto <see cref="OriginX"/>,
    /// <see cref="OriginY"/>. Zero significa nessuna rotazione.
    ///
    /// <para>
    /// Sta qui e non nei modelli dei plugin — che ospitano solo le proprietà specifiche del
    /// tipo — perché ruotare non è una proprietà del rettangolo o del cerchio: è
    /// un'operazione che vale per qualunque forma, e il disegno la ottiene allo stesso modo
    /// per tutte. Il plugin continua a produrre il proprio tag senza saperne niente: la
    /// trasformazione la applica chi compone il documento.
    /// </para>
    /// </summary>
    public double Rotation { get; set; }

    /// <summary>Specchia l'elemento rispetto all'asse verticale passante per l'origine.</summary>
    public bool FlipX { get; set; }

    /// <summary>Specchia l'elemento rispetto all'asse orizzontale passante per l'origine.</summary>
    public bool FlipY { get; set; }

    /// <summary>
    /// Ascissa del punto attorno a cui l'elemento ruota e si specchia.
    ///
    /// <para>
    /// Il punto è esplicito e non ricavato dall'ingombro della forma, per due ragioni.
    /// La prima è che l'ingombro lo conosce solo il plugin, e chiederglielo allargherebbe il
    /// contratto per una funzione che non lo richiede. La seconda è che, nei retini, ruotare
    /// attorno a un punto scelto — il centro della cella, un angolo — è proprio ciò che
    /// serve: un'ipotesi automatica andrebbe poi corretta a mano quasi sempre.
    /// </para>
    /// </summary>
    public double OriginX { get; set; }

    /// <summary>Ordinata del punto attorno a cui l'elemento ruota e si specchia.</summary>
    public double OriginY { get; set; }

    /// <summary>
    /// Vero quando l'elemento non è né ruotato né specchiato, e quindi non ha bisogno di
    /// alcuna trasformazione nel documento generato.
    /// </summary>
    public bool HasTransform => Rotation % 360 != 0 || FlipX || FlipY;

    protected VectorElement(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'Id di un VectorElement non può essere Guid.Empty.", nameof(id));
        }

        Id = id;
    }
}
