namespace PatternEditor.Services.Riconoscimento;

/// <summary>
/// Il bordo di una zona, e la poligonale che lo riassume.
/// </summary>
///
/// <remarks>
/// <para>
/// Il bordo si segue col metodo di Moore: si parte dal punto più in alto a sinistra e si gira
/// attorno alla zona tenendo sempre la mano sul muro, finché non si torna al punto di partenza.
/// Quello che ne esce è una catena di punti fitta — uno per pixel di bordo — e va assottigliata,
/// altrimenti un quadrato di cento pixel di lato diventerebbe un poligono da quattrocento
/// vertici invece che da quattro.
/// </para>
/// <para>
/// Ad assottigliarla è Ramer–Douglas–Peucker: si tiene il primo e l'ultimo punto, si cerca il
/// punto più lontano dalla retta che li unisce, e se è più lontano della tolleranza lo si tiene
/// e si ripete sui due tratti. È il modo di dire «tieni solo gli angoli veri»: su un bordo
/// diritto non tiene niente, su un angolo tiene l'angolo.
/// </para>
/// </remarks>
public static class Contorno
{
    /// <summary>
    /// Gli otto vicini in ordine antiorario, a partire da destra.
    /// </summary>
    private static readonly (int X, int Y)[] Intorno =
    [
        (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1),
    ];

    /// <summary>Segue il bordo esterno della zona, un punto per pixel.</summary>
    public static List<(int X, int Y)> Segui(Regione regione)
    {
        ArgumentNullException.ThrowIfNull(regione);

        var dentro = regione.Punti.ToHashSet();

        // Il punto più in alto, e fra quelli il più a sinistra: da lì si è certi che il vicino
        // di sinistra è fuori, e quindi di che verso prendere.
        var partenza = regione.Punti
            .OrderBy(p => p.Y)
            .ThenBy(p => p.X)
            .First();

        var contorno = new List<(int X, int Y)> { partenza };

        var qui = partenza;
        var provenienza = 4; // si arriva da sinistra
        var passi = 0;
        var massimo = regione.Area * 4 + 32;

        while (passi++ < massimo)
        {
            var trovato = false;

            // Si riprende a cercare dal vicino da cui si è arrivati, girando in senso
            // antiorario. Ripartire da un punto fisso invece che da quello è l'errore che
            // fa chiudere il giro dopo tre punti su certe forme: la mano lascia il muro.
            for (var giro = 1; giro <= 8; giro++)
            {
                var direzione = (provenienza + giro) % 8;
                var prossimo = (qui.X + Intorno[direzione].X, qui.Y + Intorno[direzione].Y);

                if (!dentro.Contains(prossimo))
                {
                    continue;
                }

                provenienza = (direzione + 4) % 8;
                qui = prossimo;
                contorno.Add(qui);
                trovato = true;
                break;
            }

            // Un punto isolato: non ha bordo da seguire.
            if (!trovato)
            {
                break;
            }

            if (qui == partenza && contorno.Count > 2)
            {
                contorno.RemoveAt(contorno.Count - 1);
                break;
            }
        }

        return contorno;
    }

    /// <summary>
    /// Assottiglia la catena tenendo solo i punti che cambiano davvero la forma.
    /// </summary>
    ///
    /// <param name="contorno">La catena chiusa da assottigliare.</param>
    /// <param name="tolleranza">Di quanto il poligono può scostarsi dal bordo vero.</param>
    public static List<(double X, double Y)> Semplifica(
        IReadOnlyList<(int X, int Y)> contorno, double tolleranza)
    {
        ArgumentNullException.ThrowIfNull(contorno);

        if (contorno.Count < 4)
        {
            return [.. contorno.Select(p => ((double)p.X, (double)p.Y))];
        }

        // La catena è chiusa: si taglia nel punto più lontano dal primo, così i due capi
        // cadono su due angoli veri e non in mezzo a un lato diritto.
        var opposto = 0;
        var piuLontano = -1.0;

        for (var i = 1; i < contorno.Count; i++)
        {
            var d = Quadrato(contorno[0], contorno[i]);
            if (d > piuLontano)
            {
                piuLontano = d;
                opposto = i;
            }
        }

        var tenuti = new List<(int X, int Y)>();
        Assottiglia(contorno, 0, opposto, tolleranza, tenuti);
        tenuti.Add(contorno[opposto]);
        Assottiglia(contorno, opposto, contorno.Count - 1, tolleranza, tenuti);
        tenuti.Add(contorno[^1]);

        return [.. tenuti.Select(p => ((double)p.X, (double)p.Y))];
    }

    private static void Assottiglia(IReadOnlyList<(int X, int Y)> punti, int da, int a,
                                    double tolleranza, List<(int X, int Y)> tenuti)
    {
        if (a <= da + 1)
        {
            tenuti.Add(punti[da]);
            return;
        }

        var peggiore = 0.0;
        var quale = da;

        for (var i = da + 1; i < a; i++)
        {
            var distanza = DallaRetta(punti[i], punti[da], punti[a]);
            if (distanza > peggiore)
            {
                peggiore = distanza;
                quale = i;
            }
        }

        if (peggiore <= tolleranza)
        {
            tenuti.Add(punti[da]);
            return;
        }

        Assottiglia(punti, da, quale, tolleranza, tenuti);
        Assottiglia(punti, quale, a, tolleranza, tenuti);
    }

    private static double DallaRetta((int X, int Y) punto, (int X, int Y) da, (int X, int Y) a)
    {
        double dx = a.X - da.X;
        double dy = a.Y - da.Y;
        var lunghezza = Math.Sqrt(dx * dx + dy * dy);

        if (lunghezza < 1e-9)
        {
            return Math.Sqrt(Quadrato(punto, da));
        }

        return Math.Abs((punto.X - da.X) * dy - (punto.Y - da.Y) * dx) / lunghezza;
    }

    private static double Quadrato((int X, int Y) a, (int X, int Y) b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    /// <summary>L'area racchiusa da una poligonale, col metodo del laccio di scarpa.</summary>
    public static double Area(IReadOnlyList<(double X, double Y)> vertici)
    {
        ArgumentNullException.ThrowIfNull(vertici);

        if (vertici.Count < 3)
        {
            return 0;
        }

        var somma = 0.0;
        for (int i = 0, j = vertici.Count - 1; i < vertici.Count; j = i++)
        {
            somma += (vertici[j].X + vertici[i].X) * (vertici[j].Y - vertici[i].Y);
        }

        return Math.Abs(somma) / 2;
    }
}
