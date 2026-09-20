namespace PatternEditor.Sample.Api.Auth;

/// <summary>
/// Un utente registrato, così come viene conservato sul server.
/// </summary>
///
/// <remarks>
/// <para>
/// Di questa classe <b>non esiste una versione con la password</b>, e non è una svista:
/// quello che arriva dal client vive per il tempo di calcolarne l'hash e poi sparisce.
/// Qui dentro restano solo verificatori — stringhe con cui si può dire «sì, era questa» ma
/// da cui non si può risalire a che cosa fosse.
/// </para>
/// <para>
/// Non esce mai da sola verso il client: c'è <see cref="AccountPubblico"/> per quello, ed è
/// fatto apposta per rendere impossibile spedire per sbaglio un hash.
/// </para>
/// </remarks>
public sealed class UserAccount
{
    public Guid Id { get; set; }

    /// <summary>Il nome come l'utente lo ha scritto, maiuscole comprese: è così che lo rivedrà.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Lo stesso nome in minuscolo, che è la forma su cui si cerca e si confronta.
    /// Conservarlo invece di ricalcolarlo serve a una cosa sola ma decisiva: garantisce che
    /// «Mario» e «mario» non possano coesistere come due account diversi.
    /// </summary>
    public string UsernameNormalizzato { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>La domanda la sceglie l'utente e la rivede in chiaro: è un promemoria, non un segreto.</summary>
    public string SecurityQuestion { get; set; } = string.Empty;

    /// <summary>La risposta invece è un segreto, e vale quanto la password.</summary>
    public string SecurityAnswerHash { get; set; } = string.Empty;

    /// <summary>Colore del cerchio con le iniziali, in forma #rrggbb.</summary>
    public string AvatarColor { get; set; } = "#4f46e5";

    /// <summary>
    /// Tipo dell'immagine caricata, oppure <c>null</c> se l'utente non ne ha una e va
    /// mostrato il cerchio con le iniziali.
    /// </summary>
    public string? AvatarContentType { get; set; }

    /// <summary>
    /// Quante volte la foto è stata scritta o tolta.
    /// </summary>
    ///
    /// <remarks>
    /// Serve a una cosa sola: far cambiare l'<b>indirizzo</b> della foto quando la foto
    /// cambia. L'immagine si serve con una cache lunga — è la stessa per giorni e ricaricarla
    /// a ogni scheda sarebbe uno spreco — e una cache lunga su un indirizzo fisso significa
    /// che chi ne carica una nuova continua a vedere la vecchia finché la cache non scade.
    /// Un numero che avanza nell'indirizzo risolve entrambe le cose: la cache resta lunga e
    /// la foto nuova si vede subito, perché per il browser è un'altra risorsa.
    /// </remarks>
    public int AvatarVersion { get; set; }

    /// <summary>
    /// Può approvare le pubblicazioni.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// È un campo dell'account e non un'utenza separata con una password sua, e la scelta
    /// merita una riga: una seconda coppia nome/password vorrebbe dire un secondo posto dove
    /// si calcolano hash, si contano tentativi e si aprono sessioni — cioè il doppio delle
    /// occasioni di sbagliare la parte che non si può sbagliare. Qui l'amministratore entra
    /// dalla stessa porta di tutti, con lo stesso blocco dopo otto tentativi e lo stesso
    /// limite di frequenza, e in più ha questo.
    /// </para>
    /// <para>
    /// Non si concede da nessun endpoint, di proposito: si assegna dalla riga di comando
    /// (<c>dotnet run -- amministratore &lt;nome&gt;</c>), cioè da chi ha accesso al server. Un
    /// permesso che si può chiedere via rete è un permesso che prima o poi qualcuno si
    /// prende.
    /// </para>
    /// </remarks>
    public bool IsAdmin { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>Tentativi falliti consecutivi, azzerati dal primo accesso riuscito.</summary>
    public int TentativiFalliti { get; set; }

    /// <summary>
    /// Fino a quando l'account non accetta accessi. Serve contro chi prova password una
    /// dopo l'altra: il limite di frequenza generale protegge il server, questo protegge
    /// il singolo utente preso di mira.
    /// </summary>
    public DateTimeOffset? BloccatoFinoA { get; set; }

    public AccountPubblico Pubblico() => new(
        Id,
        Username,
        AvatarColor,
        AvatarContentType is not null,
        AvatarVersion,
        CreatedAt,
        IsAdmin);
}

/// <summary>
/// Quello che il client può sapere di un account: chi è, come si disegna, da quando esiste.
/// Nessun hash, nessuna domanda di recupero, nessuna data di ultimo accesso.
/// </summary>
/// <param name="IsAdmin">
/// Se questo account modera le pubblicazioni. Esce verso il client perché serve a
/// <b>mostrare</b> la voce di menu: chi non modera non deve vedersi offrire una pagina che
/// gli verrebbe poi negata. Non è un permesso — quello lo verifica l'API a ogni richiesta,
/// ed è l'unico posto che un browser non può contraddire.
/// </param>
public sealed record AccountPubblico(
    Guid Id,
    string Username,
    string AvatarColor,
    bool HasPhoto,
    int PhotoVersion,
    DateTimeOffset CreatedAt,
    bool IsAdmin);
