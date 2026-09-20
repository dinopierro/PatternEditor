namespace PatternEditor.Core.Models.Filters;

/// <summary>
/// Il catalogo delle primitive che questa installazione sa trattare.
///
/// <para>
/// Esiste per un motivo solo: avere <b>un</b> posto in cui il nome SVG di un passaggio e la
/// classe che lo rappresenta si incontrano. Serializzatore, menu di inserimento e messaggi di
/// errore leggono da qui, e il giorno in cui si aggiunge una primitiva si tocca questa riga e
/// null'altro — invece di scoprire, un mese dopo, che si può inserirla ma non rileggerla.
/// </para>
/// </summary>
public static class FilterPrimitiveTypes
{
    /// <summary>
    /// Che cosa si può inserire, nell'ordine in cui conviene presentarlo: prima le tre che
    /// bastano da sole a cambiare l'aspetto di un disegno, poi quelle che servono a comporre.
    /// </summary>
    public static IReadOnlyList<VoceCatalogo> Catalogo { get; } =
    [
        new("feColorMatrix", "Colour", "Saturation, hue, greyscale, or the twenty coefficients written by hand.",
            () => new ColorMatrixPrimitive()),

        new("feComponentTransfer", "Levels", "Brightness, contrast, gamma, inversion: one curve per channel.",
            () => new ComponentTransferPrimitive()),

        new("feGaussianBlur", "Blur", "Softens. It is the step used most often, and the one that spills furthest.",
            () => new GaussianBlurPrimitive()),

        new("feDropShadow", "Shadow", "A drop shadow, with direction, softness and colour.",
            () => new DropShadowPrimitive()),

        new("feMorphology", "Thickness", "Thickens or thins the strokes. On a fine hatch it is the way to give it weight.",
            () => new MorphologyPrimitive()),

        new("feOffset", "Offset", "Moves, and nothing else. It earns its place inside a chain, not on its own.",
            () => new OffsetPrimitive()),

        new("feFlood", "Solid colour", "Fills with a colour. On its own it covers everything: it has to be composed with something.",
            () => new FloodPrimitive()),

        new("feTurbulence", "Noise", "Generates veins or clouds. It is the raw material for displacement.",
            () => new TurbulencePrimitive()),

        new("feDisplacementMap", "Displacement", "Moves the pixels following a second image: with noise it gives the hand-drawn stroke.",
            () => new DisplacementMapPrimitive()),

        new("feConvolveMatrix", "Convolution", "Sharpening, embossing, outlines: every pixel becomes a weighted average of its neighbours.",
            () => new ConvolveMatrixPrimitive()),

        new("feBlend", "Blend", "Blends two images with the modes of photo editing: multiply, screen, overlay…",
            () => new BlendPrimitive()),

        new("feComposite", "Composite", "Combines two images: over, in, out, or metered one by one.",
            () => new CompositePrimitive()),

        new("feMerge", "Stack", "Stacks several results, the first at the bottom. It is how the drawing goes back on top of its shadow.",
            () => new MergePrimitive()),
    ];

    private static readonly Dictionary<string, VoceCatalogo> PerNome =
        Catalogo.ToDictionary(v => v.SvgName, StringComparer.Ordinal);

    /// <summary>Vero se questo nome SVG corrisponde a una primitiva che sappiamo trattare.</summary>
    public static bool Conosciuta(string? svgName) =>
        svgName is not null && PerNome.ContainsKey(svgName);

    /// <summary>Il tipo CLR corrispondente a un nome SVG, o null se è una primitiva ignota.</summary>
    public static Type? TipoDi(string? svgName) =>
        svgName is not null && PerNome.TryGetValue(svgName, out var voce) ? voce.TipoClr : null;

    /// <summary>Una primitiva nuova del tipo indicato, con i valori predefiniti.</summary>
    public static FilterPrimitive? Crea(string? svgName) =>
        svgName is not null && PerNome.TryGetValue(svgName, out var voce) ? voce.Crea() : null;

    /// <summary>
    /// Una voce del catalogo: il nome SVG, come si chiama per chi lo usa, che cosa fa in una
    /// riga, e come se ne fabbrica una nuova.
    /// </summary>
    /// <param name="SvgName">Il nome del nodo previsto dalla specifica.</param>
    /// <param name="Etichetta">
    /// Il nome mostrato, in inglese: è il ripiego quando manca il catalogo. La traduzione si
    /// cerca con il nome SVG, che è tecnico e non cambia mai.
    /// </param>
    /// <param name="Descrizione">Una riga che dice a che cosa serve, per il menu di inserimento.</param>
    /// <param name="Crea">Fabbrica una primitiva nuova già regolata su valori sensati.</param>
    public sealed record VoceCatalogo(
        string SvgName,
        string Etichetta,
        string Descrizione,
        Func<FilterPrimitive> Crea)
    {
        /// <summary>Il tipo concreto prodotto dalla fabbrica, ricavato una volta sola.</summary>
        public Type TipoClr { get; } = Crea().GetType();
    }
}
