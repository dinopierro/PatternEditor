namespace PatternEditor.Services.Riconoscimento;

/// <summary>
/// Quanto un'immagine somiglia a se stessa, spostata.
/// </summary>
///
/// <remarks>
/// <para>
/// È la superficie su cui si legge la periodicità. Si sovrappone l'immagine a una sua copia
/// traslata di <c>(dx, dy)</c> e si misura quanto le due si assomigliano: dove la somiglianza
/// ha un picco, quella traslazione porta il motivo su se stesso. I picchi, tutti insieme,
/// <b>sono</b> il reticolo.
/// </para>
/// <para>
/// Si misura sul <b>gradiente</b> e non sui colori, ed è la differenza fra funzionare su una
/// fotografia e no. Una foto di pavimento ha un'illuminazione che cambia da un angolo
/// all'altro, e quella sfumatura somiglia a se stessa per qualunque spostamento piccolo: sui
/// colori produce un unico dosso largo attorno all'origine che seppellisce i picchi veri. Il
/// gradiente butta via le variazioni lente e tiene i bordi, che sono la parte del disegno che
/// si ripete davvero.
/// </para>
/// <para>
/// E si misura come <b>correlazione normalizzata</b>, non come differenza. Due zone della
/// stessa trama, una in ombra e una in luce, differiscono molto e si somigliano moltissimo: la
/// correlazione normalizzata toglie media e ampiezza prima di confrontare, quindi vede la
/// forma e non l'esposizione. Il riconoscitore precedente usava una differenza normalizzata
/// sull'inchiostro, ed era il motivo per cui un passo giusto veniva scartato per un'ombra.
/// </para>
/// </remarks>
public sealed class Correlazione
{
    private readonly double[] _bordi;
    private readonly int _larghezza;
    private readonly int _altezza;

    public Correlazione(Immagine immagine)
    {
        ArgumentNullException.ThrowIfNull(immagine);

        _larghezza = immagine.Larghezza;
        _altezza = immagine.Altezza;
        _bordi = Gradiente(immagine);
    }

    /// <summary>
    /// La somiglianza per un solo spostamento, da -1 a 1.
    /// </summary>
    ///
    /// <remarks>
    /// Il confronto avviene sulla sola parte <b>sovrapposta</b>, e non avvolgendo i bordi. Una
    /// trama senza cuciture si confronterebbe meglio avvolgendola, ma il ritaglio di una
    /// fotografia no — e chiedere all'utente di ritagliare al pixel giusto sarebbe chiedergli
    /// proprio il lavoro che questo strumento dovrebbe fare.
    /// </remarks>
    public double Per(int dx, int dy)
    {
        // Spostare di (dx, dy) o di (-dx, -dy) confronta la stessa coppia di zone.
        if (dx < 0)
        {
            (dx, dy) = (-dx, -dy);
        }

        var daX = Math.Max(0, -dx);
        var aX = Math.Min(_larghezza, _larghezza - dx);
        var daY = Math.Max(0, -dy);
        var aY = Math.Min(_altezza, _altezza - dy);

        var quanti = (aX - daX) * (aY - daY);
        if (quanti < 16)
        {
            return 0;
        }

        double sommaA = 0, sommaB = 0, sommaAA = 0, sommaBB = 0, sommaAB = 0;

        for (var y = daY; y < aY; y++)
        {
            var riga = y * _larghezza;
            var rigaSpostata = (y + dy) * _larghezza + dx;

            for (var x = daX; x < aX; x++)
            {
                var a = _bordi[riga + x];
                var b = _bordi[rigaSpostata + x];

                sommaA += a;
                sommaB += b;
                sommaAA += a * a;
                sommaBB += b * b;
                sommaAB += a * b;
            }
        }

        var covarianza = sommaAB - sommaA * sommaB / quanti;
        var varianzaA = sommaAA - sommaA * sommaA / quanti;
        var varianzaB = sommaBB - sommaB * sommaB / quanti;

        // Una zona piatta non ha nessuna forma da riconoscere: dire che somiglia a tutto
        // sarebbe vero e inutile, e riempirebbe la superficie di picchi finti.
        var scala = Math.Sqrt(varianzaA * varianzaB);
        return scala < 1e-9 ? 0 : covarianza / scala;
    }

