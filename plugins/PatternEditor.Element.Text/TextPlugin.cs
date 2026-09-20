using System.Globalization;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Formatting;
using PatternEditor.Core.Localization;
using PatternEditor.Core.Models;
using PatternEditor.Core.Validation;
using PatternEditor.Element.Text.Components;

namespace PatternEditor.Element.Text;

/// <summary>
/// Plugin dell'elemento SVG &lt;text&gt;, mostrato all'utente come «Testo».
///
/// Un &lt;text&gt; è l'unico elemento del catalogo che porta un **contenuto** oltre agli
/// attributi: le lettere da disegnare stanno fra i tag, non in un attributo. La distinzione
/// conta al momento di generare il markup, perché le regole di protezione dei caratteri
/// speciali sono diverse dentro un attributo e dentro il contenuto di un nodo (vedi
/// <see cref="SvgText"/>).
///
/// La posizione (<c>x</c>, <c>y</c>) individua il punto di partenza della linea di base, non
/// l'angolo superiore sinistro: è la ragione per cui un testo con y pari a zero risulta
/// quasi tutto sopra il bordo della cella.
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
public sealed class TextPlugin : IVectorElementPlugin
{
    private static readonly string[] AllowedAnchors =
        [TextElement.AnchorStart, TextElement.AnchorMiddle, TextElement.AnchorEnd];

    private static readonly string[] AllowedWeights =
        [TextElement.WeightNormal, TextElement.WeightBold];

    /// <summary>
    /// Identificativo tecnico del tipo, scritto nel JSON come discriminatore. È stabile nel
    /// tempo e indipendente dalla lingua: cambiarlo renderebbe illeggibili i documenti già
    /// salvati, che verrebbero riletti come elementi di tipo sconosciuto.
    /// </summary>
    public string Type => TextElement.TypeName;

    /// <summary>
    /// Nome mostrato nell'interfaccia. Serve solo agli occhi: non va mai usato per
    /// riconoscere il tipo né finisce nella serializzazione.
    /// </summary>
    // Il nome che il plugin porta con sé: l'inglese, come tutto quello che la
    // libreria dice senza un catalogo. Le traduzioni stanno nei cataloghi
    // dell'editor, alla chiave «tipo.text», e questo resta il ripiego.
    public string DisplayName => "Text";

    /// <summary>
    /// Contenuto interno dell'icona, in un sistema di coordinate 16×16. Non contiene il tag
    /// &lt;svg&gt;, che aggiunge chi la mostra, e usa <c>stroke="currentColor"</c> ereditato
    /// dal chiamante: così l'icona segue il colore del testo e funziona su tema chiaro e scuro
    /// senza avere due versioni.
    /// </summary>
    public string IconSvg =>
        "<path d=\"M3.5 3.5 h9 M8 3.5 v9 M5.5 12.5 h5\" />";

    /// <summary>
    /// Tipo concreto del modello. È l'informazione che permette al serializzatore di
    /// ricostruire l'oggetto giusto leggendo il discriminatore, senza che il livello comune
    /// debba conoscere questo assembly.
    /// </summary>
    public Type ElementClrType => typeof(TextElement);

    /// <summary>
    /// Componente Blazor che modifica l'elemento. Viene passato come <see cref="Type"/> e non
    /// come componente tipizzato perché il livello delle astrazioni non dipende da Blazor:
    /// è l'editor principale a istanziarlo dinamicamente.
    /// </summary>
    public Type EditorComponentType => typeof(TextEditor);

    /// <summary>
    /// Nuova istanza con valori predefiniti già validi. I valori li decide il plugin, non
    /// l'editor: è il plugin a sapere cosa sia un esemplare sensato del proprio tipo.
    /// </summary>
    public VectorElement Create() => TextElement.CreateDefault();

