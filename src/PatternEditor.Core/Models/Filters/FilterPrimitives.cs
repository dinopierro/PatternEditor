using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using PatternEditor.Core.Localization;

namespace PatternEditor.Core.Models.Filters;

/// <summary>
/// Sfoca. È la primitiva più usata e quella che sconfina di più: una deviazione di 4
/// sporca visibilmente una fascia di una dozzina di pixel tutto attorno.
/// </summary>
public sealed class GaussianBlurPrimitive : FilterPrimitive
{
    public double StdDeviationX { get; set; } = 2;

    public double StdDeviationY { get; set; } = 2;

    /// <summary>Che cosa leggere appena fuori dal bordo. La specifica predefinisce <c>none</c>.</summary>
    public EdgeMode EdgeMode { get; set; } = EdgeMode.None;

    [JsonIgnore]
    public override string SvgName => "feGaussianBlur";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feGaussianBlur", "Blur");

    [JsonIgnore]
    public override TestoNominato Sintesi => StdDeviationX == StdDeviationY
        ? new("filtro.sfocatura.raggio", "radius {0}", Numero.Testo(StdDeviationX))
        : new("filtro.sfocatura.raggioXY", "radius {0} × {1}",
              Numero.Testo(StdDeviationX), Numero.Testo(StdDeviationY));

    [JsonIgnore]
    public override bool Sconfina => true;
}

/// <summary>
/// Ombra portata. È una scorciatoia della specifica per la sequenza
/// sfoca-sposta-colora-rimetti-sotto, e conviene usarla al posto di scriverla a mano.
/// </summary>
public sealed class DropShadowPrimitive : FilterPrimitive
{
    public double Dx { get; set; } = 2;

    public double Dy { get; set; } = 2;

    public double StdDeviation { get; set; } = 2;

    public string FloodColor { get; set; } = "#000000";

    public double FloodOpacity { get; set; } = 0.5;

    [JsonIgnore]
    public override string SvgName => "feDropShadow";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feDropShadow", "Shadow");

    [JsonIgnore]
    public override TestoNominato Sintesi =>
        new("filtro.ombra.sintesi", "{0}, {1} · blurred {2}",
            Numero.Testo(Dx), Numero.Testo(Dy), Numero.Testo(StdDeviation));

    [JsonIgnore]
    public override bool Sconfina => true;
}

/// <summary>Sposta e basta. Da sola non si nota; serve come passaggio di una catena.</summary>
public sealed class OffsetPrimitive : FilterPrimitive
{
    public double Dx { get; set; } = 2;

    public double Dy { get; set; } = 2;

    [JsonIgnore]
    public override string SvgName => "feOffset";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feOffset", "Offset");

    [JsonIgnore]
    public override TestoNominato Sintesi =>
        new("filtro.spostamento.sintesi", "{0}, {1}", Numero.Testo(Dx), Numero.Testo(Dy));

    [JsonIgnore]
    public override bool Sconfina => true;
}

/// <summary>
/// Rimappa i colori con una matrice. È la primitiva del colore: saturazione, tinta e
/// trasparenza-dalla-luminosità sono tre casi particolari della stessa operazione, e la
/// specifica li espone come scorciatoie della forma generale a venti coefficienti.
/// </summary>
public sealed class ColorMatrixPrimitive : FilterPrimitive
{
    /// <summary>La matrice neutra: ogni canale resta se stesso.</summary>
    public static double[] MatriceIdentita() =>
    [
        1, 0, 0, 0, 0,
        0, 1, 0, 0, 0,
        0, 0, 1, 0, 0,
        0, 0, 0, 1, 0,
    ];

    public ColorMatrixKind Kind { get; set; } = ColorMatrixKind.Saturate;

    /// <summary>
    /// Il singolo numero delle forme abbreviate: quanta saturazione per
    /// <see cref="ColorMatrixKind.Saturate"/>, quanti gradi per
    /// <see cref="ColorMatrixKind.HueRotate"/>. Ignorato dalle altre due.
    /// </summary>
    public double Value { get; set; } = 1;

    /// <summary>
    /// I venti coefficienti, quattro righe da cinque. Usati solo da
    /// <see cref="ColorMatrixKind.Matrix"/>, ma conservati sempre: cambiare forma per
    /// provarla e poi tornare indietro non deve cancellare quello che si era scritto.
    /// </summary>
    public List<double> Matrix { get; set; } = [.. MatriceIdentita()];

