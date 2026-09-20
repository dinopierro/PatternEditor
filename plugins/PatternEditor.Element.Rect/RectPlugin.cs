using System.Globalization;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Formatting;
using PatternEditor.Core.Localization;
using PatternEditor.Core.Models;
using PatternEditor.Core.Validation;
using PatternEditor.Element.Rect.Components;

namespace PatternEditor.Element.Rect;

/// <summary>
/// Plugin dell'elemento SVG &lt;rect&gt;, mostrato all'utente come «Rettangolo».
///
/// Un &lt;rect&gt; è definito dall'angolo superiore sinistro (<c>x</c>, <c>y</c>) e dalle sue
/// dimensioni (<c>width</c>, <c>height</c>). La specifica prevede anche <c>rx</c> e <c>ry</c>
/// per gli angoli arrotondati: non sono esposti perché il modello resta volutamente al
/// minimo indispensabile, e un angolo arrotondato si ottiene comunque con un tracciato.
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
public sealed class RectPlugin : IVectorElementPlugin
{
    /// <summary>
    /// Identificativo tecnico del tipo, scritto nel JSON come discriminatore. È stabile nel
    /// tempo e indipendente dalla lingua: cambiarlo renderebbe illeggibili i documenti già
    /// salvati, che verrebbero riletti come elementi di tipo sconosciuto.
    /// </summary>
    public string Type => RectElement.TypeName;

    /// <summary>
    /// Nome mostrato nell'interfaccia. Serve solo agli occhi: non va mai usato per
    /// riconoscere il tipo né finisce nella serializzazione.
    /// </summary>
    // Il nome che il plugin porta con sé: l'inglese, come tutto quello che la
    // libreria dice senza un catalogo. Le traduzioni stanno nei cataloghi
    // dell'editor, alla chiave «tipo.rect», e questo resta il ripiego.
    public string DisplayName => "Rectangle";

    /// <summary>
    /// Contenuto interno dell'icona, in un sistema di coordinate 16×16. Non contiene il tag
    /// &lt;svg&gt;, che aggiunge chi la mostra, e usa <c>stroke="currentColor"</c> ereditato
    /// dal chiamante: così l'icona segue il colore del testo e funziona su tema chiaro e scuro
    /// senza avere due versioni.
    /// </summary>
    public string IconSvg =>
        "<rect x=\"2.5\" y=\"4\" width=\"11\" height=\"8\" rx=\"1\" />";

    /// <summary>
    /// Tipo concreto del modello. È l'informazione che permette al serializzatore di
    /// ricostruire l'oggetto giusto leggendo il discriminatore, senza che il livello comune
    /// debba conoscere questo assembly.
    /// </summary>
    public Type ElementClrType => typeof(RectElement);

    /// <summary>
    /// Componente Blazor che modifica l'elemento. Viene passato come <see cref="Type"/> e non
    /// come componente tipizzato perché il livello delle astrazioni non dipende da Blazor:
    /// è l'editor principale a istanziarlo dinamicamente.
    /// </summary>
    public Type EditorComponentType => typeof(RectEditor);

    /// <summary>
    /// Nuova istanza con valori predefiniti già validi. I valori li decide il plugin, non
    /// l'editor: è il plugin a sapere cosa sia un esemplare sensato del proprio tipo.
    /// </summary>
    public VectorElement Create() => RectElement.CreateDefault();

    public ValidationResult Validate(VectorElement element)
    {
        if (element is not RectElement rect)
        {
            return ValidationResult.Failure("L'elemento non è un RectElement.");
        }

        // Gli errori si accumulano invece di fermarsi al primo: chi ha sbagliato due valori
        // preferisce vederli entrambi subito, non scoprirne uno alla volta a ogni tentativo.
        var errors = new List<TestoNominato>();

        if (rect.Width <= 0)
        {
            errors.Add(new TestoNominato("val.larghezza", "The width must be greater than zero."));
        }

        if (rect.Height <= 0)
        {
            errors.Add(new TestoNominato("val.altezza", "The height must be greater than zero."));
        }

        if (rect.StrokeWidth < 0)
        {
            errors.Add(new TestoNominato("val.spessoreBordo", "The stroke thickness cannot be negative."));
        }

        if (rect.FillOpacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaRiempimento", "The fill opacity must be between 0 and 1."));
        }

        if (rect.StrokeOpacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaBordo", "The stroke opacity must be between 0 and 1."));
        }

        if (rect.Opacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaComplessiva", "The overall opacity must be between 0 and 1."));
        }

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(errors);
    }

    public string Render(VectorElement element)
    {
        if (element is not RectElement rect)
        {
            throw new ArgumentException("L'elemento non è un RectElement.", nameof(element));
        }

        // Cultura invariante obbligatoria: con una cultura italiana un valore come 1,5
        // finirebbe nel markup con la virgola, e un lettore SVG lo scarterebbe.
        var inv = CultureInfo.InvariantCulture;
        var attributes = new List<string>
        {
            $"x=\"{rect.X.ToString(inv)}\"",
            $"y=\"{rect.Y.ToString(inv)}\"",
            $"width=\"{rect.Width.ToString(inv)}\"",
            $"height=\"{rect.Height.ToString(inv)}\"",
        };

        if (rect.Fill is null)
        {
            attributes.Add("fill=\"none\"");
        }
        else
        {
            attributes.Add($"fill=\"{SvgText.Escape(rect.Fill)}\"");
            attributes.Add($"fill-opacity=\"{rect.FillOpacity.ToString(inv)}\"");
        }

        if (rect.Stroke is null)
        {
            attributes.Add("stroke=\"none\"");
        }
        else
        {
            attributes.Add($"stroke=\"{SvgText.Escape(rect.Stroke)}\"");
            attributes.Add($"stroke-width=\"{rect.StrokeWidth.ToString(inv)}\"");
            attributes.Add($"stroke-opacity=\"{rect.StrokeOpacity.ToString(inv)}\"");
        }

        attributes.Add($"opacity=\"{rect.Opacity.ToString(inv)}\"");

        return $"<rect {string.Join(' ', attributes)} />";
    }
}
