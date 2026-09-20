using System.Reflection;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Models;
using PatternEditor.Element.Circle;
using PatternEditor.Element.Ellipse;
using PatternEditor.Element.Image;
using PatternEditor.Element.Line;
using PatternEditor.Element.Path;
using PatternEditor.Element.Polygon;
using PatternEditor.Element.Polyline;
using PatternEditor.Element.Rect;
using PatternEditor.Element.Text;
using Xunit;

namespace PatternEditor.Abstractions.Tests;

/// <summary>
/// La dotazione completa dei plugin, usata dai test che devono valere per <b>tutti</b> i
/// tipi di elemento e non per i due che capitava di avere sotto mano.
///
/// <para>
/// L'elenco è scritto a mano e non ricavato per riflessione dagli assembly caricati. È una
/// scelta: ricavarlo automaticamente farebbe passare i test anche il giorno in cui un plugin
/// sparisce dalla soluzione, che è esattamente il caso che si vuole scoprire. Aggiungendo un
/// decimo elemento, questa riga è una delle due da cambiare — l'altra è il
/// <c>Program.cs</c> dell'applicazione — e il test che conta i tipi lo ricorda.
/// </para>
/// </summary>
internal static class AllPlugins
{
    public static IReadOnlyList<IVectorElementPlugin> Instances { get; } = new IVectorElementPlugin[]
    {
        new LinePlugin(),
        new RectPlugin(),
        new CirclePlugin(),
        new EllipsePlugin(),
        new PathPlugin(),
        new PolygonPlugin(),
        new PolylinePlugin(),
        new TextPlugin(),
        new ImagePlugin(),
    };

    /// <summary>I tipi, nella forma che xUnit sa stampare quando un caso fallisce.</summary>
    public static TheoryData<string> Types
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var plugin in Instances)
            {
                data.Add(plugin.Type);
            }

            return data;
        }
    }

    public static IVectorElementPlugin Of(string type) =>
        Instances.Single(p => p.Type == type);

    public static VectorElementPluginRegistry Registry()
    {
        var registry = new VectorElementPluginRegistry();
        foreach (var plugin in Instances)
        {
            registry.Register(plugin);
        }

        return registry;
    }

    /// <summary>Il registro di un'installazione a cui è stato tolto un plugin.</summary>
    public static VectorElementPluginRegistry RegistryWithout(string type)
    {
        var registry = new VectorElementPluginRegistry();
        foreach (var plugin in Instances.Where(p => p.Type != type))
        {
            registry.Register(plugin);
        }

        return registry;
    }

    /// <summary>
    /// Le proprietà pubbliche scrivibili di un elemento concreto, escluso l'identificativo.
    ///
    /// I test le percorrono per riflessione invece di elencarle a mano: così una proprietà
    /// aggiunta domani a un elemento entra automaticamente nelle verifiche di
    /// serializzazione e di copia, che sono i due punti in cui dimenticarla non si vede.
    /// </summary>
    public static IReadOnlyList<PropertyInfo> WritableProperties(VectorElement element) =>
        element.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.SetMethod is { IsPublic: true })
            .Where(p => p.Name != nameof(VectorElement.Id))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Un elemento con ogni proprietà portata via dal proprio valore predefinito.
    ///
    /// Serve a distinguere «la proprietà ha attraversato il giro» da «la proprietà è tornata
    /// al valore di partenza, che per caso è lo stesso»: con i valori predefiniti un
    /// serializzatore che perde un campo passerebbe ugualmente il test.
    /// </summary>
    public static VectorElement Populated(IVectorElementPlugin plugin)
    {
        var element = plugin.Create();
        var progressivo = 0;
        var opacita = 0;

        foreach (var property in WritableProperties(element))
        {
            progressivo++;
            object valore;

            if (property.PropertyType == typeof(double))
            {
                // Le opacità hanno un intervallo proprio: assegnarle 11,5 come a una
                // coordinata darebbe un elemento distinguibile ma non valido, e i test che
                // passano anche dal validatore fallirebbero per il motivo sbagliato.
                valore = property.Name.Contains("Opacity", StringComparison.Ordinal)
                    ? 0.25 + 0.15 * ++opacita
                    : 10.5 + progressivo;
            }
            else if (property.PropertyType == typeof(string))
            {
                // string e string? sono lo stesso tipo a runtime: il punto interrogativo
                // vive nei metadati del compilatore, non nel sistema dei tipi.
                //
                // I valori sono scelti per nome e non a caso: devono essere DIVERSI dai
                // predefiniti — altrimenti una proprietà persa nel giro non si noterebbe —
                // e al tempo stesso VALIDI, perché gli stessi elementi vengono dati in pasto
                // al validatore e al renderer.
                valore = property.Name switch
                {
                    "Fill" => "#1a2b3c",
                    "Stroke" => "#4d5e6f",
                    "D" => "M 1 2 L 30.5 40 Z",
                    "Points" => "1,2 30.5,40 5,6",
                    "Content" => "Ciao & <mondo>",       // anche la protezione del markup
                    "FontFamily" => "Georgia, serif",
                    "FontWeight" => "bold",
                    "TextAnchor" => "middle",
                    "Href" => "https://esempio.invalid/immagine.png",
                    "PreserveAspectRatio" => "xMidYMid slice",
                    _ => Sconosciuta(element, property.Name),
                };
            }
            else if (property.PropertyType == typeof(bool))
            {
                // Le specchiature: due interruttori sulla classe base, uguali per ogni tipo.
                valore = true;
            }
            else
            {
                // Deliberatamente un fallimento e non un salto silenzioso: una proprietà di
                // un tipo che questo test non sa popolare resterebbe non verificata, e
                // nessuno se ne accorgerebbe.
                valore = Sconosciuta(element, property.Name, property.PropertyType.Name);
            }

            property.SetValue(element, valore);
        }

        Assert.NotEmpty(WritableProperties(element));
        return element;
    }

    private static string Sconosciuta(VectorElement element, string nome, string? tipo = null)
    {
        Assert.Fail(
            $"Il test non sa quale valore assegnare a {element.GetType().Name}.{nome}" +
            (tipo is null ? "" : $" (tipo {tipo})") +
            ": va insegnato ad AllPlugins.Populated, così che la nuova proprietà entri " +
            "nelle verifiche di serializzazione, di copia e di rendering.");
        return string.Empty;
    }

    /// <summary>Confronta proprietà per proprietà due elementi dello stesso tipo.</summary>
    public static void AssertSameProperties(VectorElement expected, VectorElement actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());

        foreach (var property in WritableProperties(expected))
        {
            Assert.Equal(
                property.GetValue(expected),
                property.GetValue(actual));
        }
    }
}
