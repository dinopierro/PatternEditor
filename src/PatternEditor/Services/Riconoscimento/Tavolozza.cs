namespace PatternEditor.Services.Riconoscimento;

/// <summary>
/// L'immagine ridotta a pochi colori, e la mappa di quale colore sta dove.
/// </summary>
///
/// <param name="Colori">I colori scelti, dal più esteso al meno.</param>
/// <param name="Mappa">Per ogni punto, l'indice del colore che gli è toccato.</param>
/// <param name="Larghezza">Larghezza della mappa.</param>
/// <param name="Altezza">Altezza della mappa.</param>
public sealed record Tavolozza(
    IReadOnlyList<(byte R, byte G, byte B)> Colori, int[] Mappa, int Larghezza, int Altezza)
{
    /// <summary>Quanti punti sono toccati a ciascun colore.</summary>
    public int Quanti(int colore) => Mappa.Count(c => c == colore);
}

/// <summary>
/// Riduce un'immagine a poche bande di colore.
/// </summary>
///
/// <remarks>
/// <para>
/// È il primo passo, e sostituisce quello che prima era la domanda sbagliata. Il riconoscitore
/// precedente chiedeva «qual è il fondo e quale il disegno», e su un pavimento dove ogni
/// mattone è di un rosso diverso quella domanda non ha risposta: qualunque scelta è il negativo
/// di un'altra altrettanto plausibile. Qui non si chiede niente del genere. Si dice soltanto:
/// questi sono i sei colori dell'immagine, e ogni punto appartiene a uno di essi. Quale sia lo
/// sfondo lo dirà poi la geometria — è il colore che copre più superficie — e non una soglia.
/// </para>
/// <para>
/// I colori si scelgono <b>sull'immagine</b> col taglio mediano, non a intervalli fissi. A
/// intervalli fissi una fotografia di mattoni finisce tutta in due o tre caselle vicine mentre
/// metà tavolozza resta vuota su verdi e blu che nell'immagine non ci sono; e una sfumatura si
/// spezza dove capita il confine, invece che dove il colore cambia davvero. Il taglio mediano
/// parte da una scatola che contiene tutti i colori e la divide ripetutamente a metà lungo il
/// lato in cui i colori sono più sparpagliati, così le bande si stringono dove i colori sono
/// fitti e restano larghe dove sono radi.
/// </para>
/// <para>
/// Poi qualche passata di riassestamento: ogni punto va al colore che gli è più vicino, e ogni
/// colore si rifà media dei punti che gli sono toccati. Il taglio mediano dà scatole, che sono
/// una divisione ragionevole ma rigida; le passate le lasciano assestare sui grumi veri.
/// </para>
/// </remarks>
public static class Bande
{
    /// <summary>Quante volte si riassestano i colori dopo il taglio mediano.</summary>
    private const int Passate = 4;

    /// <summary>
    /// I colori si contano raggruppati, trentadue livelli per canale invece di
    /// duecentocinquantasei.
    /// </summary>
    ///
    /// <remarks>
    /// Un bianco di scansione non è mai lo stesso bianco due volte — è 253, 254, 255 a seconda
    /// del punto — e contandoli separati si conterebbero le sfumature della compressione invece
    /// dei colori del disegno. Trentadue livelli sono abbastanza fini da non fondere due tinte
    /// che l'occhio distingue, e abbastanza grossi da far pesare una tinta piatta per quello
    /// che è: una sola.
    /// </remarks>
    private const int Livelli = 32;

    private const int Passo = 256 / Livelli;

