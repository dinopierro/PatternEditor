using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Localization;
using PatternEditor.Core.Models;
using PatternEditor.Core.Models.Filters;
using PatternEditor.Core.Validation;

namespace PatternEditor.Services;

/// <summary>
/// Verifica che un pattern sia pubblicabile.
///
/// <para>
/// La validazione è **a due livelli** e la divisione è la stessa che regge tutto il sistema:
/// le proprietà generali del pattern le controlla questo servizio, quelle di ogni elemento
/// il plugin che lo governa. Il componente principale non contiene una sola regola specifica
/// di un tipo concreto, e non deve contenerla: il giorno in cui si aggiunge un elemento, qui
/// non si tocca niente.
/// </para>
/// </summary>
public interface IPatternValidator
{
    /// <summary>
    /// Esamina il pattern e restituisce errori e avvisi insieme. Non solleva eccezioni per i
    /// dati sbagliati: dati sbagliati sono il caso normale mentre si compila un modulo, non
    /// una circostanza eccezionale.
    /// </summary>
    ValidationResult Validate(Pattern pattern);
}

/// <summary>
/// Implementazione predefinita del validatore.
///
/// <para>
/// Distingue con cura **errori** e **avvisi**, che non sono due gradi della stessa cosa. Un
/// errore dice che il dato è sbagliato e impedisce la conferma; un avviso dice che la scelta
/// è legittima ma ha una conseguenza che chi l'ha fatta potrebbe non aspettarsi — un
/// carattere che il lettore potrebbe non avere, un'immagine incorporata che appesantisce il
/// documento. Trasformare un avviso in errore vorrebbe dire vietare scelte valide.
/// </para>
///
/// <para>
/// Gli elementi di tipo sconosciuto non vengono validati nel merito — non c'è nessuno che
/// sappia come — ma nemmeno scartati: producono un avviso e vengono conservati intatti. È la
/// contropartita della regola per cui un documento scritto da un'installazione con più
/// plugin di questa deve poter essere riletto e risalvato senza perdere nulla.
/// </para>
/// </summary>
public sealed class PatternValidator : IPatternValidator
{
    private readonly IVectorElementPluginRegistry _registry;

