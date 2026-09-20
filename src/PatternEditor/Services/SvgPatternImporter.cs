using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Formatting;
using PatternEditor.Core.Localization;
using PatternEditor.Core.Models;

namespace PatternEditor.Services;

/// <summary>
/// Esito di un'importazione: il pattern ottenuto, quanto è stato preso e che cosa è stato
/// lasciato fuori.
///
/// <para>
/// Gli avvisi non sono un ornamento. Un documento SVG qualsiasi contiene quasi sempre
/// qualcosa che questo modello non sa rappresentare, e l'importazione che tace lascia
/// credere di aver portato dentro tutto: l'unico modo per accorgersene sarebbe confrontare
/// il disegno originale con l'anteprima, cioè proprio il lavoro che lo strumento dovrebbe
/// risparmiare. Meglio un elenco di ciò che manca.
/// </para>
/// </summary>
public sealed class SvgImportResult
{
    private SvgImportResult(Pattern? pattern, int imported, int skipped,
                            IReadOnlyList<TestoNominato> avvisi)
    {
        Pattern = pattern;
        Imported = imported;
        Skipped = skipped;
        Avvisi = avvisi;

        // Si compongono una volta sola: sono liste corte, e rifarle a ogni lettura vorrebbe
        // dire ripetere gli string.Format a ogni ciclo di rendering.
        Warnings = [.. avvisi.Select(a => a.ToString())];
    }

    /// <summary>Il pattern ricavato dal documento, oppure null se il file non è utilizzabile.</summary>
    public Pattern? Pattern { get; }

    /// <summary>Quanti elementi sono stati importati.</summary>
    public int Imported { get; }

    /// <summary>Quanti elementi disegnabili sono stati incontrati e lasciati fuori.</summary>
    public int Skipped { get; }

    /// <summary>Che cosa non è stato importato, e perché. In inglese, pronto da mostrare.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Gli stessi avvisi, ancora traducibili: chi importa conosce il caso, non la lingua di
    /// chi leggerà.
    /// </summary>
    public IReadOnlyList<TestoNominato> Avvisi { get; }

    /// <summary>Vero quando non c'è niente da aprire: file illeggibile o senza elementi noti.</summary>
    public bool IsEmpty => Pattern is null || Imported == 0;

    /// <summary>
    /// Un esito negativo con il suo motivo. È pubblico perché anche l'applicazione ospitante
    /// ne ha bisogno: un file troppo grande o illeggibile è un fallimento dell'importazione
    /// come gli altri, e merita la stessa finestra invece di una via separata.
    /// </summary>
    public static SvgImportResult Failed(string reason) =>
        new(null, 0, 0, [TestoNominato.Fisso(reason)]);

    /// <summary>Un esito negativo il cui motivo si può ancora tradurre.</summary>
    public static SvgImportResult Fallita(TestoNominato motivo) => new(null, 0, 0, [motivo]);

    /// <summary>
    /// Lo stesso esito, con altri avvisi messi davanti ai suoi.
    /// </summary>
    ///
    /// <remarks>
    /// Serve a chi arriva <b>prima</b> dell'importazione e ha già qualcosa da dire: chi ricava
    /// un SVG da un'immagine sa il passo che ha trovato e le macchie che ha lasciato fuori, e
    /// quelle notizie vengono prima di tutto ciò che l'importatore osserverà sul documento.
    /// </remarks>
    public SvgImportResult ConAvvisiIniziali(IReadOnlyList<TestoNominato> prima) =>
        prima.Count == 0 ? this : new(Pattern, Imported, Skipped, [.. prima, .. Avvisi]);

    internal static SvgImportResult Riuscita(Pattern pattern, int importati, int ignorati,
                                             IReadOnlyList<TestoNominato> avvisi) =>
        new(pattern, importati, ignorati, avvisi);
}

/// <summary>
/// Costruisce un <see cref="Pattern"/> a partire da un documento SVG.
/// </summary>
public interface ISvgPatternImporter
{
    /// <summary>
    /// Legge il documento e ne ricava un pattern. Non salva nulla: il risultato va aperto
    /// nell'editor, guardato e confermato, come qualunque altra modifica.
    /// </summary>
    /// <param name="svg">Contenuto del file.</param>
    /// <param name="name">Nome da dare al pattern (di norma quello del file).</param>
    SvgImportResult Import(string svg, string name);
}

