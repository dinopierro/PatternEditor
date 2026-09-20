namespace PatternEditor.Services.Riconoscimento;

/// <summary>Uno spostamento intero sul piano dell'immagine.</summary>
public readonly record struct Vettore(int X, int Y)
{
    public double Lunghezza => Math.Sqrt((double)X * X + (double)Y * Y);

    /// <summary>Il prodotto vettoriale con un altro: zero quando sono paralleli.</summary>
    public long Incrocio(Vettore altro) => (long)X * altro.Y - (long)Y * altro.X;

    public long Scalare(Vettore altro) => (long)X * altro.X + (long)Y * altro.Y;

    public static Vettore operator -(Vettore a, Vettore b) => new(a.X - b.X, a.Y - b.Y);

    public static Vettore operator *(Vettore a, int k) => new(a.X * k, a.Y * k);
}

/// <summary>
/// Il reticolo di ripetizione di una trama, e la cella rettangolare che ne discende.
/// </summary>
///
/// <param name="A">Il primo vettore di traslazione.</param>
/// <param name="B">Il secondo.</param>
/// <param name="Larghezza">La larghezza della cella rettangolare.</param>
/// <param name="Altezza">L'altezza della cella rettangolare.</param>
/// <param name="Motivi">Quanti motivi del reticolo stanno dentro la cella rettangolare.</param>
/// <param name="Forza">Quanto la ripetizione si vede, da 0 a 1.</param>
/// <param name="Sicuro">
/// Falso quando la ripetizione non si è trovata, e la cella restituita è l'immagine intera
/// per mancanza di meglio.
/// </param>
public sealed record ReticoloTrovato(
    Vettore A, Vettore B, int Larghezza, int Altezza, int Motivi, double Forza, bool Sicuro);

/// <summary>
/// Trova ogni quanto una trama si ripete.
/// </summary>
///
/// <remarks>
/// <para>
/// La ripetizione di un motivo piano è un <b>reticolo</b>: due direzioni di traslazione, non
/// necessariamente perpendicolari e non necessariamente allineate ai bordi dell'immagine. È la
/// nozione che mancava al riconoscitore precedente, che cercava un passo orizzontale e uno
/// verticale <i>separatamente</i>. Su una muratura a corsi sfalsati quella ricerca trova
/// l'altezza di un corso e sbaglia: il motivo non si ripete ogni corso, si ripete ogni
/// <b>due</b>, perché il corso intermedio è spostato di mezzo mattone. Una spina di pesce, un
/// impacchettamento esagonale, una carta da parati a metà caduta: tutti casi in cui due assi
/// indipendenti non bastano a dire il vero.
/// </para>
/// <para>
/// I picchi dell'autocorrelazione si scelgono per <b>regione di dominanza</b> e non per
/// altezza: la dominanza di un picco è la distanza dal picco più alto di lui. Serve perché i
/// picchi più alti dopo l'origine sono i suoi vicini immediati — l'autocorrelazione è liscia,
/// e attorno a ogni massimo c'è una collina di valori quasi altrettanto alti. Ordinando per
/// altezza si raccoglie dieci volte lo stesso picco; ordinando per dominanza si raccolgono
/// dieci picchi diversi, che è quello che serve per riconoscere un reticolo.
/// </para>
/// <para>
/// Dal reticolo si ricava poi la <b>cella rettangolare</b>, perché è l'unica che questo
/// modello sappia esprimere: un pattern ha una larghezza e un'altezza, non una coppia di
/// vettori obliqui. Il rettangolo più piccolo che si ripete non è quasi mai la cella del
/// reticolo — per una muratura a corsi sfalsati è il doppio, e contiene due mattoni — e
/// trovarlo è un conto sugli interi, non una ricerca.
/// </para>
/// </remarks>
public static class Reticolo
{
    /// <summary>
    /// Il lato a cui si cerca il reticolo. Il conto costa la <b>quarta potenza</b> del lato —
    /// ogni spostamento contro ogni punto — quindi cercare a grandezza piena su un'immagine da
    /// novecento pixel vorrebbe dire minuti. La precisione si recupera dopo, rifinendo a
    /// grandezza vera.
    ///
    /// Non si può però rimpicciolire quanto si vuole, ed è costato una prova capirlo: un muro a
    /// corsi sfalsati ha il suo vettore obliquo a mezzo mattone e mezzo corso, e a un quarto di
    /// scala quel mezzo diventa un quarto di pixel. La cella rettangolare torna comunque
    /// giusta, perché la si trova anche dal solo sottoreticolo allineato, ma dell'obliquità —
    /// che è l'informazione utile a chi poi deve disegnare la tessera — non resta traccia.
    /// </summary>
    private const int LatoDiRicerca = 192;

