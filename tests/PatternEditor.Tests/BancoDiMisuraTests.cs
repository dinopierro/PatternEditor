using PatternEditor.Abstractions.Serialization;
using PatternEditor.Abstractions.Tests;
using PatternEditor.Services;
using PatternEditor.Services.Riconoscimento;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// Il banco di misura della conversione da immagine a pattern.
/// </summary>
///
/// <remarks>
/// <para>
/// Il riconoscitore non c'è: è stato tolto perché reggeva i disegni piatti e cedeva su tutto
/// il resto. Quello che resta, e che queste prove tengono in piedi, è il <b>metro</b> —
/// ridipingere una ricostruzione e confrontarla punto per punto con il disegno di partenza.
/// </para>
/// <para>
/// Vale la pena tenerlo vivo da solo, senza niente da misurare, per una ragione precisa: è
/// l'unico modo di sapere se la catena che verrà è meglio o peggio di quella che c'era. Senza
/// un numero si giudica a occhio, e a occhio una trama ripetuta inganna — la percentuale della
/// prima conversione saliva dell'86 all'87 mentre il motivo si sfasciava, perché la misura è
/// dominata dal fondo. Anche questo, il banco deve saperlo dire, ed è il motivo per cui la
/// differenza non è un numero solo ma quattro stati distinti.
/// </para>
/// </remarks>
public class BancoDiMisuraTests
{
    private const int Lato = 40;

    private static readonly (byte R, byte G, byte B) Carta = (255, 255, 255);

    /// <summary>Una tela bianca e opaca, su cui disegnare.</summary>
    private static byte[] Tela(int larghezza, int altezza)
    {
        var pixel = new byte[larghezza * altezza * 4];
        Array.Fill(pixel, (byte)255);
        return pixel;
    }

    private static void Quadrato(byte[] tela, int larghezza, int x0, int y0, int lato,
                                 byte r = 0, byte g = 0, byte b = 0) =>
        Riquadro(tela, larghezza, x0, y0, lato, lato, r, g, b);

    private static void Riquadro(byte[] tela, int larghezza, int x0, int y0, int w, int h,
                                 byte r = 0, byte g = 0, byte b = 0)
    {
        for (var y = y0; y < y0 + h; y++)
        {
            for (var x = x0; x < x0 + w; x++)
            {
                var i = (y * larghezza + x) * 4;
                tela[i] = r;
                tela[i + 1] = g;
                tela[i + 2] = b;
                tela[i + 3] = 255;
            }
        }
    }

    private static Confronto Misura(byte[] tela, params Forma[] forme) =>
        Somiglianza.Misura(forme, Carta, Lato, Lato, tela, Lato, Lato);

    private static EsitoConversione Converti(byte[] tela, int colori = 4,
                                             Fedelta fedelta = Fedelta.Normale)
    {
        var registro = AllPlugins.Registry();
        return new RasterPatternImporter(
            new SvgPatternImporter(registro, new PatternSerializer(registro)))
            .Import(tela, Lato, Lato, "Prova", colori, fedelta);
    }

    // ------------------------------------------------------------------- il metro ----

    [Fact]
    public void Una_ricostruzione_esatta_vale_uno()
    {
        var tela = Tela(Lato, Lato);
        Quadrato(tela, Lato, 10, 10, 12);

        var confronto = Misura(tela, new FormaRettangolo(10, 10, 12, 12, "#000000"));

        Assert.Equal(1.0, confronto.Somiglianza, 3);
    }

    [Fact]
    public void Una_ricostruzione_vuota_vale_quanto_il_solo_fondo()
    {
        var tela = Tela(Lato, Lato);
        Quadrato(tela, Lato, 10, 10, 12);

        var confronto = Misura(tela);

        // 144 punti di disegno su 1600: un foglio bianco «somiglia» già al 91%. È esattamente
        // l'inganno da cui deve difendere la mappa delle differenze.
        Assert.Equal(1.0 - 144.0 / (Lato * Lato), confronto.Somiglianza, 3);
    }

