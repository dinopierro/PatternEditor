using System.Globalization;
using System.Text;
using PatternEditor.Core.Formatting;
using PatternEditor.Core.Localization;
using PatternEditor.Services.Riconoscimento;

namespace PatternEditor.Services;

/// <summary>
/// Ricava un pattern vettoriale da un'immagine a punti.
/// </summary>
///
/// <remarks>
/// <para>
/// Il servizio <b>scrive un SVG</b> e lo passa all'importatore che c'è già. Non è un
/// espediente: è ciò che permette a una funzione nuova di rientrare dalla porta di una
/// vecchia. La materializzazione degli elementi, il rispetto del registro dei plugin, la
/// finestra che dice che cosa è entrato — tutto quel lavoro è già stato fatto una volta, e
/// rifarlo qui vorrebbe dire averne due versioni che divergono.
/// </para>
/// <para>
/// La catena è: <b>bande di colore</b>, <b>zone</b> di ciascuna banda, <b>forme</b> per ogni
/// zona — più, quando si trova, il <b>reticolo</b> che dice quale pezzo di immagine è la
/// tessera. L'ordine conta: le bande vengono per prime perché tolgono di mezzo la domanda
/// impossibile. Il riconoscitore precedente cominciava chiedendosi che cosa fosse sfondo e che
/// cosa disegno, e su un pavimento dove ogni mattone è di un rosso diverso quella domanda non
/// ha risposta — qualunque scelta è il negativo di un'altra altrettanto buona. Qui non si
/// chiede: si dice quanti sono i colori, e si disegna ognuno dove sta.
/// </para>
/// </remarks>
/// <summary>
/// Quanto si vuole essere fedeli all'immagine, e che cosa si è disposti a pagare.
/// </summary>
public enum Fedelta
{
    /// <summary>
    /// Poche centinaia di elementi: un pattern che si apre nell'editor e si modifica a mano.
    /// </summary>
    Normale,

    /// <summary>
    /// Le correzioni vanno avanti finché il residuo non è trascurabile. Resta tutto
    /// vettoriale — quindi nitido a ogni scala e ancora modificabile — ma gli elementi
    /// diventano migliaia, e un pattern con migliaia di elementi si guarda più che toccarlo.
    /// </summary>
    Massima,

    /// <summary>
    /// Il vettoriale leggero, e sopra un'immagine trasparente con tutto quello che non è
    /// riuscito a rendere.
    /// </summary>
    ///
    /// <remarks>
    /// Si chiama così perché è quello che è: una copia da cui ricalcare, non un disegno. Somiglia
    /// all'originale quasi alla perfezione e paga tre prezzi che vanno detti prima e non dopo.
    /// Torna a dipendere dalla risoluzione, e su una stratigrafia stampata a scale diverse la
    /// toppa sgrana mentre il vettoriale resta nitido. Non si modifica: dentro l'immagine non
    /// si ricolora una fuga né si sposta un mattone. E pesa, perché i punti viaggiano dentro il
    /// documento del pattern.
    /// </remarks>
    Ricalco,
}

public interface IRasterPatternImporter
{
    /// <summary>
    /// Analizza i pixel e restituisce l'esito, pronto per la stessa finestra dell'importazione
    /// di un SVG, insieme al confronto con il disegno di partenza.
    /// </summary>
    ///
    /// <param name="rgba">I pixel già decodificati, quattro byte per punto.</param>
    /// <param name="larghezza">Larghezza in punti.</param>
    /// <param name="altezza">Altezza in punti.</param>
    /// <param name="nome">Il nome da dare al pattern.</param>
    /// <param name="colori">
    /// In quante bande dividere i colori. Poche danno un disegno netto e pochi elementi, molte
    /// un disegno fedele e un pattern che non si modifica più.
    /// </param>
    /// <param name="fedelta">Quanto avvicinarsi all'immagine, e a quale prezzo.</param>
    EsitoConversione Import(byte[] rgba, int larghezza, int altezza, string nome, int colori = 4,
                            Fedelta fedelta = Fedelta.Normale);
}

