using System.Text.Json;
using System.Text.RegularExpressions;
using PatternEditor.Abstractions.Localization;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Models.Filters;
using PatternEditor.Element.Circle;
using PatternEditor.Element.Ellipse;
using PatternEditor.Element.Image;
using PatternEditor.Element.Line;
using PatternEditor.Element.Path;
using PatternEditor.Element.Polygon;
using PatternEditor.Element.Polyline;
using PatternEditor.Element.Rect;
using PatternEditor.Element.Text;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// I cataloghi dell'editor sono coerenti fra loro e con quello che i componenti chiedono.
/// </summary>
///
/// <remarks>
/// <para>
/// Sono due file distinti da quelli dell'applicazione: l'editor è una libreria, e chi la usa
/// altrove si porta dietro le sue traduzioni senza doverle ricopiare. Distinti vuol dire però
/// anche verificati a parte, perché una voce dimenticata qui non la trova nessun test di là.
/// </para>
/// <para>
/// Una chiave sbagliata non fa fallire niente: l'indicizzatore la restituisce com'è, e a
/// schermo compare <c>editor.confrma</c> al posto di un pulsante. Lo si scopre guardando, e
/// solo se si guarda proprio quella schermata in quella lingua. Confrontare quello che il
/// markup chiede con quello che i cataloghi hanno costa un test e lo trova subito.
/// </para>
/// </remarks>
public class CataloghiEditorTests
{
    /// <summary>
    /// La radice del repository, cercata risalendo dalla cartella di output.
    /// </summary>
    ///
    /// <remarks>
    /// Si cerca invece di contare i <c>..\..\..</c>: la profondità della cartella di output
    /// cambia con la configurazione e con il framework, e un percorso relativo scritto a mano
    /// è giusto finché qualcuno non aggiunge un livello.
    /// </remarks>
    private static string Radice()
    {
        var qui = new DirectoryInfo(AppContext.BaseDirectory);

        while (qui is not null && !File.Exists(Path.Combine(qui.FullName, "PatternEditor.sln")))
        {
            qui = qui.Parent;
        }

        Assert.True(qui is not null, "Non ho trovato la radice del repository (PatternEditor.sln).");

        return qui!.FullName;
    }

    /// <summary>L'inglese: non un file da cercare, ma quello che l'assembly si porta dentro.</summary>
    private static IReadOnlyDictionary<string, string> Inglese => TestiEditor.Incorporato;

