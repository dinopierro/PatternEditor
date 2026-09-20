using System.Globalization;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Formatting;
using PatternEditor.Core.Localization;
using PatternEditor.Core.Models;
using PatternEditor.Core.Validation;
using PatternEditor.Element.Path.Components;

namespace PatternEditor.Element.Path;

/// <summary>
/// Plugin dell'elemento SVG &lt;path&gt;, mostrato all'utente come «Tracciato».
///
/// Un &lt;path&gt; è la forma più generale della specifica: l'intera geometria sta
/// nell'attributo <c>d</c>, una sequenza di comandi (M/m spostamento, L/l linea, H/h e V/v
/// linee ortogonali, C/c e S/s curve cubiche, Q/q e T/t curve quadratiche, A/a archi,
/// Z/z chiusura). Le lettere maiuscole indicano coordinate assolute, le minuscole relative
/// al punto corrente.
///
/// Il modello conserva il tracciato come testo e non lo interpreta: il Pattern Editor non
/// è un editor di curve, e riscrivere il comando di qualcun altro - anche solo
/// normalizzandone la spaziatura - vorrebbe dire restituirgli un tracciato diverso da
/// quello che ha incollato.
///
/// <para>
/// Un plugin è il punto di estensione del sistema: raccoglie in un solo posto tutto ciò
/// che riguarda il proprio tipo - modello dati, valori predefiniti, interfaccia di modifica,
/// validazione e generazione del markup - e nient'altro. In particolare **non** conosce
/// basi di dati, filesystem, API, il salvataggio del pattern, il contenitore dell'editor
/// né gli altri plugin: se due plugin avessero bisogno della stessa logica, quella logica
/// andrebbe nel livello comune, mai in uno dei due.
/// </para>
///
/// <para>
/// La classe non ha stato: viene registrata una volta sola come singleton e le sue
/// operazioni ricevono sempre l'elemento su cui lavorare.
/// </para>
/// </summary>
public sealed class PathPlugin : IVectorElementPlugin
{
    /// <summary>
    /// Identificativo tecnico del tipo, scritto nel JSON come discriminatore. È stabile nel
    /// tempo e indipendente dalla lingua: cambiarlo renderebbe illeggibili i documenti già
    /// salvati, che verrebbero riletti come elementi di tipo sconosciuto.
    /// </summary>
    public string Type => PathElement.TypeName;

    /// <summary>
    /// Nome mostrato nell'interfaccia. Serve solo agli occhi: non va mai usato per
    /// riconoscere il tipo né finisce nella serializzazione.
    /// </summary>
    // Il nome che il plugin porta con sé: l'inglese, come tutto quello che la
    // libreria dice senza un catalogo. Le traduzioni stanno nei cataloghi
    // dell'editor, alla chiave «tipo.path», e questo resta il ripiego.
    public string DisplayName => "Path";

    /// <summary>
    /// Contenuto interno dell'icona, in un sistema di coordinate 16×16. Non contiene il tag
    /// &lt;svg&gt;, che aggiunge chi la mostra, e usa <c>stroke="currentColor"</c> ereditato
    /// dal chiamante: così l'icona segue il colore del testo e funziona su tema chiaro e scuro
    /// senza avere due versioni.
    /// </summary>
    public string IconSvg =>
        "<path d=\"M2 12 C 4.5 3.5, 11.5 12.5, 14 4\" /><circle cx=\"2\" cy=\"12\" r=\"1.2\" /><circle cx=\"14\" cy=\"4\" r=\"1.2\" />";

    /// <summary>
    /// Tipo concreto del modello. È l'informazione che permette al serializzatore di
    /// ricostruire l'oggetto giusto leggendo il discriminatore, senza che il livello comune
    /// debba conoscere questo assembly.
    /// </summary>
    public Type ElementClrType => typeof(PathElement);

    /// <summary>
    /// Componente Blazor che modifica l'elemento. Viene passato come <see cref="Type"/> e non
    /// come componente tipizzato perché il livello delle astrazioni non dipende da Blazor:
    /// è l'editor principale a istanziarlo dinamicamente.
    /// </summary>
    public Type EditorComponentType => typeof(PathEditor);