/// <summary>
/// Che cosa è venuto fuori da un'immagine: il pattern, e quanto somiglia all'originale.
/// </summary>
///
/// <param name="Esito">L'esito dell'importazione, uguale a quello di un SVG.</param>
/// <param name="Confronto">
/// Il confronto fra il disegno di partenza e la ricostruzione, oppure <c>null</c> se non si
/// è arrivati a ricostruire niente.
/// </param>
public sealed record EsitoConversione(SvgImportResult Esito, Confronto? Confronto);

/// <inheritdoc cref="IRasterPatternImporter" />
public sealed class RasterPatternImporter : IRasterPatternImporter
{
    /// <summary>
    /// Il lato oltre il quale l'immagine si rimpicciolisce prima di guardarla.
    /// </summary>
    ///
    /// <remarks>
    /// Non è un compromesso fra precisione e velocità: è la misura a cui si riconosce
    /// <b>meglio</b>. Un retino tecnico di quattrocento pixel di lato ha già più dettaglio di
    /// quanto ne serva a dire dove sta un cerchio; una fotografia di pavimento da novecento ne
    /// ha sei volte troppo, e quel troppo non è disegno ma grana — che rimpicciolendo si media
    /// via, mentre a grandezza piena diventa centinaia di zone da disegnare.
    /// </remarks>
    public const int LatoDiLavoro = 400;

    /// <summary>
    /// Oltre questo numero di elementi non si va: un pattern che non si può più modificare a
    /// mano è il ricalco che questo strumento serve proprio a non fare.
    /// </summary>
    private const int MassimoDiForme = 400;

    /// <summary>Il tetto a fedeltà massima: alto abbastanza da non mordere quasi mai.</summary>
    private const int MassimoDiFormeAssai = 4000;

    /// <summary>Di quanto un poligono può scostarsi dal bordo vero della zona.</summary>
    private const double Tolleranza = 1.2;

    /// <summary>
    /// La tolleranza delle correzioni, più stretta.
    /// </summary>
    ///
    /// <remarks>
    /// Una correzione non è una zona qualsiasi: è quasi sempre una scheggia lunga e sottile
    /// lungo un bordo, e su una scheggia larga tre punti una tolleranza di uno e due la
    /// ingrossa invece di semplificarla. Sulla foto di pavimento quel solo scarto dipingeva di
    /// troppo il sette per cento della tessera — cioè rimetteva, come errore, metà di quello
    /// che era appena riuscita a togliere.
    /// </remarks>
    private const double TolleranzaDelResiduo = 0.6;

    /// <summary>
    /// Quanto una correzione deve riempire il proprio ingombro per valere la pena.
    /// </summary>
    ///
    /// <remarks>
    /// È lo stesso guasto della prima passata, un piano più sotto, e va riconosciuto due volte
    /// perché si ripresenta identico: anche il <b>residuo</b> di una rete è a forma di rete, e
    /// riempirne il contorno esterno copre tutti i punti che erano già giusti. Nella prima
    /// passata il rimedio è disegnare anche le zone di dentro, che le ricoprono; qui non si può,
    /// perché i punti di dentro sono corretti e nel residuo non compaiono proprio.
    ///
    /// Quindi si tengono solo le correzioni <b>compatte</b>. Una scheggia lungo un bordo riempie
    /// quasi tutto il proprio ingombro ed è esattamente ciò che serve; una che ne riempie un
    /// quinto non è una correzione ma un pezzo di disegno sbagliato in modo strutturale, e
    /// rattopparlo con un poligono pieno fa più danno che bene.
    /// </remarks>
    private const double CompattezzaDiUnaCorrezione = 0.34;

    /// <summary>Quanto una zona deve riempire il proprio ingombro per essere un rettangolo.</summary>
    private const double PienoDiUnRettangolo = 0.93;

    /// <summary>Quante volte si torna a guardare dove il disegno sbaglia.</summary>
    private const int Giri = 3;

