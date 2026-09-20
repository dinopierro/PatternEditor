using System.Text.Json.Serialization;
using PatternEditor.Core.Localization;
namespace PatternEditor.Core.Models.Filters;

/// <summary>
/// Una primitiva di filtro SVG: un passaggio della catena che trasforma l'immagine.
///
/// <para>
/// La specifica chiama «filter primitive» ciascuno dei nodi <c>fe*</c> che possono stare
/// dentro un <c>&lt;filter&gt;</c>. Non sono effetti finiti ma operazioni elementari — sfoca,
/// sposta, mescola due immagini, rimappa i colori — e un effetto riconoscibile nasce quasi
/// sempre da due o tre di esse messe in fila.
/// </para>
///
/// <para>
/// <b>Come si collegano.</b> Ogni primitiva ha un ingresso (<see cref="In"/>) e può dare un
/// nome al proprio risultato (<see cref="Result"/>). Lasciandoli vuoti la specifica prevede
/// il comportamento che serve quasi sempre: la prima primitiva riceve l'immagine di partenza
/// e ciascuna delle successive riceve il risultato di quella che la precede — una catena.
/// I due campi esistono per quando la catena non basta: un'ombra, per esempio, va poi
/// rimessa <i>sotto</i> il disegno originale, e per farlo bisogna poterlo nominare
/// (<c>SourceGraphic</c>).
/// </para>
/// </summary>
public abstract class FilterPrimitive
{
    /// <summary>
    /// Identificativo interno della primitiva. Non finisce nell'SVG: serve all'interfaccia
    /// per distinguere due righe identiche dell'elenco mentre le si riordina.
    /// </summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// Se falso la primitiva resta nel documento ma non viene generata.
    ///
    /// <para>
    /// Serve a provare: spegnere un passaggio e riaccenderlo è il modo in cui si capisce che
    /// cosa faccia davvero, e cancellarlo per poi riscriverlo a mano non è la stessa cosa.
    /// </para>
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Ingresso esplicito: il nome di un risultato precedente oppure una delle sorgenti
    /// previste dalla specifica (<c>SourceGraphic</c>, <c>SourceAlpha</c>,
    /// <c>BackgroundImage</c>, <c>BackgroundAlpha</c>, <c>FillPaint</c>, <c>StrokePaint</c>).
    /// Vuoto significa «quello che esce dal passaggio precedente».
    /// </summary>
    public string? In { get; set; }

    /// <summary>
    /// Nome con cui questo risultato può essere richiamato più avanti. Vuoto significa che
    /// nessuno lo richiama per nome, e passa direttamente al passaggio successivo.
    /// </summary>
    public string? Result { get; set; }

    /// <summary>Il nome del nodo SVG da generare: <c>feGaussianBlur</c>, <c>feBlend</c>, …</summary>
    [JsonIgnore]
    public abstract string SvgName { get; }

    /// <summary>
    /// Come si chiama questo passaggio per chi lo usa. Sta nel modello e non
    /// nell'interfaccia perché è la stessa parola ovunque compaia — elenco, menu di
    /// inserimento, messaggi del validatore — e tenerla in tre posti significa vederla
    /// divergere.
    /// </summary>
    [JsonIgnore]
    public abstract TestoNominato Titolo { get; }

    /// <summary>Il nome già scritto, in inglese.</summary>
    [JsonIgnore]
    public string Etichetta => Titolo.ToString();

    /// <summary>
    /// Una riga che riassume come è regolata questa primitiva, da mostrare accanto al nome
    /// nell'elenco: con sei passaggi chiusi, sapere quale sia «quello sfocato di 3» senza
    /// doverli riaprire uno per uno è la differenza fra un elenco e un indovinello.
    /// </summary>
    ///
    /// <remarks>
    /// Nome e valori restano separati fino all'ultimo. Il modello sa che cosa dire — quale
    /// caso, quali numeri — e non in che lingua: quella la decide chi mostra.
    /// </remarks>
    [JsonIgnore]
    public abstract TestoNominato Sintesi { get; }

    /// <summary>Il riassunto già scritto, in inglese.</summary>
    [JsonIgnore]
    public string Riassunto => Sintesi.ToString();

    /// <summary>
    /// Vero se la primitiva prende una <b>seconda</b> immagine in ingresso. Le tre che lo
    /// fanno la dichiarano qui invece di costringere chi legge a riconoscerle per tipo.
    /// </summary>
    [JsonIgnore]
    public virtual bool HaSecondoIngresso => false;

    /// <summary>
    /// Vero se l'effetto si estende <b>oltre</b> i pixel di partenza: sfocature, ombre,
    /// spostamenti, ispessimenti.
    ///
    /// <para>
    /// La distinzione non è accademica. Una primitiva che tocca solo il colore di ogni pixel
    /// dà lo stesso risultato ovunque la si applichi; una che sconfina ha bisogno di spazio
    /// attorno, e se non ce n'è viene tagliata. È il motivo per cui questo editor applica il
    /// filtro alla superficie intera e non alla singola tessera, e il motivo per cui l'area
    /// del filtro esiste ed è regolabile.
    /// </para>
    /// </summary>
    [JsonIgnore]
    public virtual bool Sconfina => false;

    /// <summary>
    /// Vero se la primitiva <b>genera</b> un'immagine invece di trasformarne una: il rumore e
    /// la tinta piena, che la specifica dichiara prive di ingresso.
    ///
    /// <para>
    /// La distinzione serve a riconoscere una trappola. Una primitiva senza ingresso esplicito
    /// riceve «il risultato del passaggio precedente», e se il passaggio precedente è un
    /// generatore quel risultato è l'immagine generata — non il disegno. Chi mette un rumore e
    /// subito dopo una fusione si ritrova a fondere il rumore con se stesso, e a schermo resta
    /// solo quello. Non è un errore per la specifica: è un difetto che si vede e non si
    /// spiega, e il validatore lo dice.
    /// </para>
    /// </summary>
    [JsonIgnore]
    public virtual bool Genera => false;

    /// <summary>
    /// Copia profonda della primitiva.
    ///
    /// <para>
    /// Parte da una copia campo per campo fatta dal runtime e poi chiede alla classe concreta
    /// di sdoppiare ciò che è per riferimento. Scritta al contrario — un metodo che elenca a
    /// mano le proprietà, in ognuna delle tredici classi — funzionerebbe finché qualcuno non
    /// aggiunge la quattordicesima proprietà a una di esse e si dimentica di questa riga:
    /// il difetto che ne nasce è una copia che condivide un valore con l'originale, e si
    /// scopre mesi dopo modificando un pattern e vedendone cambiare un altro.
    /// </para>
    /// </summary>
    public FilterPrimitive Copia()
    {
        var copia = (FilterPrimitive)MemberwiseClone();
        SdoppiaRiferimenti(copia);
        return copia;
    }

    /// <summary>
    /// Sostituisce nella copia i membri che <see cref="MemberwiseClone"/> ha condiviso invece
    /// di duplicare: liste e oggetti annidati. Chi non ne ha non implementa niente.
    /// </summary>
    protected virtual void SdoppiaRiferimenti(FilterPrimitive copia)
    {
    }
}
