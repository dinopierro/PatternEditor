using PatternEditor.Core.Formatting;

namespace PatternEditor.Services.Riconoscimento;

/// <summary>
/// Una forma riconosciuta nella cella.
/// </summary>
///
/// <remarks>
/// <para>
/// Sa fare due cose, e la seconda è il motivo per cui esiste: <b>scriversi</b> in SVG, e
/// <b>ridisegnarsi</b> su una maschera. Finché il riconoscimento produceva soltanto del
/// markup non c'era modo di sapere quanto somigliasse all'originale: si poteva guardare, non
/// misurare. Potendola ridisegnare si confronta punto per punto ciò che si è capito con ciò
/// che c'era, e quel confronto è l'unica cosa che renda la funzione affidabile invece che
/// suggestiva.
/// </para>
/// <para>
/// Il disegno avvolge i bordi. Una cella è una tessera, e una forma che esce a destra rientra
/// a sinistra: senza avvolgere, ogni tratto inclinato risulterebbe «inventato» per metà e la
/// misura direbbe male di una ricostruzione giusta.
/// </para>
/// </remarks>
public abstract record Forma(string Colore)
{
    /// <summary>Come si scrive nel documento.</summary>
    public abstract string Svg();

    /// <summary>Si ridisegna sulla tela, con il proprio colore.</summary>
    public abstract void Disegna(Tela dove);

    protected static string N(double valore) => InvariantNumber.Format(Math.Round(valore, 2));

    /// <summary>
    /// Il colore della forma in tre byte, letto una volta sola dalla sua scrittura esadecimale.
    /// </summary>
    protected (byte R, byte G, byte B) Tinta => _tinta ??= Leggi(Colore);

    private (byte R, byte G, byte B)? _tinta;

    private static (byte R, byte G, byte B) Leggi(string colore)
    {
        // Un colore che non si legge non è un caso da eccezione: nel peggiore dei casi la
        // forma si ridisegna nera, e la misura ne risente per quella forma sola.
        if (colore.Length != 7 || colore[0] != '#')
        {
            return (0, 0, 0);
        }

        return (Convert.ToByte(colore.Substring(1, 2), 16),
                Convert.ToByte(colore.Substring(3, 2), 16),
                Convert.ToByte(colore.Substring(5, 2), 16));
    }
}

/// <summary>Un rettangolo con i lati paralleli agli assi.</summary>
public sealed record FormaRettangolo(double X, double Y, double Larghezza, double Altezza, string Colore)
    : Forma(Colore)
{
    public override string Svg() =>
        $"<rect x=\"{N(X)}\" y=\"{N(Y)}\" width=\"{N(Larghezza)}\" height=\"{N(Altezza)}\" " +
        $"fill=\"{Colore}\" />";

    public override void Disegna(Tela dove)
    {
        for (var y = (int)Math.Floor(Y); y < Y + Altezza; y++)
        {
            for (var x = (int)Math.Floor(X); x < X + Larghezza; x++)
            {
                dove.Dipingi(x, y, Tinta);
            }
        }
    }
}

/// <summary>Un cerchio.</summary>
public sealed record FormaCerchio(double Cx, double Cy, double Raggio, string Colore) : Forma(Colore)
{
    public override string Svg() =>
        $"<circle cx=\"{N(Cx)}\" cy=\"{N(Cy)}\" r=\"{N(Raggio)}\" fill=\"{Colore}\" />";

    public override void Disegna(Tela dove) =>
        new FormaEllisse(Cx, Cy, Raggio, Raggio, Colore).Disegna(dove);
}

