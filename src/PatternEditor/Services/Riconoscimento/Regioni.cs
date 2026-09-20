namespace PatternEditor.Services.Riconoscimento;

/// <summary>Una zona di colore uniforme: i suoi punti, il suo ingombro, la sua banda.</summary>
public sealed class Regione
{
    private readonly List<(int X, int Y)> _punti = [];

    public Regione(int banda) => Banda = banda;

    /// <summary>A quale colore della tavolozza appartiene.</summary>
    public int Banda { get; }

    public IReadOnlyList<(int X, int Y)> Punti => _punti;

    public int MinX { get; private set; } = int.MaxValue;

    public int MinY { get; private set; } = int.MaxValue;

    public int MaxX { get; private set; } = int.MinValue;

    public int MaxY { get; private set; } = int.MinValue;

    public int Area => _punti.Count;

    public int Larghezza => MaxX - MinX + 1;

    public int Altezza => MaxY - MinY + 1;

    /// <summary>Quanto la zona riempie il proprio rettangolo di ingombro: da 0 a 1.</summary>
    public double Riempimento => (double)Area / (Larghezza * Altezza);

    internal void Aggiungi(int x, int y)
    {
        _punti.Add((x, y));
        MinX = Math.Min(MinX, x);
        MinY = Math.Min(MinY, y);
        MaxX = Math.Max(MaxX, x);
        MaxY = Math.Max(MaxY, y);
    }
}

/// <summary>
/// Divide ogni banda di colore nelle zone che la compongono.
/// </summary>
///
/// <remarks>
/// Una banda di colore è sparsa per tutta l'immagine: il rosso dei mattoni tocca ogni mattone,
/// ma i mattoni sono venti oggetti distinti e non uno. Le zone si separano per
/// <b>contiguità</b>, dentro una banda per volta — e questa volta senza srotolare le
/// coordinate oltre il bordo, perché si lavora su una cella già ritagliata e una zona che esce
/// da un lato è semplicemente una zona che tocca il bordo.
/// </remarks>
public static class Regioni
{
    /// <summary>
    /// Tutte le zone della tavolozza, dalla più estesa alla meno.
    /// </summary>
    ///
    /// <param name="tavolozza">L'immagine ridotta a bande.</param>
    /// <param name="minima">
    /// Sotto quest'area una zona è pulviscolo: un punto di antialiasing rimasto isolato fra due
    /// bande, un granello di compressione. Disegnarla costerebbe un elemento e non si vedrebbe.
    /// </param>
    public static List<Regione> Trova(Tavolozza tavolozza, int minima = 6)
    {
        ArgumentNullException.ThrowIfNull(tavolozza);

        return Trova(tavolozza.Mappa, tavolozza.Larghezza, tavolozza.Altezza, minima);
    }

    /// <summary>
    /// Le zone di una mappa di bande qualsiasi.
    /// </summary>
    ///
    /// <param name="mappa">Per ogni punto, la banda a cui appartiene.</param>
    /// <param name="larghezza">Larghezza della mappa.</param>
    /// <param name="altezza">Altezza della mappa.</param>
    /// <param name="minima">Sotto quest'area una zona è pulviscolo.</param>
    ///
    /// <remarks>
    /// L'ingresso è una mappa e non una tavolozza perché serve anche per le zone del
    /// <b>residuo</b>, dove i punti già resi bene portano una banda negativa che non
    /// appartiene a nessun colore e che chi chiama butta via.
    /// </remarks>
    public static List<Regione> Trova(int[] mappa, int larghezza, int altezza, int minima = 6)
    {
        ArgumentNullException.ThrowIfNull(mappa);

        var visti = new bool[larghezza * altezza];
        var trovate = new List<Regione>();

        // La pila invece della ricorsione: una zona di fondo può essere centomila punti, e una
        // ricorsione così profonda esaurisce lo stack prima di finire il lavoro.
        var pila = new Stack<(int X, int Y)>();

        for (var y0 = 0; y0 < altezza; y0++)
        {
            for (var x0 = 0; x0 < larghezza; x0++)
            {
                if (visti[y0 * larghezza + x0])
                {
                    continue;
                }

                var banda = mappa[y0 * larghezza + x0];
                var regione = new Regione(banda);

                pila.Push((x0, y0));
                visti[y0 * larghezza + x0] = true;

                while (pila.Count > 0)
                {
                    var (x, y) = pila.Pop();
                    regione.Aggiungi(x, y);

                    // Quattro vicini e non otto: con otto, due zone che si toccano solo in
                    // diagonale diventano una sola, e il contorno che ne esce si strozza a
                    // clessidra in un punto dove il disegno non ha niente.
                    Prova(x - 1, y);
                    Prova(x + 1, y);
                    Prova(x, y - 1);
                    Prova(x, y + 1);
                }

                if (regione.Area >= minima)
                {
                    trovate.Add(regione);
                }

                void Prova(int x, int y)
                {
                    if (x < 0 || y < 0 || x >= larghezza || y >= altezza)
                    {
                        return;
                    }

                    var i = y * larghezza + x;
                    if (visti[i] || mappa[i] != banda)
                    {
                        return;
                    }

                    visti[i] = true;
                    pila.Push((x, y));
                }
            }
        }

        trovate.Sort((a, b) => b.Area.CompareTo(a.Area));
        return trovate;
    }
}