    public PatternValidator(IVectorElementPluginRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public ValidationResult Validate(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        // Si raccoglie tutto e si riferisce alla fine: fermarsi al primo errore costringerebbe
        // a scoprire i problemi uno alla volta, con un tentativo di conferma per ciascuno.
        var errors = new List<TestoNominato>();
        var warnings = new List<TestoNominato>();

        if (pattern.Definition.Width <= 0)
        {
            errors.Add(new("val.cellaLarghezza", "The cell width must be greater than zero."));
        }

        if (pattern.Definition.Height <= 0)
        {
            errors.Add(new("val.cellaAltezza", "The cell height must be greater than zero."));
        }

        if (pattern.Definition.Scale <= 0)
        {
            errors.Add(new("val.scala", "The scale must be greater than zero."));
        }

        // La rotazione è limitata a un giro: 370 gradi sarebbe legittimo per la specifica ed
        // equivarrebbe a 10, ma in un campo di interfaccia è quasi sempre un errore di
        // battitura, e accettarlo renderebbe due pattern identici indistinguibili nei dati.
        if (pattern.Definition.Rotation is < 0 or > 360)
        {
            errors.Add(new("val.rotazione", "The rotation must be between 0 and 360 degrees."));
        }

        // Ogni elemento viene passato al suo plugin. I messaggi tornano indietro preceduti dal
        // nome del tipo, perché "La larghezza deve essere maggiore di zero" senza sapere di
        // quale elemento fra dodici non aiuta nessuno.
        // Identificativi ripetuti: sono un documento malformato, non una stranezza
        // innocua. Servono a distinguere un elemento dall'altro in ogni operazione che li
        // tratta singolarmente - modifica, duplicazione, riordino - e due elementi
        // indistinguibili le rendono tutte ambigue.
        var ripetuti = pattern.Definition.Elements
            .GroupBy(e => e.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var id in ripetuti)
        {
            errors.Add(new(
                "val.idRipetuto",
                "Identifier {0} is used by more than one element: it has to be unique.",
                id));
        }

        foreach (var element in pattern.Definition.Elements)
        {
            if (element is UnknownVectorElement unknown)
            {
                warnings.Add(new(
                    "val.elementoIgnoto",
                    "The element of type \u201c{0}\u201d (id {1}) is not handled by the current "
                    + "plugins and will be preserved unchanged.",
                    unknown.Type, unknown.Id));
                continue;
            }

            if (_registry.TryGet(element.Type, out var plugin) && plugin is not null)
            {
                var result = plugin.Validate(element);

                // Il nome del tipo entra nel messaggio come testo a sé e non come parola già
                // scritta: chi mostrerà la frase saprà tradurre anche quello, e «[Rectangle]»
                // in mezzo a una riga italiana si noterebbe.
                var quale = new TestoNominato("tipo." + plugin.Type, plugin.DisplayName);

                errors.AddRange(result.Errori.Select(e =>
                    new TestoNominato("val.elemento", "[{0}] {1}", quale, e)));
                warnings.AddRange(result.Avvisi.Select(w =>
                    new TestoNominato("val.elemento", "[{0}] {1}", quale, w)));
            }
            else
            {
                // Type dichiarato ma plugin non registrato: comportamento equivalente
                // a un elemento sconosciuto, non deve bloccare la validazione.
                warnings.Add(new(
                    "val.pluginMancante",
                    "No plugin registered for type \u201c{0}\u201d (id {1}).",
                    element.Type, element.Id));
            }
        }

        EsaminaFiltro(pattern.Definition.Filter, errors, warnings);

        return new ValidationResult(errors, warnings);
    }

    /// <summary>
    /// I nomi che la specifica mette a disposizione senza che nessuno li debba produrre.
    /// Un ingresso che si chiama così è sempre valido anche se nessun passaggio precedente
    /// ha dichiarato quel risultato.
    /// </summary>
    private static readonly HashSet<string> SorgentiPredefinite = new(StringComparer.Ordinal)
    {
        "SourceGraphic",
        "SourceAlpha",
        "BackgroundImage",
        "BackgroundAlpha",
        "FillPaint",
        "StrokePaint",
    };

    /// <summary>
    /// Controlla il filtro.
    ///
    /// <para>
    /// La divisione fra errore e avviso qui è particolarmente netta, e vale la pena dirla.
    /// Sono <b>errori</b> i numeri che la specifica dichiara illegali: un browser di fronte a
    /// un raggio negativo non disegna un effetto strano, disabilita l'intero filtro, e il
    /// risultato è che il pattern esce dall'editor con un aspetto e si apre altrove con un
    /// altro. Sono <b>avvisi</b> i collegamenti che non portano da nessuna parte: producono un
    /// passaggio che non fa niente, che è legittimo e quasi sempre un errore di battitura.
    /// </para>
    /// </summary>
    private static void EsaminaFiltro(
        PatternFilter? filtro, List<TestoNominato> errors, List<TestoNominato> warnings)
    {
        if (filtro is null)
        {
            return;
        }

        if (filtro.Width <= 0 || filtro.Height <= 0)
        {
            errors.Add(new(
                "val.filtro.area",
                "The filter area must have a width and a height greater than zero."));
        }

        // I nomi disponibili crescono man mano che si scorre la catena: un passaggio può
        // richiamare solo ciò che è stato prodotto PRIMA di lui. Un riferimento in avanti è
        // il difetto tipico di chi riordina i passaggi dopo averli collegati.
        var disponibili = new HashSet<string>(SorgentiPredefinite, StringComparer.Ordinal);

        // Che cosa riceverebbe, adesso, un passaggio che non dichiara il proprio ingresso.
        // Serve a riconoscere la trappola dei generatori, spiegata più sotto.
        FilterPrimitive? precedente = null;

        for (var i = 0; i < filtro.Primitives.Count; i++)
        {
            var p = filtro.Primitives[i];
            // Dove si trova il guaio: entra nei messaggi come primo valore, e resta
            // traducibile fin dentro il nome del passaggio.
            var dove = new TestoNominato("val.filtro.dove", "Filter, step {0} ({1})", i + 1, p.Titolo);

            if (p is UnknownFilterPrimitive ignota)
            {
                warnings.Add(new(
                    "val.filtro.ignota",
                    "{0}: the step \u201c{1}\u201d is not handled by this version and will be kept "
                    + "without being applied.",
                    dove, ignota.Tipo));
                continue;
            }

            EsaminaValori(p, dove, errors);

            if (p.Enabled)
            {
                ControllaIngresso(p.In, dove, Ruolo.Ingresso, disponibili, warnings);

                // Un passaggio senza ingresso esplicito riceve il risultato del precedente. Se
                // il precedente è un generatore, quel risultato è l'immagine generata e non il
                // disegno: chi mette un rumore e subito dopo una fusione finisce per fondere il
                // rumore con se stesso, e a schermo resta solo quello. Non è illegale, ed è
                // quasi sempre indesiderato — cioè esattamente un avviso.
                if (precedente is { Genera: true } && string.IsNullOrWhiteSpace(p.In) && !p.Genera)
                {
                    warnings.Add(new(
                        "val.filtro.dopoGeneratore",
                        "{0}: it follows a generator ({1}) and does not declare its own input, so it "
                        + "receives the generated image instead of the drawing. Give the result you need a "
                        + "name and call it here.",
                        dove, precedente.Titolo));
                }

                if (p is BlendPrimitive b)
                {
                    ControllaIngresso(b.In2, dove, Ruolo.Secondo, disponibili, warnings);
                }
                else if (p is CompositePrimitive c)
                {
                    ControllaIngresso(c.In2, dove, Ruolo.Secondo, disponibili, warnings);
                }
                else if (p is DisplacementMapPrimitive d)
                {
                    ControllaIngresso(d.In2, dove, Ruolo.Mappa, disponibili, warnings);
                }
                else if (p is MergePrimitive m)
                {
                    foreach (var ingresso in m.Inputs)
                    {
                        ControllaIngresso(ingresso, dove, Ruolo.Livello, disponibili, warnings);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(p.Result))
            {
                disponibili.Add(p.Result.Trim());
            }

            // Solo i passaggi accesi entrano nella catena: uno spento non c'è, e non può
            // essere lui il «precedente» di quello dopo.
            if (p.Enabled)
            {
                precedente = p;
            }
        }
    }

    /// <summary>Come si chiamano gli ingressi di un passaggio, quando li si nomina in un avviso.</summary>
    private static class Ruolo
    {
        public static readonly TestoNominato Ingresso = new("val.filtro.ruolo.ingresso", "the input");
        public static readonly TestoNominato Secondo = new("val.filtro.ruolo.secondo", "the second input");
        public static readonly TestoNominato Mappa = new("val.filtro.ruolo.mappa", "the displacement map");
        public static readonly TestoNominato Livello = new("val.filtro.ruolo.livello", "a layer");
    }

    /// <summary>I valori che la specifica dichiara illegali, primitiva per primitiva.</summary>
    private static void EsaminaValori(FilterPrimitive p, TestoNominato dove, List<TestoNominato> errors)
    {
        switch (p)
        {
            case GaussianBlurPrimitive b when b.StdDeviationX < 0 || b.StdDeviationY < 0:
                errors.Add(new("val.filtro.sfocatura", "{0}: the blur radius cannot be negative.", dove));
                break;

            case DropShadowPrimitive o when o.StdDeviation < 0:
                errors.Add(new("val.filtro.ombra", "{0}: the shadow blur cannot be negative.", dove));
                break;

            case MorphologyPrimitive m when m.RadiusX < 0 || m.RadiusY < 0:
                errors.Add(new("val.filtro.raggio", "{0}: the radius cannot be negative.", dove));
                break;

            case TurbulencePrimitive t when t.BaseFrequencyX < 0 || t.BaseFrequencyY < 0:
                errors.Add(new("val.filtro.frequenza", "{0}: the noise frequency cannot be negative.", dove));
                break;

            case TurbulencePrimitive t2 when t2.NumOctaves < 1:
                errors.Add(new("val.filtro.ottave", "{0}: there has to be at least one octave.", dove));
                break;

            case ColorMatrixPrimitive c when c.Kind == ColorMatrixKind.Matrix && c.Matrix.Count != 20:
                errors.Add(new(
                    "val.filtro.matrice",
                    "{0}: the colour matrix needs exactly twenty coefficients, it has {1}.",
                    dove, c.Matrix.Count));
                break;

            case ConvolveMatrixPrimitive v when v.KernelMatrix.Count != v.Lato * v.Lato:
                errors.Add(new(
                    "val.filtro.nucleo",
                    "{0}: a {1} × {1} kernel needs {2} coefficients, it has {3}.",
                    dove, v.Lato, v.Lato * v.Lato, v.KernelMatrix.Count));
                break;

            case ComponentTransferPrimitive ct:
                foreach (var (nome, funzione) in ct.Canali())
                {
                    var tabellare = funzione.Kind is TransferFunctionKind.Table or TransferFunctionKind.Discrete;
                    if (tabellare && funzione.TableValues.Count < 2)
                    {
                        errors.Add(new(
                            "val.filtro.tabella",
                            "{0}: channel {1} uses a table, which needs at least two values.",
                            dove, nome));
                    }
                }

                break;
        }
    }

    private static void ControllaIngresso(
        string? nome,
        TestoNominato dove,
        TestoNominato ruolo,
        HashSet<string> disponibili,
        List<TestoNominato> warnings)
    {
        var pulito = nome?.Trim();

        // Vuoto è corretto e significa «il risultato del passaggio precedente».
        if (string.IsNullOrEmpty(pulito) || disponibili.Contains(pulito))
        {
            return;
        }

        warnings.Add(new(
            "val.filtro.ingresso",
            "{0}: {1} refers to \u201c{2}\u201d, which no earlier step produces. The step will "
            + "have no effect.",
            dove, ruolo, pulito));
    }
}
