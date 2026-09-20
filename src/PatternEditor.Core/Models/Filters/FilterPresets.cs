namespace PatternEditor.Core.Models.Filters;

/// <summary>
/// Gli effetti già pronti.
///
/// <para>
/// Le primitive della specifica sono operazioni elementari, e quasi nessun effetto
/// riconoscibile corrisponde a una sola di esse: una tinta unica sono tre passaggi, un tratto
/// irregolare sono due, un'inversione è una curva scritta in un modo che bisogna conoscere.
/// Chi vuole «tutto in grigio» non deve dover sapere che si ottiene con una
/// <c>feColorMatrix</c> di tipo <c>saturate</c> a zero.
/// </para>
///
/// <para>
/// Un preset non è una modalità: <b>scrive</b> dei passaggi normali nell'elenco, e da quel
/// momento si aprono e si modificano come tutti gli altri. È il punto di partenza di una
/// regolazione, non una scatola chiusa.
/// </para>
/// </summary>
public static class FilterPresets
{
    /// <summary>
    /// Un effetto pronto: a quale famiglia appartiene, come si chiama, che cosa fa, e i
    /// passaggi che scrive.
    /// </summary>
    /// <param name="Chiave">
    /// Il nome tecnico dell'effetto: stabile, indipendente dalla lingua, ed è con questo che
    /// si cerca la traduzione. Senza, l'unica cosa con cui cercarla sarebbe il nome italiano
    /// — e il giorno in cui lo si ritocca sparirebbero tutte le traduzioni insieme.
    /// </param>
    /// <param name="Gruppo">La famiglia sotto cui compare nel pannello.</param>
    /// <param name="Nome">Il nome mostrato, in inglese: è il ripiego quando manca il catalogo.</param>
    /// <param name="Descrizione">Che cosa succede, in una riga.</param>
    /// <param name="Passaggi">Fabbrica le primitive, ogni volta nuove.</param>
    public sealed record Preset(
        string Chiave,
        string Gruppo,
        string Nome,
        string Descrizione,
        Func<IEnumerable<FilterPrimitive>> Passaggi);

    /// <summary>Le quattro famiglie, nell'ordine in cui vanno presentate.</summary>
    public const string Colore = "Colour";

    /// <inheritdoc cref="Colore" />
    public const string Toni = "Tones";

    /// <inheritdoc cref="Colore" />
    public const string Tratto = "Stroke";

    /// <inheritdoc cref="Colore" />
    public const string Superficie = "Surface";

    /// <summary>I coefficienti del viraggio seppia, quelli in uso da vent'anni nei fogli di stile.</summary>
    private static readonly double[] Seppia =
    [
        0.393, 0.769, 0.189, 0, 0,
        0.349, 0.686, 0.168, 0, 0,
        0.272, 0.534, 0.131, 0, 0,
        0,     0,     0,     1, 0,
    ];

