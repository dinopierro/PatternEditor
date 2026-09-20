namespace PatternEditor.Sample.Api;

/// <summary>
/// Che cosa l'autore chiede: <c>Privata</c> oppure <c>Pubblica</c>.
/// </summary>
///
/// <remarks>
/// <para>
/// È una stringa e non l'enumerazione, e la differenza conta. Legata direttamente al tipo,
/// una parola sconosciuta diventerebbe un 400 generato dal binding, con un testo che parla
/// di JSON a un utente che ha premuto un pulsante. Come stringa, a decidere è l'endpoint, e
/// può rispondere «visibilità non riconosciuta».
/// </para>
/// <para>
/// Chiedere <c>InAttesa</c> è ammesso e vale come chiedere <c>Pubblica</c>: è quello che
/// manderebbe indietro un client che rispedisce lo stato che ha appena letto.
/// </para>
/// </remarks>
public sealed record VisibilitaRichiesta(string? Visibility);

/// <summary>
/// Lo stato in cui il pattern si è effettivamente trovato dopo la richiesta.
/// </summary>
///
/// <remarks>
/// Non è l'eco di ciò che è stato chiesto: chi chiede «pubblica» si sente rispondere
/// «InAttesa», ed è esattamente l'informazione che serve al client per scrivere la frase
/// giusta. Rispondere con un 204 senza corpo costringerebbe l'interfaccia a indovinare la
/// regola del server, cioè a duplicarla.
/// </remarks>
public sealed record VisibilitaRisposta(string Visibility);