/// <summary>
/// Importatore di documenti SVG.
///
/// <para>
/// <b>Che cosa sa fare, e perché il confine sta lì.</b> SVG è un formato molto più ampio del
/// modello di questa applicazione: gruppi, gradienti, ritagli, maschere, filtri, riferimenti,
/// fogli di stile. Il modello ha nove tipi di elemento e un elenco piatto. L'importazione
/// prende quello che il modello sa rappresentare — le forme, la loro geometria e i loro
/// colori — e segnala il resto.
/// </para>
///
/// <para>
/// <b>Come fa a non conoscere i tipi concreti.</b> Il nome del tag SVG e l'identificativo del
/// plugin coincidono per costruzione (<c>rect</c>, <c>circle</c>, <c>path</c>, …): un tag è
/// importabile se il registro ha un plugin con quel nome, e l'elenco dei tipi importabili è
/// quindi quello che l'applicazione ospitante ha registrato, senza alcuna lista scritta qui.
/// Le proprietà si sovrappongono per nome sul documento JSON dell'elemento, che il plugin
/// stesso ha prodotto con i propri valori predefiniti: una proprietà che il modello non ha
/// viene semplicemente ignorata, e una che il documento non indica resta al valore del
/// plugin. Nessun riferimento a <c>RectElement</c> compare in questo file, e un decimo tipo
/// di elemento sarà importabile senza toccarlo.
/// </para>
///
/// <para>
/// <b>Le trasformazioni.</b> Il modello non ha gruppi né trasformazioni per elemento, quindi
/// quelle del documento vanno applicate alla geometria. Sono supportate traslazione e scala,
/// composte lungo tutto l'albero; rotazioni, inclinazioni e matrici generiche no — gli
/// elementi che le subiscono vengono lasciati fuori e segnalati, perché importarli ignorando
/// la rotazione darebbe un disegno sbagliato senza dirlo.
/// </para>
/// </summary>
public sealed class SvgPatternImporter : ISvgPatternImporter
{
    private const double CellaPredefinita = 100;
    private const string SpazioSvg = "http://www.w3.org/2000/svg";

    private readonly IVectorElementPluginRegistry _registry;
    private readonly IPatternSerializer _serializer;

    public SvgPatternImporter(IVectorElementPluginRegistry registry, IPatternSerializer serializer)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    // ------------------------------------------------------------------ ingresso

    public SvgImportResult Import(string svg, string name)
    {
        if (string.IsNullOrWhiteSpace(svg))
        {
            return SvgImportResult.Fallita(new("imp.fileVuoto", "The file is empty."));
        }

        XDocument documento;
        try
        {
            // DtdProcessing.Prohibit: un documento non deve poter far leggere file locali o
            // aprire connessioni attraverso una definizione di tipo. È il rischio classico
            // di chi dà in pasto a un lettore XML un file che arriva da fuori.
            using var lettore = XmlReader.Create(new StringReader(svg), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            });
            documento = XDocument.Load(lettore);
        }
        catch (XmlException e)
        {
            // Il messaggio dell'eccezione NON si riporta: in WebAssembly le risorse di
            // traduzione non ci sono, e al loro posto esce il nome interno del messaggio —
            // «Xml_InvalidRootData, 1, 1» — che a chi legge non dice niente. Riga e
            // posizione invece sono numeri, e valgono in qualunque lingua.
            return SvgImportResult.Fallita(new(
                "imp.xmlNonValido",
                "The file is not a valid XML document (line {0}, position {1}). It may not be an "
                + "SVG, or it may have been truncated.",
                e.LineNumber, e.LinePosition));
        }