    /// <summary>
    /// Sotto questo spostamento non si cerca: l'autocorrelazione ha sempre una collina attorno
    /// all'origine, e prenderla per una ripetizione darebbe celle di pochi pixel fatte di
    /// rumore.
    /// </summary>
    private const int PassoMinimo = 4;

    /// <summary>Quanti picchi dominanti si tengono per ricostruire il reticolo.</summary>
    private const int PicchiTenuti = 24;

    /// <summary>
    /// Quanto devono essere distinte le due direzioni. Sotto questo seno sono la stessa
    /// direzione misurata due volte, e non generano nessun reticolo.
    /// </summary>
    private const double AbbastanzaDiverse = 0.25;

    /// <summary>
    /// Quanto alto dev'essere un picco, rispetto al più alto, per poter generare il reticolo.
    /// </summary>
    private const double AbbastanzaAlti = 0.8;

    /// <summary>Quanti picchi corti si provano come generatori, a due a due.</summary>
    private const int CoppieProvate = 8;

    /// <summary>Quanto deve valere la correlazione lungo i due lati della cella.</summary>
    private const double AbbastanzaForte = 0.35;

    /// <summary>
    /// Quanta parte dei picchi dominanti la base deve spiegare. Un reticolo vero li spiega
    /// quasi tutti: se meno della metà cade dove il reticolo dice, quei picchi non erano una
    /// ripetizione ma somiglianze casuali.
    /// </summary>
    private const double AbbastanzaSpiegati = 0.5;

    /// <summary>Quanto un picco può discostarsi dal reticolo e contare come spiegato.</summary>
    private const double ScartoAmmesso = 0.28;

    /// <summary>Cerca il reticolo dell'immagine.</summary>
    public static ReticoloTrovato Trova(Immagine immagine)
    {
        ArgumentNullException.ThrowIfNull(immagine);

        var senza = new ReticoloTrovato(
            default, default, immagine.Larghezza, immagine.Altezza, 1, 0, Sicuro: false);

        var fattore = Math.Max(
            1, (Math.Max(immagine.Larghezza, immagine.Altezza) + LatoDiRicerca - 1) / LatoDiRicerca);

        var piccola = immagine.Ridotta(fattore);
        if (piccola.Larghezza < PassoMinimo * 3 || piccola.Altezza < PassoMinimo * 3)
        {
            return senza;
        }

        // Metà immagine: oltre, la parte sovrapposta è così piccola che la correlazione dice
        // più del caso che del disegno.
        var superficie = new Correlazione(piccola).Mappa(piccola.Larghezza / 2, piccola.Altezza / 2);

        var picchi = Dominanti(superficie);
        if (picchi.Count < 2)
        {
            return senza;
        }

        var (a, b) = Base(picchi);
        if (a == default || b == default)
        {
            return senza;
        }

        if (Spiegati(picchi, a, b) < AbbastanzaSpiegati)
        {
            return senza;
        }

        // Da qui in poi si lavora a grandezza vera: la miniatura ha detto dove guardare, e un
        // errore di un pixel sulla cella si vede come una cucitura a ogni tessera.
        var intera = new Correlazione(immagine);
        a = Rifinisci(intera, a * fattore, fattore);
        b = Rifinisci(intera, b * fattore, fattore);

        // Quanto raddrizzare i vettori non si decide a priori: si prova, e a dire quale
        // raddrizzamento fosse quello giusto è la cella che ne esce. Si parte dal non
        // raddrizzare affatto, così un reticolo davvero obliquo resta obliquo, e si cede un
        // pixel per volta solo finché la cella non regge alla verifica.
        for (var tolleranza = 0; tolleranza <= Math.Max(1, fattore); tolleranza++)
        {
            var ra = Raddrizza(a, tolleranza);
            var rb = Raddrizza(b, tolleranza);

            var (larghezza, altezza) = Rettangolo(ra, rb);

            if (larghezza < PassoMinimo || altezza < PassoMinimo
                || larghezza > immagine.Larghezza || altezza > immagine.Altezza)
            {
                continue;
            }

            // L'ultima parola ce l'ha la cella, non il reticolo da cui discende: se spostarsi
            // di una larghezza o di un'altezza non riporta il disegno su se stesso, il conto
            // sugli interi era giusto e le misure da cui partiva no.
            var forza = Math.Min(intera.Per(larghezza, 0), intera.Per(0, altezza));
            if (forza < AbbastanzaForte)
            {
                continue;
            }

            var area = Math.Abs(ra.Incrocio(rb));
            var motivi = (int)Math.Max(1, Math.Round((double)larghezza * altezza / area));

            return new ReticoloTrovato(ra, rb, larghezza, altezza, motivi, forza, Sicuro: true);
        }

        return senza;
    }

