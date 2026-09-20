namespace PatternEditor.State;

/// <summary>
/// La cronologia della sessione di editing: cosa annullare e cosa ripetere.
/// </summary>
///
/// <remarks>
/// <para>
/// Conserva <b>istantanee complete</b> del pattern serializzato, non i singoli comandi.
/// Un sistema a comandi obbligherebbe ogni plugin a descrivere le proprie modifiche in una
/// forma reversibile: nove implementazioni da scrivere, e la decima — quella del plugin che
/// arriverà domani — da ricordarsi. L'istantanea non chiede niente a nessuno, e per un
/// documento di questa scala costa poco: un pattern con trecento elementi sta in qualche
/// decina di migliaia di caratteri, e la cronologia ne tiene al più
/// <see cref="Massimo"/>.
/// </para>
/// <para>
/// La classe non conosce Blazor e non sa che cosa sia un pattern: riceve e restituisce
/// testo. È una scelta che la rende verificabile con un test normale.
/// </para>
/// </remarks>
public sealed class PatternHistory
{
    /// <summary>
    /// Quante istantanee tenere. Oltre questa soglia le più vecchie si perdono: una
    /// cronologia illimitata farebbe crescere la memoria per tutta la durata della
    /// sessione, e nessuno annulla cento passi indietro.
    /// </summary>
    public const int Massimo = 60;

    /// <summary>
    /// Entro questa distanza due modifiche contano come una sola.
    ///
    /// Serve perché i cursori e i campi di testo notificano a ogni battuta e a ogni
    /// pixel: senza accorpamento, annullare uno spostamento del cursore richiederebbe
    /// quaranta pressioni di «Annulla», una per ogni valore intermedio.
    /// </summary>
    public static readonly TimeSpan Finestra = TimeSpan.FromMilliseconds(600);

    private readonly List<string> _stati = [];
    private int _indice = -1;
    private DateTimeOffset _ultima;

    /// <summary>Quante istantanee sono conservate in questo momento.</summary>
    public int Conteggio => _stati.Count;

    /// <summary>Posizione corrente nella cronologia, da 0; -1 quando è vuota.</summary>
    public int Posizione => _indice;

    public bool PuoAnnullare => _indice > 0;

    public bool PuoRipetere => _indice >= 0 && _indice < _stati.Count - 1;

    /// <summary>
    /// Apre una nuova cronologia sullo stato iniziale. Tutto quello che c'era prima si
    /// perde: una sessione di editing non deve poter annullare dentro la precedente.
    /// </summary>
    public void Inizia(string istantanea)
    {
        ArgumentNullException.ThrowIfNull(istantanea);

        _stati.Clear();
        _stati.Add(istantanea);
        _indice = 0;

        // Data lontana: la prima modifica non deve mai accorparsi allo stato di apertura,
        // altrimenti «Annulla» non riporterebbe al punto di partenza.
        _ultima = DateTimeOffset.MinValue;
    }

    /// <summary>
    /// Registra lo stato raggiunto dopo una modifica.
    /// </summary>
    ///
    /// <param name="istantanea">Il pattern serializzato dopo la modifica.</param>
    /// <param name="quando">L'istante della modifica, per l'accorpamento.</param>
    /// <returns>
    /// <c>true</c> se la cronologia è cambiata. <c>false</c> quando l'istantanea è identica
    /// a quella corrente: una notifica che non ha modificato niente non deve lasciare un
    /// passo di «Annulla» che non annulla niente.
    /// </returns>
    public bool Registra(string istantanea, DateTimeOffset quando)
    {
        ArgumentNullException.ThrowIfNull(istantanea);

        if (_indice < 0)
        {
            Inizia(istantanea);
            return true;
        }

        if (_stati[_indice] == istantanea)
        {
            return false;
        }

        // Modificare dopo aver annullato cancella ciò che si sarebbe potuto ripetere: da
        // qui in avanti la storia è un'altra, e tenerne due sarebbe un albero, non una
        // cronologia.
        if (_indice < _stati.Count - 1)
        {
            _stati.RemoveRange(_indice + 1, _stati.Count - _indice - 1);
        }

        // Accorpamento: si sovrascrive l'ultima istantanea invece di aggiungerne una.
        // Mai sulla prima, che è lo stato di apertura e deve restare raggiungibile.
        if (_indice > 0 && quando - _ultima < Finestra)
        {
            _stati[_indice] = istantanea;
            _ultima = quando;
            return true;
        }

        _stati.Add(istantanea);
        _indice = _stati.Count - 1;
        _ultima = quando;

        if (_stati.Count > Massimo)
        {
            _stati.RemoveRange(0, _stati.Count - Massimo);
            _indice = _stati.Count - 1;
        }

        return true;
    }

    /// <summary>
    /// Torna indietro di un passo e restituisce lo stato da ripristinare, oppure
    /// <c>null</c> se non c'è niente da annullare.
    /// </summary>
    public string? Annulla()
    {
        if (!PuoAnnullare)
        {
            return null;
        }

        _indice--;
        FermaAccorpamento();
        return _stati[_indice];
    }

    /// <summary>
    /// Rifà il passo annullato e restituisce lo stato da ripristinare, oppure <c>null</c>
    /// se non c'è niente da ripetere.
    /// </summary>
    public string? Ripeti()
    {
        if (!PuoRipetere)
        {
            return null;
        }

        _indice++;
        FermaAccorpamento();
        return _stati[_indice];
    }

    /// <summary>
    /// Dopo un annullamento la modifica successiva deve aprire un passo nuovo, anche se
    /// arriva subito: accorparla a quella ripristinata la farebbe sparire dentro uno stato
    /// che l'utente ha appena scelto di rivedere.
    /// </summary>
    private void FermaAccorpamento() => _ultima = DateTimeOffset.MinValue;
}