    /// <summary>Quante a fedeltà massima, dove si insiste finché il residuo non si consuma.</summary>
    private const int GiriAssai = 8;

    /// <summary>Sotto questa quota di punti sbagliati non vale la pena di un altro giro.</summary>
    private const double ResiduoTrascurabile = 0.01;

    /// <summary>
    /// L'area minima di una zona del residuo, più bassa di quella di partenza: una correzione
    /// è quasi sempre una scheggia lungo un bordo, ed è proprio quella che serve.
    /// </summary>
    private const int MinimaDelResiduo = 4;

    private readonly ISvgPatternImporter _svg;

    public RasterPatternImporter(ISvgPatternImporter svg) => _svg = svg;

    public EsitoConversione Import(byte[] rgba, int larghezza, int altezza, string nome,
                                   int colori = 4, Fedelta fedelta = Fedelta.Normale)
    {
        var massimo = fedelta == Fedelta.Massima ? MassimoDiFormeAssai : MassimoDiForme;
        var giri = fedelta == Fedelta.Massima ? GiriAssai : Giri;

        var immagine = new Immagine(rgba, larghezza, altezza)
            .Ridotta(FattoreDiRiduzione(larghezza, altezza));

        var reticolo = Reticolo.Trova(immagine);

        // Se la ripetizione si vede, si lavora sulla sola tessera: disegnarla una volta è tutto
        // il senso di un pattern. Se non si vede, si prende l'immagine intera e lo si dice.
        var cella = reticolo.Sicuro
            ? immagine.Ritaglia(0, 0, reticolo.Larghezza, reticolo.Altezza)
            : immagine;

        var tavolozza = Bande.Dividi(cella, colori);
        var zone = Regioni.Trova(tavolozza);

        var fondo = tavolozza.Colori[0];
        var forme = new List<Forma>();

        // La prima passata prende metà del bilancio, non tutto.
        //
        // Sembra uno spreco e non lo è: le zone si ordinano per estensione, e l'estensione non
        // dice quanto una zona serva al disegno. Con tre colori la foto di pavimento produceva
        // settecentosessanta zone, il tetto ne teneva quattrocento, e quelle quattrocento erano
        // le più <i>grandi</i> — cioè spesso quelle che il fondo già rendeva bene. La metà
        // che resta si spende dove il disegno sbaglia, e sbagliare lo si sa solo dopo aver
        // disegnato.
        Aggiungi(zone, forme, massimo / 2, massimo,
                 zona => Esadecimale(tavolozza.Colori[zona.Banda]), saltaIlFondo: true,
                 Tolleranza);

        // Poi si guarda e si corregge. Si ridipinge quello che si è capito, si confronta punto
        // per punto con l'originale, e dove non corrisponde si ricomincia: quei punti tornano
        // a essere disegno da riconoscere, col loro colore vero preso dall'immagine. È la
        // stessa misura che fin qui serviva solo a raccontare il risultato, usata per
        // decidere il passo dopo.
        var residue = 0;

        for (var giro = 0; giro < giri && forme.Count < massimo; giro++)
        {
            var dipinto = Somiglianza.Dipingi(forme, fondo, cella.Larghezza, cella.Altezza);
            var sbagliate = Sbagliate(cella, dipinto, tavolozza);

            if (sbagliate.Quanti < cella.Larghezza * cella.Altezza * ResiduoTrascurabile)
            {
                break;
            }

            // L'ultimo giro si prende tutto quello che avanza: tenere da parte un bilancio che
            // nessuno spenderà più sarebbe solo disegno lasciato fuori.
            var quanti = giro == giri - 1
                ? massimo - forme.Count
                : (massimo - forme.Count) / 2;

            residue += Aggiungi(
                Regioni.Trova(sbagliate.Mappa, cella.Larghezza, cella.Altezza, MinimaDelResiduo)
                    .Where(z => z.Banda >= 0)
                    .ToList(),
                forme, quanti, massimo, zona => Esadecimale(Media(cella, zona)),
                saltaIlFondo: false, TolleranzaDelResiduo, CompattezzaDiUnaCorrezione);
        }

        // Il ricalco: quello che non si è riuscito a rendere viene incollato sopra come
        // immagine, con lo sfondo trasparente e i colori presi dall'originale. Si calcola qui,
        // dopo le correzioni, perché è di quello che <i>resta</i> che si tratta.
        var ricalco = fedelta == Fedelta.Ricalco
            ? Ricalco(cella, Somiglianza.Dipingi(forme, fondo, cella.Larghezza, cella.Altezza))
            : null;

        var confronto = Somiglianza.Misura(
            forme, fondo, cella.Larghezza, cella.Altezza,
            cella.Punti, cella.Larghezza, cella.Altezza);

        return Componi(
            reticolo, cella, tavolozza, forme, fondo, confronto, zone.Count, residue, massimo,
            ricalco, nome);
    }

