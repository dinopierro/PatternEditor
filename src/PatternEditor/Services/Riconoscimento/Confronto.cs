namespace PatternEditor.Services.Riconoscimento;

/// <summary>
/// Il confronto fra quello che c'era e quello che si è capito.
/// </summary>
///
/// <param name="Larghezza">Larghezza della cella, in punti.</param>
/// <param name="Altezza">Altezza della cella, in punti.</param>
/// <param name="Originale">La cella ritagliata dall'immagine, quattro byte per punto.</param>
/// <param name="Differenza">
/// La mappa di ciò che non torna: in rosso il disegno che manca alla ricostruzione, in blu
/// quello che ha aggiunto, in arancione ciò che c'è ma di un altro colore, in grigio chiaro
/// ciò che coincide.
/// </param>
/// <param name="Somiglianza">Quanti punti si somigliano, da 0 a 1.</param>
public sealed record Confronto(
    int Larghezza,
    int Altezza,
    byte[] Originale,
    byte[] Differenza,
    double Somiglianza);

/// <summary>
/// Misura quanto una ricostruzione somiglia al disegno da cui è nata.
/// </summary>
///
/// <remarks>
/// <para>
/// È la parte che distingue uno strumento da un trucco. Un riconoscimento produce sempre
/// qualcosa: la domanda non è se produca, ma se quel qualcosa somigli. Senza una misura si può
/// solo guardare — e guardare una trama ripetuta inganna, perché l'occhio perdona a una
/// tessera minuscola errori che su una parete si vedono tutti.
/// </para>
/// <para>
/// Il confronto è sul <b>colore</b>, punto per punto, e non sull'inchiostro. Non è un
/// raffinamento: è ciò che permette di mettere a paragone due letture opposte dello stesso
/// disegno. Un pavimento di mattoni arancioni con le fughe bianche si può leggere come
/// «arancione di fondo con sopra delle righe bianche» oppure come «bianco di fondo con sopra
/// dei mattoni arancioni», e contando il solo inchiostro tutte e due coprono per intero quello
/// che si sono scelte. A dire quale delle due sia il pavimento è il colore.
/// </para>
/// </remarks>
public static class Somiglianza
{
    private static readonly byte[] Coincide = [0xdd, 0xe1, 0xe6, 0xff];
    private static readonly byte[] Manca = [0xd6, 0x45, 0x45, 0xff];
    private static readonly byte[] Aggiunto = [0x3b, 0x6e, 0xf5, 0xff];
    private static readonly byte[] Storto = [0xc2, 0x70, 0x1c, 0xff];

    /// <summary>
    /// Quanto due colori possono differire e continuare a sembrare lo stesso colore.
    /// </summary>
    ///
    /// <remarks>
    /// Largo apposta. Un JPEG sporca ogni tinta piatta, un'ombreggiatura fa di un mattone dieci
    /// aranci diversi, e un riconoscimento che ne sceglie la media non ha sbagliato: ha fatto
    /// esattamente quello che deve. La misura deve accorgersi di una forma nel posto sbagliato,
    /// non di un mattone due toni più chiaro.
    /// </remarks>
    private const double ColoriUguali = 48.0;

    /// <summary>
    /// Ridisegna le forme e le confronta con il disegno di partenza.
    /// </summary>
    ///
    /// <param name="forme">Le forme riconosciute, nell'ordine in cui verranno scritte.</param>
    /// <param name="fondo">Il colore che sta sotto tutto.</param>
    /// <param name="larghezza">Larghezza della cella.</param>
    /// <param name="altezza">Altezza della cella.</param>
    /// <param name="pixel">I punti dell'immagine intera.</param>
    /// <param name="larghezzaImmagine">Larghezza dell'immagine intera.</param>
    /// <param name="altezzaImmagine">Altezza dell'immagine intera.</param>
    public static Confronto Misura(IReadOnlyList<Forma> forme, (byte R, byte G, byte B) fondo,
                                   int larghezza, int altezza,
                                   byte[] pixel, int larghezzaImmagine, int altezzaImmagine)
    {
        ArgumentNullException.ThrowIfNull(forme);
        ArgumentNullException.ThrowIfNull(pixel);

        var tela = Dipingi(forme, fondo, larghezza, altezza);

        var uguali = 0;
        var differenza = new byte[larghezza * altezza * 4];
        var ritaglio = new byte[larghezza * altezza * 4];

        for (var y = 0; y < altezza; y++)
        {
            for (var x = 0; x < larghezza; x++)
            {
                var i = (y * larghezza + x) * 4;

                // La cella a colori si ritaglia con lo stesso avvolgimento del ritaglio
                // dell'inchiostro: altrimenti non sarebbe la stessa cella.
                var sorgente = ((y % altezzaImmagine) * larghezzaImmagine + x % larghezzaImmagine) * 4;
                Array.Copy(pixel, sorgente, ritaglio, i, 4);

                var cera = (pixel[sorgente], pixel[sorgente + 1], pixel[sorgente + 2]);
                var ce = tela.Colore(x, y);

                if (Vicini(cera, ce))
                {
                    uguali++;
                    Array.Copy(Coincide, 0, differenza, i, 4);
                    continue;
                }

                // Tre modi di sbagliare, e distinguerli dice dove guardare: qui non è stato
                // disegnato niente, qui è stato disegnato di troppo, qui c'è la forma giusta
                // del colore sbagliato.
                var eraFondo = Vicini(cera, fondo);
                var eFondo = Vicini(ce, fondo);

                Array.Copy(
                    eFondo ? Manca : eraFondo ? Aggiunto : Storto,
                    0, differenza, i, 4);
            }
        }

        return new Confronto(
            larghezza, altezza, ritaglio, differenza,
            (double)uguali / (larghezza * altezza));
    }

    /// <summary>
    /// Ridipinge le forme su una tela, nell'ordine in cui verranno scritte.
    /// </summary>
    ///
    /// <remarks>
    /// Serve due volte, e la seconda è quella che conta. La prima per misurare: si confronta
    /// quello che si è capito con quello che c'era. La seconda per <b>continuare</b>: dove la
    /// tela non corrisponde al disegno c'è ancora qualcosa da riconoscere, e ripartire da lì
    /// spende il bilancio degli elementi dove il disegno sbaglia invece che dove è grande.
    /// </remarks>
    public static Tela Dipingi(IReadOnlyList<Forma> forme, (byte R, byte G, byte B) fondo,
                               int larghezza, int altezza)
    {
        ArgumentNullException.ThrowIfNull(forme);

        var tela = new Tela(larghezza, altezza, fondo);
        foreach (var forma in forme)
        {
            forma.Disegna(tela);
        }

        return tela;
    }

    /// <summary>Vero se i due colori si possono considerare lo stesso colore.</summary>
    public static bool SiSomigliano((byte R, byte G, byte B) a, (byte R, byte G, byte B) b) =>
        Vicini(a, b);

    private static bool Vicini((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
    {
        double dr = a.R - b.R;
        double dg = a.G - b.G;
        double db = a.B - b.B;

        return dr * dr + dg * dg + db * db <= ColoriUguali * ColoriUguali;
    }
}
