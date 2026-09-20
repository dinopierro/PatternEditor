namespace PatternEditor.Sample.Api;

/// <summary>
/// Rappresentazione sintetica di un pattern per la pagina di gestione: quanto basta a
/// riconoscerlo e a capire com'è fatto, senza trasferire l'intera definizione.
/// </summary>
/// <param name="PreviewSvg">Anteprima già pronta, resa con lo stesso renderer dell'editor.</param>
/// <param name="ElementTypes">
/// Tipi tecnici degli elementi che compongono la cella, in ordine e senza ripetizioni.
/// Il client li traduce in icone ed etichette tramite il proprio registro dei plugin: qui
/// viaggiano i type, non i nomi visualizzati, che sono testo per l'utente.
/// </param>
/// <param name="OwnerId">
/// L'autore, oppure <c>null</c> per un pattern che non ne ha — perché è più vecchio degli
/// account. Il client se ne serve per due cose: mostrare chi l'ha fatto e sapere in anticipo
/// se il pulsante «Conferma» avrà senso, invece di scoprirlo da un 403 dopo il lavoro.
/// </param>
/// <param name="OwnerName">
/// Il nome dell'autore, letto dal documento stesso.
/// </param>
/// <param name="SizeBytes">
/// Quanto pesa il documento SVG, in byte.
///
/// <para>
/// È la misura di <see cref="PreviewSvg"/>, che è lo stesso documento che si ottiene
/// scaricando il pattern a meno di due byte negli attributi di dimensione
/// (<c>100%</c> invece di <c>100vw</c>). Si conta qui perché il documento è già stato
/// prodotto per l'anteprima: farlo generare al client significherebbe spedirgli le
/// definizioni complete di quattrocento pattern per contare dei byte.
/// </para>
/// </param>
/// <param name="Visibility">
/// Lo stato di pubblicazione, per nome (<c>Privata</c>, <c>InAttesa</c>, <c>Pubblica</c>).
/// Viaggia come testo e non come numero per la stessa ragione per cui ci viaggia nei
/// documenti: un valore inserito in mezzo all'enumerazione cambierebbe il significato di
/// tutto quello che è già stato scritto.
/// </param>
/// <param name="OwnerColor">
/// Il colore del suo cerchio, e <see cref="OwnerHasPhoto"/> se al posto delle iniziali va
/// mostrata una foto. Sono gli unici due dati che il documento non può contenere, perché
/// appartengono all'account e cambiano quando l'utente li cambia: si risolvono una volta per
/// autore e non una per riga.
/// </param>
public sealed record PatternSummaryDto(
    Guid Id,
    string Name,
    string PreviewSvg,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    double Width,
    double Height,
    int ElementCount,
    IReadOnlyList<string> ElementTypes,
    Guid? OwnerId,
    string? OwnerName,
    string? OwnerColor,
    bool OwnerHasPhoto,
    int OwnerPhotoVersion,
    string Visibility,
    int SizeBytes);