    private static Dictionary<string, string> Tradotto(string codice)
    {
        var percorso = Path.Combine(Radice(), "src", "PatternEditor", "wwwroot", "i18n", $"{codice}.json");

        Assert.True(File.Exists(percorso), $"Manca il catalogo dell'editor: {percorso}");

        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(percorso))
               ?? throw new InvalidOperationException($"Il catalogo «{codice}» non è un oggetto JSON.");
    }

    /// <summary>
    /// Le chiavi di servizio cominciano con un trattino basso: sono note per chi apre il file,
    /// non testi da mostrare, e non partecipano al confronto.
    /// </summary>
    private static IEnumerable<string> Voci(IEnumerable<string> chiavi) =>
        chiavi.Where(k => !k.StartsWith('_'));

    /// <summary>Le lingue dell'editor presenti fra le risorse statiche della libreria.</summary>
    public static TheoryData<string> Lingue()
    {
        var dati = new TheoryData<string>();
        var cartella = Path.Combine(Radice(), "src", "PatternEditor", "wwwroot", "i18n");

        foreach (var file in Directory.EnumerateFiles(cartella, "*.json"))
        {
            dati.Add(Path.GetFileNameWithoutExtension(file));
        }

        return dati;
    }

    [Fact]
    public void LIngleseIncorporato_cE_e_non_e_vuoto()
    {
        // È la rete di sicurezza: se la risorsa non finisce nell'assembly, l'editor mostra i
        // nomi delle chiavi al posto delle parole — e lo fa solo a runtime, in produzione.
        Assert.NotEmpty(Voci(Inglese.Keys));
    }

    [Theory]
    [MemberData(nameof(Lingue))]
    public void OgniLingua_haEsattamenteLeVociDellInglese(string codice)
    {
        var inglese = Voci(Inglese.Keys).ToHashSet(StringComparer.Ordinal);
        var lingua = Voci(Tradotto(codice).Keys).ToHashSet(StringComparer.Ordinal);

        Assert.Equal([], inglese.Except(lingua).Order());
        Assert.Equal([], lingua.Except(inglese).Order());
    }

    [Theory]
    [MemberData(nameof(Lingue))]
    public void NessunaVoce_eVuota(string codice)
    {
        var vuote = Tradotto(codice)
            .Where(v => !v.Key.StartsWith('_') && string.IsNullOrWhiteSpace(v.Value))
            .Select(v => v.Key)
            .Order();

        Assert.Equal([], vuote);
    }

    [Theory]
    [MemberData(nameof(Lingue))]
    public void OgniVoce_haGliStessiSegnaposto(string codice)
    {
        // «{0}» e «{1}» non sono decorazione: sono i valori che la frase riceve. Una
        // traduzione che ne perde uno butta via un numero, una che ne inventa uno fa saltare
        // string.Format con un'eccezione, a schermo, mentre qualcuno sta lavorando.
        var tradotto = Tradotto(codice);

        var diverse = Voci(Inglese.Keys)
            .Where(chiave => tradotto.ContainsKey(chiave))
            .Where(chiave => !Segnaposti(Inglese[chiave]).SetEquals(Segnaposti(tradotto[chiave])))
            .Order();

        Assert.Equal([], diverse);
    }

    private static HashSet<string> Segnaposti(string testo) =>
        Regex.Matches(testo, @"\{\d+\}").Select(m => m.Value).ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void OgniChiaveChiesta_esisteNelCatalogo()
    {
        // L'altro verso: non «il catalogo è completo» ma «quello che si chiede c'è». Un nome
        // scritto storto non lo intercetta il compilatore — è una stringa — e a schermo si
        // legge la chiave al posto della parola.
        var mancanti = ChiaviChieste()
            .Where(c => !Inglese.ContainsKey(c.Chiave))
            .Select(c => $"{c.File}: {c.Chiave}")
            .Order();

        Assert.Equal([], mancanti);
    }

    [Fact]
    public void OgniVoceDelCatalogo_eChiestaDaQualcuno()
    {
        // Una chiave che nessuno chiede più è lavoro che si continua a tradurre a ogni lingua
        // nuova senza che nessuno la legga mai.
        var chieste = ChiaviChieste().Select(c => c.Chiave).ToHashSet(StringComparer.Ordinal);

        // Le famiglie che si compongono a runtime non compaiono scritte da nessuna parte: la
        // chiave nasce da un tipo di elemento, da un effetto pronto, da un nome SVG. A
        // verificarle sono i tre test qui sotto, che le chiedono al modello invece che al
        // testo dei file.
        string[] composte = ["tipo.", "preset.", "filtro.fe"];

        var inutili = Voci(Inglese.Keys)
            .Where(c => !composte.Any(p => c.StartsWith(p, StringComparison.Ordinal)))
            .Where(c => !chieste.Contains(c))
            .Order();

        Assert.Equal([], inutili);
    }

    [Fact]
    public void OgniTipoDiElemento_haIlSuoNomeNelCatalogo()
    {
        // I nove tipi della casa sono nostri e li traduciamo noi. Un plugin di qualcun altro
        // non finirebbe qui — e infatti NomeDelTipo ripiega sul nome che quel plugin porta
        // con sé, invece che sulla chiave.
        var mancanti = Nostri()
            .Select(p => "tipo." + p.Type)
            .Where(c => !Inglese.ContainsKey(c))
            .Order();

        Assert.Equal([], mancanti);
    }

    [Fact]
    public void OgniEffettoPronto_haNomeENotaNelCatalogo()
    {
        var mancanti = FilterPresets.Tutti
            .SelectMany(p => new[] { "preset." + p.Chiave, "preset." + p.Chiave + ".nota" })
            .Concat(FilterPresets.Gruppi.Select(g => "preset.gruppo." + g.Nome))
            .Where(c => !Inglese.ContainsKey(c))
            .Distinct()
            .Order();

        Assert.Equal([], mancanti);
    }

    [Fact]
    public void OgniPrimitivaDelCatalogo_haNomeENotaNelCatalogo()
    {
        var mancanti = FilterPrimitiveTypes.Catalogo
            .SelectMany(v => new[] { "filtro." + v.SvgName, "filtro." + v.SvgName + ".nota" })
            .Where(c => !Inglese.ContainsKey(c))
            .Order();

        Assert.Equal([], mancanti);
    }

    [Fact]
    public void UnTipoSconosciuto_tieneIlNomeDelSuoPlugin()
    {
        // La regola che rende innocuo l'elenco incompleto: un plugin che nessun catalogo
        // conosce si presenta come si chiama lui.
        var testi = new TestiEditor();

        Assert.Equal("Star", testi.NomeDelTipo("stella", "Star"));
    }

    /// <summary>I nove plugin della casa: gli stessi che l'applicazione registra.</summary>
    private static IReadOnlyList<IVectorElementPlugin> Nostri() =>
    [
        new RectPlugin(), new LinePlugin(), new CirclePlugin(), new EllipsePlugin(),
        new PathPlugin(), new PolygonPlugin(), new PolylinePlugin(), new TextPlugin(),
        new ImagePlugin(),
    ];

    /// <summary>
    /// Le chiavi che i sorgenti chiedono, lette dai file.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Si leggono i file invece di ispezionare gli assembly: le chiavi sono stringhe sparse
    /// fra le chiamate al renderer e i costruttori dei messaggi, e ritrovarle nel codice
    /// compilato sarebbe più fragile che leggerle dove sono scritte.
    /// </para>
    /// <para>
    /// Il riconoscimento è per <b>forma</b>: un letterale fatto di un prefisso noto, un punto
    /// e altre lettere è una chiave, ovunque stia. Così la ricerca vale sia per il markup sia
    /// per i messaggi dei validatori e per i riassunti delle primitive, senza tre regole
    /// diverse che si scoprirebbero incomplete una alla volta.
    /// </para>
    /// </remarks>
    private static IEnumerable<(string File, string Chiave)> ChiaviChieste()
    {
        var radice = Radice();

        var cartelle = new[]
        {
            Path.Combine(radice, "src", "PatternEditor"),
            Path.Combine(radice, "src", "PatternEditor.Core"),
            Path.Combine(radice, "src", "PatternEditor.Abstractions"),
            Path.Combine(radice, "plugins"),
        };

        var trovate = new List<(string, string)>();
        // L'ultimo carattere dev'essere una lettera o una cifra: «preset.gruppo.» è un
        // prefisso da comporre, non una chiave, e va lasciato ai test che compongono.
        var forma = new Regex("""["]((?:editor|campo|filtro|preset|val|tipo|imp|ras)\.[A-Za-z0-9.]*[A-Za-z0-9])["]""");

        foreach (var cartella in cartelle.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(cartella, "*.*", SearchOption.AllDirectories))
            {
                // Le cartelle di compilazione contengono il codice generato da Razor, che
                // ripete le stesse chiavi: leggerlo raddoppierebbe il lavoro senza aggiungere
                // niente, e includerebbe file di versioni precedenti rimaste lì.
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                if (Path.GetExtension(file) is not (".razor" or ".cs"))
                {
                    continue;
                }

                foreach (Match trovata in forma.Matches(File.ReadAllText(file)))
                {
                    trovate.Add((Path.GetFileName(file), trovata.Groups[1].Value));
                }
            }
        }

        Assert.NotEmpty(trovate);

        return trovate.Distinct();
    }
}