    /// <summary>
    /// Divide l'immagine in <paramref name="quante"/> bande di colore.
    /// </summary>
    ///
    /// <param name="immagine">L'immagine da ridurre.</param>
    /// <param name="quante">Quante bande. Da 2 in su; oltre la ventina non ha senso.</param>
    public static Tavolozza Dividi(Immagine immagine, int quante)
    {
        ArgumentNullException.ThrowIfNull(immagine);

        quante = Math.Max(2, quante);

        var larghezza = immagine.Larghezza;
        var altezza = immagine.Altezza;

        // L'istogramma raggruppato: un colore, quante volte compare.
        var conteggi = new int[Livelli * Livelli * Livelli];
        for (var y = 0; y < altezza; y++)
        {
            for (var x = 0; x < larghezza; x++)
            {
                var (r, g, b) = immagine.Colore(x, y);
                conteggi[Casella(r, g, b)]++;
            }
        }

        var presenti = new List<int>();
        for (var i = 0; i < conteggi.Length; i++)
        {
            if (conteggi[i] > 0)
            {
                presenti.Add(i);
            }
        }

        var centri = TaglioMediano(presenti, conteggi, quante);
        centri = Riassesta(immagine, centri);

        // I colori si riordinano per estensione: il più diffuso è quello che finirà sotto tutti
        // gli altri, e chi legge il pattern si aspetta di trovare il fondo per primo.
        var mappa = new int[larghezza * altezza];
        var quantiPer = new int[centri.Count];

        for (var y = 0; y < altezza; y++)
        {
            for (var x = 0; x < larghezza; x++)
            {
                var quale = PiuVicino(immagine.Colore(x, y), centri);
                mappa[y * larghezza + x] = quale;
                quantiPer[quale]++;
            }
        }

        var ordine = Enumerable.Range(0, centri.Count)
            .Where(i => quantiPer[i] > 0)
            .OrderByDescending(i => quantiPer[i])
            .ToList();

        var nuovo = new int[centri.Count];
        for (var i = 0; i < ordine.Count; i++)
        {
            nuovo[ordine[i]] = i;
        }

        for (var i = 0; i < mappa.Length; i++)
        {
            mappa[i] = nuovo[mappa[i]];
        }

        return new Tavolozza([.. ordine.Select(i => centri[i])], mappa, larghezza, altezza);
    }

    private static int Casella(byte r, byte g, byte b) =>
        (r / Passo) * Livelli * Livelli + (g / Passo) * Livelli + b / Passo;

    private static (byte R, byte G, byte B) Colore(int casella)
    {
        var b = casella % Livelli;
        var g = casella / Livelli % Livelli;
        var r = casella / (Livelli * Livelli);

        // Il centro della casella, non il suo spigolo: mezzo passo di errore su ogni canale si
        // vedrebbe come un velo su tutta la ricostruzione.
        return ((byte)(r * Passo + Passo / 2),
                (byte)(g * Passo + Passo / 2),
                (byte)(b * Passo + Passo / 2));
    }

    /// <summary>
    /// Divide ripetutamente in due la scatola di colori più sparpagliata.
    /// </summary>
    private static List<(byte R, byte G, byte B)> TaglioMediano(
        List<int> presenti, int[] conteggi, int quante)
    {
        var scatole = new List<List<int>> { presenti };

        while (scatole.Count < quante)
        {
            // Si divide quella che ha il lato più lungo: è lì che due colori distinti stanno
            // ancora insieme.
            var quale = -1;
            var piuLunga = 0;
            var lungoQuale = 0;

            for (var i = 0; i < scatole.Count; i++)
            {
                if (scatole[i].Count < 2)
                {
                    continue;
                }

                var (lato, canale) = LatoPiuLungo(scatole[i]);
                if (lato > piuLunga)
                {
                    piuLunga = lato;
                    quale = i;
                    lungoQuale = canale;
                }
            }

            if (quale < 0)
            {
                break;
            }

            var scatola = scatole[quale];
            scatola.Sort((a, b) => Canale(a, lungoQuale).CompareTo(Canale(b, lungoQuale)));

            // Il taglio a metà del <b>peso</b>, non a metà della lista: una tinta piatta che
            // occupa mezza immagine è una casella sola, e dividere per numero di caselle la
            // lascerebbe insieme a tutto il resto.
            var totale = scatola.Sum(c => (long)conteggi[c]);
            long finora = 0;
            var taglio = 1;

            for (var i = 0; i < scatola.Count - 1; i++)
            {
                finora += conteggi[scatola[i]];
                if (finora * 2 >= totale)
                {
                    taglio = i + 1;
                    break;
                }
            }

            scatole[quale] = scatola.GetRange(0, taglio);
            scatole.Add(scatola.GetRange(taglio, scatola.Count - taglio));
        }

        return [.. scatole.Where(s => s.Count > 0).Select(s => Media(s, conteggi))];
    }