    /// <summary>
    /// Aggiunge al disegno le zone più estese, fino al numero concesso.
    /// </summary>
    ///
    /// <param name="zone">Le zone, già ordinate dalla più estesa.</param>
    /// <param name="forme">Il disegno a cui aggiungerle.</param>
    /// <param name="quante">Quante se ne possono aggiungere.</param>
    /// <param name="tinta">Che colore dare a ciascuna zona.</param>
    /// <param name="saltaIlFondo">
    /// Vero nella prima passata: le zone del colore di fondo che vengono prima di ogni altra
    /// non servono, perché sotto hanno già il loro stesso colore e nessuno le ha ancora coperte.
    /// Falso nelle correzioni, dove ogni zona nasce proprio da un punto reso male.
    /// </param>
    private static int Aggiungi(List<Regione> zone, List<Forma> forme, int quante, int massimo,
                                Func<Regione, string> tinta, bool saltaIlFondo,
                                double tolleranza, double compattezzaMinima = 0)
    {
        var messe = 0;

        foreach (var zona in zone)
        {
            if (messe >= quante || forme.Count >= massimo)
            {
                break;
            }

            if (saltaIlFondo && zona.Banda == 0 && forme.Count == 0)
            {
                continue;
            }

            if (zona.Riempimento < compattezzaMinima)
            {
                continue;
            }

            forme.Add(Disegna(zona, tinta(zona), tolleranza));
            messe++;
        }

        return messe;
    }

    /// <summary>
    /// L'immagine di quello che le forme non sono riuscite a rendere, in base64.
    /// </summary>
    ///
    /// <remarks>
    /// Opaca dove il disegno sbaglia, col colore che quel punto ha nell'originale; trasparente
    /// dove il disegno è già giusto. Sovrapposta al vettoriale lo completa esattamente, ed è
    /// per questo che esiste e per questo che va chiesta: è la differenza fra un disegno e una
    /// copia.
    /// </remarks>
    private static string Ricalco(Immagine cella, Tela dipinto)
    {
        var punti = new byte[cella.Larghezza * cella.Altezza * 4];

        for (var y = 0; y < cella.Altezza; y++)
        {
            for (var x = 0; x < cella.Larghezza; x++)
            {
                var i = (y * cella.Larghezza + x) * 4;
                var (r, g, b) = cella.Colore(x, y);

                if (Somiglianza.SiSomigliano((r, g, b), dipinto.Colore(x, y)))
                {
                    continue;
                }

                punti[i] = r;
                punti[i + 1] = g;
                punti[i + 2] = b;
                punti[i + 3] = 255;
            }
        }

        return Convert.ToBase64String(Png.Scrivi(punti, cella.Larghezza, cella.Altezza));
    }

