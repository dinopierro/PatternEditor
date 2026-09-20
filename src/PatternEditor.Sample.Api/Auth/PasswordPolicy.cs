using System.Globalization;

namespace PatternEditor.Sample.Api.Auth;

/// <summary>
/// Che cosa si accetta come password e come nome utente.
/// </summary>
///
/// <remarks>
/// <para>
/// La regola è la <b>lunghezza</b>, non la composizione. È un cambio di rotta rispetto a
/// quello che molti si aspettano ancora — «almeno una maiuscola, un numero e un simbolo» —
/// e viene dalle linee guida NIST SP 800-63B: obbligare a mescolare i caratteri produce
/// «Password1!», che è corta, prevedibile e difficile da ricordare, cioè il peggior
/// risultato possibile. Dodici caratteri liberi valgono molto di più, e permettono una frase.
/// </para>
/// <para>
/// Restano tre divieti, e ciascuno chiude una porta vera: le password più usate al mondo,
/// che sono la prima cosa che prova chiunque; il nome utente dentro la password, che è la
/// seconda; e gli spazi ai bordi, che nessuno sa di aver scritto e che rendono impossibile
/// riscrivere la password la volta dopo.
/// </para>
/// </remarks>
public static class PasswordPolicy
{
    public const int LunghezzaMinima = 12;

    /// <summary>
    /// Un tetto serve: PBKDF2 lavora su tutto quello che riceve, e una password di un
    /// megabyte sarebbe un modo gratuito per occupare il processore del server.
    /// </summary>
    public const int LunghezzaMassima = 256;

    public const int UtenteMinimo = 3;
    public const int UtenteMassimo = 32;

    /// <summary>
    /// Le più prevedibili in assoluto, comprese le varianti che nascono proprio dalle
    /// vecchie regole di composizione. Non è un dizionario — quello sta su un server vero —
    /// ma copre ciò che si sceglie quando si vuole solo superare il controllo.
    /// </summary>
    private static readonly string[] Proibite =
    [
        "password", "passw0rd", "password1", "password123", "password1234",
        "qwertyuiop", "qwerty123456", "123456789012", "1234567890123",
        "abcdefghijkl", "letmeinplease", "iloveyou1234", "administrator",
        "amministratore", "benvenuto123", "cambiami123", "passwordlunga",
    ];