    [Fact]
    public void La_forma_giusta_del_colore_sbagliato_non_e_una_forma_mancante()
    {
        var tela = Tela(Lato, Lato);
        Quadrato(tela, Lato, 10, 10, 12);

        var confronto = Misura(tela, new FormaRettangolo(10, 10, 12, 12, "#c02020"));

        // Stessa somiglianza di una ricostruzione vuota, ma per un motivo diverso: qui il
        // disegno c'è. Distinguere le due cose è tutto il valore della mappa.
        Assert.Equal(144, Quanti(confronto, Stato.Storto));
        Assert.Equal(0, Quanti(confronto, Stato.Manca));
    }

    [Fact]
    public void Il_disegno_inventato_si_distingue_da_quello_mancante()
    {
        var tela = Tela(Lato, Lato);
        Quadrato(tela, Lato, 10, 10, 12);

        // Il rettangolo giusto è spostato di venti punti: metà del suo posto resta vuota,
        // metà viene dipinta dove non c'era niente.
        var confronto = Misura(tela, new FormaRettangolo(10, 25, 12, 12, "#000000"));

        Assert.Equal(144, Quanti(confronto, Stato.Manca));
        Assert.Equal(144, Quanti(confronto, Stato.Aggiunto));
        Assert.Equal(0, Quanti(confronto, Stato.Storto));
    }

    [Fact]
    public void Una_sfumatura_di_compressione_non_conta_come_errore()
    {
        // Un JPEG non restituisce mai il nero che gli hai dato. Se il metro fosse esatto,
        // qualunque ricostruzione giusta di un'immagine compressa risulterebbe sbagliata.
        var tela = Tela(Lato, Lato);
        Quadrato(tela, Lato, 10, 10, 12, 14, 9, 11);

        var confronto = Misura(tela, new FormaRettangolo(10, 10, 12, 12, "#000000"));

        Assert.Equal(1.0, confronto.Somiglianza, 3);
    }

    // ------------------------------------------------------------------ la cella ----

    [Fact]
    public void Una_forma_che_esce_dalla_cella_rientra_dall_altra_parte()
    {
        // Una cella è una tessera. Senza avvolgimento ogni forma a cavallo del bordo
        // risulterebbe inventata per metà, e il metro direbbe male di una ricostruzione giusta.
        var tela = Tela(Lato, Lato);
        Riquadro(tela, Lato, 0, 10, 6, 6);
        Riquadro(tela, Lato, 36, 10, 4, 6);

        var confronto = Misura(tela, new FormaRettangolo(36, 10, 10, 6, "#000000"));

        Assert.Equal(1.0, confronto.Somiglianza, 3);
    }

    [Fact]
    public void Il_ritaglio_restituito_e_grande_quanto_la_cella()
    {
        var confronto = Misura(Tela(Lato, Lato));

        Assert.Equal(Lato, confronto.Larghezza);
        Assert.Equal(Lato, confronto.Altezza);
        Assert.Equal(Lato * Lato * 4, confronto.Originale.Length);
        Assert.Equal(Lato * Lato * 4, confronto.Differenza.Length);
    }

    // -------------------------------------------------------------- la riduzione ----

    [Fact]
    public void Rimpicciolire_media_i_punti_che_si_fondono()
    {
        var tela = Tela(4, 4);
        Quadrato(tela, 4, 0, 0, 2);

        var piccola = new Immagine(tela, 4, 4).Ridotta(2);

        Assert.Equal(2, piccola.Larghezza);
        Assert.Equal((0, 0, 0), piccola.Colore(0, 0));
        Assert.Equal((255, 255, 255), piccola.Colore(1, 1));
    }

    [Fact]
    public void Un_immagine_gia_piccola_non_si_tocca()
    {
        var immagine = new Immagine(Tela(Lato, Lato), Lato, Lato);

        Assert.Equal(1, RasterPatternImporter.FattoreDiRiduzione(Lato, Lato));
        Assert.Same(immagine, immagine.Ridotta(1));
    }

