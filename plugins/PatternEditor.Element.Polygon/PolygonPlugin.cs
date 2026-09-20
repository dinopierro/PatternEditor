using System.Globalization;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Formatting;
using PatternEditor.Core.Localization;
using PatternEditor.Core.Models;
using PatternEditor.Core.Validation;
using PatternEditor.Element.Polygon.Components;

namespace PatternEditor.Element.Polygon;

/// <summary>
/// Plugin dell'elemento SVG &lt;polygon&gt;, mostrato all'utente come «Poligono».
///
/// Un &lt;polygon&gt; è una spezzata che la specifica chiude automaticamente: l'ultimo
/// punto viene ricongiunto al primo senza doverlo ripetere, e la figura risultante può
/// essere riempita. È questa la differenza con &lt;polyline&gt;, ed è la ragione per cui i
/// due elementi restano distinti anche qui.
///
/// I punti stanno nell'attributo <c>points</c>, come coppie separate da spazi o virgole.
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
public sealed class PolygonPlugin : IVectorElementPlugin
{
    /// <summary>
    /// Identificativo tecnico del tipo, scritto nel JSON come discriminatore. È stabile nel
    /// tempo e indipendente dalla lingua: cambiarlo renderebbe illeggibili i documenti già
    /// salvati, che verrebbero riletti come elementi di tipo sconosciuto.
    /// </summary>
    public string Type => PolygonElement.TypeName;

    /// <summary>
    /// Nome mostrato nell'interfaccia. Serve solo agli occhi: non va mai usato per
    /// riconoscere il tipo né finisce nella serializzazione.
    /// </summary>
    // Il nome che il plugin porta con sé: l'inglese, come tutto quello che la
    // libreria dice senza un catalogo. Le traduzioni stanno nei cataloghi
    // dell'editor, alla chiave «tipo.polygon», e questo resta il ripiego.
    public string DisplayName => "Polygon";

    /// <summary>
    /// Contenuto interno dell'icona, in un sistema di coordinate 16×16. Non contiene il tag
    /// &lt;svg&gt;, che aggiunge chi la mostra, e usa <c>stroke="currentColor"</c> ereditato
    /// dal chiamante: così l'icona segue il colore del testo e funziona su tema chiaro e scuro
    /// senza avere due versioni.
    /// </summary>
    public string IconSvg =>
        "<path d=\"M8 2 L14 6.4 L11.7 13.5 L4.3 13.5 L2 6.4 Z\" />";

    /// <summary>
    /// Tipo concreto del modello. È l'informazione che permette al serializzatore di
    /// ricostruire l'oggetto giusto leggendo il discriminatore, senza che il livello comune
    /// debba conoscere questo assembly.
    /// </summary>
    public Type ElementClrType => typeof(PolygonElement);

    /// <summary>
    /// Componente Blazor che modifica l'elemento. Viene passato come <see cref="Type"/> e non
    /// come componente tipizzato perché il livello delle astrazioni non dipende da Blazor:
    /// è l'editor principale a istanziarlo dinamicamente.
    /// </summary>
    public Type EditorComponentType => typeof(PolygonEditor);

    /// <summary>
    /// Nuova istanza con valori predefiniti già validi. I valori li decide il plugin, non
    /// l'editor: è il plugin a sapere cosa sia un esemplare sensato del proprio tipo.
    /// </summary>
    public VectorElement Create() => PolygonElement.CreateDefault();

    public ValidationResult Validate(VectorElement element)
    {
        if (element is not PolygonElement polygon)
        {
            return ValidationResult.Failure("L'elemento non è un PolygonElement.");
        }

        // Gli errori si accumulano invece di fermarsi al primo: chi ha sbagliato due valori
        // preferisce vederli entrambi subito, non scoprirne uno alla volta a ogni tentativo.
        var errors = new List<TestoNominato>();

        if (!SvgPoints.TryCountPoints(polygon.Points, out var pointCount))
        {
            errors.Add(new TestoNominato("val.verticiFormato", "The vertices must be pairs of numbers in the form “x,y” separated by spaces."));
        }
        else if (pointCount < PolygonElement.MinimumPoints)
        {
            errors.Add(new TestoNominato(
                "val.poligonoVertici",
                "A polygon needs at least {0} vertices ({1} given).",
                PolygonElement.MinimumPoints, pointCount));
        }

        if (polygon.StrokeWidth < 0)
        {
            errors.Add(new TestoNominato("val.spessoreBordo", "The stroke thickness cannot be negative."));
        }

        if (polygon.FillOpacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaRiempimento", "The fill opacity must be between 0 and 1."));
        }

        if (polygon.StrokeOpacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaBordo", "The stroke opacity must be between 0 and 1."));
        }

        if (polygon.Opacity is < 0 or > 1)
        {
            errors.Add(new TestoNominato("val.opacitaComplessiva", "The overall opacity must be between 0 and 1."));
        }

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(errors);
    }

    public string Render(VectorElement element)
    {
        if (element is not PolygonElement polygon)
        {
            throw new ArgumentException("L'elemento non è un PolygonElement.", nameof(element));
        }

        // Cultura invariante obbligatoria: con una cultura italiana un valore come 1,5
        // finirebbe nel markup con la virgola, e un lettore SVG lo scarterebbe.
        var inv = CultureInfo.InvariantCulture;

        // I vertici sono testo libero: l'escape va applicato anche quando la validazione
        // li segnala come non validi, perché l'anteprima viene generata a ogni battuta.
        var attributes = new List<string>
        {
            $"points=\"{SvgText.Escape(polygon.Points?.Trim())}\"",
        };

        if (polygon.Fill is null)
        {
            attributes.Add("fill=\"none\"");
        }
        else
        {
            attributes.Add($"fill=\"{SvgText.Escape(polygon.Fill)}\"");
            attributes.Add($"fill-opacity=\"{polygon.FillOpacity.ToString(inv)}\"");
        }

        if (polygon.Stroke is null)
        {
            attributes.Add("stroke=\"none\"");
        }
        else
        {
            attributes.Add($"stroke=\"{SvgText.Escape(polygon.Stroke)}\"");
            attributes.Add($"stroke-width=\"{polygon.StrokeWidth.ToString(inv)}\"");
            attributes.Add($"stroke-opacity=\"{polygon.StrokeOpacity.ToString(inv)}\"");
        }

        attributes.Add($"opacity=\"{polygon.Opacity.ToString(inv)}\"");

        return $"<polygon {string.Join(' ', attributes)} />";
    }
}