    /// <summary>
    /// Verifica una password. Restituisce <c>null</c> se va bene, altrimenti il motivo — uno
    /// solo, quello che si incontra per primo: un elenco di cinque regole violate non aiuta
    /// nessuno a scriverne una buona.
    /// </summary>
    public static string? Controlla(string? password, string? nomeUtente = null)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "La password non può essere vuota.";
        }

        if (password.Length != password.Trim().Length)
        {
            return "La password non può iniziare o finire con uno spazio: sarebbe invisibile e impossibile da riscrivere.";
        }

        if (password.Length < LunghezzaMinima)
        {
            return $"La password deve essere lunga almeno {LunghezzaMinima} caratteri. Una frase che ricordi va benissimo: conta la lunghezza, non i simboli strani.";
        }

        if (password.Length > LunghezzaMassima)
        {
            return $"La password non può superare i {LunghezzaMassima} caratteri.";
        }

        var minuscola = password.ToLowerInvariant();

        if (Array.Exists(Proibite, p => minuscola == p))
        {
            return "Questa password è fra le più usate al mondo ed è la prima che verrebbe provata. Scegline un'altra.";
        }

        if (!string.IsNullOrWhiteSpace(nomeUtente)
            && nomeUtente.Length >= 3
            && minuscola.Contains(nomeUtente.ToLowerInvariant(), StringComparison.Ordinal))
        {
            return "La password non può contenere il nome utente: chi conosce il primo avrebbe già mezza password.";
        }

        // Una sola lettera ripetuta, o due alternate: la lunghezza da sola non basta a
        // rendere imprevedibile «aaaaaaaaaaaa».
        if (CaratteriDistinti(password) < 5)
        {
            return "La password usa troppi pochi caratteri diversi: allungare la stessa lettera non la rende più difficile.";
        }

        return null;
    }

    /// <summary>
    /// Verifica un nome utente: solo lettere non accentate, cifre, punto, trattino e
    /// trattino basso.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Il limite all'alfabeto <b>latino di base</b> non è pigrizia verso le altre lingue: è
    /// la difesa contro gli omografi. «а» cirillica e «a» latina sono due caratteri diversi
    /// che sullo schermo sono lo stesso segno, e con un alfabeto aperto si potrebbero
    /// registrare due account indistinguibili a vista — uno dei quali firmerebbe pattern
    /// spacciandosi per l'altro senza indovinare nemmeno una password.
    /// </para>
    /// <para>
    /// Il controllo dell'unicità (§ archivio degli utenti) confronta i nomi in minuscolo, e
    /// il confronto fa quello che deve: distingue caratteri diversi. È proprio per questo che
    /// a doverli escludere è la regola sull'alfabeto, non il confronto.
    /// </para>
    /// <para>
    /// Il prezzo è che un nome in cirillico o in greco non si può registrare. È un prezzo
    /// accettabile per un identificativo tecnico che compare accanto a ogni pattern: chi
    /// volesse mostrare un nome nella propria lingua avrebbe bisogno di un secondo campo,
    /// visualizzato e non usato per entrare.
    /// </para>
    /// </remarks>
    public static string? ControllaNomeUtente(string? nomeUtente)
    {
        if (string.IsNullOrWhiteSpace(nomeUtente))
        {
            return "Il nome utente non può essere vuoto.";
        }

        if (nomeUtente.Length < UtenteMinimo || nomeUtente.Length > UtenteMassimo)
        {
            return $"Il nome utente deve essere lungo fra {UtenteMinimo} e {UtenteMassimo} caratteri.";
        }

        foreach (var c in nomeUtente)
        {
            if (!Ammesso(c))
            {
                return "Il nome utente può contenere solo lettere non accentate (a–z), cifre, "
                     + "punto, trattino e trattino basso. Lettere di altri alfabeti sono escluse "
                     + "perché alcune sono indistinguibili da quelle latine.";
            }
        }

        if (!Alfanumerico(nomeUtente[0]))
        {
            return "Il nome utente deve iniziare con una lettera o una cifra.";
        }

        return null;
    }

    /// <summary>Lettera latina di base o cifra, senza passare dalle tabelle Unicode.</summary>
    private static bool Alfanumerico(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static bool Ammesso(char c) => Alfanumerico(c) || c is '.' or '_' or '-';

    /// <summary>
    /// La risposta alla domanda di sicurezza è una seconda password, e va trattata come
    /// tale: è l'unica strada per rientrare, quindi una risposta di tre lettere annullerebbe
    /// tutto il resto. Non si chiede però la stessa lunghezza: dev'essere qualcosa che si
    /// ricorda a distanza di mesi.
    /// </summary>
    public static string? ControllaRisposta(string? risposta)
    {
        if (string.IsNullOrWhiteSpace(risposta))
        {
            return "La risposta non può essere vuota.";
        }

        var pulita = Normalizza(risposta);

        if (pulita.Length < 4)
        {
            return "La risposta è troppo breve: è l'unica strada per rientrare, e una parola di tre lettere si indovina.";
        }

        return pulita.Length > 200 ? "La risposta non può superare i 200 caratteri." : null;
    }

    public static string? ControllaDomanda(string? domanda)
    {
        if (string.IsNullOrWhiteSpace(domanda))
        {
            return "La domanda di recupero non può essere vuota.";
        }

        var pulita = domanda.Trim();
        return pulita.Length is < 8 or > 200
            ? "La domanda di recupero deve essere lunga fra 8 e 200 caratteri."
            : null;
    }

    /// <summary>
    /// La forma in cui la risposta viene confrontata: senza maiuscole, senza spazi ai bordi
    /// e senza spazi doppi in mezzo.
    ///
    /// <para>
    /// A distanza di mesi nessuno riscrive «Via Garibaldi» esattamente come l'aveva scritta,
    /// e far fallire il recupero per una maiuscola significherebbe non averlo fatto. Si
    /// normalizza <b>prima</b> di calcolare l'hash, così la stessa regola vale alla
    /// registrazione e al recupero senza che nessuno debba ricordarsene.
    /// </para>
    /// </summary>
    public static string Normalizza(string risposta)
    {
        ArgumentNullException.ThrowIfNull(risposta);

        var pezzi = risposta.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', pezzi).ToLower(CultureInfo.InvariantCulture);
    }

    private static int CaratteriDistinti(string testo)
    {
        var visti = new HashSet<char>();
        foreach (var c in testo)
        {
            visti.Add(c);
        }

        return visti.Count;
    }
}
