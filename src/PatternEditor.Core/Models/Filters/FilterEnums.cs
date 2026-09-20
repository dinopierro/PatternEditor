namespace PatternEditor.Core.Models.Filters;

/// <summary>
/// Lo spazio colore in cui le primitive fanno i conti (<c>color-interpolation-filters</c>).
///
/// <para>
/// La differenza si vede: sfocare o fondere in <see cref="LinearRgb"/> è corretto dal punto
/// di vista fisico — è il modo in cui la luce si somma davvero — e produce transizioni che a
/// chi guarda sembrano schiarite. In <see cref="Srgb"/> i conti si fanno sui valori così
/// come sono scritti, e il risultato è quello che ci si aspetta guardando lo schermo.
/// </para>
/// </summary>
public enum FilterColorSpace
{
    /// <summary>I conti sui valori come sono scritti. È ciò che quasi tutti si aspettano.</summary>
    Srgb,

    /// <summary>I conti sulla luce. È il valore predefinito della specifica.</summary>
    LinearRgb,
}

/// <summary>Che cosa fa una <c>feColorMatrix</c>: l'attributo <c>type</c> della specifica.</summary>
public enum ColorMatrixKind
{
    /// <summary>Quanto colore resta: 0 è grigio, 1 lascia tutto com'è, oltre 1 satura.</summary>
    Saturate,

    /// <summary>Ruota la tinta di tutti i colori dello stesso angolo.</summary>
    HueRotate,

    /// <summary>Trasforma la luminosità in trasparenza: il chiaro sparisce, lo scuro resta.</summary>
    LuminanceToAlpha,

    /// <summary>I venti coefficienti scritti a mano: tutto il resto è un caso particolare di questo.</summary>
    Matrix,
}

/// <summary>Come una <c>feFuncR|G|B|A</c> rimappa un canale: l'attributo <c>type</c>.</summary>
public enum TransferFunctionKind
{
    /// <summary>Il canale resta com'è.</summary>
    Identity,

    /// <summary>Una retta: <c>pendenza × valore + intercetta</c>. È contrasto e luminosità.</summary>
    Linear,

    /// <summary>Una curva: <c>ampiezza × valore^esponente + scostamento</c>.</summary>
    Gamma,

    /// <summary>Una scala di valori interpolati fra loro: due valori invertiti ribaltano il canale.</summary>
    Table,

    /// <summary>Come la tabella ma a gradini: è la posterizzazione.</summary>
    Discrete,
}

/// <summary>Le due operazioni di <c>feMorphology</c>.</summary>
public enum MorphologyOperator
{
    /// <summary>Assottiglia: mangia i bordi.</summary>
    Erode,

    /// <summary>Ispessisce: gonfia i bordi.</summary>
    Dilate,
}

/// <summary>I modi di fusione di <c>feBlend</c>, nell'ordine della specifica.</summary>
public enum BlendMode
{
    Normal,
    Multiply,
    Screen,
    Overlay,
    Darken,
    Lighten,
    ColorDodge,
    ColorBurn,
    HardLight,
    SoftLight,
    Difference,
    Exclusion,
    Hue,
    Saturation,
    Color,
    Luminosity,
}

/// <summary>Gli operatori di <c>feComposite</c>, cioè l'algebra di Porter-Duff più uno.</summary>
public enum CompositeOperator
{
    Over,
    In,
    Out,
    Atop,
    Xor,

    /// <summary>La combinazione pesata <c>k1·i1·i2 + k2·i1 + k3·i2 + k4</c>: serve a dosare.</summary>
    Arithmetic,
}

/// <summary>I due rumori di <c>feTurbulence</c>.</summary>
public enum TurbulenceKind
{
    /// <summary>Nuvole morbide.</summary>
    FractalNoise,

    /// <summary>Venature nervose, adatte a irregolarità e sporco.</summary>
    Turbulence,
}

/// <summary>Quale canale di un'immagine usare come numero: l'attributo <c>*ChannelSelector</c>.</summary>
public enum ColorChannel
{
    R,
    G,
    B,
    A,
}

/// <summary>Che cosa c'è appena fuori dall'immagine, quando una primitiva va a leggere là.</summary>
public enum EdgeMode
{
    /// <summary>Si ripete il pixel del bordo.</summary>
    Duplicate,

    /// <summary>Si riprende dal lato opposto.</summary>
    Wrap,

    /// <summary>Non c'è niente: trasparente.</summary>
    None,
}

/// <summary>L'ordine in cui <c>feConvolveMatrix</c> legge i coefficienti del nucleo.</summary>
public enum ConvolveOrder
{
    /// <summary>Tre per tre: nove coefficienti. Copre nitidezza, rilievo e contorni.</summary>
    Tre,

    /// <summary>Cinque per cinque: venticinque coefficienti, per effetti più larghi.</summary>
    Cinque,
}
