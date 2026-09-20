using PatternEditor.Core.Models;
using Xunit;

namespace PatternEditor.Core.Tests;

/// <summary>
/// Verifica il modello del pattern: identificativo generato, date, e il fatto che un pattern
/// appena creato sia già coerente senza bisogno di essere sistemato dal chiamante.
/// </summary>
public class PatternTests
{
    [Fact]
    public void New_pattern_gets_a_non_empty_id()
    {
        var pattern = new Pattern();

        Assert.NotEqual(Guid.Empty, pattern.Id);
    }

    [Fact]
    public void New_pattern_uses_current_version_by_default()
    {
        var pattern = new Pattern();

        Assert.Equal(Pattern.CurrentVersion, pattern.Version);
    }

    [Fact]
    public void Two_new_patterns_get_different_ids()
    {
        var a = new Pattern();
        var b = new Pattern();

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Reconstruction_constructor_preserves_the_given_id()
    {
        var id = Guid.CreateVersion7();
        var definition = new PatternDefinition();

        var pattern = new Pattern(id, "Nome", definition);

        Assert.Equal(id, pattern.Id);
        Assert.Equal("Nome", pattern.Name);
        Assert.Same(definition, pattern.Definition);
    }

    [Fact]
    public void Reconstruction_constructor_rejects_empty_id()
    {
        Assert.Throws<ArgumentException>(() => new Pattern(Guid.Empty, "Nome", new PatternDefinition()));
    }

    [Fact]
    public void Reconstruction_constructor_rejects_null_definition()
    {
        Assert.Throws<ArgumentNullException>(() => new Pattern(Guid.CreateVersion7(), "Nome", null!));
    }
}