    [JsonIgnore]
    public override string SvgName => "feColorMatrix";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feColorMatrix", "Colour");

    [JsonIgnore]
    public override TestoNominato Sintesi => Kind switch
    {
        ColorMatrixKind.Saturate when Value == 0 => new("filtro.colore.grigi", "greyscale"),
        ColorMatrixKind.Saturate =>
            new("filtro.colore.saturazione", "saturation {0}", Numero.Percento(Value)),
        ColorMatrixKind.HueRotate =>
            new("filtro.colore.tinta", "hue rotated by {0}°", Numero.Testo(Value)),
        ColorMatrixKind.LuminanceToAlpha =>
            new("filtro.colore.luminanza", "luminance → transparency"),
        _ => new("filtro.colore.matrice", "hand-written matrix"),
    };

    protected override void SdoppiaRiferimenti(FilterPrimitive copia) =>
        ((ColorMatrixPrimitive)copia).Matrix = [.. Matrix];
}

/// <summary>
/// Rimappa ciascun canale per conto proprio. È il posto dove stanno luminosità, contrasto,
/// gamma, inversione e posterizzazione: tutte curve applicate a R, G, B e A separatamente.
/// </summary>
public sealed class ComponentTransferPrimitive : FilterPrimitive
{
    public TransferFunction R { get; set; } = new();

    public TransferFunction G { get; set; } = new();

    public TransferFunction B { get; set; } = new();

    public TransferFunction A { get; set; } = new();

    [JsonIgnore]
    public override string SvgName => "feComponentTransfer";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feComponentTransfer", "Levels");

    [JsonIgnore]
    public override TestoNominato Sintesi
    {
        get
        {
            var attivi = Canali().Where(c => c.Funzione.Kind != TransferFunctionKind.Identity).ToList();

            if (attivi.Count == 0)
            {
                return new("filtro.livelli.nessuno", "no channel changed");
            }

            // I tre canali di colore regolati allo stesso modo sono il caso normale — è così
            // che si scrivono luminosità e contrasto — e dire «R, G, B» invece di «colore»
            // costringerebbe a riconoscerlo ogni volta.
            if (attivi.Count == 3 && attivi.All(c => c.Nome != "A") && TuttiUguali(R, G, B))
            {
                return new("filtro.livelli.colore", "colour · {0}", R.Sintesi);
            }

            // Quanti canali siano, e quali, si sa solo adesso: la frase non può stare intera
            // in un catalogo, e allora si traducono le parti.
            return TestoNominato.Unisci(" · ",
                [.. attivi.Select(c => new TestoNominato(
                    "filtro.livelli.canale", "{0}: {1}", c.Nome, c.Funzione.Sintesi))]);
        }
    }

    /// <summary>I quattro canali con il loro nome, per scorrerli senza ripetere quattro volte lo stesso codice.</summary>
    public IEnumerable<(string Nome, TransferFunction Funzione)> Canali()
    {
        yield return ("R", R);
        yield return ("G", G);
        yield return ("B", B);
        yield return ("A", A);
    }

    private static bool TuttiUguali(TransferFunction a, TransferFunction b, TransferFunction c) =>
        a.Riassunto == b.Riassunto && b.Riassunto == c.Riassunto;

    protected override void SdoppiaRiferimenti(FilterPrimitive copia)
    {
        var altra = (ComponentTransferPrimitive)copia;
        altra.R = R.Copia();
        altra.G = G.Copia();
        altra.B = B.Copia();
        altra.A = A.Copia();
    }
}

/// <summary>Ispessisce o assottiglia. Su un retino di linee sottili è il modo per dargli peso.</summary>
public sealed class MorphologyPrimitive : FilterPrimitive
{
    public MorphologyOperator Operator { get; set; } = MorphologyOperator.Dilate;

    public double RadiusX { get; set; } = 1;

    public double RadiusY { get; set; } = 1;

    [JsonIgnore]
    public override string SvgName => "feMorphology";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feMorphology", "Thickness");

