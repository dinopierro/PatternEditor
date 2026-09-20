using System.Globalization;
using System.Reflection;
using System.Text.Json;
using PatternEditor.Core.Localization;

namespace PatternEditor.Abstractions.Localization;

/// <summary>
/// I testi dell'editor, nella lingua scelta dall'applicazione ospite.
/// </summary>
///
/// <remarks>
/// <para>
/// Sta in <b>Abstractions</b> e non nella libreria dei componenti perché serve a entrambe le
/// parti che mostrano testo: il componente editor e i nove plugin di elemento. I plugin non
/// referenziano la libreria — è una regola dell'architettura, non una dimenticanza — e un
/// servizio dichiarato là sarebbe fuori dalla loro portata.
/// </para>
/// <para>
/// La libreria <b>non decide</b> in che lingua si parla, esattamente come non decide chi sia
/// l'utente: di lingue non sa niente, sa solo che ogni parola che mostra ha un nome. A
/// scegliere è l'ospite, che chiama <see cref="Applica"/> e può rifarlo quando vuole.
/// </para>
/// <para>
/// Quello che la libreria porta con sé è l'<b>inglese</b>, in un file incorporato
/// nell'assembly. Non è una lingua privilegiata: è la rete di sicurezza. Un ospite che non
/// applichi niente — perché è a lingua singola, perché il file delle traduzioni non è
/// arrivato — deve trovare un editor che parla, non un editor pieno di sigle.
/// </para>
/// <para>
/// Il ripiego è <b>per chiave e non per catalogo</b>: se il catalogo applicato non contiene
/// una voce, quella singola voce torna all'inglese e tutto il resto resta tradotto. Una
/// traduzione incompleta lascia scoperte le parole che le mancano, non l'interfaccia intera.
/// </para>
/// </remarks>
public sealed class TestiEditor
{
    private IReadOnlyDictionary<string, string> _catalogo =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Il codice della lingua applicata. Vuoto finché nessuno ha applicato niente, cioè
    /// finché vale l'inglese incorporato.
    /// </summary>
    public string Lingua { get; private set; } = string.Empty;

    /// <summary>
    /// La lingua è cambiata: chi disegna del testo deve ridisegnarsi.
    /// </summary>
    ///
    /// <remarks>
    /// Un evento e non un parametro a cascata: i componenti che mostrano testo sono una
    /// ventina e annidati, e farsi passare la lingua da tutti i genitori vorrebbe dire che
    /// ognuno la dichiari e la inoltri — e che il giorno in cui uno se ne dimentica quel
    /// pezzo di interfaccia resti indietro di una lingua.
    /// </remarks>
    public event Action? Cambiato;

    /// <summary>
    /// Il testo con questo nome, nella lingua corrente.
    /// </summary>
    ///
    /// <remarks>
    /// Una chiave sconosciuta torna indietro <b>com'è</b>: a schermo si legge
    /// <c>editor.conferma</c>, che è brutto e si nota. Uno spazio vuoto al posto di un
    /// pulsante sarebbe un difetto che si scopre molto più tardi, e da molto più lontano.
    /// </remarks>
    public string this[string chiave] =>
        _catalogo.TryGetValue(chiave, out var tradotto) ? tradotto
        : Incorporato.TryGetValue(chiave, out var inglese) ? inglese
        : chiave;

    /// <summary>
    /// Il testo con questo nome, con i segnaposto sostituiti.
    /// </summary>
    ///
    /// <remarks>
    /// I segnaposto sono numerati (<c>{0}</c>, <c>{1}</c>) e non incollati per
    /// concatenazione: una frase cucita a pezzi non si lascia riordinare, e ogni lingua mette
    /// le proprie parti in un ordine suo.
    /// </remarks>
    public string this[string chiave, params object?[] valori] =>
        string.Format(CultureInfo.CurrentCulture, this[chiave], valori);

    /// <summary>
    /// Come si chiama un tipo di elemento: «Rectangle», «Rettangolo».
    /// </summary>
    ///
    /// <param name="tipo">Il tipo dichiarato dal plugin: <c>rect</c>, <c>circle</c>.</param>
    /// <param name="predefinito">
    /// Il nome che il plugin porta con sé, usato quando il catalogo non sa niente di questo
    /// tipo.
    /// </param>
    ///
    /// <remarks>
    /// <para>
    /// Non è un'indicizzazione come le altre perché il ripiego non è l'inglese incorporato ma
    /// il plugin stesso. Un plugin scritto da qualcun altro non compare nei nostri cataloghi,
    /// e non ha motivo di comparirci: il suo nome lo sa lui. Farlo cadere sulla chiave —
    /// <c>tipo.stella</c> a schermo — lo punirebbe per essere arrivato dopo.
    /// </para>
    /// <para>
    /// La chiave non è dichiarata da nessuna parte: si compone dal tipo. È il prezzo per non
    /// dover toccare l'interfaccia dei plugin, che è pubblica e che chiunque può aver già
    /// implementato.
    /// </para>
    /// </remarks>
    public string NomeDelTipo(string tipo, string predefinito) =>
        Voce("tipo." + tipo, predefinito);