    [Fact]
    public void Una_immagine_grande_si_porta_sotto_il_lato_di_lavoro()
    {
        var fattore = RasterPatternImporter.FattoreDiRiduzione(900, 900);

        Assert.Equal(3, fattore);
        Assert.True(900 / fattore <= RasterPatternImporter.LatoDiLavoro);
    }

    // ------------------------------------------------------------------ il tubo ----

    [Fact]
    public void Un_quadretto_su_carta_torna_indietro_quasi_identico()
    {
        var tela = Tela(Lato, Lato);
        Quadrato(tela, Lato, 10, 10, 12);

        var esito = Converti(tela);

        Assert.False(esito.Esito.IsEmpty, string.Join(" · ", esito.Esito.Warnings));
        Assert.NotNull(esito.Confronto);

        // Due colori e una forma sola: se questo non torna quasi esatto, non tornerÃ  niente.
        Assert.True(esito.Confronto!.Somiglianza > 0.98,
                    $"somiglianza {esito.Confronto.Somiglianza:0.000}");
    }

    [Fact]
    public void Una_forma_storta_diventa_una_poligonale()
    {
        // Un triangolo non riempie il proprio ingombro, quindi non puÃ² essere un rettangolo:
        // deve uscirne una poligonale che ne segue il bordo.
        var tela = Tela(Lato, Lato);
        for (var y = 6; y < 30; y++)
        {
            Riquadro(tela, Lato, 20 - (y - 6) / 2, y, y - 5, 1);
        }

        var esito = Converti(tela);

        Assert.False(esito.Esito.IsEmpty, string.Join(" · ", esito.Esito.Warnings));
        Assert.True(esito.Confronto!.Somiglianza > 0.95,
                    $"somiglianza {esito.Confronto.Somiglianza:0.000}");
    }

    [Fact]
    public void Tre_colori_restano_tre_colori()
    {
        var tela = Tela(Lato, Lato);
        Quadrato(tela, Lato, 4, 4, 12, 200, 30, 30);
        Quadrato(tela, Lato, 24, 24, 12, 30, 60, 200);

        var esito = Converti(tela);

        Assert.False(esito.Esito.IsEmpty, string.Join(" · ", esito.Esito.Warnings));

        // Il rosso e il blu non devono fondersi in un viola: Ã¨ il guasto tipico di una
        // tavolozza a intervalli fissi, e il motivo per cui si usa il taglio mediano.
        Assert.True(esito.Confronto!.Somiglianza > 0.97,
                    $"somiglianza {esito.Confronto.Somiglianza:0.000}");
    }

    [Fact]
    public void Con_poche_bande_le_correzioni_recuperano_i_colori_lasciati_fuori()
    {
        // Cinque tinte distinte e due sole bande: la tavolozza non può renderle: ne pareggia
        // tre. Quello che le recupera è il giro sul residuo — si dipinge, si guarda dove non
        // corrisponde, e si ridisegna lì col colore vero. Senza, il disegno resterebbe a due
        // colori qualunque cosa ci fosse sotto.
        var tela = Tela(Lato, Lato);
        Quadrato(tela, Lato, 2, 2, 14, 210, 40, 40);
        Quadrato(tela, Lato, 24, 2, 14, 40, 170, 60);
        Quadrato(tela, Lato, 2, 24, 14, 40, 60, 200);
        Quadrato(tela, Lato, 24, 24, 14, 220, 190, 30);

        var esito = Converti(tela, colori: 2);

        Assert.False(esito.Esito.IsEmpty, string.Join(" · ", esito.Esito.Warnings));

        // Due bande sole su cinque tinte: senza correzioni non si andrebbe oltre il fondo più
        // una tinta, cioè attorno al settanta per cento.
        Assert.True(esito.Confronto!.Somiglianza > 0.95,
                    $"somiglianza {esito.Confronto.Somiglianza:0.000}");
    }