    /// <summary>
    /// I picchi della superficie, dal più dominante al meno.
    /// </summary>
    ///
    /// <remarks>
    /// La dominanza di un picco è la distanza dal picco più alto di lui — il raggio entro cui
    /// comanda. Un massimo locale nato dalla collina di un picco vero ha un vicino più alto a
    /// due passi, e finisce in fondo alla lista; un picco strutturale non ne ha nessuno per
    /// tutta la sua zona.
    /// </remarks>
    private static List<(Vettore Dove, double Valore, double Dominanza)> Dominanti(Superficie s)
    {
        var massimi = new List<(Vettore Dove, double Valore)>();

        for (var dx = 0; dx <= s.MassimoX; dx++)
        {
            for (var dy = -s.MassimoY; dy <= s.MassimoY; dy++)
            {
                // Il semipiano: con dx a zero, (0, -dy) è lo stesso spostamento di (0, dy).
                if (dx == 0 && dy <= 0)
                {
                    continue;
                }

                if (dx * dx + dy * dy < PassoMinimo * PassoMinimo)
                {
                    continue;
                }

                var qui = s[dx, dy];
                if (qui <= 0)
                {
                    continue;
                }

                var piuAlto = false;
                for (var ex = -1; ex <= 1 && !piuAlto; ex++)
                {
                    for (var ey = -1; ey <= 1; ey++)
                    {
                        if ((ex != 0 || ey != 0) && s[dx + ex, dy + ey] > qui)
                        {
                            piuAlto = true;
                            break;
                        }
                    }
                }

                if (!piuAlto)
                {
                    massimi.Add((new Vettore(dx, dy), qui));
                }
            }
        }

        var ordinati = massimi.OrderByDescending(m => m.Valore).ToList();
        var dominanti = new List<(Vettore Dove, double Valore, double Dominanza)>(ordinati.Count);

        for (var i = 0; i < ordinati.Count; i++)
        {
            // Il più alto di tutti non ha nessuno sopra di sé: domina quanto la superficie.
            var dominanza = double.MaxValue;

            for (var j = 0; j < i; j++)
            {
                var d = (ordinati[i].Dove - ordinati[j].Dove).Lunghezza;
                dominanza = Math.Min(dominanza, d);

                // E anche il riflesso, che è lo stesso picco dall'altra parte dell'origine.
                var riflesso = new Vettore(-ordinati[j].Dove.X, -ordinati[j].Dove.Y);
                dominanza = Math.Min(dominanza, (ordinati[i].Dove - riflesso).Lunghezza);
            }

            dominanti.Add((ordinati[i].Dove, ordinati[i].Valore, dominanza));
        }

        return [.. dominanti.OrderByDescending(p => p.Dominanza).Take(PicchiTenuti)];
    }

