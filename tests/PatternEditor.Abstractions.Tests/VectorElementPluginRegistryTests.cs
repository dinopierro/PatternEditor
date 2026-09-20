using PatternEditor.Abstractions.Plugins;
using PatternEditor.Element.Line;
using PatternEditor.Element.Rect;
using Xunit;

namespace PatternEditor.Abstractions.Tests;

/// <summary>
/// Verifica il registro dei plugin.
///
/// <para>
/// Due comportamenti sono deliberati e vanno protetti da chi in futuro potrebbe trovarli
/// scomodi: registrare due plugin con lo stesso identificativo **deve** fallire subito
/// all'avvio, mentre chiedere un tipo sconosciuto **non deve** fallire affatto. Il primo è
/// un errore di configurazione, il secondo è il caso normale di un documento più ricco di
/// questa installazione.
/// </para>
/// </summary>
public class VectorElementPluginRegistryTests
{
    [Fact]
    public void Registered_plugin_can_be_resolved_by_type()
    {
        var registry = new VectorElementPluginRegistry();
        registry.Register(new RectPlugin());

        var found = registry.TryGet("rect", out var plugin);

        Assert.True(found);
        Assert.IsType<RectPlugin>(plugin);
    }

    [Fact]
    public void Unknown_type_is_not_found()
    {
        var registry = new VectorElementPluginRegistry();
        registry.Register(new RectPlugin());

        Assert.False(registry.TryGet("tipo-senza-plugin", out _));
    }

    [Fact]
    public void Registering_the_same_type_twice_throws()
    {
        var registry = new VectorElementPluginRegistry();
        registry.Register(new RectPlugin());

        Assert.Throws<InvalidOperationException>(() => registry.Register(new RectPlugin()));
    }

    [Fact]
    public void All_returns_every_registered_plugin()
    {
        var registry = new VectorElementPluginRegistry();
        registry.Register(new RectPlugin());
        registry.Register(new LinePlugin());

        Assert.Equal(2, registry.All.Count);
    }
}
