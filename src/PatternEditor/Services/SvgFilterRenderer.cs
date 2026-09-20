using System.Globalization;
using System.Text;
using PatternEditor.Core.Formatting;
using PatternEditor.Core.Models.Filters;

namespace PatternEditor.Services;

/// <summary>
/// Traduce un <see cref="PatternFilter"/> nel nodo <c>&lt;filter&gt;</c> corrispondente.
///
/// <para>
/// Sta accanto al generatore del pattern e non dentro di esso perché è una traduzione a sé:
/// legge il modello, scrive markup, non sa niente di celle né di ripetizioni. Il generatore
/// principale gli chiede la definizione e decide dove applicarla.
/// </para>
///
/// <para>
/// <b>Che cosa viene scritto e che cosa no.</b> Si scrive un attributo solo quando dice
/// qualcosa: gli attributi lasciati al valore predefinito dalla specifica si omettono. Un
/// documento in cui ogni nodo porta dodici attributi, undici dei quali ripetono il
/// comportamento normale, è un documento in cui non si vede più qual è la regolazione che
/// conta. Le due eccezioni sono dichiarate dove capitano.
/// </para>
/// </summary>
public interface ISvgFilterRenderer
{
    /// <summary>
    /// Il nodo <c>&lt;filter&gt;</c> completo, indentato per stare dentro i <c>&lt;defs&gt;</c>,
    /// oppure <c>null</c> quando il filtro è assente, spento o senza passaggi accesi.
    ///
    /// <para>
    /// Il <c>null</c> non è una comodità: un <c>&lt;filter&gt;</c> <b>senza</b> primitive non
    /// è un filtro che non fa niente, è un filtro che secondo la specifica produce
    /// un'immagine completamente trasparente. Generarlo farebbe sparire il disegno.
    /// </para>
    /// </summary>
    /// <param name="filter">Il filtro da tradurre. Può essere null.</param>
    /// <param name="filterId">L'identificativo del nodo, già univoco nel documento ospite.</param>
    /// <param name="rientro">Di quanti spazi rientrare la prima riga.</param>
    string? RenderFilterDefinition(PatternFilter? filter, string filterId, int rientro = 4);
}

