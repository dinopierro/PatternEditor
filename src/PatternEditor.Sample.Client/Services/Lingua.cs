using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using PatternEditor.Abstractions.Localization;

namespace PatternEditor.Sample.Client.Services;

/// <summary>Una lingua disponibile: il suo codice e come si chiama nella lingua stessa.</summary>
///
/// <param name="Codice">
/// Il codice della lingua, come lo scrive un browser: <c>en</c>, <c>it</c>. È anche il nome
/// del file del catalogo, ed è voluto — aggiungere una lingua significa aggiungere un file e
/// nominarlo, non modificare del codice.
/// </param>
/// <param name="Nome">
/// Il nome <b>nella lingua stessa</b>: «Italiano», non «Italian». Chi cerca la propria lingua
/// in un elenco la cerca come la chiama lui, e se sapesse riconoscerne il nome in inglese non
/// avrebbe bisogno di cambiarla.
/// </param>
public sealed record LinguaDisponibile(string Codice, string Nome);

/// <summary>
/// In che lingua parla l'applicazione.
/// </summary>
///
/// <remarks>
/// <para>
/// I testi stanno in <b>file JSON</b> sotto <c>wwwroot/i18n</c>, uno per lingua, con accanto
/// un elenco delle lingue disponibili. Aggiungerne una è aggiungere un file e una riga in
/// quell'elenco: nessuna ricompilazione, nessun assembly satellite, e chi traduce lavora su
/// un file di testo invece che dentro il codice.
/// </para>
/// <para>
/// L'inglese è la lingua di riposo, non una lingua privilegiata: è il catalogo completo su
/// cui le altre ripiegano <b>voce per voce</b>. Una traduzione incompleta lascia scoperte le
/// parole che le mancano, non l'intera interfaccia, e una voce scoperta si legge in inglese
/// invece che come una sigla.
/// </para>
/// </remarks>
public sealed class Lingua
{
    /// <summary>
    /// La lingua su cui si ripiega: quella il cui catalogo dev'esserci sempre e dev'essere
    /// completo. Le altre sono sovrapposizioni.
    /// </summary>
    public const string Riposo = "en";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly TestiEditor _editor;

    private IReadOnlyDictionary<string, string> _catalogo = Vuoto();
    private IReadOnlyDictionary<string, string> _riposo = Vuoto();
    private IJSObjectReference? _modulo;

    public Lingua(HttpClient http, IJSRuntime js, TestiEditor editor)
    {
        _http = http;
        _js = js;
        _editor = editor;
    }

    /// <summary>Le lingue fra cui si può scegliere, lette dall'elenco accanto ai cataloghi.</summary>
    public IReadOnlyList<LinguaDisponibile> Disponibili { get; private set; } = [];

    /// <summary>Il codice della lingua in uso.</summary>
    public string Corrente { get; private set; } = Riposo;

    /// <summary>
    /// Si è già deciso in che lingua parlare. Prima che sia vero non si può dire che la
    /// lingua sia l'inglese: si può solo dire che non si sa ancora, e disegnare
    /// un'interfaccia intera per poi tradurla un istante dopo si vede.
    /// </summary>
    public bool Pronta { get; private set; }

    /// <summary>La lingua è cambiata: chi mostra del testo deve ridisegnarsi.</summary>
    public event Action? Cambiata;

    /// <summary>
    /// Il testo con questo nome.
    /// </summary>
    ///
    /// <remarks>
    /// Una chiave sconosciuta torna indietro <b>com'è</b>: a schermo si legge
    /// <c>gestione.titolo</c>, che è brutto e si nota. Una stringa vuota al posto di un
    /// pulsante sarebbe un difetto che si scopre molto più tardi, e da molto più lontano.
    /// </remarks>
    public string this[string chiave] =>
        _catalogo.TryGetValue(chiave, out var tradotto) ? tradotto
        : _riposo.TryGetValue(chiave, out var inglese) ? inglese
        : chiave;

