using PatternEditor.Abstractions.Plugins;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using PatternEditor.Element.Rect;
using PatternEditor.Services;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// Verifica la duplicazione di un pattern intero.
///
/// <para>
/// I casi che contano sono tre, e sono tutti modi in cui una copia può sembrare giusta e non
/// esserlo: identificativi riciclati (due pattern indistinguibili nell'archivio), elementi
/// con gli stessi identificativi dell'originale, e — il più insidioso — una definizione
/// condivisa, che fa sì che modificare la copia cambi anche l'originale. Quest'ultimo
/// difetto non si vedrebbe finché qualcuno non apre la copia, la modifica e si ritrova
/// l'originale cambiato.
/// </para>
/// </summary>
public class PatternClonerTests
{
    private static PatternCloner CreaCloner()
    {
        var registry = new VectorElementPluginRegistry();
        registry.Register(new RectPlugin());
        return new PatternCloner(new VectorElementCloner(new PatternSerializer(registry)));
    }

    private static Pattern CreaOriginale()
    {
        var pattern = new Pattern { Name = "Mattoni" };
        pattern.Definition.Width = 120;
        pattern.Definition.Height = 80;
        pattern.Definition.Scale = 1.35;
        pattern.Definition.Rotation = 45;
        pattern.Definition.TranslateX = 25;
        pattern.Definition.TranslateY = -10;
        pattern.Definition.Elements.Add(RectElement.CreateDefault());
        pattern.Definition.Elements.Add(RectElement.CreateDefault());
        return pattern;
    }

    [Fact]
    public void The_copy_gets_a_new_identifier()
    {
        var originale = CreaOriginale();

        var copia = CreaCloner().Clone(originale, "Copia di Mattoni");

        Assert.NotEqual(originale.Id, copia.Id);
        Assert.NotEqual(Guid.Empty, copia.Id);
    }

    [Fact]
    public void Every_element_of_the_copy_gets_a_new_identifier()
    {
        var originale = CreaOriginale();

        var copia = CreaCloner().Clone(originale, "Copia");

        var idOriginali = originale.Definition.Elements.Select(e => e.Id).ToList();
        var idCopiati = copia.Definition.Elements.Select(e => e.Id).ToList();

        Assert.Empty(idCopiati.Intersect(idOriginali));
        Assert.Equal(idCopiati.Count, idCopiati.Distinct().Count());
    }

    [Fact]
    public void The_copy_keeps_name_geometry_and_order()
    {
        var originale = CreaOriginale();
        originale.Definition.Elements[0] = new RectElement(
            Guid.CreateVersion7(), x: 3, y: 4, width: 5, height: 6,
            fill: "#AABBCC", fillOpacity: 0.5, stroke: "#112233", strokeWidth: 2,
            strokeOpacity: 0.25, opacity: 0.75);

        var copia = CreaCloner().Clone(originale, "Copia di Mattoni");

        Assert.Equal("Copia di Mattoni", copia.Name);
        Assert.Equal(120, copia.Definition.Width);
        Assert.Equal(80, copia.Definition.Height);
        Assert.Equal(1.35, copia.Definition.Scale);
        Assert.Equal(45, copia.Definition.Rotation);
        Assert.Equal(25, copia.Definition.TranslateX);
        Assert.Equal(-10, copia.Definition.TranslateY);
        Assert.Equal(2, copia.Definition.Elements.Count);

        var primo = Assert.IsType<RectElement>(copia.Definition.Elements[0]);
        Assert.Equal(3, primo.X);
        Assert.Equal("#AABBCC", primo.Fill);
        Assert.Equal(0.5, primo.FillOpacity);
        Assert.Equal(2, primo.StrokeWidth);
        Assert.Equal(0.75, primo.Opacity);
    }

    [Fact]
    public void Changing_the_copy_leaves_the_original_alone()
    {
        // È il difetto che si paga più caro: riusare l'istanza di Definition invece di
        // copiarne i valori farebbe condividere gli stessi oggetti fra originale e copia.
        var originale = CreaOriginale();

        var copia = CreaCloner().Clone(originale, "Copia");
        copia.Definition.Width = 999;
        ((RectElement)copia.Definition.Elements[0]).X = 42;
        copia.Definition.Elements.RemoveAt(1);

        Assert.Equal(120, originale.Definition.Width);
        Assert.Equal(2, originale.Definition.Elements.Count);
        Assert.NotEqual(42, ((RectElement)originale.Definition.Elements[0]).X);
    }

    [Fact]
    public void The_copy_does_not_inherit_the_dates_of_the_original()
    {
        // Le date della copia le assegna il livello di persistenza alla creazione: ereditarle
        // produrrebbe un pattern che dichiara di esistere da prima di essere stato creato.
        var originale = CreaOriginale();
        originale.CreatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        originale.ModifiedAt = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var copia = CreaCloner().Clone(originale, "Copia");

        Assert.NotEqual(originale.CreatedAt, copia.CreatedAt);
        Assert.True(copia.CreatedAt > originale.ModifiedAt);
    }

    [Fact]
    public void Elements_of_unknown_type_survive_the_copy()
    {
        // Un pattern scritto da un'installazione con più plugin deve poter essere duplicato
        // senza perdere per strada quello che questa installazione non sa modificare.
        var id = Guid.CreateVersion7();
        var grezzo = System.Text.Json.JsonDocument.Parse(
            $$"""{"type":"stella","id":"{{id}}","punte":5}""").RootElement;

        var originale = new Pattern { Name = "Esotico" };
        originale.Definition.Elements.Add(new UnknownVectorElement(id, "stella", grezzo));

        var copia = CreaCloner().Clone(originale, "Copia di Esotico");

        var elemento = Assert.IsType<UnknownVectorElement>(copia.Definition.Elements.Single());
        Assert.Equal("stella", elemento.Type);
        Assert.Equal(5, elemento.RawJson.GetProperty("punte").GetInt32());
    }
    [Fact]
    public void A_copy_does_not_inherit_the_author_of_the_original()
    {
        // La copia è un documento nuovo, e chi la fa non eredita la paternità di chi l'ha
        // disegnata: sarebbe una firma altrui su un file che quell'altro non ha mai visto.
        var originale = new Pattern
        {
            Name = "Originale",
            AuthorId = Guid.CreateVersion7(),
            AuthorName = "mario",
        };

        var copia = CreaCloner().Clone(originale, "Copia di Originale");

        Assert.Null(copia.AuthorId);
        Assert.Null(copia.AuthorName);
    }

    [Fact]
    public void A_copy_does_not_inherit_the_approval_of_the_original()
    {
        // Sarebbe il modo più comodo per aggirare la moderazione: far approvare un disegno e
        // poi duplicarlo cambiandolo. La copia nasce privata come qualunque pattern nuovo.
        var originale = new Pattern { Name = "Approvato", Visibility = PatternVisibility.Pubblica };

        var copia = CreaCloner().Clone(originale, "Copia di Approvato");

        Assert.Equal(PatternVisibility.Privata, copia.Visibility);
    }
}