    /// <summary>
    /// Comandi ammessi dalla specifica SVG per l'attributo "d": moveto, lineto, curve,
    /// archi e chiusura, nelle varianti assoluta (maiuscola) e relativa (minuscola).
    /// </summary>
    private const string CommandLetters = "MmLlHhVvCcSsQqTtAaZz";

    /// <summary>
    /// Nuova istanza con valori predefiniti già validi. I valori li decide il plugin, non
    /// l'editor: è il plugin a sapere cosa sia un esemplare sensato del proprio tipo.
    /// </summary>
    public VectorElement Create() => PathElement.CreateDefault();

    public ValidationResult Validate(VectorElement element)
    {
        if (element is not PathElement path)
        {
            return ValidationResult.Failure("L'elemento non è un PathElement.");
        }

        // Gli errori si accumulano invece di fermarsi al primo: chi ha sbagliato due valori
        // preferisce vederli entrambi subito, non scoprirne uno alla volta a ogni tentativo.
        var errors = new List<TestoNominato>();
        var d = path.D?.Trim() ?? string.Empty;

        if (d.Length == 0)
        {
            errors.Add(new TestoNominato("val.tracciatoVuoto", "The path cannot be empty."));
        }
        else
        {
            if (d[0] is not ('M' or 'm'))
            {
                errors.Add(new TestoNominato("val.tracciatoInizio", "The path must start with a move command (M or m)."));
            }

            var invalid = d.Where(c => !IsAllowedInPathData(c)).Distinct().ToArray();
            if (invalid.Length > 0)
            {
                errors.Add(new TestoNominato(
                "val.tracciatoCaratteri",
                "The path contains characters that are not allowed: {0}.",
                string.Join(' ', invalid)));
            }
        }

        if (path.StrokeWidth < 0)
        {
            errors.Add(new TestoNominato("val.spessoreTratto", "The stroke thickness cannot be negative."));
        }

        if (path.FillOpacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaRiempimento", "The fill opacity must be between 0 and 1."));
        }

        if (path.StrokeOpacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaTratto", "The stroke opacity must be between 0 and 1."));
        }

        if (path.Opacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaComplessiva", "The overall opacity must be between 0 and 1."));
        }

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(errors);
    }

    public string Render(VectorElement element)
    {
        if (element is not PathElement path)
        {
            throw new ArgumentException("L'elemento non è un PathElement.", nameof(element));
        }

        // Cultura invariante obbligatoria: con una cultura italiana un valore come 1,5
        // finirebbe nel markup con la virgola, e un lettore SVG lo scarterebbe.
        var inv = CultureInfo.InvariantCulture;

        // Il tracciato è testo libero inserito dall'utente: va sempre passato per l'escape,
        // anche quando la validazione lo segnala come non valido (l'anteprima viene
        // comunque generata mentre si scrive e non deve mai produrre markup rotto).
        var attributes = new List<string>
        {
            $"d=\"{SvgText.Escape(path.D?.Trim())}\"",
        };

        if (path.Fill is null)
        {
            attributes.Add("fill=\"none\"");
        }
        else
        {
            attributes.Add($"fill=\"{SvgText.Escape(path.Fill)}\"");
            attributes.Add($"fill-opacity=\"{path.FillOpacity.ToString(inv)}\"");
        }

        if (path.Stroke is null)
        {
            attributes.Add("stroke=\"none\"");
        }
        else
        {
            attributes.Add($"stroke=\"{SvgText.Escape(path.Stroke)}\"");
            attributes.Add($"stroke-width=\"{path.StrokeWidth.ToString(inv)}\"");
            attributes.Add($"stroke-opacity=\"{path.StrokeOpacity.ToString(inv)}\"");
        }

        attributes.Add($"opacity=\"{path.Opacity.ToString(inv)}\"");

        return $"<path {string.Join(' ', attributes)} />";
    }

    private static bool IsAllowedInPathData(char c) =>
        char.IsAsciiDigit(c)
        || char.IsWhiteSpace(c)
        || c is ',' or '.' or '+' or '-'
        || c is 'e' or 'E'          // notazione esponenziale, es. 1e-3
        || CommandLetters.Contains(c, StringComparison.Ordinal);
}