    /// <summary>
    /// Il testo con questo nome, con i segnaposto sostituiti.
    /// </summary>
    ///
    /// <remarks>
    /// I segnaposto sono numerati (<c>{0}</c>, <c>{1}</c>) e non incollati per
    /// concatenazione: una frase cucita a pezzi non si lascia riordinare, e ogni lingua
    /// mette le sue parti in un ordine suo.
    /// </remarks>
    public string this[string chiave, params object?[] valori] =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, this[chiave], valori);

    /// <summary>
    /// Il testo con questo nome, o quello che porta con sé chi lo chiede.
    /// </summary>
    ///
    /// <remarks>
    /// Serve dove la chiave si <b>compone</b> a runtime e l'elenco delle chiavi possibili non
    /// sta qui: il nome di una proprietà dichiarata da un plugin, per esempio. Una proprietà
    /// che nessun catalogo conosce si legge con il suo nome tecnico — brutto, ma vero, e
    /// soprattutto si nota il giorno in cui un plugin ne aggiunge una.
    /// </remarks>
    public string Voce(string chiave, string predefinito) =>
        _catalogo.TryGetValue(chiave, out var tradotto) ? tradotto
        : _riposo.TryGetValue(chiave, out var inglese) ? inglese
        : predefinito;

    /// <summary>
    /// Decide la lingua e carica i cataloghi. Si chiama una volta, dopo il primo disegno:
    /// prima di allora il runtime JavaScript non c'è, e senza di lui non si sanno né la
    /// scelta di chi torna né la lingua del browser.
    /// </summary>
    ///
    /// <remarks>
    /// L'ordine con cui si decide è quello che rispetta la volontà espressa:
    /// <list type="number">
    /// <item>la <b>scelta fatta a mano</b>, se c'è stata: è l'unica dichiarazione esplicita, e
    /// vince su qualunque indizio;</item>
    /// <item>la <b>lingua del browser</b>, se di quella lingua esiste un catalogo: chi arriva
    /// per la prima volta merita la propria lingua senza doverla chiedere;</item>
    /// <item>l'<b>inglese</b>, che c'è sempre.</item>
    /// </list>
    /// </remarks>
    public async Task IniziaAsync()
    {
        if (Pronta)
        {
            return;
        }

        Disponibili = await ElencoAsync();
        _riposo = await CaricaAsync(Riposo);

        Corrente = await SceltaAsync();
        _catalogo = Corrente == Riposo ? _riposo : await CaricaAsync(Corrente);

        await AllineaEditorAsync(Corrente);
        ApplicaCultura(Corrente);
        Dimensione.NomeDelByte = this["comune.byte"];
        await DichiaraAsync(Corrente);

        Pronta = true;
        Cambiata?.Invoke();
    }

    /// <summary>
    /// Cambia lingua adesso e la ricorda. Il cambio è immediato: si sostituisce un
    /// dizionario e si ridisegna, senza ricaricare la pagina — ricaricarla perderebbe quello
    /// che si stava facendo, e cambiare lingua non è cambiare pagina.
    /// </summary>
    public async Task CambiaAsync(string codice)
    {
        if (codice == Corrente || !Disponibili.Any(l => l.Codice == codice))
        {
            return;
        }

        _catalogo = codice == Riposo ? _riposo : await CaricaAsync(codice);
        Corrente = codice;

        await AllineaEditorAsync(codice);
        ApplicaCultura(codice);
        Dimensione.NomeDelByte = this["comune.byte"];
        await RicordaAsync(codice);
        await DichiaraAsync(codice);

        Cambiata?.Invoke();
    }

    /// <summary>
    /// Allinea alla lingua anche il modo di scrivere i numeri.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Le parole tradotte e i numeri scritti all'italiana, nella stessa riga, si notano: un
    /// peso che in inglese si legge «1,3 kB» è la stessa incoerenza di una data scritta al
    /// contrario. La cultura si imposta sul thread, ed è quella che <c>string.Format</c>
    /// consulta ovunque nell'applicazione, senza che nessuno debba passarla.
    /// </para>
    /// <para>
    /// Non tocca i formati <b>espliciti</b>, ed è giusto così: le date brevi delle schede sono
    /// scritte a mano come <c>dd/MM</c> perché devono stare in una riga di tabella, e sono una
    /// scelta di impaginazione, non di lingua.
    /// </para>
    /// <para>
    /// Un codice che il sistema non riconosce come cultura non è un guasto: si è comunque
    /// caricato un catalogo, e le parole si leggono. Si tiene la cultura di prima.
    /// </para>
    /// </remarks>
    private static void ApplicaCultura(string codice)
    {
        try
        {
            var cultura = System.Globalization.CultureInfo.GetCultureInfo(codice);

            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultura;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultura;
        }
        catch (System.Globalization.CultureNotFoundException)
        {
            // Il catalogo c'è lo stesso: si traducono le parole e non i numeri.
        }
    }

    // ------------------------------------------------------------------- come si decide ---

    private async Task<string> SceltaAsync()
    {
        if (await LeggiRicordataAsync() is { Length: > 0 } ricordata
            && Disponibili.Any(l => l.Codice == ricordata))
        {
            return ricordata;
        }

        return DalBrowser(await CulturaBrowserAsync(), Disponibili);
    }

    /// <summary>
    /// La lingua del browser ridotta a una di quelle che sappiamo parlare.
    /// </summary>
    ///
    /// <remarks>
    /// Un browser dichiara <c>it-IT</c> o <c>en-GB</c>: si prova prima la forma intera — un
    /// giorno potremmo distinguere il portoghese del Brasile da quello del Portogallo — e poi
    /// la sola lingua. Senza il secondo tentativo, un browser italiano non troverebbe mai il
    /// catalogo <c>it</c>.
    /// </remarks>
    public static string DalBrowser(string? cultura, IReadOnlyList<LinguaDisponibile> disponibili)
    {
        if (string.IsNullOrWhiteSpace(cultura))
        {
            return Riposo;
        }

        static string? Fra(IReadOnlyList<LinguaDisponibile> elenco, string codice) =>
            elenco.FirstOrDefault(l => string.Equals(l.Codice, codice, StringComparison.OrdinalIgnoreCase))?.Codice;

        return Fra(disponibili, cultura)
            ?? Fra(disponibili, cultura.Split('-')[0])
            ?? Riposo;
    }

    // ------------------------------------------------------------------------ i file -----

    private async Task<IReadOnlyList<LinguaDisponibile>> ElencoAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<List<LinguaDisponibile>>("i18n/lingue.json", Json)
                   ?? [new LinguaDisponibile(Riposo, "English")];
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException)
        {
            // Senza l'elenco resta l'inglese incorporato nei file: l'applicazione parla
            // comunque, e un selettore con una voce sola dice la verità su quello che c'è.
            return [new LinguaDisponibile(Riposo, "English")];
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> CaricaAsync(string codice)
    {
        try
        {
            return await _http.GetFromJsonAsync<Dictionary<string, string>>($"i18n/{codice}.json", Json)
                   ?? Vuoto();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException)
        {
            // Un catalogo che non arriva non deve lasciare l'applicazione muta: si ripiega
            // sull'inglese, voce per voce, ed è esattamente ciò che fa l'indicizzatore.
            return Vuoto();
        }
    }

    /// <summary>
    /// Passa all'editor il suo catalogo.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// L'editor è una libreria, e i suoi testi non stanno in <c>wwwroot/i18n</c>
    /// dell'applicazione ma fra le sue risorse statiche, sotto
    /// <c>_content/PatternEditor/i18n</c>. Sono due cataloghi separati perché sono due
    /// vocabolari separati: chi usa la libreria in un'altra applicazione si porta dietro le
    /// sue traduzioni senza doverle ricopiare, e chi traduce l'applicazione non deve sapere
    /// che cosa dice l'editor.
    /// </para>
    /// <para>
    /// L'inglese non si scarica: la libreria ce l'ha dentro, e applicare <c>null</c> è il modo
    /// di tornarci. Vale anche quando il file di una lingua non arriva — a quel punto l'editor
    /// parla inglese mentre il resto dell'applicazione è tradotto, che è brutto ma leggibile.
    /// </para>
    /// </remarks>
    private async Task AllineaEditorAsync(string codice)
    {
        if (codice == Riposo)
        {
            _editor.Applica(codice, null);
            return;
        }

        try
        {
            var testi = await _http.GetFromJsonAsync<Dictionary<string, string>>(
                $"_content/PatternEditor/i18n/{codice}.json", Json);

            _editor.Applica(codice, testi);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException)
        {
            _editor.Applica(codice, null);
        }
    }

    // ------------------------------------------------------------------ come si ricorda ---

    /// <summary>
    /// Il modulo che sa parlare con la pagina: la lingua del browser, la scelta ricordata,
    /// l'attributo <c>lang</c> sul documento. Tutto quello che C# non può sapere da solo.
    /// </summary>
    private async Task<IJSObjectReference?> ModuloAsync()
    {
        if (_modulo is not null)
        {
            return _modulo;
        }

        try
        {
            _modulo = await _js.InvokeAsync<IJSObjectReference>("import", "./js/lingua.js");
        }
        catch (JSException)
        {
            // Senza modulo si resta sull'inglese: l'applicazione parla, e il selettore
            // continua a funzionare per la durata della visita.
        }

        return _modulo;
    }

    private async Task<string?> LeggiRicordataAsync()
    {
        try
        {
            return await ModuloAsync() is { } modulo
                ? await modulo.InvokeAsync<string?>("leggi")
                : null;
        }
        catch (JSException)
        {
            // Finestra anonima, dati dei siti bloccati: si riparte dalla lingua del browser.
            return null;
        }
    }

    private async Task RicordaAsync(string codice)
    {
        try
        {
            if (await ModuloAsync() is { } modulo)
            {
                await modulo.InvokeVoidAsync("ricorda", codice);
            }
        }
        catch (JSException)
        {
            // La scelta vale per questa visita e basta. Meglio che rifiutare il cambio.
        }
    }

    private async Task<string?> CulturaBrowserAsync()
    {
        try
        {
            return await ModuloAsync() is { } modulo
                ? await modulo.InvokeAsync<string?>("cultura")
                : null;
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <summary>
    /// Scrive la lingua nell'attributo <c>lang</c> del documento. Serve alla sillabazione, al
    /// controllo ortografico dei campi e ai lettori di schermo: una pagina che dichiara una
    /// lingua e ne mostra un'altra si fa leggere male ad alta voce.
    /// </summary>
    private async Task DichiaraAsync(string codice)
    {
        try
        {
            if (await ModuloAsync() is { } modulo)
            {
                await modulo.InvokeVoidAsync("dichiara", codice);
            }
        }
        catch (JSException)
        {
            // Un attributo in meno non toglie niente a chi legge a schermo.
        }
    }

    private static Dictionary<string, string> Vuoto() => new(StringComparer.Ordinal);
}