    /// <summary>
    /// Il colore medio dei punti della zona, letto dall'immagine di partenza.
    /// </summary>
    ///
    /// <remarks>
    /// Per le correzioni il colore non si prende dalla tavolozza ma dall'originale: una
    /// correzione nasce proprio dove le poche bande scelte non bastavano, e ridarle una di
    /// quelle bande vorrebbe dire ripetere l'errore che si sta correggendo.
    /// </remarks>
    private static (byte R, byte G, byte B) Media(Immagine immagine, Regione zona)
    {
        long r = 0, g = 0, b = 0;

        foreach (var (x, y) in zona.Punti)
        {
            var (pr, pg, pb) = immagine.Colore(x, y);
            r += pr;
            g += pg;
            b += pb;
        }

        var quanti = Math.Max(1, zona.Area);
        return ((byte)(r / quanti), (byte)(g / quanti), (byte)(b / quanti));
    }

    /// <summary>
    /// La mappa dei punti che il disegno non rende ancora bene.
    /// </summary>
    ///
    /// <remarks>
    /// Chi è già reso bene prende una banda negativa, che non appartiene a nessun colore e che
    /// il raggruppamento in zone scarta. Gli altri tengono la loro banda, così le correzioni
    /// restano separate per colore invece di fondersi in una macchia sola lungo ogni bordo.
    /// </remarks>
    private static (int[] Mappa, int Quanti) Sbagliate(Immagine cella, Tela dipinto,
                                                       Tavolozza tavolozza)
    {
        var mappa = new int[cella.Larghezza * cella.Altezza];
        var quanti = 0;

        for (var y = 0; y < cella.Altezza; y++)
        {
            for (var x = 0; x < cella.Larghezza; x++)
            {
                var i = y * cella.Larghezza + x;

                if (Somiglianza.SiSomigliano(cella.Colore(x, y), dipinto.Colore(x, y)))
                {
                    mappa[i] = -1;
                    continue;
                }

                mappa[i] = tavolozza.Mappa[i];
                quanti++;
            }
        }

        return (mappa, quanti);
    }

    /// <summary>
    /// La forma che rende una zona: un rettangolo quando lo è, una poligonale altrimenti.
    /// </summary>
    ///
    /// <remarks>
    /// Per ora sono solo due. Il riconoscitore precedente ne provava cinque a soglie fissate a
    /// mano, e quelle soglie erano metà dei suoi guai: un disco veniva poligono, un esagono
    /// veniva un fascio di tratti. Meglio poche forme giuste che molte forme indovinate — il
    /// cerchio e l'ellisse torneranno quando ci sarà un criterio che li scelga misurando
    /// invece che soppesando.
    /// </remarks>
    private static Forma Disegna(Regione zona, string colore, double tolleranza)
    {
        if (zona.Riempimento >= PienoDiUnRettangolo)
        {
            return new FormaRettangolo(zona.MinX, zona.MinY, zona.Larghezza, zona.Altezza, colore);
        }

        var vertici = Contorno.Semplifica(Contorno.Segui(zona), tolleranza);

        return vertici.Count >= 3
            ? new FormaPoligono(vertici, colore)
            : new FormaRettangolo(zona.MinX, zona.MinY, zona.Larghezza, zona.Altezza, colore);
    }

