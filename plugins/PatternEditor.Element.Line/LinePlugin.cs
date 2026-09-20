using System.Globalization;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Formatting;
using PatternEditor.Core.Localization;
using PatternEditor.Core.Models;
using PatternEditor.Core.Validation;
using PatternEditor.Element.Line.Components;

namespace PatternEditor.Element.Line;

/// <summary>
/// Plugin dell'elemento SVG &lt;line&gt;, mostrato all'utente come «Linea».
///
/// Una &lt;line&gt; congiunge due punti, (<c>x1</c>, <c>y1</c>) e (<c>x2</c>, <c>y2</c>).
/// È l'unico elemento del catalogo privo di riempimento: la specifica prevede l'attributo
/// <c>fill</c> anche qui, ma su una linea non ha alcun effetto visibile, e il modello non
/// lo espone per non suggerire un comando che non fa nulla.
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
public sealed class LinePlugin : IVectorElementPlugin
{
    /// <summary>
    /// Identificativo tecnico del tipo, scritto nel JSON come discriminatore. È stabile nel
    /// tempo e indipendente dalla lingua: cambiarlo renderebbe illeggibili i documenti già
    /// salvati, che verrebbero riletti come elementi di tipo sconosciuto.
    /// </summary>
    public string Type => LineElement.TypeName;

    /// <summary>
    /// Nome mostrato nell'interfaccia. Serve solo agli occhi: non va mai usato per
    /// riconoscere il tipo né finisce nella serializzazione.
    /// </summary>
    // Il nome che il plugin porta con sé: l'inglese, come tutto quello che la
    // libreria dice senza un catalogo. Le traduzioni stanno nei cataloghi
    // dell'editor, alla chiave «tipo.line», e questo resta il ripiego.
    public string DisplayName => "Line";

    /// <summary>
    /// Contenuto interno dell'icona, in un sistema di coordinate 16×16. Non contiene il tag
    /// &lt;svg&gt;, che aggiunge chi la mostra, e usa <c>stroke="currentColor"</c> ereditato
    /// dal chiamante: così l'icona segue il colore del testo e funziona su tema chiaro e scuro
    /// senza avere due versioni.
    /// </summary>
    public string IconSvg =>
        "<line x1=\"2.5\" y1=\"13.5\" x2=\"13.5\" y2=\"2.5\" />";

    /// <summary>
    /// Tipo concreto del modello. È l'informazione che permette al serializzatore di
    /// ricostruire l'oggetto giusto leggendo il discriminatore, senza che il livello comune
    /// debba conoscere questo assembly.
    /// </summary>
    public Type ElementClrType => typeof(LineElement);

    /// <summary>
    /// Componente Blazor che modifica l'elemento. Viene passato come <see cref="Type"/> e non
    /// come componente tipizzato perché il livello delle astrazioni non dipende da Blazor:
    /// è l'editor principale a istanziarlo dinamicamente.
    /// </summary>
    public Type EditorComponentType => typeof(LineEditor);

    /// <summary>
    /// Nuova istanza con valori predefiniti già validi. I valori li decide il plugin, non
    /// l'editor: è il plugin a sapere cosa sia un esemplare sensato del proprio tipo.
    /// </summary>
    public VectorElement Create() => LineElement.CreateDefault();

    public ValidationResult Validate(VectorElement element)
    {
        if (element is not LineElement line)
        {
            return ValidationResult.Failure("L'elemento non è un LineElement.");
        }

        // Gli errori si accumulano invece di fermarsi al primo: chi ha sbagliato due valori
        // preferisce vederli entrambi subito, non scoprirne uno alla volta a ogni tentativo.
        var errors = new List<TestoNominato>();

        if (line.StrokeWidth < 0)
        {
            errors.Add(new TestoNominato("val.spessoreTratto", "The stroke thickness cannot be negative."));
        }

        if (line.StrokeOpacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaTratto", "The stroke opacity must be between 0 and 1."));
        }

        if (line.Opacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaComplessiva", "The overall opacity must be between 0 and 1."));
        }

        // Le coordinate possono essere negative o superare i limiti della cella: una linea
        // può intenzionalmente estendersi oltre i confini del pattern.

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(errors);
    }

    public string Render(VectorElement element)
    {
        if (element is not LineElement line)
        {
            throw new ArgumentException("L'elemento non è un LineElement.", nameof(element));
        }

        // Cultura invariante obbligatoria: con una cultura italiana un valore come 1,5
        // finirebbe nel markup con la virgola, e un lettore SVG lo scarterebbe.
        var inv = CultureInfo.InvariantCulture;

        return "<line "
            + $"x1=\"{line.X1.ToString(inv)}\" "
            + $"y1=\"{line.Y1.ToString(inv)}\" "
            + $"x2=\"{line.X2.ToString(inv)}\" "
            + $"y2=\"{line.Y2.ToString(inv)}\" "
            + $"stroke=\"{SvgText.Escape(line.Stroke)}\" "
            + $"stroke-width=\"{line.StrokeWidth.ToString(inv)}\" "
            + $"stroke-opacity=\"{line.StrokeOpacity.ToString(inv)}\" "
            + $"opacity=\"{line.Opacity.ToString(inv)}\" />";
    }
}
