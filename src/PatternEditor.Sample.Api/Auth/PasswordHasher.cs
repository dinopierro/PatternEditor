using System.Security.Cryptography;

namespace PatternEditor.Sample.Api.Auth;

/// <summary>
/// Trasforma una password in qualcosa che si può conservare senza conservare la password.
/// </summary>
///
/// <remarks>
/// <para>
/// PBKDF2 con HMAC-SHA256, sale casuale di 128 bit, chiave derivata di 256 bit. La scelta
/// non è originale ed è un pregio: è l'algoritmo che le linee guida OWASP indicano quando
/// non si vuole introdurre una dipendenza esterna, ed è disponibile nella libreria standard
/// senza aggiungere un solo pacchetto. Argon2id sarebbe migliore — resiste anche alle schede
/// grafiche, non solo al tempo — ma richiede una libreria di terze parti, e il formato qui
/// sotto è fatto apposta perché quel passaggio sia possibile domani senza toccare gli utenti
/// già registrati.
/// </para>
/// <para>
/// Il numero di iterazioni è scritto <b>dentro</b> l'hash. Serve a poterlo alzare quando le
/// macchine diventano più veloci: le password vecchie continuano a verificarsi con il
/// numero con cui furono scritte, e si rigenerano al primo accesso riuscito
/// (<see cref="NeedsUpgrade"/>). Senza questo, alzare il costo significherebbe invalidare
/// tutte le credenziali esistenti.
/// </para>
/// <para>
/// Il confronto finale è a <b>tempo costante</b>. Un confronto normale si ferma al primo
/// byte diverso, e il tempo che impiega racconta quanti byte erano giusti: ripetendo la
/// misura si ricostruisce l'hash un byte per volta.
/// </para>
/// </remarks>
public sealed class PasswordHasher
{
    /// <summary>
    /// Iterazioni predefinite: il valore raccomandato da OWASP per PBKDF2-HMAC-SHA256.
    /// Costa qualche decimo di secondo per verifica, ed è esattamente il punto: rende cara
    /// ogni singola prova a chi tenta milioni di password su un archivio rubato.
    /// </summary>
    public const int IterazioniPredefinite = 600_000;

    private const string Algoritmo = "pbkdf2-sha256";
    private const int ByteDelSale = 16;
    private const int ByteDellaChiave = 32;

    private readonly int _iterazioni;

    /// <param name="iterazioni">
    /// Iterazioni per i nuovi hash. Abbassarlo ha senso solo nei test, dove centinaia di
    /// registrazioni pagherebbero ciascuna il costo pensato per difendersi da un attacco.
    /// </param>
    public PasswordHasher(int iterazioni = IterazioniPredefinite)
    {
        if (iterazioni < 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(iterazioni), "Un numero di iterazioni così basso non protegge niente.");
        }

        _iterazioni = iterazioni;
    }

    /// <summary>
    /// Produce la stringa da conservare: <c>pbkdf2-sha256$iterazioni$sale$chiave</c>.
    ///
    /// <para>
    /// Ogni chiamata usa un sale nuovo, quindi la stessa password genera due stringhe
    /// diverse: è ciò che impedisce di riconoscere, guardando l'archivio, che due utenti
    /// hanno scelto la stessa password.
    /// </para>
    /// </summary>
    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var sale = RandomNumberGenerator.GetBytes(ByteDelSale);
        var chiave = Deriva(password, sale, _iterazioni);

        return string.Join('$',
            Algoritmo,
            _iterazioni.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToBase64String(sale),
            Convert.ToBase64String(chiave));
    }

    /// <summary>
    /// Verifica una password contro un hash conservato.
    ///
    /// <para>
    /// Un hash illeggibile — troncato, di un algoritmo che non conosciamo, scritto a mano —
    /// restituisce <c>false</c> e non un'eccezione: è un dato corrotto, non un guasto del
    /// programma, e il risultato giusto per chi tenta di accedere è comunque «no».
    /// </para>
    /// </summary>
    public bool Verifica(string password, string? hashConservato)
    {
        if (password is null || string.IsNullOrEmpty(hashConservato))
        {
            return false;
        }

        var pezzi = hashConservato.Split('$');
        if (pezzi.Length != 4 || pezzi[0] != Algoritmo)
        {
            return false;
        }

        if (!int.TryParse(pezzi[1], System.Globalization.NumberStyles.Integer,
                          System.Globalization.CultureInfo.InvariantCulture, out var iterazioni)
            || iterazioni < 1)
        {
            return false;
        }

        byte[] sale;
        byte[] atteso;
        try
        {
            sale = Convert.FromBase64String(pezzi[2]);
            atteso = Convert.FromBase64String(pezzi[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (sale.Length == 0 || atteso.Length == 0)
        {
            return false;
        }

        var calcolato = Deriva(password, sale, iterazioni, atteso.Length);
        return CryptographicOperations.FixedTimeEquals(calcolato, atteso);
    }

    /// <summary>
    /// L'hash è stato scritto con meno iterazioni di quante ne useremmo oggi, e conviene
    /// riscriverlo. Ha senso chiamarlo solo dopo una verifica riuscita, che è l'unico
    /// momento in cui la password in chiaro è disponibile per rigenerarlo.
    /// </summary>
    public bool NeedsUpgrade(string? hashConservato)
    {
        if (string.IsNullOrEmpty(hashConservato))
        {
            return true;
        }

        var pezzi = hashConservato.Split('$');
        return pezzi.Length != 4
            || pezzi[0] != Algoritmo
            || !int.TryParse(pezzi[1], System.Globalization.NumberStyles.Integer,
                             System.Globalization.CultureInfo.InvariantCulture, out var iterazioni)
            || iterazioni < _iterazioni;
    }

    private static byte[] Deriva(string password, byte[] sale, int iterazioni, int lunghezza = ByteDellaChiave) =>
        Rfc2898DeriveBytes.Pbkdf2(password, sale, iterazioni, HashAlgorithmName.SHA256, lunghezza);
}