/// <summary>Un'ellisse con gli assi paralleli a quelli della cella.</summary>
public sealed record FormaEllisse(double Cx, double Cy, double Rx, double Ry, string Colore)
    : Forma(Colore)
{
    public override string Svg() =>
        $"<ellipse cx=\"{N(Cx)}\" cy=\"{N(Cy)}\" rx=\"{N(Rx)}\" ry=\"{N(Ry)}\" fill=\"{Colore}\" />";

    public override void Disegna(Tela dove)
    {
        var rx = Math.Max(0.5, Rx);
        var ry = Math.Max(0.5, Ry);

        for (var y = (int)Math.Floor(Cy - ry); y <= Cy + ry; y++)
        {
            for (var x = (int)Math.Floor(Cx - rx); x <= Cx + rx; x++)
            {
                var dx = (x - Cx) / rx;
                var dy = (y - Cy) / ry;

                if (dx * dx + dy * dy <= 1.0)
                {
                    dove.Dipingi(x, y, Tinta);
                }
            }
        }
    }
}

/// <summary>Un segmento con uno spessore.</summary>
public sealed record FormaSegmento(double X1, double Y1, double X2, double Y2,
                                   double Spessore, string Colore) : Forma(Colore)
{
    public override string Svg() =>
        $"<line x1=\"{N(X1)}\" y1=\"{N(Y1)}\" x2=\"{N(X2)}\" y2=\"{N(Y2)}\" " +
        $"stroke=\"{Colore}\" stroke-width=\"{N(Spessore)}\" />";

    public override void Disegna(Tela dove)
    {
        var mezzo = Math.Max(0.5, Spessore / 2);
        var dx = X2 - X1;
        var dy = Y2 - Y1;
        var quadrato = dx * dx + dy * dy;

        var da = (int)Math.Floor(Math.Min(X1, X2) - mezzo);
        var a = (int)Math.Ceiling(Math.Max(X1, X2) + mezzo);
        var su = (int)Math.Floor(Math.Min(Y1, Y2) - mezzo);
        var giu = (int)Math.Ceiling(Math.Max(Y1, Y2) + mezzo);

        for (var y = su; y <= giu; y++)
        {
            for (var x = da; x <= a; x++)
            {
                // Distanza dal segmento, non dalla retta: il parametro si limita agli estremi,
                // altrimenti il tratto continuerebbe oltre le proprie punte.
                var t = quadrato < 1e-9 ? 0 : ((x - X1) * dx + (y - Y1) * dy) / quadrato;
                t = Math.Clamp(t, 0, 1);

                var px = X1 + t * dx - x;
                var py = Y1 + t * dy - y;

                if (px * px + py * py <= mezzo * mezzo)
                {
                    dove.Dipingi(x, y, Tinta);
                }
            }
        }
    }
}

/// <summary>Una poligonale chiusa e piena.</summary>
public sealed record FormaPoligono(IReadOnlyList<(double X, double Y)> Vertici, string Colore)
    : Forma(Colore)
{
    public override string Svg() =>
        $"<polygon points=\"{string.Join(' ', Vertici.Select(v => $"{N(v.X)},{N(v.Y)}"))}\" " +
        $"fill=\"{Colore}\" />";

    public override void Disegna(Tela dove)
    {
        if (Vertici.Count < 3)
        {
            return;
        }

        var da = (int)Math.Floor(Vertici.Min(v => v.X));
        var a = (int)Math.Ceiling(Vertici.Max(v => v.X));
        var su = (int)Math.Floor(Vertici.Min(v => v.Y));
        var giu = (int)Math.Ceiling(Vertici.Max(v => v.Y));

        for (var y = su; y <= giu; y++)
        {
            for (var x = da; x <= a; x++)
            {
                if (Dentro(x, y))
                {
                    dove.Dipingi(x, y, Tinta);
                }
            }
        }
    }

    /// <summary>
    /// Il punto sta dentro la poligonale? Si contano le volte in cui un raggio che esce dal
    /// punto attraversa il bordo: dispari dentro, pari fuori.
    /// </summary>
    private bool Dentro(double x, double y)
    {
        var dentro = false;

        for (int i = 0, j = Vertici.Count - 1; i < Vertici.Count; j = i++)
        {
            var (xi, yi) = Vertici[i];
            var (xj, yj) = Vertici[j];

            if (yi > y != yj > y && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
            {
                dentro = !dentro;
            }
        }

        return dentro;
    }
}
