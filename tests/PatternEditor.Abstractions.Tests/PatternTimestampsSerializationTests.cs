using System.Text.Json.Nodes;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using Xunit;

namespace PatternEditor.Abstractions.Tests;

/// <summary>
/// Verifica il trattamento delle date nel documento salvato.
///
/// <para>
/// Il caso che conta davvero è il documento **senza** date, scritto prima che esistessero:
/// deve restare leggibile, e la data di creazione va ricavata dall'identificativo UUIDv7,
/// che nei primi 48 bit porta l'istante in cui è stato generato. È un recupero silenzioso,
/// e proprio per questo va verificato: se smettesse di funzionare, nessuno se ne
/// accorgerebbe finché non comparissero pattern «creati» nel 1970.
/// </para>
/// </summary>
public class PatternTimestampsSerializationTests
{
    private static PatternSerializer CreateSerializer() =>
        new(new VectorElementPluginRegistry());

    private const string IdCreatedIn2024 = "018e34a4-8f80-7000-8000-000000000001";

    [Fact]
    public void The_dates_survive_a_serialization_round_trip()
    {
        var serializer = CreateSerializer();
        var pattern = new Pattern { Name = "Prova" };
        pattern.CreatedAt = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        pattern.ModifiedAt = new DateTimeOffset(2025, 6, 7, 8, 9, 10, TimeSpan.Zero);

        var roundTripped = serializer.Deserialize(serializer.Serialize(pattern));

        Assert.Equal(pattern.CreatedAt, roundTripped.CreatedAt);
        Assert.Equal(pattern.ModifiedAt, roundTripped.ModifiedAt);
    }

    [Fact]
    public void The_dates_are_written_in_utc_whatever_the_offset_of_the_value()
    {
        var serializer = CreateSerializer();
        var pattern = new Pattern();
        pattern.CreatedAt = new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.FromHours(2));
        pattern.ModifiedAt = pattern.CreatedAt;

        var root = JsonNode.Parse(serializer.Serialize(pattern))!.AsObject();

        Assert.Equal("2024-01-02T10:00:00.0000000+00:00", root["createdAt"]!.GetValue<string>());
    }

    [Fact]
    public void A_document_without_dates_recovers_the_creation_date_from_the_uuid_v7()
    {
        // Formato precedente all'introduzione delle date: nessun campo createdAt/modifiedAt.
        var serializer = CreateSerializer();
        var json = $$"""
        {
          "version": 1,
          "id": "{{IdCreatedIn2024}}",
          "name": "Vecchio",
          "definition": { "width": 50, "height": 50, "elements": [] }
        }
        """;

        var pattern = serializer.Deserialize(json);

        Assert.True(Uuid7.TryGetCreationTime(pattern.Id, out var fromId));
        Assert.Equal(fromId, pattern.CreatedAt);

        // L'ultima modifica non è ricavabile da nulla: eguaglia la creazione.
        Assert.Equal(pattern.CreatedAt, pattern.ModifiedAt);
    }

    [Fact]
    public void A_document_with_only_the_creation_date_keeps_it_and_derives_the_other()
    {
        var serializer = CreateSerializer();
        var json = $$"""
        {
          "version": 1,
          "id": "{{IdCreatedIn2024}}",
          "name": "Parziale",
          "createdAt": "2020-05-04T10:00:00.0000000+00:00",
          "definition": { "width": 50, "height": 50, "elements": [] }
        }
        """;

        var pattern = serializer.Deserialize(json);

        Assert.Equal(new DateTimeOffset(2020, 5, 4, 10, 0, 0, TimeSpan.Zero), pattern.CreatedAt);
        Assert.Equal(pattern.CreatedAt, pattern.ModifiedAt);
    }

    [Fact]
    public void A_malformed_date_does_not_make_the_whole_document_unreadable()
    {
        var serializer = CreateSerializer();
        var json = $$"""
        {
          "version": 1,
          "id": "{{IdCreatedIn2024}}",
          "name": "Data rotta",
          "createdAt": "non e' una data",
          "definition": { "width": 50, "height": 50, "elements": [] }
        }
        """;

        var pattern = serializer.Deserialize(json);

        Assert.Equal("Data rotta", pattern.Name);
        Assert.True(Uuid7.TryGetCreationTime(pattern.Id, out var fromId));
        Assert.Equal(fromId, pattern.CreatedAt);
    }
}
