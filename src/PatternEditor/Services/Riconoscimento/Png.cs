using System.IO.Compression;

namespace PatternEditor.Services.Riconoscimento;

/// <summary>
/// Scrive un'immagine a punti come PNG.
/// </summary>
///
/// <remarks>
/// <para>
/// Serve per una sola cosa, e dichiarata: il <b>ricalco</b>, cioè la modalità in cui quello
/// che il vettoriale non è riuscito a rendere viene incollato sopra come immagine con lo
/// sfondo trasparente. Ovunque altro le immagini a punti le disegna il browser, che lo sa fare
/// meglio e gratis; ma qui l'immagine deve finire <b>dentro</b> il documento SVG che questa
/// libreria scrive, e a quel punto il browser è già fuori dal giro.
/// </para>
/// <para>
/// Un PNG è una firma, qualche blocco, e dentro un blocco i punti compressi con zlib. Le
/// righe si scrivono senza filtro — il byte zero in testa a ciascuna — perché i filtri
/// servono a far comprimere meglio le fotografie, e qui la stragrande maggioranza dei punti è
/// trasparente, cioè già una fila di zeri che zlib schiaccia da sola.
/// </para>
/// </remarks>
public static class Png
{
    private static readonly byte[] Firma = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    private static readonly uint[] Tabella = Tavola();

    /// <summary>
    /// Il PNG dei punti indicati, quattro byte per punto.
    /// </summary>
    public static byte[] Scrivi(byte[] rgba, int larghezza, int altezza)
    {
        ArgumentNullException.ThrowIfNull(rgba);

        if (larghezza <= 0 || altezza <= 0 || rgba.Length < larghezza * altezza * 4)
        {
            throw new ArgumentException("I punti non bastano per le misure dichiarate.", nameof(rgba));
        }

        var fuori = new MemoryStream();
        fuori.Write(Firma);

        // Larghezza, altezza, otto bit per canale, colore con trasparenza, nessun filtro,
        // nessun interlacciamento.
        var testa = new byte[13];
        Grande(testa, 0, (uint)larghezza);
        Grande(testa, 4, (uint)altezza);
        testa[8] = 8;
        testa[9] = 6;

        Blocco(fuori, "IHDR", testa);
        Blocco(fuori, "IDAT", Compressi(rgba, larghezza, altezza));
        Blocco(fuori, "IEND", []);

        return fuori.ToArray();
    }

    /// <summary>Le righe, ciascuna preceduta dal proprio byte di filtro, compresse con zlib.</summary>
    private static byte[] Compressi(byte[] rgba, int larghezza, int altezza)
    {
        var grezzi = new byte[altezza * (larghezza * 4 + 1)];

        for (var y = 0; y < altezza; y++)
        {
            var riga = y * (larghezza * 4 + 1);
            grezzi[riga] = 0;
            Array.Copy(rgba, y * larghezza * 4, grezzi, riga + 1, larghezza * 4);
        }

        var stretti = new MemoryStream();
        using (var zlib = new ZLibStream(stretti, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(grezzi);
        }

        return stretti.ToArray();
    }

    private static void Blocco(Stream dove, string tipo, byte[] dati)
    {
        var lunghezza = new byte[4];
        Grande(lunghezza, 0, (uint)dati.Length);
        dove.Write(lunghezza);

        var testa = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            testa[i] = (byte)tipo[i];
        }

        dove.Write(testa);
        dove.Write(dati);

        // La somma di controllo copre il tipo e i dati, non la lunghezza.
        var controllo = Controllo(testa, dati);
        var coda = new byte[4];
        Grande(coda, 0, controllo);
        dove.Write(coda);
    }

    private static void Grande(byte[] dove, int da, uint valore)
    {
        dove[da] = (byte)(valore >> 24);
        dove[da + 1] = (byte)(valore >> 16);
        dove[da + 2] = (byte)(valore >> 8);
        dove[da + 3] = (byte)valore;
    }

    private static uint Controllo(byte[] testa, byte[] dati)
    {
        var somma = 0xffffffffu;

        foreach (var b in testa)
        {
            somma = Tabella[(somma ^ b) & 0xff] ^ (somma >> 8);
        }

        foreach (var b in dati)
        {
            somma = Tabella[(somma ^ b) & 0xff] ^ (somma >> 8);
        }

        return somma ^ 0xffffffffu;
    }

    private static uint[] Tavola()
    {
        var tabella = new uint[256];

        for (var i = 0u; i < 256; i++)
        {
            var valore = i;
            for (var giro = 0; giro < 8; giro++)
            {
                valore = (valore & 1) != 0 ? 0xedb88320u ^ (valore >> 1) : valore >> 1;
            }

            tabella[i] = valore;
        }

        return tabella;
    }
}