    /// <summary>
    /// La coppia di vettori che spiega meglio i picchi.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// La base si <b>sceglie</b>, non si prende. Prendere i due picchi più corti e indipendenti
    /// sembra ovvio e non regge: fra i picchi ce n'è sempre qualcuno spurio — un massimo locale
    /// nato da una somiglianza casuale — e se capita corto diventa il primo vettore, e da lì in
    /// poi ogni conto è sbagliato con precisione. Si provano quindi diverse coppie e si tiene
    /// quella che manda più picchi a cadere dove il reticolo dice: un reticolo vero li spiega
    /// quasi tutti, uno inventato quasi nessuno.
    /// </para>
    /// <para>
    /// Si guarda solo fra i picchi <b>forti</b>. Un generatore del reticolo porta il disegno
    /// esattamente su se stesso, quindi la sua correlazione è fra le più alte della superficie;
    /// un picco a metà altezza può essere tante cose, ma non una traslazione che rispetta il
    /// motivo.
    /// </para>
    /// </remarks>
    private static (Vettore A, Vettore B) Base(
        List<(Vettore Dove, double Valore, double Dominanza)> picchi)
    {
        var piuAlto = picchi.Max(p => p.Valore);

        var forti = picchi
            .Where(p => p.Valore >= piuAlto * AbbastanzaAlti)
            .OrderBy(p => p.Dove.Lunghezza)
            .Select(p => p.Dove)
            .Take(CoppieProvate)
            .ToList();

        var migliore = (A: default(Vettore), B: default(Vettore));
        var meglio = 0.0;
        var areaMigliore = long.MaxValue;

        for (var i = 0; i < forti.Count; i++)
        {
            for (var j = i + 1; j < forti.Count; j++)
            {
                var seno = Math.Abs(forti[i].Incrocio(forti[j]))
                           / (forti[i].Lunghezza * forti[j].Lunghezza);

                if (seno < AbbastanzaDiverse)
                {
                    continue;
                }

                var (a, b) = Riduci(forti[i], forti[j]);
                if (a == default || b == default)
                {
                    continue;
                }

                var area = Math.Abs(a.Incrocio(b));
                var spiegati = Spiegati(picchi, a, b);

                // A parità di picchi spiegati vince il reticolo più fitto: un reticolo doppio
                // spiega tutto quello che spiega quello semplice, ma raddoppia la cella e
                // quindi il lavoro di chi poi la modifica.
                if (spiegati > meglio + 1e-9 || (spiegati > meglio - 1e-9 && area < areaMigliore))
                {
                    meglio = spiegati;
                    areaMigliore = area;
                    migliore = (a, b);
                }
            }
        }

        return migliore;
    }

    /// <summary>
    /// Riduzione di Gauss: la coppia più corta che genera lo stesso reticolo.
    /// </summary>
    ///
    /// <remarks>
    /// Si accorcia ripetutamente il più lungo sottraendogli il multiplo più vicino del più
    /// corto, finché non si accorcia più. Serve perché due picchi qualsiasi del reticolo lo
    /// generano tutto, ma la cella che ne esce può essere enorme: ridotti, danno la cella
    /// minima.
    /// </remarks>
    private static (Vettore A, Vettore B) Riduci(Vettore a, Vettore b)
    {
        for (var giro = 0; giro < 32; giro++)
        {
            if (b.Lunghezza < a.Lunghezza)
            {
                (a, b) = (b, a);
            }

            var quadrato = a.Scalare(a);
            if (quadrato == 0)
            {
                return (default, default);
            }

            var quante = (int)Math.Round((double)b.Scalare(a) / quadrato);
            if (quante == 0)
            {
                break;
            }

            b -= a * quante;

            if (b == default)
            {
                return (default, default);
            }
        }

        return (a, b);
    }