    public static IReadOnlyList<Preset> Tutti { get; } =
    [
        // ---------------------------------------------------------------- colore ------
        new("grigi", Colore, "Greyscale", "Takes the colour away and leaves the tones.",
            () => [new ColorMatrixPrimitive { Kind = ColorMatrixKind.Saturate, Value = 0 }]),

        new("seppia", Colore, "Sepia", "Turns everything brown, like an old print.",
            () => [new ColorMatrixPrimitive { Kind = ColorMatrixKind.Matrix, Matrix = [.. Seppia] }]),

        new("invertiti", Colore, "Inverted colours", "The negative: light becomes dark.",
            () => [Livelli(TransferFunctionKind.Table, f => f.TableValues = [1, 0])]),

        new("caldo", Colore, "Warm cast", "Shifts everything towards red: terracotta, earth, wood.",
            () => [Canali(TransferFunctionKind.Linear,
                r => { r.Slope = 1.10; r.Intercept = 0.015; },
                g => g.Slope = 1.00,
                b => { b.Slope = 0.86; b.Intercept = -0.015; })]),

        new("freddo", Colore, "Cool cast", "Shifts everything towards blue: concrete, metal, glass.",
            () => [Canali(TransferFunctionKind.Linear,
                r => { r.Slope = 0.88; r.Intercept = -0.01; },
                g => g.Slope = 0.98,
                b => { b.Slope = 1.12; b.Intercept = 0.015; })]),

        new("unica", Colore, "Single hue", "Repaints everything in one colour, keeping the tones.",
            () =>
            [
                // La luminosità diventa trasparenza: da qui in poi il disegno è una maschera,
                // e il colore originale non c'è più.
                new ColorMatrixPrimitive { Kind = ColorMatrixKind.LuminanceToAlpha, Result = "toni" },

                // Il colore pieno, ritagliato su quella maschera. Senza il ritaglio coprirebbe
                // l'intera area del filtro, disegno compreso.
                new FloodPrimitive { FloodColor = "#3a6ea5", Result = "tinta" },
                new CompositePrimitive { In = "tinta", In2 = "toni", Operator = CompositeOperator.In },
            ]),

        // ------------------------------------------------------------------ toni ------
        new("piuContrasto", Toni, "More contrast", "Pushes the lights away from the darks.",
            () => [Livelli(TransferFunctionKind.Linear, f => { f.Slope = 1.4; f.Intercept = -0.2; })]),

        new("menoContrasto", Toni, "Less contrast", "Brings the tones together: the drawing settles into the background.",
            () => [Livelli(TransferFunctionKind.Linear, f => { f.Slope = 0.65; f.Intercept = 0.18; })]),

        new("schiarisci", Toni, "Lighten", "Raises every tone without touching the colour.",
            () => [Livelli(TransferFunctionKind.Linear, f => f.Intercept = 0.18)]),

        new("scurisci", Toni, "Darken", "Lowers every tone.",
            () => [Livelli(TransferFunctionKind.Linear, f => f.Intercept = -0.18)]),

        new("sbiadito", Toni, "Faded", "Lighter and flatter all round: a hatch meant to sit under something else.",
            () => [Livelli(TransferFunctionKind.Linear, f => { f.Slope = 0.5; f.Intercept = 0.42; })]),

        new("biancoNero", Toni, "Hard black and white", "Two tones only, no greys. It survives the photocopier and black-and-white printing.",
            () =>
            [
                new ColorMatrixPrimitive { Kind = ColorMatrixKind.Saturate, Value = 0 },

                // Due soli gradini: sotto metà nero, sopra bianco. È la soglia, scritta come la
                // scrive la specifica.
                Livelli(TransferFunctionKind.Discrete, f => f.TableValues = [0, 1]),
            ]),

        new("pochiToni", Toni, "Few tones", "Cuts each channel down to five steps: the look of screen printing.",
            () => [Livelli(TransferFunctionKind.Discrete, f => f.TableValues = [0, 0.25, 0.5, 0.75, 1])]),

        // ---------------------------------------------------------------- tratto ------
        new("marcato", Tratto, "Bold stroke", "Thickens the lines: a thin hatch stops disappearing in print.",
            () => [new MorphologyPrimitive { Operator = MorphologyOperator.Dilate, RadiusX = 0.5, RadiusY = 0.5 }]),

        new("sottile", Tratto, "Thin stroke", "Thins the lines: a hatch that was too dense can breathe again.",
            () => [new MorphologyPrimitive { Operator = MorphologyOperator.Erode, RadiusX = 0.4, RadiusY = 0.4 }]),

        new("nitidezza", Tratto, "Sharpen", "Accentuates the edges. Go gently: past a point it starts to halo.",
            () =>
            [
                new ConvolveMatrixPrimitive
                {
                    Order = ConvolveOrder.Tre,
                    KernelMatrix =
                    [
                         0, -1,  0,
                        -1,  5, -1,
                         0, -1,  0,
                    ],
                    PreserveAlpha = true,
                },
            ]),

        new("contorni", Tratto, "Outlines", "Keeps only the edges and drops the fills: the drawing becomes an outline.",
            () =>
            [
                // I coefficienti sommano a zero: resta acceso solo dove il colore cambia,
                // cioè sui contorni. La specifica, davanti a una somma nulla, usa uno come
                // divisore — ed è il motivo per cui il campo resta vuoto.
                new ConvolveMatrixPrimitive
                {
                    Order = ConvolveOrder.Tre,
                    KernelMatrix =
                    [
                        -1, -1, -1,
                        -1,  8, -1,
                        -1, -1, -1,
                    ],
                    PreserveAlpha = true,
                },
            ]),

        new("rilievo", Tratto, "Emboss", "Lights from the left and shades to the right: the motif looks engraved.",
            () =>
            [
                // I coefficienti sommano a uno: la luminosità media resta dov'era, e il rilievo
                // nasce dalla differenza fra un lato e l'altro di ogni bordo.
                new ConvolveMatrixPrimitive
                {
                    Order = ConvolveOrder.Tre,
                    KernelMatrix =
                    [
                        -2, -1, 0,
                        -1,  1, 1,
                         0,  1, 2,
                    ],
                    PreserveAlpha = true,
                },
            ]),

        new("ombraMorbida", Tratto, "Soft shadow", "Lifts the motif off the background with the faintest shadow.",
            () => [new DropShadowPrimitive { Dx = 1, Dy = 1.5, StdDeviation = 1.5, FloodOpacity = 0.35 }]),

        new("alone", Tratto, "Glow", "A coloured glow around the shapes, the drawing on top untouched.",
            () =>
            [
                // Si lavora sulla sola sagoma: SourceAlpha è il disegno senza i suoi colori,
                // e l'alone deve avere il colore che scegliamo, non quello che c'era.
                new MorphologyPrimitive
                {
                    In = "SourceAlpha",
                    Operator = MorphologyOperator.Dilate,
                    RadiusX = 1,
                    RadiusY = 1,
                    Result = "gonfia",
                },
                new GaussianBlurPrimitive { In = "gonfia", StdDeviationX = 2.5, StdDeviationY = 2.5, Result = "sfumata" },

                new FloodPrimitive { FloodColor = "#ffcc55", FloodOpacity = 0.85, Result = "colore" },
                new CompositePrimitive { In = "colore", In2 = "sfumata", Operator = CompositeOperator.In, Result = "alone" },

                // L'alone sotto, il disegno originale sopra: senza questo passaggio resterebbe
                // solo il bagliore, e la trama sarebbe sparita.
                new MergePrimitive { Inputs = ["alone", "SourceGraphic"] },
            ]),

        // ------------------------------------------------------------ superficie ------
        new("sfocato", Superficie, "Blurred", "Softens everything. Useful for a background that must not be read.",
            () => [new GaussianBlurPrimitive { StdDeviationX = 1.5, StdDeviationY = 1.5 }]),

        new("aMano", Superficie, "Hand-drawn", "Breaks the edges up with noise: the lines stop looking machine-drawn.",
            () =>
            [
                new TurbulencePrimitive
                {
                    Kind = TurbulenceKind.Turbulence,
                    BaseFrequencyX = 0.04,
                    BaseFrequencyY = 0.04,
                    NumOctaves = 3,
                    StitchTiles = true,
                    Result = "rumore",
                },
                new DisplacementMapPrimitive { In = "SourceGraphic", In2 = "rumore", Scale = 3 },
            ]),

        new("smerigliato", Superficie, "Frosted glass", "Like looking through ribbed glass: it ripples and softens.",
            () =>
            [
                new TurbulencePrimitive
                {
                    Kind = TurbulenceKind.FractalNoise,
                    BaseFrequencyX = 0.12,
                    BaseFrequencyY = 0.12,
                    NumOctaves = 2,
                    StitchTiles = true,
                    Result = "vetro",
                },
                new DisplacementMapPrimitive { In = "SourceGraphic", In2 = "vetro", Scale = 8, Result = "increspato" },
                new GaussianBlurPrimitive { In = "increspato", StdDeviationX = 0.6, StdDeviationY = 0.6 },
            ]),

        new("invecchiata", Superficie, "Aged paper", "Brown tones and an uneven grain: four steps, every one of them editable.",
            () =>
            [
                // Il disegno virato, messo da parte con un nome. Senza il nome andrebbe
                // perduto: il rumore qui sotto non consuma niente ma PRODUCE, e diventa
                // «il risultato precedente» per chi viene dopo.
                new ColorMatrixPrimitive
                {
                    Kind = ColorMatrixKind.Matrix,
                    Matrix = [.. Seppia],
                    Result = "carta",
                },

                new TurbulencePrimitive
                {
                    Kind = TurbulenceKind.FractalNoise,
                    BaseFrequencyX = 0.7,
                    BaseFrequencyY = 0.7,
                    NumOctaves = 3,
                    StitchTiles = true,
                    Result = "grezzo",
                },

                // Il rumore grezzo è colorato e va da nero a bianco: moltiplicato così com'è
                // coprirebbe il disegno di coriandoli. Qui diventa grigio e si comprime fra
                // il 75% e il 100%, cioè una macchiatura appena percettibile, e opaco —
                // altrimenti la trasparenza del rumore bucherebbe la fusione.
                new ColorMatrixPrimitive
                {
                    In = "grezzo",
                    Kind = ColorMatrixKind.Matrix,
                    Matrix =
                    [
                        0.08, 0.08, 0.08, 0, 0.75,
                        0.08, 0.08, 0.08, 0, 0.75,
                        0.08, 0.08, 0.08, 0, 0.75,
                        0,    0,    0,    0, 1,
                    ],
                    Result = "grana",
                },

                new BlendPrimitive { In = "carta", In2 = "grana", Mode = BlendMode.Multiply },
            ]),
    ];

