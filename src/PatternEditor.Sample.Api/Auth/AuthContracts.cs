namespace PatternEditor.Sample.Api.Auth;

/// <summary>Registrazione di un nuovo utente.</summary>
public sealed record RegistrazioneRichiesta(
    string? Username,
    string? Password,
    string? SecurityQuestion,
    string? SecurityAnswer,
    string? AvatarColor);

/// <summary>Accesso.</summary>
public sealed record AccessoRichiesta(string? Username, string? Password);

/// <summary>Primo passo del recupero: qual è la mia domanda.</summary>
public sealed record DomandaRichiesta(string? Username);

/// <summary>Secondo passo: la risposta e la nuova password.</summary>
public sealed record RecuperoRichiesta(string? Username, string? Answer, string? NewPassword);

/// <summary>Cambio password da dentro il profilo, con la password attuale a garanzia.</summary>
public sealed record CambioPasswordRichiesta(string? CurrentPassword, string? NewPassword);

/// <summary>Modifica del profilo. Per ora soltanto il colore delle iniziali.</summary>
public sealed record ProfiloRichiesta(string? AvatarColor);

/// <summary>
/// Esito di un accesso riuscito: il gettone e chi si è.
///
/// <para>
/// I due viaggiano insieme perché sono la stessa notizia: separarli costringerebbe il
/// client a una seconda chiamata subito dopo, per sapere come disegnare l'avatar di chi ha
/// appena fatto l'accesso.
/// </para>
/// </summary>
public sealed record SessioneApertaRisposta(string Token, DateTimeOffset Scadenza, AccountPubblico User);

/// <summary>La domanda di recupero, in chiaro: è un promemoria, non un segreto.</summary>
public sealed record DomandaRisposta(string Question);

/// <summary>
/// Un errore con un testo destinato a essere letto da una persona.
///
/// <para>
/// Tutti gli errori dell'autenticazione hanno questa forma, così il client ha un solo caso
/// da gestire invece di distinguere fra un testo semplice e un oggetto a seconda
/// dell'endpoint.
/// </para>
/// </summary>
public sealed record ErroreRisposta(string Message);