    // ------------------------------------------------------------------ fedeltà ----

    [Fact]
    public void Il_ricalco_mette_sopra_un_immagine_e_lo_dice()
    {
        // Una sfumatura continua: nessun riempimento piatto la può rendere, ed è il caso in cui
        // il ricalco serve davvero.
        var tela = Tela(Lato, Lato);
        for (var y = 0; y < Lato; y++)
        {
            for (var x = 0; x < Lato; x++)
            {
                var i = (y * Lato + x) * 4;
                tela[i] = (byte)(x * 255 / Lato);
                tela[i + 1] = (byte)(y * 255 / Lato);
                tela[i + 2] = 128;
            }
        }

        var normale = Converti(tela);
        var ricalco = Converti(tela, fedelta: Fedelta.Ricalco);

        Assert.False(ricalco.Esito.IsEmpty, string.Join(" · ", ricalco.Esito.Warnings));

        // L'immagine c'è, ed è l'ultimo elemento: deve stare sopra tutto il resto.
        var elementi = ricalco.Esito.Pattern!.Definition.Elements;
        Assert.True(elementi[^1].Type == "image",
                    "tipi: " + string.Join(",", elementi.Select(e => e.Type).Distinct()) +
                    " | avvisi: " + string.Join(" · ", ricalco.Esito.Warnings));
        Assert.DoesNotContain(normale.Esito.Pattern!.Definition.Elements, e => e.Type == "image");

        // E deve dirlo: una toppa a punti dentro un disegno vettoriale non può essere una
        // sorpresa che si scopre stampando.
        Assert.Contains(ricalco.Esito.Warnings,
                        a => a.Contains("transparent background", StringComparison.OrdinalIgnoreCase));

        // La somiglianza <b>non</b> cambia, ed è la cosa da fissare qui. La misura continua a
        // giudicare il disegno e non la toppa: contando anche quella segnerebbe cento per cento
        // per costruzione, e si perderebbe l'unico strumento che dice se il vettorizzatore
        // migliora. Chi guarda vede un'immagine quasi identica e un numero mediocre, e i due
        // insieme sono esattamente la verità.
        Assert.Equal(normale.Confronto!.Somiglianza, ricalco.Confronto!.Somiglianza, 3);
    }

    [Fact]
    public void La_fedelta_massima_resta_vettoriale()
    {
        var tela = Tela(Lato, Lato);
        Quadrato(tela, Lato, 4, 4, 12, 210, 40, 40);
        Quadrato(tela, Lato, 24, 24, 12, 40, 60, 200);

        var esito = Converti(tela, colori: 2, fedelta: Fedelta.Massima);

        Assert.False(esito.Esito.IsEmpty, string.Join(" · ", esito.Esito.Warnings));

        // Nessun elemento a punti: è la differenza che distingue «massima» da «ricalco».
        Assert.DoesNotContain(esito.Esito.Pattern!.Definition.Elements, e => e.Type == "image");
    }

    // ------------------------------------------------------------------- appoggi ----

    private enum Stato
    {
        Coincide,
        Manca,
        Aggiunto,
        Storto,
    }

    private static int Quanti(Confronto confronto, Stato stato)
    {
        var tinta = stato switch
        {
            Stato.Coincide => (0xdd, 0xe1, 0xe6),
            Stato.Manca => (0xd6, 0x45, 0x45),
            Stato.Aggiunto => (0x3b, 0x6e, 0xf5),
            _ => (0xc2, 0x70, 0x1c),
        };

        var quanti = 0;
        for (var i = 0; i < confronto.Differenza.Length; i += 4)
        {
            if ((confronto.Differenza[i], confronto.Differenza[i + 1], confronto.Differenza[i + 2])
                == tinta)
            {
                quanti++;
            }
        }

        return quanti;
    }
}
