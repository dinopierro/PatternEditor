using Microsoft.Extensions.DependencyInjection;
using PatternEditor.Abstractions.Localization;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Services;

namespace PatternEditor.Extensions;

/// <summary>
/// Punto di ingresso per la registrazione della libreria Pattern Editor nell'applicazione host.
///
/// L'applicazione host è responsabile di registrare anche i plugin concreti (Line, Rect, ...)
/// tramite <see cref="AddPatternEditorPlugin{TPlugin}"/>: la libreria stessa non ne conosce
/// alcuno e non introduce riferimenti diretti ai relativi assembly.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPatternEditor(this IServiceCollection services)
    {
        services.AddSingleton<IVectorElementPluginRegistry>(sp =>
        {
            var registry = new VectorElementPluginRegistry();
            foreach (var plugin in sp.GetServices<IVectorElementPlugin>())
            {
                registry.Register(plugin);
            }

            return registry;
        });
        // I testi dell'editor. Singolo per tutta l'applicazione: la lingua è una sola, e
        // due copie vorrebbero dire due metà di interfaccia che cambiano in momenti diversi.
        // Senza che l'ospite applichi niente parla inglese, quindi registrarlo non obbliga
        // nessuno ad avere un catalogo.
        services.AddSingleton<TestiEditor>();

        services.AddSingleton<IPatternSerializer, PatternSerializer>();
        services.AddSingleton<IPatternFactory, PatternFactory>();
        services.AddSingleton<IPatternValidator, PatternValidator>();
        services.AddSingleton<ISvgFilterRenderer, SvgFilterRenderer>();
        services.AddSingleton<IPatternSvgRenderer, PatternSvgRenderer>();
        services.AddSingleton<IVectorElementCloner, VectorElementCloner>();
        services.AddSingleton<IPatternCloner, PatternCloner>();
        services.AddSingleton<ISvgPatternImporter, SvgPatternImporter>();
        services.AddSingleton<IRasterPatternImporter, RasterPatternImporter>();
        return services;
    }

    /// <summary>
    /// Registra un plugin di elemento vettoriale nel Plugin Registry. Da chiamare una volta
    /// per ogni tipo di elemento che l'applicazione host desidera rendere disponibile
    /// (es. <c>services.AddPatternEditorPlugin&lt;RectPlugin&gt;()</c>).
    /// </summary>
    public static IServiceCollection AddPatternEditorPlugin<TPlugin>(this IServiceCollection services)
        where TPlugin : class, IVectorElementPlugin, new()
    {
        services.AddSingleton<IVectorElementPlugin>(new TPlugin());
        return services;
    }
}
