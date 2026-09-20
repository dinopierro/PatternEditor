using System.Globalization;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Formatting;
using PatternEditor.Core.Localization;
using PatternEditor.Core.Models;
using PatternEditor.Core.Validation;
using PatternEditor.Element.Image.Components;

namespace PatternEditor.Element.Image;

/// <summary>
/// Plugin dell'elemento SVG &lt;image&gt;, mostrato all'utente come «Immagine».
///
/// Un &lt;image&gt; inserisce un'immagine esterna dentro il disegno vettoriale. L'attributo
/// <c>preserveAspectRatio</c> decide come l'immagine si adatta al rettangolo che la ospita:
/// <c>meet</c> la contiene per intero lasciando spazio vuoto, <c>slice</c> riempie il
/// rettangolo tagliando ciò che avanza, <c>none</c> la deforma per farla combaciare.
///
/// La sorgente può essere un indirizzo esterno oppure un'immagine incorporata come
/// data URI. Sono due compromessi opposti: l'indirizzo tiene il documento leggero ma lo
/// rende dipendente da un server, il data URI lo rende autonomo ma ne moltiplica il peso.
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
public sealed class ImagePlugin : IVectorElementPlugin
{
    /// <summary>
    /// Oltre questa dimensione il data URI viene comunque accettato, ma segnalato come
    /// avviso: l'immagine viaggia dentro il JSON del pattern e ne condiziona il peso.
    /// </summary>
    public const int LargeHrefWarningBytes = 512 * 1024;

    /// <summary>
    /// Identificativo tecnico del tipo, scritto nel JSON come discriminatore. È stabile nel
    /// tempo e indipendente dalla lingua: cambiarlo renderebbe illeggibili i documenti già
    /// salvati, che verrebbero riletti come elementi di tipo sconosciuto.
    /// </summary>
    public string Type => ImageElement.TypeName;

    /// <summary>
    /// Nome mostrato nell'interfaccia. Serve solo agli occhi: non va mai usato per
    /// riconoscere il tipo né finisce nella serializzazione.
    /// </summary>
    // Il nome che il plugin porta con sé: l'inglese, come tutto quello che la
    // libreria dice senza un catalogo. Le traduzioni stanno nei cataloghi
    // dell'editor, alla chiave «tipo.image», e questo resta il ripiego.
    public string DisplayName => "Image";

    /// <summary>
    /// Contenuto interno dell'icona, in un sistema di coordinate 16×16. Non contiene il tag
    /// &lt;svg&gt;, che aggiunge chi la mostra, e usa <c>stroke="currentColor"</c> ereditato
    /// dal chiamante: così l'icona segue il colore del testo e funziona su tema chiaro e scuro
    /// senza avere due versioni.
    /// </summary>
    public string IconSvg =>
        "<rect x=\"2\" y=\"3.5\" width=\"12\" height=\"9\" rx=\"1\" /><circle cx=\"5.5\" cy=\"6.5\" r=\"1\" /><path d=\"M2.5 12 L6.5 8 L9 10.5 L11 8.5 L13.5 11.5\" />";

    /// <summary>
    /// Tipo concreto del modello. È l'informazione che permette al serializzatore di
    /// ricostruire l'oggetto giusto leggendo il discriminatore, senza che il livello comune
    /// debba conoscere questo assembly.
    /// </summary>
    public Type ElementClrType => typeof(ImageElement);

    /// <summary>
    /// Componente Blazor che modifica l'elemento. Viene passato come <see cref="Type"/> e non
    /// come componente tipizzato perché il livello delle astrazioni non dipende da Blazor:
    /// è l'editor principale a istanziarlo dinamicamente.
    /// </summary>
    public Type EditorComponentType => typeof(ImageEditor);

    /// <summary>
    /// Nuova istanza con valori predefiniti già validi. I valori li decide il plugin, non
    /// l'editor: è il plugin a sapere cosa sia un esemplare sensato del proprio tipo.
    /// </summary>
    public VectorElement Create() => ImageElement.CreateDefault();

    public ValidationResult Validate(VectorElement element)
    {
        if (element is not ImageElement image)
        {
            return ValidationResult.Failure("L'elemento non è un ImageElement.");
        }

        // Gli errori si accumulano invece di fermarsi al primo: chi ha sbagliato due valori
        // preferisce vederli entrambi subito, non scoprirne uno alla volta a ogni tentativo.
        var errors = new List<TestoNominato>();
        var warnings = new List<TestoNominato>();
        var href = image.Href?.Trim() ?? string.Empty;

        if (href.Length == 0)
        {
            errors.Add(new TestoNominato("val.immagineNessuna", "No image set."));
        }
        else if (!IsSupportedSource(href))
        {
            // Sono ammesse solo sorgenti che rappresentano davvero un'immagine: un data URI
            // di tipo diverso (testo, html) o uno schema come javascript: non hanno alcun
            // uso legittimo qui e finirebbero comunque in un documento condivisibile.
            errors.Add(new TestoNominato("val.immagineIndirizzo", "The image must be a data URI of type image/… or an http/https address."));
        }
        else if (href.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && href.Length > LargeHrefWarningBytes)
        {
            warnings.Add(
                new TestoNominato(
                "val.immagineGrande",
                "The embedded image takes about {0} kB: the saved pattern will be about the same size.",
                href.Length / 1024));
        }

        if (image.Width <= 0)
        {
            errors.Add(new TestoNominato("val.larghezza", "The width must be greater than zero."));
        }

        if (image.Height <= 0)
        {
            errors.Add(new TestoNominato("val.altezza", "The height must be greater than zero."));
        }

        if (image.Opacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaComplessiva", "The overall opacity must be between 0 and 1."));
        }

        return new ValidationResult(errors, warnings);
    }

    public string Render(VectorElement element)
    {
        if (element is not ImageElement image)
        {
            throw new ArgumentException("L'elemento non è un ImageElement.", nameof(element));
        }

        // Cultura invariante obbligatoria: con una cultura italiana un valore come 1,5
        // finirebbe nel markup con la virgola, e un lettore SVG lo scarterebbe.
        var inv = CultureInfo.InvariantCulture;

        // Viene generato l'attributo "href" della specifica SVG 2. Il vecchio "xlink:href"
        // richiederebbe la dichiarazione del namespace xlink sul nodo <svg> radice, che è
        // responsabilità del renderer e non del plugin.
        var attributes = new List<string>
        {
            $"x=\"{image.X.ToString(inv)}\"",
            $"y=\"{image.Y.ToString(inv)}\"",
            $"width=\"{image.Width.ToString(inv)}\"",
            $"height=\"{image.Height.ToString(inv)}\"",
            $"preserveAspectRatio=\"{SvgText.Escape(image.PreserveAspectRatio)}\"",
            $"href=\"{SvgText.Escape(image.Href?.Trim())}\"",
            $"opacity=\"{image.Opacity.ToString(inv)}\"",
        };

        return $"<image {string.Join(' ', attributes)} />";
    }

    /// <summary>
    /// Sorgenti ammesse: data URI di un'immagine oppure indirizzo http/https.
    /// </summary>
    private static bool IsSupportedSource(string href) =>
        href.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
        || href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
