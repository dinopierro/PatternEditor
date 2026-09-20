using PatternEditor.Services.Riconoscimento;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// La ricerca del reticolo di ripetizione.
/// </summary>
///
/// <remarks>
/// <para>
/// I disegni si fanno qui, punto per punto, invece di essere file allegati: sono tre righe per
/// una muratura, e in cambio la prova sa esattamente quale cella avrebbe dovuto trovare.
/// </para>
/// <para>
/// Le prove che contano sono quelle a <b>corsi sfalsati</b>. Un reticolo si può sempre leggere
/// come due assi indipendenti quando il motivo è allineato alla griglia, e su quei casi anche
/// il riconoscitore precedente andava bene. Il punto in cui cedeva — e la ragione per cui
/// questa parte è stata rifatta — è il muro: il motivo non si ripete ogni corso, perché il
/// corso dopo è spostato di mezzo mattone, e cercando il passo verticale da solo si trova
/// l'altezza di <i>un</i> corso invece che di due.
/// </para>
/// </remarks>
public class ReticoloTests
{
    private static byte[] Tela(int lato)
    {
        var pixel = new byte[lato * lato * 4];
        Array.Fill(pixel, (byte)255);
        return pixel;
    }

    private static void Riga(byte[] tela, int lato, int x0, int y0, int larghezza, int altezza)
    {
        for (var y = y0; y < y0 + altezza; y++)
        {
            for (var x = x0; x < x0 + larghezza; x++)
            {
                if (x < 0 || y < 0 || x >= lato || y >= lato)
                {
                    continue;
                }

                var i = (y * lato + x) * 4;
                tela[i] = tela[i + 1] = tela[i + 2] = 0;
            }
        }
    }

    /// <summary>
    /// Un muro: mattoni di <paramref name="larghezza"/> × <paramref name="altezza"/> separati
    /// da una fuga, con ogni corso spostato di <paramref name="sfalsamento"/>.
    /// </summary>
    private static byte[] Muro(int lato, int larghezza, int altezza, int sfalsamento)
    {
        var tela = Tela(lato);

        for (var corso = 0; corso * altezza < lato + altezza; corso++)
        {
            var y = corso * altezza;
            var scarto = corso * sfalsamento % larghezza;

            // La fuga orizzontale, continua per tutto il corso.
            Riga(tela, lato, 0, y, lato, 2);

            // Le fughe verticali, una per mattone.
            for (var x = scarto - larghezza; x < lato + larghezza; x += larghezza)
            {
                Riga(tela, lato, x, y, 2, altezza);
            }
        }

        return tela;
    }

    private static ReticoloTrovato Trova(byte[] tela, int lato) =>
        Reticolo.Trova(new Immagine(tela, lato, lato));

    // ------------------------------------------------------ il reticolo allineato ----

    [Fact]
    public void Una_griglia_quadrata_da_una_cella_quadrata()
    {
        const int lato = 240;
        var tela = Tela(lato);

        for (var y = 0; y < lato; y += 40)
        {
            for (var x = 0; x < lato; x += 40)
            {
                Riga(tela, lato, x + 8, y + 8, 16, 16);
            }
        }

        var reticolo = Trova(tela, lato);

        Assert.True(reticolo.Sicuro, "la ripetizione non si è vista");
        Assert.Equal(40, reticolo.Larghezza);
        Assert.Equal(40, reticolo.Altezza);
        Assert.Equal(1, reticolo.Motivi);
    }

    [Fact]
    public void Una_griglia_rettangolare_tiene_i_due_lati_distinti()
    {
        const int lato = 240;
        var tela = Tela(lato);

        for (var y = 0; y < lato; y += 30)
        {
            for (var x = 0; x < lato; x += 60)
            {
                Riga(tela, lato, x + 10, y + 8, 20, 12);
            }
        }

        var reticolo = Trova(tela, lato);

        Assert.True(reticolo.Sicuro, "la ripetizione non si è vista");
        Assert.Equal(60, reticolo.Larghezza);
        Assert.Equal(30, reticolo.Altezza);
    }

    // -------------------------------------------------------- il reticolo obliquo ----

    [Fact]
    public void Un_muro_a_corsi_sfalsati_da_una_cella_alta_due_corsi()
    {
        // È il caso per cui questa parte è stata rifatta. I mattoni sono alti 20, ma il
        // disegno si ripete ogni 40: il corso intermedio è spostato di mezzo mattone, e una
        // cella alta 20 produrrebbe un muro con tutte le fughe incolonnate.
        const int lato = 240;
        var tela = Muro(lato, 60, 20, 30);

        var reticolo = Trova(tela, lato);

        Assert.True(reticolo.Sicuro, "la ripetizione non si è vista");
        Assert.Equal(60, reticolo.Larghezza);
        Assert.Equal(40, reticolo.Altezza);

        // Due mattoni nella cella: è la firma del reticolo obliquo.
        Assert.Equal(2, reticolo.Motivi);
    }

    [Fact]
    public void Un_muro_sfalsato_di_un_terzo_da_una_cella_alta_tre_corsi()
    {
        const int lato = 240;
        var tela = Muro(lato, 60, 20, 20);

        var reticolo = Trova(tela, lato);

        Assert.True(reticolo.Sicuro, "la ripetizione non si è vista");
        Assert.Equal(60, reticolo.Larghezza);
        Assert.Equal(60, reticolo.Altezza);
        Assert.Equal(3, reticolo.Motivi);
    }

    // ------------------------------------------------------------- quando non c'è ----

    [Fact]
    public void Una_macchia_sola_non_e_una_ripetizione()
    {
        const int lato = 160;
        var tela = Tela(lato);
        Riga(tela, lato, 60, 60, 40, 40);

        var reticolo = Trova(tela, lato);

        Assert.False(reticolo.Sicuro);
        Assert.Equal(lato, reticolo.Larghezza);
        Assert.Equal(lato, reticolo.Altezza);
    }

    [Fact]
    public void Un_foglio_bianco_non_ha_niente_da_ripetere()
    {
        const int lato = 120;

        var reticolo = Trova(Tela(lato), lato);

        Assert.False(reticolo.Sicuro);
    }

    // ---------------------------------------------------------------- la cella ----

    [Fact]
    public void La_cella_trovata_e_quella_giusta_anche_su_un_immagine_grande()
    {
        // L'immagine si cerca rimpicciolita, ma la cella dev'essere giusta al pixel: un errore
        // di uno si vede come una cucitura a ogni tessera.
        const int lato = 600;
        var tela = Tela(lato);

        for (var y = 0; y < lato; y += 75)
        {
            for (var x = 0; x < lato; x += 75)
            {
                Riga(tela, lato, x + 15, y + 15, 30, 30);
            }
        }

        var reticolo = Trova(tela, lato);

        Assert.True(reticolo.Sicuro, "la ripetizione non si è vista");
        Assert.Equal(75, reticolo.Larghezza);
        Assert.Equal(75, reticolo.Altezza);
    }
}
