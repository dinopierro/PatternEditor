using PatternEditor.Core.Models;
using PatternEditor.Core.Validation;

namespace PatternEditor.Abstractions.Plugins;

/// <summary>
/// Contratto comune implementato da ogni plugin di elemento vettoriale (Line, Rect, e i
/// tipi che verranno introdotti in futuro). Rappresenta il punto di estensione principale
/// del sistema: l'editor principale conosce esclusivamente questa astrazione, mai le
/// implementazioni concrete di Line o Rect.
///
/// Il plugin conosce: il proprio modello, i propri valori predefiniti, la propria UI,
/// la propria validazione, il proprio rendering SVG e il proprio Type.
/// Il plugin NON conosce: database, filesystem, API REST, salvataggio del pattern,
/// il contenitore principale dell'editor, gli altri plugin, né come l'host utilizza il pattern.
/// </summary>
public interface IVectorElementPlugin
{
    /// <summary>
    /// Identificativo tecnico stabile del tipo (es. "line", "rect"). Univoco nell'applicazione,
    /// stabile nel tempo, indipendente dalla lingua e dal nome della classe C#. Utilizzato
    /// come discriminatore "type" nel JSON persistito.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Nome visualizzato all'utente (es. "Linea", "Rettangolo"). Finalità esclusivamente UI:
    /// non deve mai essere usato per identificare tecnicamente il tipo o nella serializzazione.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Icona rappresentativa del tipo di elemento: contenuto interno di un &lt;svg&gt; con
    /// viewBox "0 0 16 16", disegnato con <c>stroke="currentColor"</c> e senza riempimento,
    /// così da adattarsi al colore e alla dimensione decisi da chi lo mostra. Non deve
    /// contenere il tag &lt;svg&gt;: lo aggiunge il chiamante.
    ///
    /// Ha un'implementazione predefinita (un quadrato generico) perché un plugin già
    /// esistente non deve essere costretto a fornirla: l'icona è una rifinitura dell'UI,
    /// non parte del contratto funzionale.
    /// </summary>
    string IconSvg => "<rect x=\"2.5\" y=\"2.5\" width=\"11\" height=\"11\" rx=\"2\" />";

    /// <summary>
    /// Tipo CLR concreto del modello dati gestito dal plugin (es. typeof(RectElement)).
    /// Usato dal serializzatore per la deserializzazione polimorfica, senza che Core o
    /// Abstractions debbano referenziare direttamente gli assembly dei singoli plugin.
    /// </summary>
    Type ElementClrType { get; }

    /// <summary>
    /// Tipo del componente Blazor da usare per modificare l'elemento. L'editor principale
    /// lo istanzia dinamicamente (es. tramite DynamicComponent) senza conoscerne i dettagli
    /// implementativi. Questo evita che il progetto Abstractions debba dipendere da
    /// Microsoft.AspNetCore.Components pur mantenendo l'editor principale del tutto agnostico
    /// rispetto al tipo concreto di elemento.
    /// </summary>
    Type EditorComponentType { get; }

    /// <summary>
    /// Crea una nuova istanza dell'elemento con valori predefiniti validi, definiti dal
    /// plugin stesso (l'editor principale non deve conoscere costruttore o parametri).
    /// </summary>
    VectorElement Create();

    /// <summary>
    /// Valida le proprietà specifiche dell'elemento. L'editor principale non deve contenere
    /// regole specifiche per nessun tipo concreto: ogni plugin valida esclusivamente il
    /// proprio modello.
    /// </summary>
    ValidationResult Validate(VectorElement element);

    /// <summary>
    /// Genera la rappresentazione SVG dell'elemento (es. "&lt;rect ... /&gt;"). Non deve generare
    /// &lt;svg&gt;, &lt;defs&gt;, &lt;pattern&gt; o patternTransform: queste responsabilità appartengono
    /// al componente principale. La stessa logica deve essere usata sia per le preview
    /// dell'editor sia per l'SVG finale scaricabile.
    /// </summary>
    string Render(VectorElement element);
}