/// <inheritdoc cref="ISvgFilterRenderer" />
public sealed class SvgFilterRenderer : ISvgFilterRenderer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <inheritdoc />
    public string? RenderFilterDefinition(PatternFilter? filter, string filterId, int rientro = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterId);

        if (filter is null || !filter.ProduceEffetto)
        {
            return null;
        }

        var passi = filter.Primitives.Where(p => p.Enabled).ToList();

        // Una primitiva ignota non si sa disegnare: si conserva nel documento salvato ma non
        // si genera. Se dopo averle tolte non resta niente, non c'è nessun filtro da scrivere.
        var generabili = passi.Where(p => p is not UnknownFilterPrimitive).ToList();
        if (generabili.Count == 0)
        {
            return null;
        }

        var spazi = new string(' ', rientro);
        var dentro = new string(' ', rientro + 2);

        var sb = new StringBuilder();
        sb.Append(spazi).Append("<filter id=\"").Append(SvgText.Escape(filterId)).Append('"');
        sb.Append(" x=\"").Append(Percentuale(filter.X)).Append('"');
        sb.Append(" y=\"").Append(Percentuale(filter.Y)).Append('"');
        sb.Append(" width=\"").Append(Percentuale(filter.Width)).Append('"');
        sb.Append(" height=\"").Append(Percentuale(filter.Height)).Append('"');

        // Prima eccezione alla regola degli attributi omessi: questo si scrive sempre. Il
        // valore predefinito della specifica è linearRGB, praticamente nessuno se lo aspetta,
        // e lasciarlo implicito significherebbe che il documento dipende da un default che
        // chi lo legge non ha in mente.
        sb.Append(" color-interpolation-filters=\"")
          .Append(filter.ColorSpace == FilterColorSpace.Srgb ? "sRGB" : "linearRGB")
          .Append("\">\n");

        foreach (var primitiva in generabili)
        {
            sb.Append(Primitiva(primitiva, dentro));
        }

        sb.Append(spazi).Append("</filter>\n");
        return sb.ToString();
    }

    /// <summary>Un singolo nodo <c>fe*</c>, con i soli attributi che dicono qualcosa.</summary>
    private static string Primitiva(FilterPrimitive p, string rientro)
    {
        var attributi = new List<string>();

        // in e result valgono per tutte e vengono prima: si legge da dove arriva e dove va
        // prima di che cosa fa, che è l'ordine in cui si segue una catena.
        Aggiungi(attributi, "in", p.In);

        switch (p)
        {
            case GaussianBlurPrimitive b:
                attributi.Add($"stdDeviation=\"{Coppia(b.StdDeviationX, b.StdDeviationY)}\"");
                if (b.EdgeMode != EdgeMode.None)
                {
                    attributi.Add($"edgeMode=\"{Bordo(b.EdgeMode)}\"");
                }

                break;

            case DropShadowPrimitive o:
                attributi.Add($"dx=\"{N(o.Dx)}\"");
                attributi.Add($"dy=\"{N(o.Dy)}\"");
                attributi.Add($"stdDeviation=\"{N(o.StdDeviation)}\"");
                attributi.Add($"flood-color=\"{SvgText.Escape(o.FloodColor)}\"");
                attributi.Add($"flood-opacity=\"{N(o.FloodOpacity)}\"");
                break;

            case OffsetPrimitive s:
                attributi.Add($"dx=\"{N(s.Dx)}\"");
                attributi.Add($"dy=\"{N(s.Dy)}\"");
                break;

            case ColorMatrixPrimitive c:
                attributi.Add($"type=\"{TipoMatrice(c.Kind)}\"");
                if (c.Kind is ColorMatrixKind.Saturate or ColorMatrixKind.HueRotate)
                {
                    attributi.Add($"values=\"{N(c.Value)}\"");
                }
                else if (c.Kind == ColorMatrixKind.Matrix)
                {
                    attributi.Add($"values=\"{Numeri(c.Matrix)}\"");
                }

                // luminanceToAlpha non prende values: scriverlo sarebbe un attributo che la
                // specifica dichiara ignorato, cioè rumore che sembra una regolazione.
                break;

            case MorphologyPrimitive m:
                attributi.Add($"operator=\"{(m.Operator == MorphologyOperator.Dilate ? "dilate" : "erode")}\"");
                attributi.Add($"radius=\"{Coppia(m.RadiusX, m.RadiusY)}\"");
                break;

            case FloodPrimitive f:
                attributi.Add($"flood-color=\"{SvgText.Escape(f.FloodColor)}\"");
                attributi.Add($"flood-opacity=\"{N(f.FloodOpacity)}\"");
                break;

            case BlendPrimitive b2:
                Aggiungi(attributi, "in2", b2.In2);
                attributi.Add($"mode=\"{ModoFusione(b2.Mode)}\"");
                break;

            case CompositePrimitive k:
                Aggiungi(attributi, "in2", k.In2);
                attributi.Add($"operator=\"{OperatoreComposizione(k.Operator)}\"");
                if (k.Operator == CompositeOperator.Arithmetic)
                {
                    attributi.Add($"k1=\"{N(k.K1)}\"");
                    attributi.Add($"k2=\"{N(k.K2)}\"");
                    attributi.Add($"k3=\"{N(k.K3)}\"");
                    attributi.Add($"k4=\"{N(k.K4)}\"");
                }

                break;

            case TurbulencePrimitive t:
                attributi.Add($"type=\"{(t.Kind == TurbulenceKind.Turbulence ? "turbulence" : "fractalNoise")}\"");
                attributi.Add($"baseFrequency=\"{Coppia(t.BaseFrequencyX, t.BaseFrequencyY)}\"");
                attributi.Add($"numOctaves=\"{t.NumOctaves.ToString(Inv)}\"");
                if (t.Seed != 0)
                {
                    attributi.Add($"seed=\"{N(t.Seed)}\"");
                }

                if (t.StitchTiles)
                {
                    attributi.Add("stitchTiles=\"stitch\"");
                }

                break;

            case DisplacementMapPrimitive d:
                Aggiungi(attributi, "in2", d.In2);
                attributi.Add($"scale=\"{N(d.Scale)}\"");
                attributi.Add($"xChannelSelector=\"{d.XChannelSelector}\"");
                attributi.Add($"yChannelSelector=\"{d.YChannelSelector}\"");
                break;

            case ConvolveMatrixPrimitive v:
                attributi.Add($"order=\"{v.Lato.ToString(Inv)}\"");
                attributi.Add($"kernelMatrix=\"{Numeri(v.KernelMatrix)}\"");

                // Un divisore nullo è illegale nella specifica e significa qui «usa la somma
                // dei coefficienti», che è anche il comportamento predefinito: si omette.
                if (v.Divisor != 0)
                {
                    attributi.Add($"divisor=\"{N(v.Divisor)}\"");
                }

                if (v.Bias != 0)
                {
                    attributi.Add($"bias=\"{N(v.Bias)}\"");
                }

                if (v.EdgeMode != EdgeMode.Duplicate)
                {
                    attributi.Add($"edgeMode=\"{Bordo(v.EdgeMode)}\"");
                }

                if (v.PreserveAlpha)
                {
                    attributi.Add("preserveAlpha=\"true\"");
                }

                break;
        }

        Aggiungi(attributi, "result", p.Result);

        var testa = $"{rientro}<{p.SvgName}{Scritti(attributi)}";

        // Le due primitive con figli si chiudono con un tag di chiusura; tutte le altre sono
        // vuote e si chiudono da sole.
        return p switch
        {
            ComponentTransferPrimitive ct => $"{testa}>\n{Canali(ct, rientro + "  ")}{rientro}</feComponentTransfer>\n",
            MergePrimitive mg => $"{testa}>\n{Livelli(mg, rientro + "  ")}{rientro}</feMerge>\n",
            _ => $"{testa} />\n",
        };
    }

    /// <summary>I quattro nodi <c>feFunc*</c>. Un canale lasciato invariato non si scrive.</summary>
    private static string Canali(ComponentTransferPrimitive ct, string rientro)
    {
        var sb = new StringBuilder();

        foreach (var (nome, funzione) in ct.Canali())
        {
            if (funzione.ELIdentita)
            {
                continue;
            }

            var attributi = new List<string> { $"type=\"{TipoCurva(funzione.Kind)}\"" };

            switch (funzione.Kind)
            {
                case TransferFunctionKind.Linear:
                    attributi.Add($"slope=\"{N(funzione.Slope)}\"");
                    attributi.Add($"intercept=\"{N(funzione.Intercept)}\"");
                    break;

                case TransferFunctionKind.Gamma:
                    attributi.Add($"amplitude=\"{N(funzione.Amplitude)}\"");
                    attributi.Add($"exponent=\"{N(funzione.Exponent)}\"");
                    attributi.Add($"offset=\"{N(funzione.Offset)}\"");
                    break;

                case TransferFunctionKind.Table:
                case TransferFunctionKind.Discrete:
                    attributi.Add($"tableValues=\"{Numeri(funzione.TableValues)}\"");
                    break;
            }

            sb.Append(rientro).Append("<feFunc").Append(nome).Append(Scritti(attributi)).Append(" />\n");
        }

        return sb.ToString();
    }

    /// <summary>I nodi <c>feMergeNode</c>, dal fondo alla cima.</summary>
    private static string Livelli(MergePrimitive merge, string rientro)
    {
        var sb = new StringBuilder();

        foreach (var ingresso in merge.Inputs)
        {
            sb.Append(rientro).Append("<feMergeNode");

            if (!string.IsNullOrWhiteSpace(ingresso))
            {
                sb.Append(" in=\"").Append(SvgText.Escape(ingresso.Trim())).Append('"');
            }

            sb.Append(" />\n");
        }

        return sb.ToString();
    }

    private static void Aggiungi(List<string> attributi, string nome, string? valore)
    {
        if (!string.IsNullOrWhiteSpace(valore))
        {
            attributi.Add($"{nome}=\"{SvgText.Escape(valore.Trim())}\"");
        }
    }

    private static string Scritti(List<string> attributi) =>
        attributi.Count == 0 ? string.Empty : " " + string.Join(' ', attributi);

    /// <summary>
    /// Una misura che la specifica accetta sia singola sia doppia. Scriverne una sola quando
    /// i due valori coincidono non è un'economia: <c>stdDeviation="2"</c> dice «uguale nelle
    /// due direzioni», che è un'informazione, mentre <c>"2 2"</c> costringe a confrontarli.
    /// </summary>
    private static string Coppia(double x, double y) =>
        x == y ? N(x) : $"{N(x)} {N(y)}";

    private static string Numeri(IEnumerable<double> valori) =>
        string.Join(' ', valori.Select(N));

    private static string N(double valore) =>
        Math.Round(valore, 6).ToString(Inv);

    private static string Percentuale(double valore) =>
        Math.Round(valore, 4).ToString(Inv) + "%";

    private static string TipoMatrice(ColorMatrixKind kind) => kind switch
    {
        ColorMatrixKind.Saturate => "saturate",
        ColorMatrixKind.HueRotate => "hueRotate",
        ColorMatrixKind.LuminanceToAlpha => "luminanceToAlpha",
        _ => "matrix",
    };

    private static string TipoCurva(TransferFunctionKind kind) => kind switch
    {
        TransferFunctionKind.Linear => "linear",
        TransferFunctionKind.Gamma => "gamma",
        TransferFunctionKind.Table => "table",
        TransferFunctionKind.Discrete => "discrete",
        _ => "identity",
    };

    private static string Bordo(EdgeMode modo) => modo switch
    {
        EdgeMode.Duplicate => "duplicate",
        EdgeMode.Wrap => "wrap",
        _ => "none",
    };

    /// <summary>
    /// I nomi dei modi di fusione. Non si ricavano dal nome dell'enumerazione perché la
    /// specifica li scrive con il trattino (<c>color-dodge</c>) e una conversione automatica
    /// produrrebbe <c>colorDodge</c>, che i browser ignorano in silenzio.
    /// </summary>
    private static string ModoFusione(BlendMode modo) => modo switch
    {
        BlendMode.Normal => "normal",
        BlendMode.Multiply => "multiply",
        BlendMode.Screen => "screen",
        BlendMode.Overlay => "overlay",
        BlendMode.Darken => "darken",
        BlendMode.Lighten => "lighten",
        BlendMode.ColorDodge => "color-dodge",
        BlendMode.ColorBurn => "color-burn",
        BlendMode.HardLight => "hard-light",
        BlendMode.SoftLight => "soft-light",
        BlendMode.Difference => "difference",
        BlendMode.Exclusion => "exclusion",
        BlendMode.Hue => "hue",
        BlendMode.Saturation => "saturation",
        BlendMode.Color => "color",
        _ => "luminosity",
    };

    private static string OperatoreComposizione(CompositeOperator operatore) => operatore switch
    {
        CompositeOperator.Over => "over",
        CompositeOperator.In => "in",
        CompositeOperator.Out => "out",
        CompositeOperator.Atop => "atop",
        CompositeOperator.Xor => "xor",
        _ => "arithmetic",
    };
}
