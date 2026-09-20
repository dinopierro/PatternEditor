using PatternEditor.Abstractions.Plugins;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using PatternEditor.Element.Rect;
using PatternEditor.Sample.Api.Persistence;
using Xunit;

namespace PatternEditor.Sample.Api.Tests;

/// <summary>
/// Verifica l'archiviazione su file: creazione, lettura, aggiornamento, eliminazione.
///
/// <para>
/// Ogni istanza della classe di test lavora in una cartella temporanea propria, creata nel
/// costruttore e distrutta alla fine. Non è pignoleria: test che condividono una cartella si
/// disturbano a vicenda quando vengono eseguiti in parallelo, e producono fallimenti che poi
/// non si riescono a riprodurre uno alla volta.
/// </para>
/// </summary>
public sealed class JsonFilePatternRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly JsonFilePatternRepository _repository;

    public JsonFilePatternRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "pattern-editor-tests-" + Guid.NewGuid());
        var registry = new VectorElementPluginRegistry();
        registry.Register(new RectPlugin());
        _repository = new JsonFilePatternRepository(_tempDirectory, new PatternSerializer(registry));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static Pattern CreateSamplePattern(string name = "Mattone")
    {
        var pattern = new Pattern { Name = name };
        pattern.Definition.Width = 50;
        pattern.Definition.Height = 50;
        pattern.Definition.Elements.Add(RectElement.CreateDefault());
        return pattern;
    }

    [Fact]
    public async Task Create_writes_a_file_named_after_the_pattern_id()
    {
        var pattern = CreateSamplePattern();

        var created = await _repository.CreateAsync(pattern);

        Assert.True(created);
        Assert.True(File.Exists(Path.Combine(_tempDirectory, $"{pattern.Id}.json")));
    }

    [Fact]
    public async Task Create_fails_when_a_pattern_with_the_same_id_already_exists()
    {
        var pattern = CreateSamplePattern();
        await _repository.CreateAsync(pattern);

        var createdAgain = await _repository.CreateAsync(pattern);

        Assert.False(createdAgain);
    }

    [Fact]
    public async Task GetById_returns_null_for_a_non_existing_pattern()
    {
        var result = await _repository.GetByIdAsync(Guid.CreateVersion7());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_returns_the_previously_created_pattern()
    {
        var pattern = CreateSamplePattern("Isolante");
        await _repository.CreateAsync(pattern);

        var fetched = await _repository.GetByIdAsync(pattern.Id);

        Assert.NotNull(fetched);
        Assert.Equal(pattern.Id, fetched!.Id);
        Assert.Equal("Isolante", fetched.Name);
    }

    [Fact]
    public async Task Update_keeps_the_same_file_name_when_the_name_changes()
    {
        var pattern = CreateSamplePattern("Nome originale");
        await _repository.CreateAsync(pattern);

        pattern.Name = "Nome aggiornato";
        var updated = await _repository.UpdateAsync(pattern);

        Assert.True(updated);
        Assert.True(File.Exists(Path.Combine(_tempDirectory, $"{pattern.Id}.json")));
        var fetched = await _repository.GetByIdAsync(pattern.Id);
        Assert.Equal("Nome aggiornato", fetched!.Name);
    }

    [Fact]
    public async Task Update_fails_for_a_pattern_that_was_never_created()
    {
        var pattern = CreateSamplePattern();

        var updated = await _repository.UpdateAsync(pattern);

        Assert.False(updated);
    }

    [Fact]
    public async Task Delete_removes_only_the_targeted_pattern()
    {
        var a = CreateSamplePattern("A");
        var b = CreateSamplePattern("B");
        await _repository.CreateAsync(a);
        await _repository.CreateAsync(b);

        var deleted = await _repository.DeleteAsync(a.Id);

        Assert.True(deleted);
        Assert.Null(await _repository.GetByIdAsync(a.Id));
        Assert.NotNull(await _repository.GetByIdAsync(b.Id));
    }

    [Fact]
    public async Task Delete_returns_false_for_a_non_existing_pattern()
    {
        var deleted = await _repository.DeleteAsync(Guid.CreateVersion7());

        Assert.False(deleted);
    }

    [Fact]
    public async Task GetAll_returns_patterns_ordered_by_creation_time_uuidv7()
    {
        var first = CreateSamplePattern("Primo");
        await _repository.CreateAsync(first);
        var second = CreateSamplePattern("Secondo");
        await _repository.CreateAsync(second);

        var all = await _repository.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal(first.Id, all[0].Id);
        Assert.Equal(second.Id, all[1].Id);
    }
}