    [JsonIgnore]
    public override TestoNominato Sintesi
    {
        get
        {
            var ispessisce = Operator == MorphologyOperator.Dilate;

            if (RadiusX == RadiusY)
            {
                return ispessisce
                    ? new("filtro.spessore.ispessisce", "thickens by {0}", Numero.Testo(RadiusX))
                    : new("filtro.spessore.assottiglia", "thins by {0}", Numero.Testo(RadiusX));
            }

            return ispessisce
                ? new("filtro.spessore.ispessisceXY", "thickens by {0} × {1}",
                      Numero.Testo(RadiusX), Numero.Testo(RadiusY))
                : new("filtro.spessore.assottigliaXY", "thins by {0} × {1}",
                      Numero.Testo(RadiusX), Numero.Testo(RadiusY));
        }
    }

    [JsonIgnore]
    public override bool Sconfina => true;
}

/// <summary>
/// Riempie l'area del filtro di un colore pieno. Da sola coprirebbe tutto: serve sempre
/// insieme a una composizione o a una fusione che decida dove quel colore debba restare.
/// </summary>
public sealed class FloodPrimitive : FilterPrimitive
{
    public string FloodColor { get; set; } = "#000000";

    public double FloodOpacity { get; set; } = 1;

    [JsonIgnore]
    public override string SvgName => "feFlood";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feFlood", "Solid colour");

    [JsonIgnore]
    public override TestoNominato Sintesi => FloodOpacity >= 1
        ? TestoNominato.Fisso(FloodColor)
        : new("filtro.tintaPiena.opacita", "{0} at {1}", FloodColor, Numero.Percento(FloodOpacity));

    [JsonIgnore]
    public override bool Genera => true;
}

/// <summary>Fonde due immagini con uno dei modi che si trovano anche nei programmi di fotoritocco.</summary>
public sealed class BlendPrimitive : FilterPrimitive
{
    /// <summary>La seconda immagine: quella <b>sotto</b>, nella terminologia della specifica.</summary>
    public string? In2 { get; set; }

    public BlendMode Mode { get; set; } = BlendMode.Multiply;

    [JsonIgnore]
    public override string SvgName => "feBlend";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feBlend", "Blend");

    [JsonIgnore]
    public override TestoNominato Sintesi => Nome(Mode);

    [JsonIgnore]
    public override bool HaSecondoIngresso => true;

    /// <summary>Come si chiama un modo di fusione: i nomi del fotoritocco.</summary>
    public static TestoNominato Nome(BlendMode modo) => modo switch
    {
        BlendMode.Normal => new("filtro.fusione.normale", "normal"),
        BlendMode.Multiply => new("filtro.fusione.moltiplica", "multiply"),
        BlendMode.Screen => new("filtro.fusione.scolora", "screen"),
        BlendMode.Overlay => new("filtro.fusione.sovrapponi", "overlay"),
        BlendMode.Darken => new("filtro.fusione.scurisci", "darken"),
        BlendMode.Lighten => new("filtro.fusione.schiarisci", "lighten"),
        BlendMode.ColorDodge => new("filtro.fusione.scherma", "colour dodge"),
        BlendMode.ColorBurn => new("filtro.fusione.brucia", "colour burn"),
        BlendMode.HardLight => new("filtro.fusione.luceIntensa", "hard light"),
        BlendMode.SoftLight => new("filtro.fusione.luceSoffusa", "soft light"),
        BlendMode.Difference => new("filtro.fusione.differenza", "difference"),
        BlendMode.Exclusion => new("filtro.fusione.esclusione", "exclusion"),
        BlendMode.Hue => new("filtro.fusione.tinta", "hue"),
        BlendMode.Saturation => new("filtro.fusione.saturazione", "saturation"),
        BlendMode.Color => new("filtro.fusione.colore", "colour"),
        _ => new("filtro.fusione.luminosita", "luminosity"),
    };
}

/// <summary>
/// Combina due immagini secondo l'algebra di Porter-Duff, più la forma aritmetica che
/// permette di dosare i due contributi con quattro coefficienti.
/// </summary>
public sealed class CompositePrimitive : FilterPrimitive
{
    public string? In2 { get; set; }

    public CompositeOperator Operator { get; set; } = CompositeOperator.Over;

    public double K1 { get; set; }

    public double K2 { get; set; } = 1;

    public double K3 { get; set; } = 1;

    public double K4 { get; set; }

    [JsonIgnore]
    public override string SvgName => "feComposite";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feComposite", "Composite");

