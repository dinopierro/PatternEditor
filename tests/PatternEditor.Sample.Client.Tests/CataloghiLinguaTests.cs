using System.Text.Json;
using PatternEditor.Sample.Client.Services;
using Xunit;

namespace PatternEditor.Sample.Client.Tests;

/// <summary>
/// I cataloghi delle lingue sono coerenti fra loro.
///
/// <para>
/// È la sola rete che una traduzione possa avere. Una voce dimenticata non fa fallire niente
/// e non si vede in nessun elenco di errori: si vede una volta sola, da un utente, in una
/// schermata che nessuno stava guardando — e a quel punto è già in produzione. Un test che
/// confronta le chiavi la trova prima, e costa una riga.
/// </para>
/// </summary>
public class CataloghiLinguaTests
{
    /// <summary>
    /// La cartella dei cataloghi, trovata risalendo dal test fino alla radice del
    /// repository.
    /// </summary>
    ///
    /// <remarks>
    /// Il percorso si <b>cerca</b> invece di scriverlo relativo alla cartella di output: la
    /// profondità di quella cartella cambia con la configurazione e con il framework, e un
    /// <c>..\..\..\..</c> contato a mano è giusto finché qualcuno non aggiunge un livello.
    /// Se la radice non si trova, il test fallisce dicendo che non l'ha trovata: è un
    /// risultato onesto, mentre un elenco di file vuoto passerebbe verificando niente.
    /// </remarks>
    private static string Cartella()
    {
        var qui = new DirectoryInfo(AppContext.BaseDirectory);

        while (qui is not null && !File.Exists(Path.Combine(qui.FullName, "PatternEditor.sln")))
        {
            qui = qui.Parent;
        }

        Assert.True(qui is not null, "Non ho trovato la radice del repository (PatternEditor.sln).");

        var cartella = Path.Combine(
            qui!.FullName, "src", "PatternEditor.Sample.Client", "wwwroot", "i18n");

        Assert.True(Directory.Exists(cartella), $"Manca la cartella dei cataloghi: {cartella}");

        return cartella;
    }

    private static Dictionary<string, string> Catalogo(string codice)
    {
        var testo = File.ReadAllText(Path.Combine(Cartella(), $"{codice}.json"));

        return JsonSerializer.Deserialize<Dictionary<string, string>>(testo)
               ?? throw new InvalidOperationException($"Il catalogo «{codice}» non è un oggetto JSON.");
    }

    /// <summary>
    /// Le chiavi di servizio cominciano con un trattino basso: sono note per chi apre il
    /// file, non testi da mostrare, e non partecipano al confronto.
    /// </summary>
    private static IEnumerable<string> Voci(Dictionary<string, string> catalogo) =>
        catalogo.Keys.Where(k => !k.StartsWith('_'));

    private static IReadOnlyList<LinguaDisponibile> Elenco()
    {
        var testo = File.ReadAllText(Path.Combine(Cartella(), "lingue.json"));

        return JsonSerializer.Deserialize<List<LinguaDisponibile>>(
                   testo, new JsonSerializerOptions(JsonSerializerDefaults.Web))
               ?? throw new InvalidOperationException("L'elenco delle lingue non è un array JSON.");
    }

    public static TheoryData<string> Lingue()
    {
        var dati = new TheoryData<string>();

        foreach (var lingua in Elenco())
        {
            dati.Add(lingua.Codice);
        }

        return dati;
    }

    [Fact]
    public void LInglese_cE_e_non_e_vuoto()
    {
        // È la lingua di riposo: se manca lei, manca il ripiego di tutte le altre.
        Assert.NotEmpty(Voci(Catalogo(Lingua.Riposo)));
    }

    [Theory]
    [MemberData(nameof(Lingue))]
    public void OgniLingua_haEsattamenteLeVociDellInglese(string codice)
    {
        var inglese = Voci(Catalogo(Lingua.Riposo)).ToHashSet(StringComparer.Ordinal);
        var lingua = Voci(Catalogo(codice)).ToHashSet(StringComparer.Ordinal);

        // Le mancanti si leggerebbero in inglese in mezzo a una pagina tradotta: non è un
        // guasto, ma è un difetto che nessuno segnala e che resta lì per mesi.
        Assert.Equal([], inglese.Except(lingua).Order());

        // Quelle di troppo sono l'altro verso dello stesso errore: una chiave rinominata nel
        // codice lascia dietro di sé la sua traduzione, che non serve più a niente e che al
        // prossimo giro qualcuno tradurrà di nuovo.
        Assert.Equal([], lingua.Except(inglese).Order());
    }

    [Theory]
    [MemberData(nameof(Lingue))]
    public void NessunaVoce_eVuota(string codice)
    {
        var vuote = Catalogo(codice)
            .Where(v => !v.Key.StartsWith('_') && string.IsNullOrWhiteSpace(v.Value))
            .Select(v => v.Key)
            .Order();

        Assert.Equal([], vuote);
    }

    [Fact]
    public void LElenco_dichiaraEsattamenteICataloghiPresenti()
    {
        // Una lingua dichiarata senza catalogo comparirebbe nel selettore e non tradurrebbe
        // niente; un catalogo non dichiarato non comparirebbe affatto — tradotto e
        // irraggiungibile, che è il modo più silenzioso di sprecare un lavoro.
        var dichiarate = Elenco().Select(l => l.Codice).Order();

        var presenti = Directory.EnumerateFiles(Cartella(), "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(nome => nome != "lingue")
            .Order();

        Assert.Equal(dichiarate, presenti);
    }

    [Fact]
    public void OgniLingua_siChiamaNellaPropriaLingua()
    {
        // «Italiano» e non «Italian»: chi cerca la propria lingua in un elenco la cerca come
        // la chiama lui. Qui si può solo verificare che un nome ci sia.
        Assert.All(Elenco(), l => Assert.False(string.IsNullOrWhiteSpace(l.Nome)));
    }

    // ------------------------------------------------- come si sceglie senza una scelta ---

    [Theory]
    [InlineData("it", "it")]
    [InlineData("it-IT", "it")]
    [InlineData("IT-it", "it")]
    [InlineData("en-GB", "en")]
    [InlineData("de-DE", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void LaLinguaDelBrowser_siRiduceAUnaCheSappiamoParlare(string? cultura, string atteso)
    {
        // Un browser dichiara «it-IT»: senza il secondo tentativo sulla sola lingua, il
        // catalogo «it» non verrebbe mai trovato e tutti gli italiani leggerebbero in inglese.
        Assert.Equal(atteso, Lingua.DalBrowser(cultura, Elenco()));
    }
}
