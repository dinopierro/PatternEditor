using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using PatternEditor.Element.Rect;
using Xunit;

namespace PatternEditor.Abstractions.Tests;

/// <summary>
/// L'autore dentro il documento.
///
/// <para>
/// È un dato che la libreria <b>trasporta</b> senza interpretarlo: non sa che cosa sia un
/// utente, non verifica niente e non impedisce niente. Quello che deve garantire è più
/// modesto e altrettanto necessario: che il campo sopravviva a un giro di scrittura e
/// rilettura, che la sua assenza non rompa nulla, e che un valore illeggibile non renda
/// illeggibile l'intero disegno.
/// </para>
/// </summary>
public class PatternAuthorSerializationTests
{
    private static PatternSerializer Serializer() => new(AllPlugins.Registry());

    private static Pattern Campione()
    {
        var pattern = new Pattern { Name = "Prova" };
        pattern.Definition.Width = 40;
        pattern.Definition.Height = 40;
        pattern.Definition.Elements.Add(RectElement.CreateDefault());
        return pattern;
    }

    [Fact]
    public void The_author_survives_a_write_and_a_read()
    {
        var serializer = Serializer();
        var pattern = Campione();
        pattern.AuthorId = Guid.CreateVersion7();
        pattern.AuthorName = "mario";

        var riletto = serializer.Deserialize(serializer.Serialize(pattern));

        Assert.Equal(pattern.AuthorId, riletto.AuthorId);
        Assert.Equal("mario", riletto.AuthorName);
    }

    [Fact]
    public void A_pattern_with_no_author_is_written_without_inventing_one()
    {
        // Un ospite che di utenti non sa niente non deve trovarsi due campi popolati a caso
        // in cima a ogni documento che scrive.
        var serializer = Serializer();
        var json = serializer.Serialize(Campione());
        var riletto = serializer.Deserialize(json);

        Assert.Null(riletto.AuthorId);
        Assert.Null(riletto.AuthorName);
    }

    [Fact]
    public void A_document_written_before_authors_existed_opens_without_one()
    {
        // Sono i pattern dell'archivio esistente: devono aprirsi, e devono dire con
        // chiarezza che un autore non ce l'hanno.
        var riletto = Serializer().Deserialize("""
            {"version":1,"id":"01a09078-34bf-7a2d-a18f-b9b8434b370a","name":"Vecchio",
             "definition":{"width":50,"height":50,"elements":[]}}
            """);

        Assert.Null(riletto.AuthorId);
        Assert.Null(riletto.AuthorName);
        Assert.Equal("Vecchio", riletto.Name);
    }

    [Theory]
    [InlineData("\"non-un-guid\"")]
    [InlineData("123")]
    [InlineData("null")]
    public void An_unreadable_author_costs_the_author_and_not_the_drawing(string valore)
    {
        // Il disegno è leggibile lo stesso, e chiudere fuori un intero pattern per un campo
        // accessorio sarebbe sproporzionato: si perde la protezione, non il lavoro.
        var riletto = Serializer().Deserialize(
            """
            {"version":1,"id":"01a09078-34bf-7a2d-a18f-b9b8434b370a","name":"Storto",
             "authorId":SEGNAPOSTO,
             "definition":{"width":50,"height":50,"elements":[]}}
            """.Replace("SEGNAPOSTO", valore, StringComparison.Ordinal));

        Assert.Null(riletto.AuthorId);
        Assert.Equal("Storto", riletto.Name);
    }

    [Fact]
    public void The_author_travels_with_the_file_and_not_beside_it()
    {
        // La ragione per cui sta nel documento: esportando o copiando il file, l'autore va
        // con lui. Un indice esterno resterebbe indietro.
        var json = Serializer().Serialize(new Pattern
        {
            Name = "Firmato",
            AuthorId = Guid.Parse("01a09078-34bf-7a2d-a18f-b9b8434b370a"),
            AuthorName = "mario",
        });

        Assert.Contains("01a09078-34bf-7a2d-a18f-b9b8434b370a", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mario", json, StringComparison.Ordinal);
    }
}