    private static int Canale(int casella, int quale) => quale switch
    {
        0 => casella / (Livelli * Livelli),
        1 => casella / Livelli % Livelli,
        _ => casella % Livelli,
    };

    private static (int Lato, int Canale) LatoPiuLungo(List<int> scatola)
    {
        var lato = 0;
        var quale = 0;

        for (var canale = 0; canale < 3; canale++)
        {
            var minimo = int.MaxValue;
            var massimo = int.MinValue;

            foreach (var casella in scatola)
            {
                var v = Canale(casella, canale);
                minimo = Math.Min(minimo, v);
                massimo = Math.Max(massimo, v);
            }

            if (massimo - minimo > lato)
            {
                lato = massimo - minimo;
                quale = canale;
            }
        }

        return (lato, quale);
    }

    private static (byte R, byte G, byte B) Media(List<int> scatola, int[] conteggi)
    {
        long r = 0, g = 0, b = 0, quanti = 0;

        foreach (var casella in scatola)
        {
            var (cr, cg, cb) = Colore(casella);
            var peso = conteggi[casella];

            r += (long)cr * peso;
            g += (long)cg * peso;
            b += (long)cb * peso;
            quanti += peso;
        }

        return quanti == 0
            ? ((byte)0, (byte)0, (byte)0)
            : ((byte)(r / quanti), (byte)(g / quanti), (byte)(b / quanti));
    }

    /// <summary>Qualche passata perché i colori si assestino sui grumi veri.</summary>
    private static List<(byte R, byte G, byte B)> Riassesta(
        Immagine immagine, List<(byte R, byte G, byte B)> centri)
    {
        for (var passata = 0; passata < Passate; passata++)
        {
            var somme = new long[centri.Count * 3];
            var quanti = new int[centri.Count];

            for (var y = 0; y < immagine.Altezza; y++)
            {
                for (var x = 0; x < immagine.Larghezza; x++)
                {
                    var (r, g, b) = immagine.Colore(x, y);
                    var quale = PiuVicino((r, g, b), centri);

                    somme[quale * 3] += r;
                    somme[quale * 3 + 1] += g;
                    somme[quale * 3 + 2] += b;
                    quanti[quale]++;
                }
            }

            var mosso = false;
            for (var i = 0; i < centri.Count; i++)
            {
                // Un colore che non ha preso nessun punto resta dov'è: spostarlo sull'origine
                // lo manderebbe sul nero, che è un colore come un altro e si prenderebbe
                // mezza immagine alla passata dopo.
                if (quanti[i] == 0)
                {
                    continue;
                }

                var nuovo = ((byte)(somme[i * 3] / quanti[i]),
                             (byte)(somme[i * 3 + 1] / quanti[i]),
                             (byte)(somme[i * 3 + 2] / quanti[i]));

                if (nuovo != centri[i])
                {
                    centri[i] = nuovo;
                    mosso = true;
                }
            }

            if (!mosso)
            {
                break;
            }
        }

        return centri;
    }

    private static int PiuVicino((byte R, byte G, byte B) colore, List<(byte R, byte G, byte B)> centri)
    {
        var quale = 0;
        var meglio = long.MaxValue;

        for (var i = 0; i < centri.Count; i++)
        {
            long dr = colore.R - centri[i].R;
            long dg = colore.G - centri[i].G;
            long db = colore.B - centri[i].B;

            var distanza = dr * dr + dg * dg + db * db;
            if (distanza < meglio)
            {
                meglio = distanza;
                quale = i;
            }
        }

        return quale;
    }
}