    /// <summary>
    /// Il testo con questo nome, o quello che porta con sé chi lo chiede.
    /// </summary>
    ///
    /// <remarks>
    /// È l'indicizzazione per le cose che <b>si contano da sole</b>: i tipi di elemento, gli
    /// effetti pronti, le primitive del filtro. Il loro elenco non sta qui ma nel modello, e
    /// il catalogo può non averle tutte — una primitiva nuova, un plugin di qualcun altro.
    /// Mostrare la chiave, in quei casi, punirebbe chi è arrivato dopo.
    /// </remarks>
    public string Voce(string chiave, string predefinito) =>
        _catalogo.TryGetValue(chiave, out var tradotto) ? tradotto
        : Incorporato.TryGetValue(chiave, out var inglese) ? inglese
        : predefinito;

    /// <summary>
    /// Un testo composto dal modello: si traduce la frase, e anche le frasi che porta dentro.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// La ricorsione sui valori non è un vezzo: il riassunto di un passaggio del filtro ne
    /// contiene altri — «colour · linear 1,2 / 0» sono due frasi annidate — e fermarsi al
    /// primo livello lascerebbe metà riga in inglese dentro una riga tradotta.
    /// </para>
    /// <para>
    /// Una chiave vuota significa che non c'è niente da tradurre: è un nome di file, un tipo
    /// letto da un documento, un colore. Si mostra com'è.
    /// </para>
    /// </remarks>
    public string Testo(TestoNominato testo)
    {
        var modello = testo.Chiave.Length == 0
            ? testo.Modello
            : Voce(testo.Chiave, testo.Modello);

        if (testo.Valori.Count == 0)
        {
            return modello;
        }

        var valori = testo.Valori
            .Select(v => v is TestoNominato dentro ? Testo(dentro) : v)
            .ToArray();

        return string.Format(CultureInfo.CurrentCulture, modello, valori);
    }

    /// <summary>
    /// Sostituisce il catalogo corrente e avverte chi disegna.
    /// </summary>
    ///
    /// <param name="lingua">
    /// Il codice della lingua applicata, conservato e restituito da <see cref="Lingua"/>. La
    /// libreria non lo interpreta: non sa che cosa sia una cultura, e non ne ricava né
    /// formati né direzione della scrittura.
    /// </param>
    /// <param name="testi">
    /// Le voci da usare. <c>null</c> o un dizionario vuoto riportano all'inglese incorporato,
    /// che è il modo per tornare indietro senza dover conoscere le chiavi.
    /// </param>
    public void Applica(string lingua, IReadOnlyDictionary<string, string>? testi)
    {
        Lingua = lingua ?? string.Empty;
        _catalogo = testi ?? new Dictionary<string, string>(StringComparer.Ordinal);
        Cambiato?.Invoke();
    }

    /// <summary>
    /// L'inglese incorporato nell'assembly, letto una volta sola alla prima richiesta.
    /// </summary>
    ///
    /// <remarks>
    /// È un file JSON e non un dizionario scritto in C#: chi traduce lavora sul file di
    /// questa cartella, e la stessa forma serve anche alle altre lingue, che invece viaggiano
    /// come risorse statiche (<c>wwwroot/i18n</c>). Due formati per la stessa cosa
    /// significherebbero due modi di sbagliarla.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> Incorporato => _incorporato.Value;

    private static readonly Lazy<IReadOnlyDictionary<string, string>> _incorporato = new(Leggi);

    private static IReadOnlyDictionary<string, string> Leggi()
    {
        var assembly = typeof(TestiEditor).Assembly;
        var nome = $"{typeof(TestiEditor).Namespace}.en.json";

        using var flusso = assembly.GetManifestResourceStream(nome);

        if (flusso is null)
        {
            // Senza il file incorporato l'editor mostrerebbe i nomi delle chiavi. Meglio di
            // una schermata vuota, e comunque un guasto di compilazione: la risorsa c'è o
            // non c'è, e non dipende da niente che accada a runtime.
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var letto = JsonSerializer.Deserialize<Dictionary<string, string>>(flusso);

        return letto is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(
                letto.Where(v => !v.Key.StartsWith('_')), StringComparer.Ordinal);
    }
}
