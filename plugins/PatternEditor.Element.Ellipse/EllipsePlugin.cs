using System.Globalization;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Formatting;
using PatternEditor.Core.Localization;
using PatternEditor.Core.Models;
using PatternEditor.Core.Validation;
using PatternEditor.Element.Ellipse.Components;

namespace PatternEditor.Element.Ellipse;

/// <summary>
/// Plugin dell'elemento SVG &lt;ellipse&gt;, mostrato all'utente come «Ellisse».
///
/// Una &lt;ellipse&gt; è definita dal centro (<c>cx</c>, <c>cy</c>) e dai due semiassi
/// <c>rx</c> e <c>ry</c>. È la generalizzazione del cerchio: con <c>rx</c> uguale a
/// <c>ry</c> le due forme coincidono, e sono tenute distinte perché distinti sono i
/// comandi che l'utente si aspetta di trovare.
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
public sealed class EllipsePlugin : IVectorElementPlugin
{
    /// <summary>
    /// Identificativo tecnico del tipo, scritto nel JSON come discriminatore. È stabile nel
    /// tempo e indipendente dalla lingua: cambiarlo renderebbe illeggibili i documenti già
    /// salvati, che verrebbero riletti come elementi di tipo sconosciuto.
    /// </summary>
    public string Type => EllipseElement.TypeName;

    /// <summary>
    /// Nome mostrato nell'interfaccia. Serve solo agli occhi: non va mai usato per
    /// riconoscere il tipo né finisce nella serializzazione.
    /// </summary>
    // Il nome che il plugin porta con sé: l'inglese, come tutto quello che la
    // libreria dice senza un catalogo. Le traduzioni stanno nei cataloghi
    // dell'editor, alla chiave «tipo.ellipse», e questo resta il ripiego.
    public string DisplayName => "Ellipse";

    /// <summary>
    /// Contenuto interno dell'icona, in un sistema di coordinate 16×16. Non contiene il tag
    /// &lt;svg&gt;, che aggiunge chi la mostra, e usa <c>stroke="currentColor"</c> ereditato
    /// dal chiamante: così l'icona segue il colore del testo e funziona su tema chiaro e scuro
    /// senza avere due versioni.
    /// </summary>
    public string IconSvg =>
        "<ellipse cx=\"8\" cy=\"8\" rx=\"6\" ry=\"4\" />";

    /// <summary>
    /// Tipo concreto del modello. È l'informazione che permette al serializzatore di
    /// ricostruire l'oggetto giusto leggendo il discriminatore, senza che il livello comune
    /// debba conoscere questo assembly.
    /// </summary>
    public Type ElementClrType => typeof(EllipseElement);

    /// <summary>
    /// Componente Blazor che modifica l'elemento. Viene passato come <see cref="Type"/> e non
    /// come componente tipizzato perché il livello delle astrazioni non dipende da Blazor:
    /// è l'editor principale a istanziarlo dinamicamente.
    /// </summary>
    public Type EditorComponentType => typeof(EllipseEditor);

    /// <summary>
    /// Nuova istanza con valori predefiniti già validi. I valori li decide il plugin, non
    /// l'editor: è il plugin a sapere cosa sia un esemplare sensato del proprio tipo.
    /// </summary>
    public VectorElement Create() => EllipseElement.CreateDefault();

    public ValidationResult Validate(VectorElement element)
    {
        if (element is not EllipseElement ellipse)
        {
            return ValidationResult.Failure("L'elemento non è un EllipseElement.");
        }

        // Gli errori si accumulano invece di fermarsi al primo: chi ha sbagliato due valori
        // preferisce vederli entrambi subito, non scoprirne uno alla volta a ogni tentativo.
        var errors = new List<TestoNominato>();

        if (ellipse.Rx <= 0)
        {
            errors.Add(new TestoNominato("val.semiasseX", "The horizontal semi-axis must be greater than zero."));
        }

        if (ellipse.Ry <= 0)
        {
            errors.Add(new TestoNominato("val.semiasseY", "The vertical semi-axis must be greater than zero."));
        }

        if (ellipse.StrokeWidth < 0)
        {
            errors.Add(new TestoNominato("val.spessoreBordo", "The stroke thickness cannot be negative."));
        }

        if (ellipse.FillOpacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaRiempimento", "The fill opacity must be between 0 and 1."));
        }

        if (ellipse.StrokeOpacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaBordo", "The stroke opacity must be between 0 and 1."));
        }

        if (ellipse.Opacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaComplessiva", "The overall opacity must be between 0 and 1."));
        }

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(errors);
    }

    public string Render(VectorElement element)
    {
        if (element is not EllipseElement ellipse)
        {
            throw new ArgumentException("L'elemento non è un EllipseElement.", nameof(element));
        }

        // Cultura invariante obbligatoria: con una cultura italiana un valore come 1,5
        // finirebbe nel markup con la virgola, e un lettore SVG lo scarterebbe.
        var inv = CultureInfo.InvariantCulture;
        var attributes = new List<string>
        {
            $"cx=\"{ellipse.Cx.ToString(inv)}\"",
            $"cy=\"{ellipse.Cy.ToString(inv)}\"",
            $"rx=\"{ellipse.Rx.ToString(inv)}\"",
            $"ry=\"{ellipse.Ry.ToString(inv)}\"",
        };

        if (ellipse.Fill is null)
        {
            attributes.Add("fill=\"none\"");
        }
        else
        {
            attributes.Add($"fill=\"{SvgText.Escape(ellipse.Fill)}\"");
            attributes.Add($"fill-opacity=\"{ellipse.FillOpacity.ToString(inv)}\"");
        }

        if (ellipse.Stroke is null)
        {
            attributes.Add("stroke=\"none\"");
        }
        else
        {
            attributes.Add($"stroke=\"{SvgText.Escape(ellipse.Stroke)}\"");
            attributes.Add($"stroke-width=\"{ellipse.StrokeWidth.ToString(inv)}\"");
            attributes.Add($"stroke-opacity=\"{ellipse.StrokeOpacity.ToString(inv)}\"");
        }

        attributes.Add($"opacity=\"{ellipse.Opacity.ToString(inv)}\"");

        return $"<ellipse {string.Join(' ', attributes)} />";
    }
}