    [JsonIgnore]
    public override TestoNominato Sintesi => Operator == CompositeOperator.Arithmetic
        ? new("filtro.composizione.aritmetica", "arithmetic {0} / {1} / {2} / {3}",
              Numero.Testo(K1), Numero.Testo(K2), Numero.Testo(K3), Numero.Testo(K4))
        : Nome(Operator);

    [JsonIgnore]
    public override bool HaSecondoIngresso => true;

    /// <summary>Come si chiama un modo di composizione.</summary>
    public static TestoNominato Nome(CompositeOperator operatore) => operatore switch
    {
        CompositeOperator.Over => new("filtro.composizione.sopra", "over"),
        CompositeOperator.In => new("filtro.composizione.dentro", "in"),
        CompositeOperator.Out => new("filtro.composizione.fuori", "out"),
        CompositeOperator.Atop => new("filtro.composizione.appoggiata", "atop"),
        CompositeOperator.Xor => new("filtro.composizione.esclusiva", "xor"),
        _ => new("filtro.composizione.aritmeticaNome", "arithmetic"),
    };
}

/// <summary>
/// Genera rumore. Non è un effetto ma una materia prima: da solo riempie l'area di nuvole
/// colorate, e diventa utile quando lo si dà in pasto a una distorsione o a una fusione.
/// </summary>
public sealed class TurbulencePrimitive : FilterPrimitive
{
    public TurbulenceKind Kind { get; set; } = TurbulenceKind.Turbulence;

    public double BaseFrequencyX { get; set; } = 0.05;

    public double BaseFrequencyY { get; set; } = 0.05;

    public int NumOctaves { get; set; } = 2;

    public double Seed { get; set; }

    /// <summary>
    /// Fa combaciare il rumore ai bordi della tessera.
    ///
    /// <para>
    /// Su un pattern è più importante che altrove: senza, la venatura cambia da tessera a
    /// tessera e il salto si vede lungo le giunzioni, che è precisamente il difetto che un
    /// retino non deve avere.
    /// </para>
    /// </summary>
    public bool StitchTiles { get; set; } = true;

    [JsonIgnore]
    public override string SvgName => "feTurbulence";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feTurbulence", "Noise");

    [JsonIgnore]
    public override TestoNominato Sintesi =>
        new("filtro.rumore.sintesi", "{0} · frequency {1}", Nome(Kind), Numero.Testo(BaseFrequencyX));

    /// <summary>Le due forme del rumore: venature di legno, oppure nuvole.</summary>
    public static TestoNominato Nome(TurbulenceKind tipo) =>
        tipo == TurbulenceKind.Turbulence
            ? new("filtro.rumore.venature", "veins")
            : new("filtro.rumore.nuvole", "clouds");

    [JsonIgnore]
    public override bool Genera => true;
}

/// <summary>
/// Sposta ogni pixel di una quantità letta da una seconda immagine. Con del rumore come
/// seconda immagine è il modo con cui si ottengono bordi irregolari e disegni fatti a mano.
/// </summary>
public sealed class DisplacementMapPrimitive : FilterPrimitive
{
    /// <summary>L'immagine che <b>dice di quanto</b> spostare: tipicamente il rumore.</summary>
    public string? In2 { get; set; }

    public double Scale { get; set; } = 6;

    public ColorChannel XChannelSelector { get; set; } = ColorChannel.R;

    public ColorChannel YChannelSelector { get; set; } = ColorChannel.G;

    [JsonIgnore]
    public override string SvgName => "feDisplacementMap";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feDisplacementMap", "Displacement");

    [JsonIgnore]
    public override TestoNominato Sintesi =>
        new("filtro.distorsione.sintesi", "amplitude {0}", Numero.Testo(Scale));

    [JsonIgnore]
    public override bool HaSecondoIngresso => true;

    [JsonIgnore]
    public override bool Sconfina => true;
}

/// <summary>
/// Sovrappone più risultati, il primo in fondo. È il modo con cui si rimette il disegno
/// originale sopra un'ombra che gli è stata costruita sotto.
/// </summary>
public sealed class MergePrimitive : FilterPrimitive
{
    /// <summary>
    /// I nomi da sovrapporre, dal fondo alla cima. Un nome vuoto vale come «quello che esce
    /// dal passaggio precedente», che è il comportamento della specifica per un
    /// <c>feMergeNode</c> senza <c>in</c>.
    /// </summary>
    public List<string> Inputs { get; set; } = ["", "SourceGraphic"];