    /// <summary>La superficie intera, per tutti gli spostamenti fino ai massimi indicati.</summary>
    public Superficie Mappa(int massimoX, int massimoY)
    {
        massimoX = Math.Clamp(massimoX, 1, Math.Max(1, _larghezza - 2));
        massimoY = Math.Clamp(massimoY, 1, Math.Max(1, _altezza - 2));

        var passo = massimoX + 1;
        var valori = new double[(massimoY * 2 + 1) * passo];

        for (var dy = -massimoY; dy <= massimoY; dy++)
        {
            for (var dx = 0; dx <= massimoX; dx++)
            {
                valori[(dy + massimoY) * passo + dx] = Per(dx, dy);
            }
        }

        return new Superficie(valori, passo, massimoX, massimoY);
    }

    /// <summary>
    /// La mappa dei bordi: quanto ogni punto è diverso da quelli che ha attorno.
    /// </summary>
    ///
    /// <remarks>
    /// Sobel sulla luminanza. La luminanza e non i tre canali perché il reticolo è una
    /// proprietà geometrica: una trama che si ripetesse nel colore ma non nella forma è un caso
    /// che in un retino non esiste, e cercarlo costerebbe tre volte tanto.
    /// </remarks>
    private static double[] Gradiente(Immagine immagine)
    {
        var larghezza = immagine.Larghezza;
        var altezza = immagine.Altezza;

        var luce = new double[larghezza * altezza];
        for (var y = 0; y < altezza; y++)
        {
            for (var x = 0; x < larghezza; x++)
            {
                var (r, g, b) = immagine.Colore(x, y);
                luce[y * larghezza + x] = 0.299 * r + 0.587 * g + 0.114 * b;
            }
        }

        var bordi = new double[larghezza * altezza];

        for (var y = 1; y < altezza - 1; y++)
        {
            for (var x = 1; x < larghezza - 1; x++)
            {
                var su = (y - 1) * larghezza + x;
                var qui = y * larghezza + x;
                var giu = (y + 1) * larghezza + x;

                var gx = luce[su + 1] + 2 * luce[qui + 1] + luce[giu + 1]
                         - luce[su - 1] - 2 * luce[qui - 1] - luce[giu - 1];

                var gy = luce[giu - 1] + 2 * luce[giu] + luce[giu + 1]
                         - luce[su - 1] - 2 * luce[su] - luce[su + 1];

                bordi[qui] = Math.Sqrt(gx * gx + gy * gy);
            }
        }

        return bordi;
    }
}

/// <summary>La superficie di autocorrelazione già calcolata, spostamento per spostamento.</summary>
public sealed class Superficie
{
    private readonly double[] _valori;
    private readonly int _passo;

    internal Superficie(double[] valori, int passo, int massimoX, int massimoY)
    {
        _valori = valori;
        _passo = passo;
        MassimoX = massimoX;
        MassimoY = massimoY;
    }

    /// <summary>Il maggiore spostamento orizzontale esaminato.</summary>
    public int MassimoX { get; }

    /// <summary>Il maggiore spostamento verticale esaminato, in valore assoluto.</summary>
    public int MassimoY { get; }

    /// <summary>La somiglianza per lo spostamento indicato; zero fuori dall'intervallo.</summary>
    public double this[int dx, int dy]
    {
        get
        {
            if (dx < 0)
            {
                (dx, dy) = (-dx, -dy);
            }

            return dx > MassimoX || Math.Abs(dy) > MassimoY
                ? 0
                : _valori[(dy + MassimoY) * _passo + dx];
        }
    }
}
