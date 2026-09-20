using PatternEditor.Services.Riconoscimento;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// Il codificatore PNG usato dal ricalco.
/// </summary>
///
/// <remarks>
/// Le prove guardano la <b>forma del file</b> e non i punti: un PNG sbagliato non si vede come
/// un'immagine brutta, si vede come un'immagine che non compare, e a quel punto il pattern
/// sembra semplicemente venuto male. Meglio accorgersene qui.
/// </remarks>
public class PngTests
{
    private static byte[] Punti(int lato)
    {
        var punti = new byte[lato * lato * 4];
        for (var i = 0; i < lato * lato; i++)
        {
            punti[i * 4] = (byte)(i % 256);
            punti[i * 4 + 3] = (byte)(i % 2 == 0 ? 255 : 0);
        }

        return punti;
    }

    [Fact]
    public void Comincia_con_la_firma_di_un_png()
    {
        var png = Png.Scrivi(Punti(8), 8, 8);

        Assert.Equal<byte[]>([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a], png[..8]);
    }

    [Fact]
    public void Dichiara_le_misure_giuste_e_il_canale_di_trasparenza()
    {
        var png = Png.Scrivi(Punti(16), 16, 16);

        // Dopo la firma: lunghezza (4), tipo (4), poi i tredici byte dell'intestazione.
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(16, (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19]);
        Assert.Equal(16, (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);
        Assert.Equal(8, png[24]);
        Assert.Equal(6, png[25]);
    }

    [Fact]
    public void Finisce_con_il_blocco_di_chiusura()
    {
        var png = Png.Scrivi(Punti(8), 8, 8);

        Assert.Equal("IEND", System.Text.Encoding.ASCII.GetString(png, png.Length - 8, 4));
    }

    [Fact]
    public void Una_tela_tutta_trasparente_pesa_pochissimo()
    {
        // È il caso normale del ricalco: quasi tutto trasparente. Se pesasse quanto i punti
        // grezzi, il pattern diventerebbe illeggibile per il peso invece che per il disegno.
        var png = Png.Scrivi(new byte[300 * 300 * 4], 300, 300);

        Assert.True(png.Length < 4096, $"{png.Length} byte per una tela vuota");
    }

    [Fact]
    public void Misure_che_non_tornano_non_passano()
    {
        Assert.Throws<ArgumentException>(() => Png.Scrivi(new byte[16], 8, 8));
    }
}