    [JsonIgnore]
    public override string SvgName => "feMerge";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feMerge", "Stack");

    [JsonIgnore]
    public override TestoNominato Sintesi => Inputs.Count == 0
        ? new("filtro.sovrapposizione.vuota", "no layer")
        : TestoNominato.Unisci(" + ",
            [.. Inputs.Select(i => string.IsNullOrWhiteSpace(i)
                ? new TestoNominato("filtro.precedente", "(previous)")
                : TestoNominato.Fisso(i))]);

    protected override void SdoppiaRiferimenti(FilterPrimitive copia) =>
        ((MergePrimitive)copia).Inputs = [.. Inputs];
}

/// <summary>
/// Applica un nucleo di convoluzione: ogni pixel diventa una media pesata di quelli che ha
/// intorno. È la primitiva di nitidezza, rilievo e contorni.
/// </summary>
public sealed class ConvolveMatrixPrimitive : FilterPrimitive
{
    /// <summary>Il nucleo neutro tre per tre: ogni pixel resta se stesso.</summary>
    public static double[] NucleoIdentita() =>
    [
        0, 0, 0,
        0, 1, 0,
        0, 0, 0,
    ];

    public ConvolveOrder Order { get; set; } = ConvolveOrder.Tre;

    public List<double> KernelMatrix { get; set; } = [.. NucleoIdentita()];

    /// <summary>
    /// Il divisore. Zero o assente significa «la somma dei coefficienti», che è la scelta
    /// giusta quasi sempre: tiene la luminosità dov'era.
    /// </summary>
    public double Divisor { get; set; }

    public double Bias { get; set; }

    public EdgeMode EdgeMode { get; set; } = EdgeMode.Duplicate;

    /// <summary>
    /// Se vero la trasparenza non partecipa al calcolo. Serve quando si vuole agire sui
    /// colori senza che i bordi trasparenti si mangino il risultato.
    /// </summary>
    public bool PreserveAlpha { get; set; } = true;

    /// <summary>Quanti coefficienti servono per l'ordine scelto.</summary>
    [JsonIgnore]
    public int Lato => Order == ConvolveOrder.Tre ? 3 : 5;

    [JsonIgnore]
    public override string SvgName => "feConvolveMatrix";

    [JsonIgnore]
    public override TestoNominato Titolo => new("filtro.feConvolveMatrix", "Convolution");

    [JsonIgnore]
    public override TestoNominato Sintesi =>
        new("filtro.convoluzione.nucleo", "kernel {0} × {1}", Lato, Lato);

    [JsonIgnore]
    public override bool Sconfina => true;

    protected override void SdoppiaRiferimenti(FilterPrimitive copia) =>
        ((ConvolveMatrixPrimitive)copia).KernelMatrix = [.. KernelMatrix];
}

/// <summary>
/// Una primitiva scritta da una versione che qui non si conosce.
///
/// <para>
/// Vale la stessa regola degli elementi vettoriali di tipo ignoto: non si disegna — nessuno
/// saprebbe come — ma non si butta. Resta nel documento esattamente com'era e viene riscritta
/// identica al salvataggio, così che aprire un pattern su un'installazione più vecchia e
/// risalvarlo non lo impoverisca in silenzio.
/// </para>
/// </summary>
public sealed class UnknownFilterPrimitive : FilterPrimitive
{
    public UnknownFilterPrimitive(string tipo, JsonElement jsonOriginale)
    {
        Tipo = tipo;
        JsonOriginale = jsonOriginale;
    }

    public string Tipo { get; }

    public JsonElement JsonOriginale { get; }

    [JsonIgnore]
    public override string SvgName => Tipo;

    [JsonIgnore]
    public override TestoNominato Titolo => TestoNominato.Fisso(Tipo);

    [JsonIgnore]
    public override TestoNominato Sintesi =>
        new("filtro.sconosciuta", "unrecognised step, kept as it was");
}

/// <summary>
/// Formattazione dei numeri per i riassunti. Cultura invariante come ovunque nel progetto, e
/// senza gli zeri finali che un <c>ToString()</c> si porta dietro.
/// </summary>
internal static class Numero
{
    public static string Testo(double valore) =>
        Math.Round(valore, 3).ToString(CultureInfo.InvariantCulture);

    public static string Percento(double frazione) =>
        Math.Round(frazione * 100).ToString(CultureInfo.InvariantCulture) + "%";
}
