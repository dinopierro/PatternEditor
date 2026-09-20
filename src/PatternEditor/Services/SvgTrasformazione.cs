using System.Globalization;
using System.Text;
using PatternEditor.Core.Formatting;
using PatternEditor.Core.Localization;

namespace PatternEditor.Services;

/// <summary>
/// La trasformazione di un documento SVG, ridotta a ciò che si può cuocere dentro la
/// geometria: una scala e una traslazione.
///
/// <para>
/// Il modello dell'applicazione non ha gruppi né trasformazioni per elemento. Un documento
/// SVG ne è invece pieno — i programmi di disegno annidano gruppi traslati a ogni livello —
/// e ignorarle metterebbe le forme nel posto sbagliato. La via d'uscita è applicarle ai
/// numeri: una traslazione e una scala si possono riscrivere dentro le coordinate senza
/// perdere niente.
/// </para>
///
/// <para>
/// Rotazioni, inclinazioni e matrici generiche no: cambierebbero la <i>forma</i> degli
/// elementi — un rettangolo ruotato non è più un rettangolo, e diventerebbe un tracciato —
/// e la conversione, oltre a essere laboriosa, produrrebbe un modello che non somiglia più a
/// quello di partenza. Quando se ne incontra una, l'elemento viene lasciato fuori e detto.
/// </para>
/// </summary>
internal readonly record struct Trasformazione(
    double A, double B, double C, double D, double E, double F, bool Supportata)
{
    /// <summary>Nessuna trasformazione.</summary>
    public static readonly Trasformazione Identita = new(1, 0, 0, 1, 0, 0, true);

    private static readonly Trasformazione NonSupportata = new(1, 0, 0, 1, 0, 0, false);

    private const double Tolleranza = 1e-9;

    /// <summary>La matrice di SVG: (x,y) diventa (A·x + C·y + E, B·x + D·y + F).</summary>
    public static Trasformazione Matrice(double a, double b, double c, double d, double e, double f) =>
        new(a, b, c, d, e, f, true);

    public static Trasformazione Scala(double sx, double sy) => new(sx, 0, 0, sy, 0, 0, true);

    public static Trasformazione Traslazione(double tx, double ty) => new(1, 0, 0, 1, tx, ty, true);

    public bool IsIdentita =>
        Supportata && Vicini(A, 1) && Vicini(D, 1) && Vicini(B, 0) && Vicini(C, 0)
        && Vicini(E, 0) && Vicini(F, 0);

    /// <summary>Vero quando non ruota né inclina: solo scala e traslazione.</summary>
    public bool SoloScalaETraslazione => Supportata && Vicini(B, 0) && Vicini(C, 0);

    public double Sx => A;

    public double Sy => D;

    public double Tx => E;

    public double Ty => F;

    /// <summary>Questa trasformazione applicata <b>dopo</b> quella indicata (come l'annidamento).</summary>
    public Trasformazione Componi(Trasformazione interna)
    {
        if (!Supportata || !interna.Supportata)
        {
            return NonSupportata;
        }

        // Prodotto di due matrici affini: l'esterna moltiplica l'interna, e la traslazione
        // interna viene trasformata da quella esterna prima di sommarsi.
        return new Trasformazione(
            A * interna.A + C * interna.B,
            B * interna.A + D * interna.B,
            A * interna.C + C * interna.D,
            B * interna.C + D * interna.D,
            A * interna.E + C * interna.F + E,
            B * interna.E + D * interna.F + F,
            true);
    }

    /// <summary>Legge l'attributo <c>transform</c>, che è un elenco applicato da sinistra a destra.</summary>
    public static Trasformazione Leggi(string testo)
    {
        var risultato = Identita;
        var posizione = 0;

        while (posizione < testo.Length)
        {
            var apertura = testo.IndexOf('(', posizione);
            if (apertura < 0)
            {
                break;
            }

            var chiusura = testo.IndexOf(')', apertura);
            if (chiusura < 0)
            {
                break;
            }

            var nome = testo[posizione..apertura].Trim(' ', ',', '\t', '\r', '\n');
            var numeri = SvgPatternImporter.Numeri(testo[(apertura + 1)..chiusura]);
            posizione = chiusura + 1;

            risultato = risultato.Componi(Singola(nome, numeri));
            if (!risultato.Supportata)
            {
                return NonSupportata;
            }
        }

        return risultato;
    }

    private static Trasformazione Singola(string nome, List<double> n) => nome switch
    {
        "translate" when n.Count >= 1 => Traslazione(n[0], n.Count > 1 ? n[1] : 0),
        "scale" when n.Count >= 1 => Scala(n[0], n.Count > 1 ? n[1] : n[0]),
        "matrix" when n.Count >= 6 => Matrice(n[0], n[1], n[2], n[3], n[4], n[5]),

        // rotate(a) e rotate(a, cx, cy): la seconda forma è la prima portata sul punto.
        "rotate" when n.Count >= 3 =>
            Traslazione(n[1], n[2]).Componi(Rotazione(n[0])).Componi(Traslazione(-n[1], -n[2])),
        "rotate" when n.Count >= 1 => Rotazione(n[0]),

        // Le inclinazioni cambiano gli angoli fra i lati: nessuna forma del modello
        // sopravvive, e non c'è un modo onesto di approssimarle.
        _ => NonSupportata,
    };

    private static Trasformazione Rotazione(double gradi)
    {
        var r = gradi * Math.PI / 180;
        return new Trasformazione(Math.Cos(r), Math.Sin(r), -Math.Sin(r), Math.Cos(r), 0, 0, true);
    }

    // ------------------------------------------------------------------ scomposizione

    /// <summary>
    /// La trasformazione divisa in due parti: quella che si può cuocere nella geometria e
    /// quella che diventa la rotazione propria dell'elemento.
    ///
    /// <para>
    /// Il modello ha una rotazione per elemento, attorno a un punto scelto. Una matrice che
    /// sia una <b>similitudine</b> — scala uniforme, rotazione, eventuale specchiatura, e una
    /// traslazione — si riscrive esattamente così: si scala e si trasla la geometria, e si
    /// ruota attorno al punto in cui la traslazione è arrivata. Il conto è quello: ruotando
    /// attorno a T, il risultato è R·(S·g + T − T) + T, cioè R·S·g + T, che è proprio la
    /// matrice di partenza.
    /// </para>
    ///
    /// <para>
    /// Quello che resta fuori è la scala non uniforme combinata con una rotazione, e
    /// l'inclinazione: lì gli angoli fra i lati cambiano, e nessuna forma del modello — che
    /// ha rettangoli, cerchi e archi — resterebbe sé stessa.
    /// </para>
    /// </summary>
    public bool ProvaAScomporre(out Trasformazione geometria, out double gradi,
                                out bool specchiaX, out double originaleX, out double originaleY)
    {
        geometria = Identita;
        gradi = 0;
        specchiaX = false;
        originaleX = E;
        originaleY = F;

        if (!Supportata)
        {
            return false;
        }

        if (SoloScalaETraslazione)
        {
            geometria = this;
            return true;
        }

        var determinante = A * D - C * B;
        var scala = Math.Sqrt(Math.Abs(determinante));

        if (scala < Tolleranza)
        {
            return false;
        }

        // Rotazione senza specchiatura: A = D e C = −B, a meno della scala.
        if (determinante > 0 && Vicini(A, D) && Vicini(C, -B))
        {
            gradi = Math.Atan2(B, A) * 180 / Math.PI;
            geometria = new Trasformazione(scala, 0, 0, scala, E, F, true);
            return true;
        }

        // Rotazione con specchiatura orizzontale: A = −D e C = B.
        if (determinante < 0 && Vicini(A, -D) && Vicini(C, B))
        {
            gradi = Math.Atan2(-B, -A) * 180 / Math.PI;
            specchiaX = true;
            geometria = new Trasformazione(scala, 0, 0, scala, E, F, true);
            return true;
        }

        return false;
    }

    private static bool Vicini(double a, double b) => Math.Abs(a - b) < 1e-6;

    // ------------------------------------------------------------------ applicazione

    /// <summary>Come ogni attributo SVG va trasformato. Sono nomi della specifica, non del modello.</summary>
    private enum Regola
    {
        Nessuna,
        AscissaAssoluta,
        OrdinataAssoluta,
        LarghezzaScalata,
        AltezzaScalata,
        ScalaMedia,
        ElencoDiPunti,
        DatiDiTracciato,
    }

    private static readonly Dictionary<string, Regola> Regole = new(StringComparer.Ordinal)
    {
        ["x"] = Regola.AscissaAssoluta,
        ["y"] = Regola.OrdinataAssoluta,
        ["cx"] = Regola.AscissaAssoluta,
        ["cy"] = Regola.OrdinataAssoluta,
        ["x1"] = Regola.AscissaAssoluta,
        ["y1"] = Regola.OrdinataAssoluta,
        ["x2"] = Regola.AscissaAssoluta,
        ["y2"] = Regola.OrdinataAssoluta,
        ["width"] = Regola.LarghezzaScalata,
        ["height"] = Regola.AltezzaScalata,
        ["rx"] = Regola.LarghezzaScalata,
        ["ry"] = Regola.AltezzaScalata,
        ["r"] = Regola.ScalaMedia,
        ["stroke-width"] = Regola.ScalaMedia,
        ["font-size"] = Regola.ScalaMedia,
        ["points"] = Regola.ElencoDiPunti,
        ["d"] = Regola.DatiDiTracciato,
    };

    /// <summary>Il valore di un attributo, riscritto nel sistema di coordinate della cella.</summary>
    public string? Applica(string nome, string? valore, Action<TestoNominato> avvisa)
    {
        if (valore is null || IsIdentita || !Regole.TryGetValue(nome, out var regola))
        {
            return valore;
        }

        var uniforme = Math.Abs(Sx - Sy) < 1e-9;
        if (!uniforme && regola is Regola.ScalaMedia)
        {
            avvisa(new(
                "imp.scalaNonUniforme",
                "«{0}» under a non-uniform scale: the average of the two factors "
                + "has been used",
                nome));
        }

        return regola switch
        {
            Regola.AscissaAssoluta => Formatta(Numero(valore) * Sx + Tx, valore),
            Regola.OrdinataAssoluta => Formatta(Numero(valore) * Sy + Ty, valore),
            Regola.LarghezzaScalata => Formatta(Numero(valore) * Sx, valore),
            Regola.AltezzaScalata => Formatta(Numero(valore) * Sy, valore),
            Regola.ScalaMedia => Formatta(Numero(valore) * (Sx + Sy) / 2, valore),
            Regola.ElencoDiPunti => Punti(valore),
            Regola.DatiDiTracciato => Tracciato(valore, uniforme, avvisa),
            _ => valore,
        };
    }

    private double? Numero(string valore) =>
        double.TryParse(valore.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;

    private static string? Formatta(double? valore, string originale) =>
        valore is null ? originale : InvariantNumber.Format(Math.Round(valore.Value, 4));

    private string Punti(string valore)
    {
        var numeri = SvgPatternImporter.Numeri(valore);
        var fuori = new StringBuilder();

        for (var i = 0; i + 1 < numeri.Count; i += 2)
        {
            if (fuori.Length > 0)
            {
                fuori.Append(' ');
            }

            fuori.Append(InvariantNumber.Format(Math.Round(numeri[i] * Sx + Tx, 4)))
                 .Append(',')
                 .Append(InvariantNumber.Format(Math.Round(numeri[i + 1] * Sy + Ty, 4)));
        }

        return fuori.ToString();
    }

    // ------------------------------------------------------------------ i tracciati

    /// <summary>Quanti numeri vuole ciascun comando, e come sono disposti.</summary>
    private static int Arita(char comando) => char.ToUpperInvariant(comando) switch
    {
        'M' or 'L' or 'T' => 2,
        'H' or 'V' => 1,
        'C' => 6,
        'S' or 'Q' => 4,
        'A' => 7,
        'Z' => 0,
        _ => -1,
    };

    /// <summary>
    /// I dati di un tracciato, riscritti.
    ///
    /// La regola che conta: i comandi in maiuscolo hanno coordinate assolute e vanno
    /// trasformati per intero, quelli in minuscolo sono spostamenti relativi e vanno solo
    /// scalati — sommare anche la traslazione li sposterebbe a ogni segmento. L'unica
    /// eccezione è il primo comando: un <c>m</c> iniziale, dice la specifica, è assoluto
    /// comunque, e trattarlo come relativo metterebbe l'intero tracciato fuori posto.
    /// </summary>
    private string Tracciato(string d, bool uniforme, Action<TestoNominato> avvisa)
    {
        var fuori = new StringBuilder();
        var posizione = 0;
        var primo = true;
        var avvisato = false;

        while (posizione < d.Length)
        {
            var c = d[posizione];

            if (char.IsWhiteSpace(c) || c == ',')
            {
                posizione++;
                continue;
            }

            if (!char.IsLetter(c))
            {
                // Un numero senza comando davanti: il tracciato è malformato e riscriverlo
                // sarebbe peggio che lasciarlo com'è.
                return d;
            }

            var arita = Arita(c);
            if (arita < 0)
            {
                return d;
            }

            posizione++;
            var numeri = new List<double>();
            while (posizione < d.Length && !char.IsLetter(d[posizione]))
            {
                if (!LeggiNumero(d, ref posizione, out var numero))
                {
                    break;
                }

                numeri.Add(numero);
            }

            if (fuori.Length > 0)
            {
                fuori.Append(' ');
            }

            fuori.Append(c);

            if (arita == 0)
            {
                primo = false;
                continue;
            }

            for (var i = 0; i + arita <= numeri.Count; i += arita)
            {
                // Il primo comando di un tracciato è assoluto anche se scritto in minuscolo,
                // ma solo per la sua prima coppia: quelle successive sono già spostamenti.
                var assoluto = char.IsUpper(c) || (primo && i == 0);
                var pezzo = numeri.GetRange(i, arita);

                if (char.ToUpperInvariant(c) == 'A')
                {
                    if (!uniforme && !avvisato)
                    {
                        avvisa(new(
                            "imp.arcoApprossimato",
                            "an arc under a non-uniform scale: its curvature is approximated"));
                        avvisato = true;
                    }

                    Aggiungi(fuori, pezzo[0] * Sx);
                    Aggiungi(fuori, pezzo[1] * Sy);
                    Aggiungi(fuori, pezzo[2]);
                    Aggiungi(fuori, pezzo[3]);
                    Aggiungi(fuori, pezzo[4]);
                    Aggiungi(fuori, assoluto ? pezzo[5] * Sx + Tx : pezzo[5] * Sx);
                    Aggiungi(fuori, assoluto ? pezzo[6] * Sy + Ty : pezzo[6] * Sy);
                    continue;
                }

                if (char.ToUpperInvariant(c) == 'H')
                {
                    Aggiungi(fuori, assoluto ? pezzo[0] * Sx + Tx : pezzo[0] * Sx);
                    continue;
                }

                if (char.ToUpperInvariant(c) == 'V')
                {
                    Aggiungi(fuori, assoluto ? pezzo[0] * Sy + Ty : pezzo[0] * Sy);
                    continue;
                }

                for (var k = 0; k + 1 < arita; k += 2)
                {
                    Aggiungi(fuori, assoluto ? pezzo[k] * Sx + Tx : pezzo[k] * Sx);
                    Aggiungi(fuori, assoluto ? pezzo[k + 1] * Sy + Ty : pezzo[k + 1] * Sy);
                }
            }

            primo = false;
        }

        return fuori.ToString();
    }

    private static void Aggiungi(StringBuilder fuori, double valore) =>
        fuori.Append(' ').Append(InvariantNumber.Format(Math.Round(valore, 4)));

    /// <summary>
    /// Un numero dei dati di tracciato.
    ///
    /// Non si può usare un lettore generico: qui i separatori sono facoltativi e
    /// «1.5.5» sono due numeri, perché il secondo punto decimale non può appartenere al
    /// primo. Lo stesso vale per il segno, che separa senza spazio: «10-5».
    /// </summary>
    private static bool LeggiNumero(string testo, ref int posizione, out double numero)
    {
        numero = 0;

        while (posizione < testo.Length && (char.IsWhiteSpace(testo[posizione]) || testo[posizione] == ','))
        {
            posizione++;
        }

        var inizio = posizione;
        var punto = false;

        if (posizione < testo.Length && (testo[posizione] == '-' || testo[posizione] == '+'))
        {
            posizione++;
        }

        while (posizione < testo.Length)
        {
            var c = testo[posizione];

            if (char.IsAsciiDigit(c))
            {
                posizione++;
                continue;
            }

            if (c == '.' && !punto)
            {
                punto = true;
                posizione++;
                continue;
            }

            if ((c == 'e' || c == 'E') && posizione + 1 < testo.Length
                && (char.IsAsciiDigit(testo[posizione + 1]) || testo[posizione + 1] is '-' or '+'))
            {
                posizione += 2;
                continue;
            }

            break;
        }

        return posizione > inizio
               && double.TryParse(testo[inizio..posizione], NumberStyles.Float,
                                  CultureInfo.InvariantCulture, out numero);
    }
}