    /// <summary>
    /// Gli effetti raccolti per famiglia, nell'ordine in cui compaiono nell'elenco.
    ///
    /// <para>
    /// Ventiquattro pastiglie di fila sono un muro: raggruppate diventano quattro elenchi
    /// corti, e chi cerca «qualcosa che scurisca» sa dove guardare senza leggerle tutte.
    /// </para>
    /// </summary>
    public static IReadOnlyList<(string Nome, IReadOnlyList<Preset> Effetti)> Gruppi { get; } =
    [
        .. Tutti.GroupBy(p => p.Gruppo)
                .Select(g => (g.Key, (IReadOnlyList<Preset>)[.. g])),
    ];

    /// <summary>
    /// Una <c>feComponentTransfer</c> che regola i tre canali di colore allo stesso modo e
    /// lascia stare la trasparenza.
    ///
    /// <para>
    /// Toccare anche il canale alfa sembrerebbe più coerente e non lo è: alzare la luminosità
    /// dell'alfa rende opaco ciò che era trasparente, e il primo effetto visibile sarebbe che
    /// lo sfondo della cella si riempie di un rettangolo colorato.
    /// </para>
    /// </summary>
    private static ComponentTransferPrimitive Livelli(TransferFunctionKind tipo, Action<TransferFunction> regola)
    {
        var primitiva = new ComponentTransferPrimitive();

        foreach (var (nome, funzione) in primitiva.Canali())
        {
            if (nome == "A")
            {
                continue;
            }

            funzione.Kind = tipo;
            regola(funzione);
        }

        return primitiva;
    }

    /// <summary>
    /// Come <see cref="Livelli"/>, ma con i tre canali regolati <b>uno per uno</b>: serve ai
    /// viraggi, dove il rosso e il blu devono andare in direzioni opposte.
    /// </summary>
    private static ComponentTransferPrimitive Canali(
        TransferFunctionKind tipo,
        Action<TransferFunction> rosso,
        Action<TransferFunction> verde,
        Action<TransferFunction> blu)
    {
        var primitiva = new ComponentTransferPrimitive();

        primitiva.R.Kind = tipo;
        primitiva.G.Kind = tipo;
        primitiva.B.Kind = tipo;

        rosso(primitiva.R);
        verde(primitiva.G);
        blu(primitiva.B);

        return primitiva;
    }
}