    /// <summary>Scrive il documento e il racconto di che cosa è successo.</summary>
    private EsitoConversione Componi(
        ReticoloTrovato reticolo, Immagine cella, Tavolozza tavolozza, List<Forma> forme,
        (byte R, byte G, byte B) fondo, Confronto confronto, int zone, int correzioni,
        int massimo, string? ricalco, string nome)
    {
        var avvisi = new List<TestoNominato>
        {
            reticolo.Sicuro
                ? new("ras.reticolo",
                      "Repeat found: the cell is {0} × {1} px, and the drawing falls back onto "
                      + "itself with {2}% confidence.",
                      reticolo.Larghezza, reticolo.Altezza,
                      (int)Math.Round(reticolo.Forza * 100))
                : new TestoNominato(
                      "ras.senzaReticolo",
                      "No repeat was found: the whole image has been taken as the cell. If the "
                      + "motif does repeat, try cropping closer to a few tiles."),

            new("ras.bande",
                "The image was read as {0} colours, {1} on {2}, and split into {3} areas.",
                tavolozza.Colori.Count, Esadecimale(tavolozza.Colori[^1]), Esadecimale(fondo), zone),
        };

        if (reticolo.Sicuro && reticolo.Motivi > 1)
        {
            avvisi.Add(new(
                "ras.reticoloObliquo",
                "The lattice is oblique — the rows are offset, as in brickwork — so the "
                + "rectangular cell holds {0} copies of the motif. Its translation vectors are "
                + "({1}, {2}) and ({3}, {4}).",
                reticolo.Motivi, reticolo.A.X, reticolo.A.Y, reticolo.B.X, reticolo.B.Y));
        }

        if (correzioni > 0)
        {
            avvisi.Add(new(
                "ras.correzioni",
                "{0} of the shapes are corrections: the drawing was painted, compared with the "
                + "original, and redrawn where it did not match — each with the colour it has "
                + "in the image.",
                correzioni));
        }

        if (ricalco is not null)
        {
            avvisi.Add(new(
                "ras.ricalco",
                "On top of the shapes sits an image with everything they could not render, on a "
                + "transparent background, {0} kB of it. The percentage below still judges the "
                + "drawing alone, not the image: the drawing is what you will be able to edit.",
                ricalco.Length * 3 / 4 / 1024));
        }

        if (zone > forme.Count + 1)
        {
            avvisi.Add(new(
                "ras.troppeZone",
                "Only the {0} largest areas were kept: a pattern with more elements than that "
                + "cannot be edited by hand. Fewer colours give a cleaner drawing.",
                massimo));
        }

        avvisi.Add(new(
            "ras.somiglianza",
            "The reconstruction matches {0}% of the drawing.",
            (int)Math.Round(confronto.Somiglianza * 100)));

        avvisi.Add(new(
            "ras.daRivedere",
            "The reconstruction is a proposal, not a copy: check the measurements and the "
            + "colours in the editor before saving."));

        var corpo = new StringBuilder();

        corpo.Append("  ")
             .Append($"<rect x=\"0\" y=\"0\" width=\"{Num(cella.Larghezza)}\" ")
             .Append($"height=\"{Num(cella.Altezza)}\" fill=\"{Esadecimale(fondo)}\" />")
             .Append('\n');

        foreach (var forma in forme)
        {
            corpo.Append("  ").Append(forma.Svg()).Append('\n');
        }

        // Il ricalco va per ultimo, e quindi sopra tutto il resto: è la toppa, e una toppa
        // sotto al disegno non copre niente.
        if (ricalco is not null)
        {
            corpo.Append("  ")
                 .Append($"<image x=\"0\" y=\"0\" width=\"{Num(cella.Larghezza)}\" ")
                 .Append($"height=\"{Num(cella.Altezza)}\" preserveAspectRatio=\"none\" ")
                 .Append($"href=\"data:image/png;base64,{ricalco}\" />")
                 .Append('\n');
        }

        var documento =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Num(cella.Larghezza)}\" " +
            $"height=\"{Num(cella.Altezza)}\" " +
            $"viewBox=\"0 0 {Num(cella.Larghezza)} {Num(cella.Altezza)}\">\n{corpo}</svg>";

        return new EsitoConversione(
            _svg.Import(documento, nome).ConAvvisiIniziali(avvisi), confronto);
    }

    /// <summary>Di quanto va rimpicciolita un'immagine per arrivare alla misura di lavoro.</summary>
    public static int FattoreDiRiduzione(int larghezza, int altezza) =>
        (Math.Max(larghezza, altezza) + LatoDiLavoro - 1) / LatoDiLavoro;

    private static string Esadecimale((byte R, byte G, byte B) colore) =>
        string.Create(CultureInfo.InvariantCulture, $"#{colore.R:x2}{colore.G:x2}{colore.B:x2}");

    private static string Num(double valore) => InvariantNumber.Format(Math.Round(valore, 2));
}