    /// <summary>Quanta parte dei picchi cade dove il reticolo dice che dovrebbe cadere.</summary>
    private static double Spiegati(
        List<(Vettore Dove, double Valore, double Dominanza)> picchi, Vettore a, Vettore b)
    {
        double determinante = a.Incrocio(b);
        if (Math.Abs(determinante) < 1e-9)
        {
            return 0;
        }

        var quanti = 0;

        foreach (var (dove, _, _) in picchi)
        {
            // Le coordinate del picco nella base del reticolo: se il reticolo è quello giusto
            // vengono due numeri interi.
            var m = (dove.X * (double)b.Y - dove.Y * (double)b.X) / determinante;
            var n = (dove.Y * (double)a.X - dove.X * (double)a.Y) / determinante;

            if (Math.Abs(m - Math.Round(m)) <= ScartoAmmesso
                && Math.Abs(n - Math.Round(n)) <= ScartoAmmesso)
            {
                quanti++;
            }
        }

        return (double)quanti / picchi.Count;
    }

    /// <summary>
    /// Cerca attorno al vettore indicato lo spostamento che somiglia di più, a grandezza vera.
    /// </summary>
    private static Vettore Rifinisci(Correlazione correlazione, Vettore attorno, int raggio)
    {
        var migliore = attorno;
        var quanto = correlazione.Per(attorno.X, attorno.Y);

        for (var ex = -raggio; ex <= raggio; ex++)
        {
            for (var ey = -raggio; ey <= raggio; ey++)
            {
                var prova = new Vettore(attorno.X + ex, attorno.Y + ey);
                var vale = correlazione.Per(prova.X, prova.Y);

                if (vale > quanto)
                {
                    quanto = vale;
                    migliore = prova;
                }
            }
        }

        return migliore;
    }

    /// <summary>
    /// Azzera la componente che è quasi zero.
    /// </summary>
    ///
    /// <remarks>
    /// Non è cosmesi: è quello che tiene in piedi il conto sugli interi che viene dopo. La
    /// cella rettangolare si ricava da un massimo comune divisore, e un massimo comune divisore
    /// non perdona: un vettore orizzontale misurato come <c>(40, 1)</c> invece che
    /// <c>(40, 0)</c> non dà una cella alta 40 ma una alta 1600. Un pixel di errore di misura
    /// diventerebbe una cella grande quanto l'immagine.
    /// </remarks>
    private static Vettore Raddrizza(Vettore v, int tolleranza) =>
        new(Math.Abs(v.X) <= tolleranza ? 0 : v.X, Math.Abs(v.Y) <= tolleranza ? 0 : v.Y);

    /// <summary>
    /// Il più piccolo rettangolo, con i lati sugli assi, che si ripete secondo questo reticolo.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// La larghezza è il più corto spostamento del reticolo che sia <b>orizzontale</b>: fra
    /// tutte le combinazioni <c>m·a + n·b</c>, quelle con la componente verticale nulla. La
    /// condizione <c>m·a.Y + n·b.Y = 0</c> ha per soluzione minima <c>m = b.Y/g</c>,
    /// <c>n = -a.Y/g</c> con <c>g</c> il massimo comune divisore delle due componenti
    /// verticali, e la larghezza che ne risulta è l'area della cella del reticolo divisa per
    /// <c>g</c>. Per l'altezza vale lo stesso ragionamento sulle componenti orizzontali.
    /// </para>
    /// <para>
    /// Per un reticolo già allineato agli assi torna la cella stessa. Per una muratura a corsi
    /// sfalsati — vettori <c>(l, 0)</c> e <c>(l/2, h)</c> — torna <c>l × 2h</c>: due mattoni,
    /// che è esattamente quello che bisogna disegnare perché il muro si ripeta senza cuciture.
    /// </para>
    /// </remarks>
    private static (int Larghezza, int Altezza) Rettangolo(Vettore a, Vettore b)
    {
        var area = Math.Abs(a.Incrocio(b));
        if (area == 0)
        {
            return (0, 0);
        }

        var perLarghezza = Divisore(a.Y, b.Y);
        var perAltezza = Divisore(a.X, b.X);

        if (perLarghezza == 0 || perAltezza == 0)
        {
            return (0, 0);
        }

        return ((int)(area / perLarghezza), (int)(area / perAltezza));
    }

    private static long Divisore(int primo, int secondo)
    {
        long a = Math.Abs((long)primo);
        long b = Math.Abs((long)secondo);

        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }
}
