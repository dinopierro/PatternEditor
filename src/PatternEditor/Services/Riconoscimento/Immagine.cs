namespace PatternEditor.Services.Riconoscimento;

/// <summary>
/// Un'immagine in memoria: i punti, e le poche operazioni che non dipendono da come si
/// intende riconoscerla.
/// </summary>
///
/// <remarks>
/// <para>
/// I pixel arrivano già decodificati, quattro byte per punto. La decodifica di PNG e JPEG la
/// fa il browser: è l'unico che li sappia leggere tutti senza portarsi dietro una libreria, e
/// una libreria di immagini dentro un'applicazione WebAssembly sarebbe qualche megabyte
/// scaricato per usarne una funzione.
/// </para>
/// <para>
/// Qui dentro non c'è nessuna idea di che cosa sia «disegno» e che cosa «sfondo». C'era, ed è
/// stata tolta apposta: divideva l'immagine in due sole classi a partire dal colore più
/// frequente, e quella era l'assunzione da cui discendeva quasi tutto ciò che non funzionava
/// nella prima conversione. Su una foto di pavimento, dove ogni mattone è di un rosso diverso,
/// «il colore di fondo» semplicemente non esiste. La separazione la deciderà chi riconosce, e
/// in termini di <b>regioni</b> invece che di due classi.
/// </para>
/// </remarks>
public sealed class Immagine
{
    private readonly byte[] _pixel;

    public Immagine(byte[] rgba, int larghezza, int altezza)
    {
        ArgumentNullException.ThrowIfNull(rgba);

        if (larghezza <= 0 || altezza <= 0 || rgba.Length < larghezza * altezza * 4)
        {
            throw new ArgumentException("I pixel non bastano per le misure dichiarate.", nameof(rgba));
        }

        _pixel = rgba;
        Larghezza = larghezza;
        Altezza = altezza;
    }

    public int Larghezza { get; }

    public int Altezza { get; }

    /// <summary>I punti, per chi deve confrontarli con una ricostruzione.</summary>
    public byte[] Punti => _pixel;

    public byte Alfa(int x, int y) => _pixel[(y * Larghezza + x) * 4 + 3];

    public (byte R, byte G, byte B) Colore(int x, int y)
    {
        var i = (y * Larghezza + x) * 4;
        return (_pixel[i], _pixel[i + 1], _pixel[i + 2]);
    }

    /// <summary>Il ritaglio di un rettangolo, avvolgendo i bordi.</summary>
    ///
    /// <remarks>
    /// L'avvolgimento non è una comodità: il ritaglio serve a isolare la cella di una trama, e
    /// una cella è una tessera. Se la ripetizione trovata non divide esattamente l'immagine —
    /// e non lo fa quasi mai — l'ultima colonna della cella va presa dalla prima dell'immagine,
    /// che è dove la trama ricomincia.
    /// </remarks>
    public Immagine Ritaglia(int x0, int y0, int larghezza, int altezza)
    {
        var punti = new byte[larghezza * altezza * 4];

        for (var y = 0; y < altezza; y++)
        {
            for (var x = 0; x < larghezza; x++)
            {
                var sorgente = (((y0 + y) % Altezza) * Larghezza + (x0 + x) % Larghezza) * 4;
                Array.Copy(_pixel, sorgente, punti, (y * larghezza + x) * 4, 4);
            }
        }

        return new Immagine(punti, larghezza, altezza);
    }

    /// <summary>
    /// Una copia rimpicciolita di un fattore intero, mediando i punti che si fondono.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Serve a lavorare alla <b>misura giusta</b>. Una foto di pavimento da novecento pixel di
    /// lato non contiene novecento pixel di disegno: contiene una ventina di mattoni e le loro
    /// fughe, cioè forse trecento pixel di informazione utile. Tenere gli altri seicento non
    /// migliora la ricostruzione, la peggiora — ogni granello di rumore diventa una macchia da
    /// riconoscere — e costa il quadrato del lato in tutto quello che viene dopo.
    /// </para>
    /// <para>
    /// La media e non il campionamento: prendere un punto ogni tre su una fotografia vuol dire
    /// prendere tre volte su dieci il punto sbagliato, mentre la media dei nove è esattamente
    /// il colore che quella zona ha. È anche il motivo per cui rimpicciolire <i>pulisce</i>: la
    /// grana del JPEG e la sporcizia di una scansione si mediano via da sole.
    /// </para>
    /// <para>
    /// Il pattern che ne esce ha la cella più piccola, e va bene così: un disegno vettoriale
    /// non ha una risoluzione, e una cella di trecento unità si modifica meglio di una di
    /// novecento.
    /// </para>
    /// </remarks>
    public Immagine Ridotta(int fattore)
    {
        if (fattore <= 1)
        {
            return this;
        }

        var larghezza = Math.Max(1, Larghezza / fattore);
        var altezza = Math.Max(1, Altezza / fattore);
        var piccoli = new byte[larghezza * altezza * 4];

        for (var y = 0; y < altezza; y++)
        {
            for (var x = 0; x < larghezza; x++)
            {
                long r = 0, g = 0, b = 0, a = 0;
                var quanti = 0;

                for (var dy = 0; dy < fattore; dy++)
                {
                    var sy = y * fattore + dy;
                    if (sy >= Altezza)
                    {
                        break;
                    }

                    for (var dx = 0; dx < fattore; dx++)
                    {
                        var sx = x * fattore + dx;
                        if (sx >= Larghezza)
                        {
                            break;
                        }

                        var i = (sy * Larghezza + sx) * 4;
                        r += _pixel[i];
                        g += _pixel[i + 1];
                        b += _pixel[i + 2];
                        a += _pixel[i + 3];
                        quanti++;
                    }
                }

                var j = (y * larghezza + x) * 4;
                piccoli[j] = (byte)(r / quanti);
                piccoli[j + 1] = (byte)(g / quanti);
                piccoli[j + 2] = (byte)(b / quanti);
                piccoli[j + 3] = (byte)(a / quanti);
            }
        }

        return new Immagine(piccoli, larghezza, altezza);
    }
}