        var radice = documento.Root;
        if (radice is null || !string.Equals(radice.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
        {
            return SvgImportResult.Fallita(new(
                "imp.nonSvg",
                "The file contains no <svg> element: it is not an SVG document."));
        }

        var avvisi = new List<TestoNominato>();
        var (larghezza, altezza, partenza) = LeggiCella(radice, avvisi);

        var candidati = new List<Candidato>();
        var ignorati = new Contatore();
        var perdite = new Contatore();
        Percorri(radice, new Contesto(partenza, new Dictionary<string, string>(StringComparer.Ordinal), 1.0),
                 candidati, ignorati, perdite);

        foreach (var riga in ignorati.Righe().Concat(perdite.Righe()))
        {
            avvisi.Add(riga);
        }

        // Il documento potrebbe essere un SVG prodotto da questa stessa applicazione: in quel
        // caso il disegno non sta nel corpo ma dentro <defs><pattern>, che di regola non si
        // importa perché è una definizione. Qui pero' quella definizione È il pattern, e
        // rifiutarla vorrebbe dire non saper rileggere il proprio stesso formato.
        var nodoPattern = candidati.Count == 0 ? TrovaNodoPattern(radice) : null;
        var misure = nodoPattern is null ? null : MisureDelNodoPattern(nodoPattern);

        if (nodoPattern is not null && misure is not null)
        {
            (larghezza, altezza) = misure.Value;
            avvisi.Clear();
            ignorati = new Contatore();
            perdite = new Contatore();

            // Si riparte dall'identità: gli elementi di un <pattern> vivono nel sistema di
            // coordinate della cella, non in quello del documento che la contiene.
            Percorri(nodoPattern,
                     new Contesto(Trasformazione.Identita,
                                  new Dictionary<string, string>(StringComparer.Ordinal), 1.0),
                     candidati, ignorati, perdite);

            foreach (var riga in ignorati.Righe().Concat(perdite.Righe()))
            {
                avvisi.Add(riga);
            }

            if (candidati.Count > 0)
            {
                avvisi.Insert(0, new TestoNominato(
                    "imp.giaPattern",
                    "The document is already a pattern: the cell and the transformation "
                    + "declared in the file have been read."));
            }
        }

        if (candidati.Count == 0)
        {
            return SvgImportResult.Fallita(new(
                "imp.nessunaForma",
                "The document holds no shape that this application knows how to represent."));
        }

        var pattern = Materializza(candidati, larghezza, altezza, name);

        // La trasformazione del nodo <pattern> si legge solo quando gli elementi vengono da
        // lì: un documento qualsiasi può contenere un <pattern> usato come riempimento di
        // una forma, e quella trasformazione non ha niente a che vedere con questa cella.
        if (nodoPattern is not null && misure is not null)
        {
            LeggiTrasformazioneDelPattern(nodoPattern, pattern.Definition, avvisi);
        }

        return SvgImportResult.Riuscita(pattern, candidati.Count, ignorati.Totale, avvisi);
    }

    // ------------------------------------------------------------------ la cella

    /// <summary>
    /// Misure della cella e trasformazione di partenza.
    ///
    /// La cella viene dal <c>viewBox</c>, che è il sistema di coordinate in cui il disegno è
    /// scritto: prendere invece <c>width</c> e <c>height</c> darebbe le misure a schermo, e
    /// un documento largo «10cm» con un viewBox 0 0 100 100 finirebbe in una cella di 10.
    /// Un viewBox che non parte da zero si porta dietro uno scostamento, che diventa la
    /// traslazione iniziale.
    /// </summary>
    private static (double Larghezza, double Altezza, Trasformazione Partenza) LeggiCella(
        XElement radice, List<TestoNominato> avvisi)
    {
        var viewBox = (string?)radice.Attribute("viewBox");
        if (viewBox is not null)
        {
            var n = Numeri(viewBox);
            if (n.Count == 4 && n[2] > 0 && n[3] > 0)
            {
                return (Arrotonda(n[2]), Arrotonda(n[3]),
                        Trasformazione.Traslazione(-n[0], -n[1]));
            }

            avvisi.Add(new(
                "imp.viewBoxIllegibile",
                "The document's viewBox cannot be read: the cell was worked out elsewhere."));
        }

        var larghezza = Lunghezza((string?)radice.Attribute("width"));
        var altezza = Lunghezza((string?)radice.Attribute("height"));

        if (larghezza is > 0 && altezza is > 0)
        {
            return (Arrotonda(larghezza.Value), Arrotonda(altezza.Value), Trasformazione.Identita);
        }

        avvisi.Add(new(
            "imp.nessunaMisura",
            "The document declares neither a viewBox nor usable measurements: the cell has been "
            + "set to {0} × {0} and needs correcting by hand.",
            CellaPredefinita));
        return (CellaPredefinita, CellaPredefinita, Trasformazione.Identita);
    }

    /// <summary>Il primo nodo &lt;pattern&gt; del documento, se c'è.</summary>
    private static XElement? TrovaNodoPattern(XElement radice) =>
        radice.Descendants().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, "pattern", StringComparison.Ordinal));

    /// <summary>
    /// Le misure della cella dichiarate da un nodo &lt;pattern&gt;.
    ///
    /// Valgono solo se sono in unità del disegno: con <c>objectBoundingBox</c> sarebbero
    /// frazioni del riquadro che le usa, e una cella di 0,25 × 0,25 non vuol dire niente qui.
    /// </summary>
    private static (double Larghezza, double Altezza)? MisureDelNodoPattern(XElement nodo)
    {
        var unita = (string?)nodo.Attribute("patternUnits");
        if (string.Equals(unita, "objectBoundingBox", StringComparison.Ordinal))
        {
            return null;
        }

        var larghezza = Lunghezza((string?)nodo.Attribute("width"));
        var altezza = Lunghezza((string?)nodo.Attribute("height"));

        return larghezza is > 0 && altezza is > 0
            ? (Arrotonda(larghezza.Value), Arrotonda(altezza.Value))
            : null;
    }

    /// <summary>
    /// La trasformazione del nodo &lt;pattern&gt; diventa quella del pattern.
    ///
    /// È l'unica trasformazione che non va cotta dentro la geometria, perché nel modello
    /// esiste già: scala, rotazione e traslazione sono proprietà della definizione. Con
    /// questa lettura un SVG esportato dall'applicazione e poi riaperto torna identico.
    /// </summary>
    private static void LeggiTrasformazioneDelPattern(XElement nodo, PatternDefinition definizione,
                                                      List<TestoNominato> avvisi)
    {
        var testo = (string?)nodo.Attribute("patternTransform");
        if (string.IsNullOrWhiteSpace(testo))
        {
            return;
        }

        var riconosciute = 0;
        var totali = 0;
        var posizione = 0;

        while (posizione < testo.Length)
        {
            var apertura = testo.IndexOf('(', posizione);
            if (apertura < 0)
            {
                break;
            }

            var chiusura = testo.IndexOf(')', apertura);
            if (chiusura < 0)
            {
                break;
            }

            var nome = testo[posizione..apertura].Trim(' ', ',', '\t', '\r', '\n');
            var numeri = Numeri(testo[(apertura + 1)..chiusura]);
            posizione = chiusura + 1;
            totali++;

            switch (nome)
            {
                case "scale" when numeri.Count >= 1:
                    definizione.Scale = Arrotonda(numeri[0]);
                    riconosciute++;
                    break;

                case "rotate" when numeri.Count >= 1:
                    definizione.Rotation = Arrotonda(((numeri[0] % 360) + 360) % 360);
                    riconosciute++;
                    break;

                case "translate" when numeri.Count >= 1:
                    definizione.TranslateX = Arrotonda(numeri[0]);
                    definizione.TranslateY = numeri.Count > 1 ? Arrotonda(numeri[1]) : 0;
                    riconosciute++;
                    break;
            }
        }

        if (totali > riconosciute)
        {
            avvisi.Add(new(
                "imp.trasformazionePersa",
                "The pattern's transformation held something the model does not have: only scale, "
                + "rotation and translation have been read."));
        }
    }

    // ------------------------------------------------------------------ l'albero

    /// <summary>Quello che un elemento eredita da chi lo contiene.</summary>
    private sealed record Contesto(
        Trasformazione Trasformazione,
        IReadOnlyDictionary<string, string> Ereditate,
        double Opacita);

    /// <summary>Una forma riconosciuta, con i valori già risolti e trasformati.</summary>
    private sealed record Candidato(
        IVectorElementPlugin Plugin,
        Dictionary<string, string?> Attributi,
        string? Testo);

    /// <summary>
    /// Tag che <b>contengono</b> disegno invece di esserlo: si attraversano.
    /// <c>switch</c> e <c>a</c> ci stanno perché nel caso normale il loro contenuto si vede.
    /// </summary>
    private static readonly HashSet<string> Contenitori =
        new(StringComparer.Ordinal) { "g", "svg", "a", "switch" };

    /// <summary>
    /// Tag che definiscono qualcosa da riusare altrove e non vengono disegnati dove stanno:
    /// il loro contenuto non va importato, altrimenti comparirebbero forme che nel documento
    /// originale non si vedono.
    /// </summary>
    private static readonly HashSet<string> Definizioni =
        new(StringComparer.Ordinal)
        {
            "defs", "symbol", "clipPath", "mask", "marker", "pattern", "linearGradient",
            "radialGradient", "filter", "style", "title", "desc", "metadata",
        };

    /// <summary>Attributi di presentazione che in SVG si ereditano, e che qui interessano.</summary>
    private static readonly string[] Ereditabili =
    [
        "fill", "fill-opacity", "stroke", "stroke-width", "stroke-opacity",
        "font-family", "font-size", "font-weight", "text-anchor",
    ];

    private void Percorri(XElement nodo, Contesto contesto, List<Candidato> candidati,
                          Contatore ignorati, Contatore perdite)
    {
        foreach (var figlio in nodo.Elements())
        {
            // Fuori dallo spazio dei nomi SVG non c'è disegno: i file dei programmi di
            // grafica portano dentro i propri dati — <sodipodi:namedview>, <rdf:RDF>,
            // <cc:Work> — e contarli fra le forme lasciate fuori farebbe leggere come un
            // difetto quello che non è nemmeno un disegno.
            var spazio = figlio.Name.NamespaceName;
            if (spazio.Length > 0 && !string.Equals(spazio, SpazioSvg, StringComparison.Ordinal))
            {
                continue;
            }

            var tag = figlio.Name.LocalName;

            if (Definizioni.Contains(tag))
            {
                if (tag is "style")
                {
                    ignorati.Aggiungi(new(
                        "imp.foglioDiStile",
                        "an internal stylesheet: colours declared with \u00abclass\u00bb are not read"));
                }

                continue;
            }

            var proprie = AttributiDi(figlio);

            if (Invisibile(proprie))
            {
                continue;
            }

            var trasformazione = contesto.Trasformazione;
            if (proprie.TryGetValue("transform", out var testoTrasformazione) && testoTrasformazione is not null)
            {
                var letta = Trasformazione.Leggi(testoTrasformazione);
                trasformazione = trasformazione.Componi(letta);
            }

            var ereditate = Eredita(contesto.Ereditate, proprie);
            var opacita = contesto.Opacita * (Numero(Valore(proprie, "opacity")) ?? 1.0);
            var figlioContesto = new Contesto(trasformazione, ereditate, opacita);

            if (Contenitori.Contains(tag))
            {
                Percorri(figlio, figlioContesto, candidati, ignorati, perdite);
                continue;
            }

            if (!_registry.TryGet(tag, out var plugin) || plugin is null)
            {
                // Un tag che nessun plugin gestisce: può essere un <use>, un <foreignObject>
                // o un tipo che questa installazione non ha registrato.
                ignorati.Aggiungi(new(
                    "imp.nessunPlugin",
                    "<{0}>: no registered plugin knows how to represent it",
                    tag));
                continue;
            }

            if (!trasformazione.ProvaAScomporre(out _, out _, out _, out _, out _))
            {
                ignorati.Aggiungi(new(
                    "imp.trasformazioneIgnota",
                    "<{0}>: it sits under a transformation the model cannot represent (a skew, or "
                    + "a rotation together with a non-uniform scale)",
                    tag));
                continue;
            }

            if (SenzaColore(proprie, ereditate))
            {
                // Un elemento che non ha né riempimento né bordo che il modello sappia
                // rappresentare non si vedrebbe: è il caso di una forma riempita con un
                // gradiente o con un altro pattern. Importarla invisibile la farebbe
                // comparire nell'elenco degli elementi senza comparire nel disegno.
                ignorati.Aggiungi(new(
                    "imp.riempimentoIgnoto",
                    "<{0}>: it is filled with something the model does not have (a gradient or "
                    + "another pattern) and it has no stroke",
                    tag));
                continue;
            }

            AnnotaPerdite(tag, proprie, perdite);
            candidati.Add(Prepara(plugin, figlio, proprie, figlioContesto, perdite));
        }
    }

    /// <summary>
    /// Quello che si perde pur importando la forma.
    ///
    /// Non sono forme scartate — la forma entra — ma pezzi del suo aspetto che il modello non
    /// ha. Vanno detti lo stesso: senza, il resoconto dichiara «tutto preso» mentre il
    /// rettangolo importato ha perso gli angoli arrotondati, e chi guarda pensa a un difetto
    /// del disegno.
    /// </summary>
    private static void AnnotaPerdite(string tag, IReadOnlyDictionary<string, string?> proprie,
                                      Contatore perdite)
    {
        if (tag == "rect" && (proprie.ContainsKey("rx") || proprie.ContainsKey("ry")))
        {
            perdite.Aggiungi(new(
                "imp.angoliArrotondati",
                "rounded corners: the model's rectangle has sharp ones, the shape comes in square"));
        }

        if (proprie.ContainsKey("clip-path") || proprie.ContainsKey("mask"))
        {
            perdite.Aggiungi(new(
                "imp.ritaglioMaschera",
                "a clip or a mask: the model has neither, the shape comes in whole"));
        }

        if (proprie.ContainsKey("filter"))
        {
            perdite.Aggiungi(new(
                "imp.filtro",
                "a filter (blur, shadow or the like): the shape comes in without it"));
        }
    }

    /// <summary>
    /// Vero quando l'elemento non ha nessuna delle due tinte che il modello sa rappresentare.
    ///
    /// Attenzione al valore assente, che non è «niente»: in SVG un elemento senza
    /// <c>fill</c> è nero, non trasparente. Solo un «none», un «transparent» o un
    /// riferimento — cioè un gradiente o un motivo — tolgono davvero la tinta.
    /// </summary>
    private static bool SenzaColore(IReadOnlyDictionary<string, string?> proprie,
                                    IReadOnlyDictionary<string, string> ereditate)
    {
        var riempimento = Valore(proprie, "fill")
                          ?? (ereditate.TryGetValue("fill", out var f) ? f : null);
        var bordo = Valore(proprie, "stroke")
                    ?? (ereditate.TryGetValue("stroke", out var b) ? b : null);

        // I due valori assenti non significano la stessa cosa, ed è qui che si sbaglia:
        // un elemento senza «fill» è NERO, un elemento senza «stroke» non ha bordo.
        var senzaRiempimento = riempimento is not null && Colore(riempimento) is null;
        var senzaBordo = bordo is null || Colore(bordo) is null;

        return senzaRiempimento && senzaBordo;
    }

    /// <summary>Attributi dell'elemento, con quelli scritti in <c>style</c> che prevalgono.</summary>
    private static Dictionary<string, string?> AttributiDi(XElement elemento)
    {
        var fuori = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var attributo in elemento.Attributes())
        {
            if (attributo.IsNamespaceDeclaration)
            {
                continue;
            }

            // xlink:href e href sono lo stesso attributo scritto in due epoche diverse.
            fuori[attributo.Name.LocalName] = attributo.Value;
        }

        // Lo stile in linea vince sugli attributi di presentazione: è la regola della
        // specifica, e i programmi di disegno scrivono quasi tutto lì dentro.
        if (fuori.TryGetValue("style", out var stile) && stile is not null)
        {
            foreach (var dichiarazione in stile.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var duepunti = dichiarazione.IndexOf(':');
                if (duepunti <= 0)
                {
                    continue;
                }

                fuori[dichiarazione[..duepunti].Trim()] = dichiarazione[(duepunti + 1)..].Trim();
            }
        }

        return fuori;
    }

    private static bool Invisibile(IReadOnlyDictionary<string, string?> attributi) =>
        string.Equals(Valore(attributi, "display"), "none", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Valore(attributi, "visibility"), "hidden", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> Eredita(
        IReadOnlyDictionary<string, string> ereditate, IReadOnlyDictionary<string, string?> proprie)
    {
        var fuori = new Dictionary<string, string>(ereditate, StringComparer.Ordinal);

        foreach (var nome in Ereditabili)
        {
            var valore = Valore(proprie, nome);
            if (valore is not null)
            {
                fuori[nome] = valore;
            }
        }

        return fuori;
    }

    // ------------------------------------------------------------------ un elemento

    private static Candidato Prepara(IVectorElementPlugin plugin, XElement nodo,
                                     Dictionary<string, string?> proprie, Contesto contesto,
                                     Contatore ignorati)
    {
        // Si parte da ciò che l'elemento eredita e ci si scrive sopra ciò che dichiara: il
        // risultato è l'aspetto che l'elemento ha davvero, che è quello da importare.
        var risolti = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (nome, valore) in contesto.Ereditate)
        {
            risolti[nome] = valore;
        }

        foreach (var (nome, valore) in proprie)
        {
            risolti[nome] = valore;
        }

        risolti["opacity"] = InvariantNumber.Format(Arrotonda(contesto.Opacita, 3));

        // La trasformazione si divide in due: la parte che si può cuocere nella geometria —
        // scala e traslazione — e la rotazione, che il modello sa rappresentare da sé. La
        // seconda viaggia come se fosse un attributo, e finisce nelle proprietà dell'elemento
        // per lo stesso meccanismo di tutte le altre.
        contesto.Trasformazione.ProvaAScomporre(
            out var geometrica, out var gradi, out var specchiaX, out var centroX, out var centroY);

        var trasformati = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (nome, valore) in risolti)
        {
            trasformati[nome] = geometrica.Applica(nome, valore, ignorati.Aggiungi);
        }

        if (Math.Abs(gradi) > 1e-9 || specchiaX)
        {
            trasformati["rotation"] = InvariantNumber.Format(Math.Round(gradi, 4));
            trasformati["originX"] = InvariantNumber.Format(Math.Round(centroX, 4));
            trasformati["originY"] = InvariantNumber.Format(Math.Round(centroY, 4));

            if (specchiaX)
            {
                trasformati["flipX"] = "true";
            }
        }

        var testo = nodo.Nodes().OfType<XText>().Any()
            ? string.Concat(nodo.Nodes().OfType<XText>().Select(t => t.Value)).Trim()
            : null;

        return new Candidato(plugin, trasformati, string.IsNullOrEmpty(testo) ? null : testo);
    }

    // ------------------------------------------------------------------ il documento

    /// <summary>
    /// Da forme risolte a Pattern, passando dal serializzatore.
    ///
    /// I valori predefiniti li mette il plugin con <c>Create()</c>; su quelli si sovrappone
    /// ciò che il documento dice. Il giro dal JSON serve proprio a questo: permette di
    /// scrivere una proprietà conoscendone solo il nome, e di ignorare quelle che il modello
    /// non ha, senza che l'importatore sappia nulla dei tipi concreti.
    /// </summary>
    private Pattern Materializza(List<Candidato> candidati, double larghezza, double altezza, string nome)
    {
        var portante = new Pattern { Name = nome };
        portante.Definition.Width = larghezza;
        portante.Definition.Height = altezza;

        foreach (var candidato in candidati)
        {
            portante.Definition.Elements.Add(candidato.Plugin.Create());
        }

        var documento = JsonNode.Parse(_serializer.Serialize(portante))!.AsObject();
        var elementi = documento["definition"]!["elements"]!.AsArray();

        for (var i = 0; i < candidati.Count; i++)
        {
            Sovrapponi(elementi[i]!.AsObject(), candidati[i]);
        }

        return _serializer.Deserialize(documento.ToJsonString());
    }

    private static void Sovrapponi(JsonObject nodo, Candidato candidato)
    {
        foreach (var (nomeSvg, valore) in candidato.Attributi)
        {
            var chiave = CamelCase(nomeSvg);

            // La proprietà deve esistere già: è il documento prodotto dal plugin a dire
            // quali proprietà il tipo possiede, e un attributo SVG che non corrisponde a
            // nessuna di esse non ha un posto dove andare.
            if (chiave is "id" or "type" || !nodo.ContainsKey(chiave))
            {
                continue;
            }

            if (nodo[chiave] is JsonValue predefinito && predefinito.TryGetValue<double>(out _))
            {
                var numero = Numero(valore);
                if (numero is not null)
                {
                    nodo[chiave] = Arrotonda(numero.Value, 4);
                }

                continue;
            }

            // Le proprietà vero/falso del modello — le specchiature — non arrivano da un
            // attributo SVG ma dalla scomposizione della trasformazione, che le scrive qui
            // come «true» o «false».
            if (nodo[chiave] is JsonValue booleano && booleano.TryGetValue<bool>(out _))
            {
                if (bool.TryParse(valore, out var acceso))
                {
                    nodo[chiave] = acceso;
                }

                continue;
            }

            // Tutto il resto è testo. Riempimento e bordo passano però dal riconoscimento
            // dei colori: «red», «rgb(…)» e «none» sono valori legittimi in SVG e non lo
            // sono nel modello, che vuole un esadecimale o niente.
            if (chiave is "fill" or "stroke")
            {
                nodo[chiave] = Colore(valore);
            }
            else if (valore is not null)
            {
                nodo[chiave] = valore;
            }
        }

        // Il testo di un <text> non è un attributo: sta dentro il tag. Se il modello dichiara
        // una proprietà che lo accoglie, ce lo si mette.
        if (candidato.Testo is not null && nodo.ContainsKey("content"))
        {
            nodo["content"] = candidato.Testo;
        }

        PredefinitiDellaSpecifica(nodo, candidato.Attributi);
    }

    /// <summary>
    /// Quello che il documento non dice lo dice la specifica, e non il plugin.
    ///
    /// <para>
    /// È la correzione di un errore che si vede solo sui file veri. Un tracciato senza
    /// <c>fill</c> in SVG è NERO PIENO; il plugin «path», che nasce per disegnare linee, ha
    /// come valore predefinito nessun riempimento e un bordo nero. Lasciando decidere il
    /// plugin, un logo importato usciva a contorno invece che pieno — con il documento che
    /// dichiarava di aver preso tutto, perché in effetti nessuna forma era stata scartata.
    /// </para>
    ///
    /// <para>
    /// I valori iniziali della specifica sono: riempimento nero, nessun bordo, bordo spesso
    /// uno. Valgono solo dove il documento tace: un attributo scritto, anche ereditato da un
    /// gruppo, è già stato applicato prima.
    /// </para>
    /// </summary>
    private static void PredefinitiDellaSpecifica(JsonObject nodo, IReadOnlyDictionary<string, string?> attributi)
    {
        if (!attributi.ContainsKey("fill") && nodo.ContainsKey("fill"))
        {
            nodo["fill"] = "#000000";
        }

        // Il bordo assente si scrive solo su un elemento che ha anche un riempimento: una
        // forma che si disegna col solo bordo — una linea — resterebbe altrimenti senza
        // niente da disegnare, e un documento che la contiene senza bordo è comunque un
        // documento in cui quella linea non si vede.
        if (!attributi.ContainsKey("stroke") && nodo.ContainsKey("stroke") && nodo.ContainsKey("fill"))
        {
            nodo["stroke"] = null;
        }

        if (!attributi.ContainsKey("stroke-width") && nodo.ContainsKey("strokeWidth"))
        {
            nodo["strokeWidth"] = 1;
        }
    }

    // ------------------------------------------------------------------ conversioni

    /// <summary><c>stroke-width</c> → <c>strokeWidth</c>: la convenzione del documento JSON.</summary>
    private static string CamelCase(string nome)
    {
        if (!nome.Contains('-', StringComparison.Ordinal))
        {
            return nome;
        }

        var pezzi = nome.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var fuori = pezzi[0];
        for (var i = 1; i < pezzi.Length; i++)
        {
            fuori += char.ToUpperInvariant(pezzi[i][0]) + pezzi[i][1..];
        }

        return fuori;
    }

    /// <summary>
    /// I colori che SVG ammette e il modello no.
    ///
    /// L'elenco dei nomi non è completo — sono centoquarantotto — ma copre quelli che si
    /// incontrano davvero. Un nome non riconosciuto lascia l'elemento al colore predefinito
    /// del plugin, che è meglio di un colore inventato.
    /// </summary>
    private static readonly Dictionary<string, string> Nomi = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#000000", ["white"] = "#ffffff", ["red"] = "#ff0000", ["lime"] = "#00ff00",
        ["blue"] = "#0000ff", ["yellow"] = "#ffff00", ["cyan"] = "#00ffff", ["aqua"] = "#00ffff",
        ["magenta"] = "#ff00ff", ["fuchsia"] = "#ff00ff", ["silver"] = "#c0c0c0", ["gray"] = "#808080",
        ["grey"] = "#808080", ["maroon"] = "#800000", ["olive"] = "#808000", ["green"] = "#008000",
        ["purple"] = "#800080", ["teal"] = "#008080", ["navy"] = "#000080", ["orange"] = "#ffa500",
        ["gold"] = "#ffd700", ["pink"] = "#ffc0cb", ["brown"] = "#a52a2a", ["beige"] = "#f5f5dc",
        ["ivory"] = "#fffff0", ["khaki"] = "#f0e68c", ["salmon"] = "#fa8072", ["coral"] = "#ff7f50",
        ["crimson"] = "#dc143c", ["indigo"] = "#4b0082", ["violet"] = "#ee82ee", ["turquoise"] = "#40e0d0",
        ["tan"] = "#d2b48c", ["plum"] = "#dda0dd", ["orchid"] = "#da70d6", ["tomato"] = "#ff6347",
        ["wheat"] = "#f5deb3", ["lavender"] = "#e6e6fa", ["darkgray"] = "#a9a9a9", ["darkgrey"] = "#a9a9a9",
        ["lightgray"] = "#d3d3d3", ["lightgrey"] = "#d3d3d3", ["dimgray"] = "#696969", ["dimgrey"] = "#696969",
        ["darkblue"] = "#00008b", ["darkgreen"] = "#006400", ["darkred"] = "#8b0000",
        ["lightblue"] = "#add8e6", ["lightgreen"] = "#90ee90", ["steelblue"] = "#4682b4",
        ["skyblue"] = "#87ceeb", ["slategray"] = "#708090", ["slategrey"] = "#708090",
        ["whitesmoke"] = "#f5f5f5", ["gainsboro"] = "#dcdcdc", ["firebrick"] = "#b22222",
        ["chocolate"] = "#d2691e", ["peru"] = "#cd853f", ["sienna"] = "#a0522d",
    };

    private static JsonNode? Colore(string? valore)
    {
        if (valore is null)
        {
            return null;
        }

        var testo = valore.Trim();

        // «none» e «transparent» significano che l'elemento non ha quel colore: nel modello
        // si dice con l'assenza del valore, che è esattamente la stessa cosa.
        if (testo.Length == 0
            || testo.Equals("none", StringComparison.OrdinalIgnoreCase)
            || testo.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Un riferimento è un gradiente, un motivo o simili: il modello non li ha, e il
        // colore resta assente invece di diventare un nero che nell'originale non c'era.
        if (testo.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (testo.Equals("currentColor", StringComparison.OrdinalIgnoreCase))
        {
            return "#000000";
        }

        if (testo.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var canali = Numeri(testo);
            if (canali.Count >= 3)
            {
                return "#" + string.Concat(canali.Take(3).Select(c =>
                    ((int)Math.Clamp(Math.Round(c), 0, 255)).ToString("x2", CultureInfo.InvariantCulture)));
            }

            return null;
        }

        if (Nomi.TryGetValue(testo, out var esadecimale))
        {
            return esadecimale;
        }

        return SvgColor.Normalize(testo) is { } normalizzato ? normalizzato : null;
    }

    /// <summary>Un numero SVG: senza unità, oppure con una di quelle che hanno una misura fissa.</summary>
    private static double? Lunghezza(string? valore)
    {
        if (valore is null)
        {
            return null;
        }

        var testo = valore.Trim();
        var fattore = 1.0;

        foreach (var (unita, quanti) in new[]
                 {
                     ("px", 1.0), ("pt", 96.0 / 72), ("pc", 16.0),
                     ("mm", 96.0 / 25.4), ("cm", 96.0 / 2.54), ("in", 96.0),
                 })
        {
            if (testo.EndsWith(unita, StringComparison.OrdinalIgnoreCase))
            {
                testo = testo[..^2];
                fattore = quanti;
                break;
            }
        }

        return double.TryParse(testo, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero)
            ? numero * fattore
            : null;
    }

    private static double? Numero(string? valore) => Lunghezza(valore);

    private static string? Valore(IReadOnlyDictionary<string, string?> attributi, string nome) =>
        attributi.TryGetValue(nome, out var valore) ? valore : null;

    internal static List<double> Numeri(string testo)
    {
        var fuori = new List<double>();
        var corrente = new System.Text.StringBuilder();

        foreach (var c in testo)
        {
            if (char.IsAsciiDigit(c) || c == '.'
                || (c == '-' && (corrente.Length == 0 || corrente[^1] is 'e' or 'E'))
                || (c == '+' && corrente.Length > 0 && corrente[^1] is 'e' or 'E')
                || ((c == 'e' || c == 'E') && corrente.Length > 0))
            {
                corrente.Append(c);
                continue;
            }

            Deposita(fuori, corrente);
        }

        Deposita(fuori, corrente);
        return fuori;

        static void Deposita(List<double> fuori, System.Text.StringBuilder corrente)
        {
            if (corrente.Length > 0)
            {
                if (double.TryParse(corrente.ToString(), NumberStyles.Float,
                                    CultureInfo.InvariantCulture, out var numero))
                {
                    fuori.Add(numero);
                }

                corrente.Clear();
            }
        }
    }

    internal static double Arrotonda(double valore, int cifre = 4) => Math.Round(valore, cifre);

    // ------------------------------------------------------------------ avvisi raggruppati

    /// <summary>
    /// Gli avvisi contati per motivo: un documento con duecento forme sotto una rotazione
    /// produrrebbe altrimenti duecento righe uguali, e nessuno le leggerebbe.
    /// </summary>
    private sealed class Contatore
    {
        // Si raggruppa per nome della frase e valori, non per la frase già scritta: due
        // elementi scartati per la stessa ragione devono contare due, e lo devono fare prima
        // che qualcuno decida in che lingua leggerli.
        private readonly Dictionary<string, (TestoNominato Motivo, int Quanti)> _motivi =
            new(StringComparer.Ordinal);

        public int Totale { get; private set; }

        public void Aggiungi(TestoNominato motivo)
        {
            var chiave = motivo.Chiave + "\u0000" + string.Join('\u0000', motivo.Valori);

            _motivi[chiave] = _motivi.TryGetValue(chiave, out var visto)
                ? (visto.Motivo, visto.Quanti + 1)
                : (motivo, 1);

            Totale++;
        }

        public IEnumerable<TestoNominato> Righe() =>
            _motivi.Values.Select(m => m.Quanti == 1
                ? m.Motivo
                : new TestoNominato("imp.piuVolte", "{0} ({1} times)", m.Motivo, m.Quanti));
    }
}