    public ValidationResult Validate(VectorElement element)
    {
        if (element is not TextElement text)
        {
            return ValidationResult.Failure("L'elemento non è un TextElement.");
        }

        // Gli errori si accumulano invece di fermarsi al primo: chi ha sbagliato due valori
        // preferisce vederli entrambi subito, non scoprirne uno alla volta a ogni tentativo.
        var errors = new List<TestoNominato>();
        var warnings = new List<TestoNominato>();

        if (string.IsNullOrWhiteSpace(text.Content))
        {
            errors.Add(new TestoNominato("val.testoVuoto", "The text cannot be empty."));
        }

        if (text.FontSize <= 0)
        {
            errors.Add(new TestoNominato("val.corpoCarattere", "The font size must be greater than zero."));
        }

        if (!AllowedAnchors.Contains(text.TextAnchor, StringComparer.Ordinal))
        {
            errors.Add(new TestoNominato(
                "val.allineamento",
                "The alignment must be one of: {0}.",
                string.Join(", ", AllowedAnchors)));
        }

        if (!AllowedWeights.Contains(text.FontWeight, StringComparer.Ordinal))
        {
            errors.Add(new TestoNominato(
                "val.pesoCarattere",
                "The font weight must be one of: {0}.",
                string.Join(", ", AllowedWeights)));
        }

        if (string.IsNullOrWhiteSpace(text.FontFamily))
        {
            errors.Add(new TestoNominato("val.famigliaVuota", "The font family cannot be empty."));
        }
        else if (!IsGenericFamily(text.FontFamily))
        {
            // Il font non viaggia dentro l'SVG: è un riferimento a ciò che è installato
            // su chi lo apre. Non è un errore, ma va detto.
            warnings.Add(
                new TestoNominato(
                "val.carattereNonGenerico",
                "The font “{0}” has to be installed on whoever opens the SVG, otherwise it will be substituted. The generic families (sans-serif, serif, monospace) are always available.",
                text.FontFamily));
        }

        if (text.StrokeWidth < 0)
        {
            errors.Add(new TestoNominato("val.spessoreContorno", "The outline thickness cannot be negative."));
        }

        if (text.FillOpacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaTesto", "The text opacity must be between 0 and 1."));
        }

        if (text.StrokeOpacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaContorno", "The outline opacity must be between 0 and 1."));
        }

        if (text.Opacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaComplessiva", "The overall opacity must be between 0 and 1."));
        }

        return new ValidationResult(errors, warnings);
    }

    public string Render(VectorElement element)
    {
        if (element is not TextElement text)
        {
            throw new ArgumentException("L'elemento non è un TextElement.", nameof(element));
        }

        // Cultura invariante obbligatoria: con una cultura italiana un valore come 1,5
        // finirebbe nel markup con la virgola, e un lettore SVG lo scarterebbe.
        var inv = CultureInfo.InvariantCulture;
        var attributes = new List<string>
        {
            $"x=\"{text.X.ToString(inv)}\"",
            $"y=\"{text.Y.ToString(inv)}\"",
            $"font-family=\"{SvgText.Escape(text.FontFamily)}\"",
            $"font-size=\"{text.FontSize.ToString(inv)}\"",
            $"font-weight=\"{SvgText.Escape(text.FontWeight)}\"",
            $"text-anchor=\"{SvgText.Escape(text.TextAnchor)}\"",
        };

        if (text.Fill is null)
        {
            attributes.Add("fill=\"none\"");
        }
        else
        {
            attributes.Add($"fill=\"{SvgText.Escape(text.Fill)}\"");
            attributes.Add($"fill-opacity=\"{text.FillOpacity.ToString(inv)}\"");
        }

        if (text.Stroke is null)
        {
            attributes.Add("stroke=\"none\"");
        }
        else
        {
            attributes.Add($"stroke=\"{SvgText.Escape(text.Stroke)}\"");
            attributes.Add($"stroke-width=\"{text.StrokeWidth.ToString(inv)}\"");
            attributes.Add($"stroke-opacity=\"{text.StrokeOpacity.ToString(inv)}\"");
        }

        attributes.Add($"opacity=\"{text.Opacity.ToString(inv)}\"");

        // Unico elemento che non è autochiuso: il testo è contenuto del nodo, e come tale
        // va protetto (gli apici, qui, non hanno bisogno di essere sostituiti).
        return $"<text {string.Join(' ', attributes)}>{SvgText.EscapeContent(text.Content)}</text>";
    }

    /// <summary>
    /// Famiglie generiche definite da CSS: sono sempre risolvibili, qualunque sia il
    /// dispositivo, perché non indicano un font preciso ma una categoria.
    /// </summary>
    private static bool IsGenericFamily(string fontFamily) =>
        fontFamily.Trim() is "sans-serif" or "serif" or "monospace" or "cursive" or "fantasy" or "system-ui";
}
