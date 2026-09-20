using PatternEditor.Abstractions.Plugins;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using PatternEditor.Element.Rect;
using PatternEditor.Sample.Api.Persistence;
using Xunit;

namespace PatternEditor.Sample.Api.Tests;

/// <summary>
/// Verifica che le date siano responsabilità del livello di persistenza e di nessun altro.
///
/// <para>
/// Le due regole protette da questi test: alla creazione le due date coincidono; a ogni
/// aggiornamento cambia solo quella di modifica, mentre quella di creazione resta ciò che
/// era anche se il chiamante ne invia un'altra. L'orologio è iniettato, così i test possono
/// far scorrere il tempo senza aspettarlo.
/// </para>
/// </summary>
public sealed class PatternTimestampsRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IPatternSerializer _serializer;
    private DateTimeOffset _now = new(2025, 1, 1, 8, 0, 0, TimeSpan.Zero);

    public PatternTimestampsRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "pattern-editor-tests-" + Guid.NewGuid());
        var registry = new VectorElementPluginRegistry();
        registry.Register(new RectPlugin());
        _serializer = new PatternSerializer(registry);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private JsonFilePatternRepository CreateRepository() =>
        new(_tempDirectory, _serializer, () => _now);

    private static Pattern CreateSamplePattern()
    {
        var pattern = new Pattern { Name = "Prova" };
        pattern.Definition.Width = 50;
        pattern.Definition.Height = 50;
        pattern.Definition.Elements.Add(RectElement.CreateDefault());
        return pattern;
    }

    [Fact]
    public async Task Creating_a_pattern_sets_both_dates_to_the_same_instant()
    {
        var repository = CreateRepository();
        var pattern = CreateSamplePattern();

        await repository.CreateAsync(pattern);
        var stored = await repository.GetByIdAsync(pattern.Id);

        Assert.NotNull(stored);
        Assert.Equal(_now, stored!.CreatedAt);
        Assert.Equal(_now, stored.ModifiedAt);
    }

    [Fact]
    public async Task Updating_a_pattern_moves_only_the_modification_date()
    {
        var repository = CreateRepository();
        var pattern = CreateSamplePattern();
        await repository.CreateAsync(pattern);
        var createdAt = _now;

        _now = createdAt.AddDays(3);
        pattern.Name = "Rinominato";
        await repository.UpdateAsync(pattern);

        var stored = await repository.GetByIdAsync(pattern.Id);

        Assert.NotNull(stored);
        Assert.Equal(createdAt, stored!.CreatedAt);
        Assert.Equal(createdAt.AddDays(3), stored.ModifiedAt);
    }

    [Fact]
    public async Task The_creation_date_of_the_stored_document_cannot_be_overwritten_by_the_client()
    {
        var repository = CreateRepository();
        var pattern = CreateSamplePattern();
        await repository.CreateAsync(pattern);
        var createdAt = _now;

        // Un client che rimandasse indietro una data di creazione diversa non deve
        // poter riscrivere la storia del documento già salvato.
        _now = createdAt.AddHours(1);
        pattern.CreatedAt = new DateTimeOffset(1999, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await repository.UpdateAsync(pattern);

        var stored = await repository.GetByIdAsync(pattern.Id);

        Assert.Equal(createdAt, stored!.CreatedAt);
    }

    [Fact]
    public async Task A_document_saved_before_the_dates_existed_is_read_back_using_its_uuid_v7()
    {
        var repository = CreateRepository();
        var pattern = CreateSamplePattern();

        // Simula un file del formato precedente: le due date non compaiono nel JSON.
        var json = _serializer.Serialize(pattern)
            .Split('\n')
            .Where(line => !line.Contains("\"createdAt\"") && !line.Contains("\"modifiedAt\""))
            .Aggregate((a, b) => a + "\n" + b);
        Directory.CreateDirectory(_tempDirectory);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, $"{pattern.Id}.json"), json);

        var stored = await repository.GetByIdAsync(pattern.Id);

        Assert.NotNull(stored);
        Assert.True(Uuid7.TryGetCreationTime(pattern.Id, out var fromId));
        Assert.Equal(fromId, stored!.CreatedAt);
        Assert.Equal(fromId, stored.ModifiedAt);
    }
}
