using PatternEditor.Abstractions.Serialization;
using PatternEditor.Abstractions.Tests;
using PatternEditor.Core.Models;
using PatternEditor.Element.Rect;
using PatternEditor.Services;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// La trasformazione del singolo elemento: rotazione e specchiature.
///
/// <para>
/// Vive sulla classe base e non nei modelli dei plugin perché ruotare non è una proprietà
/// del rettangolo o del cerchio: è un'operazione che vale per qualunque forma. Il plugin
/// continua a produrre il proprio tag senza saperne niente, e l'involucro lo mette il
/// renderer — una volta sola, invece di nove volte con la decima dimenticata.
/// </para>
/// </summary>
public class ElementTransformTests
{
    private static PatternSvgRenderer Renderer() => new(AllPlugins.Registry(), new SvgFilterRenderer());

    private static Pattern ConUnRettangolo(Action<RectElement>? regola = null)
    {
        var pattern = new Pattern { Name = "Prova" };
        pattern.Definition.Width = 100;
        pattern.Definition.Height = 60;

        var rect = RectElement.CreateDefault();
        regola?.Invoke(rect);
        pattern.Definition.Elements.Add(rect);
        return pattern;
    }

    [Fact]
    public void An_element_without_transform_is_not_wrapped_in_anything()
    {
        // Il documento non deve portarsi dietro involucri che non fanno niente: un <g> con
        // una trasformazione identica è rumore in ogni file salvato e in ogni anteprima.
        var markup = Renderer().RenderElementsMarkup(ConUnRettangolo());

        Assert.DoesNotContain("<g", markup);
        Assert.StartsWith("  <rect", markup);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(360)]
    [InlineData(-720)]
    public void A_rotation_that_amounts_to_nothing_is_not_written(double gradi)
    {
        var markup = Renderer().RenderElementsMarkup(ConUnRettangolo(r => r.Rotation = gradi));

        Assert.DoesNotContain("<g", markup);
    }

    [Fact]
    public void A_rotation_wraps_the_shape_the_plugin_produced()
    {
        var markup = Renderer().RenderElementsMarkup(ConUnRettangolo(r => r.Rotation = 45));

        Assert.Contains("<g transform=\"rotate(45)\">", markup);
        Assert.Contains("<rect", markup);
        Assert.Contains("</g>", markup);
    }

    [Fact]
    public void The_order_of_the_transform_brings_the_origin_back_where_it_was()
    {
        // È l'ordine che decide attorno a che cosa si ruota. Scritto al contrario,
        // l'elemento girerebbe attorno all'angolo della cella invece che attorno al punto
        // scelto, e il disegno finirebbe altrove.
        var markup = Renderer().RenderElementsMarkup(ConUnRettangolo(r =>
        {
            r.Rotation = 90;
            r.OriginX = 50;
            r.OriginY = 30;
        }));

        Assert.Contains("transform=\"translate(50,30) rotate(90) translate(-50,-30)\"", markup);
    }

    [Fact]
    public void Mirroring_is_a_negative_scale_around_the_same_point()
    {
        var markup = Renderer().RenderElementsMarkup(ConUnRettangolo(r =>
        {
            r.FlipX = true;
            r.OriginX = 25;
        }));

        Assert.Contains("transform=\"translate(25,0) scale(-1,1) translate(-25,0)\"", markup);
    }

    [Fact]
    public void Rotation_and_mirroring_live_together_in_one_transform()
    {
        var markup = Renderer().RenderElementsMarkup(ConUnRettangolo(r =>
        {
            r.Rotation = 30;
            r.FlipY = true;
        }));

        Assert.Contains("transform=\"rotate(30) scale(1,-1)\"", markup);
    }

    [Fact]
    public void The_transform_is_written_with_the_dot_whatever_the_language()
    {
        var markup = Renderer().RenderElementsMarkup(ConUnRettangolo(r =>
        {
            r.Rotation = 22.5;
            r.OriginX = 12.5;
        }));

        // In SVG la virgola separa due numeri: «22,5» non è un numero con la virgola, sono
        // due numeri, e la forma finirebbe da tutt'altra parte.
        Assert.Contains("rotate(22.5)", markup);
        Assert.Contains("translate(12.5,0)", markup);
    }

    [Fact]
    public void An_element_of_any_type_can_be_rotated()
    {
        // Nessun plugin sa di poter essere ruotato: la prova è che vale per tutti e nove
        // senza che nessuno di loro abbia una riga in proposito.
        var pattern = new Pattern { Name = "Tutti" };
        pattern.Definition.Width = 100;
        pattern.Definition.Height = 100;

        foreach (var plugin in AllPlugins.Instances)
        {
            // Dai valori predefiniti e non da un elemento popolato: lì anche le specchiature
            // sono accese, e il confronto guarderebbe una trasformazione diversa.
            var elemento = plugin.Create();
            elemento.Rotation = 15;
            pattern.Definition.Elements.Add(elemento);
        }

        var markup = Renderer().RenderElementsMarkup(pattern);

        Assert.Equal(AllPlugins.Instances.Count,
                     markup.Split("<g transform=\"rotate(15)\">").Length - 1);
    }

    [Fact]
    public void The_transform_survives_saving_and_reopening()
    {
        var serializer = new PatternSerializer(AllPlugins.Registry());
        var pattern = ConUnRettangolo(r =>
        {
            r.Rotation = 33.5;
            r.FlipX = true;
            r.FlipY = true;
            r.OriginX = 7;
            r.OriginY = 9;
        });

        var riletto = serializer.Deserialize(serializer.Serialize(pattern));
        var elemento = Assert.Single(riletto.Definition.Elements);

        Assert.Equal(33.5, elemento.Rotation);
        Assert.True(elemento.FlipX);
        Assert.True(elemento.FlipY);
        Assert.Equal(7, elemento.OriginX);
        Assert.Equal(9, elemento.OriginY);
    }

    [Fact]
    public void A_document_written_before_this_existed_opens_with_no_transform()
    {
        // Nessun pattern salvato finora ha questi campi: devono valere come «nessuna
        // trasformazione», altrimenti quattrocento pattern cambierebbero aspetto tutti
        // insieme alla prima apertura.
        var serializer = new PatternSerializer(AllPlugins.Registry());

        var riletto = serializer.Deserialize("""
            {"version":1,"id":"01a09078-34bf-7a2d-a18f-b9b8434b370a","name":"Vecchio",
             "definition":{"width":50,"height":50,"elements":[
               {"type":"rect","id":"01a09078-34bf-7a2d-a18f-b9b8434b371b",
                "x":0,"y":0,"width":50,"height":50,"fill":"#9f2828",
                "fillOpacity":1,"strokeWidth":0,"strokeOpacity":1,"opacity":1}]}}
            """);

        var elemento = Assert.Single(riletto.Definition.Elements);
        Assert.False(elemento.HasTransform);
        Assert.DoesNotContain("<g", Renderer().RenderElementsMarkup(riletto));
    }

    [Fact]
    public void A_copy_keeps_the_transform_of_the_element_it_comes_from()
    {
        var serializer = new PatternSerializer(AllPlugins.Registry());
        var pattern = ConUnRettangolo(r =>
        {
            r.Rotation = 45;
            r.FlipX = true;
            r.OriginX = 11;
        });

        var copia = new VectorElementCloner(serializer).Clone(pattern.Definition.Elements[0]);

        Assert.Equal(45, copia.Rotation);
        Assert.True(copia.FlipX);
        Assert.Equal(11, copia.OriginX);
        Assert.NotEqual(pattern.Definition.Elements[0].Id, copia.Id);
    }

    [Fact]
    public void The_single_cell_and_the_repetition_show_the_same_transformed_shape()
    {
        // Le due anteprime e il file scaricato passano dallo stesso codice: se la
        // trasformazione comparisse in una sola, il disegno confermato non sarebbe quello
        // guardato.
        var renderer = Renderer();
        var pattern = ConUnRettangolo(r => r.Rotation = 20);

        var elementi = renderer.RenderElementsMarkup(pattern).Trim();

        Assert.Contains(elementi, renderer.RenderSingleCellSvg(pattern, 200));
        Assert.Contains(elementi, renderer.RenderStandaloneSvg(pattern));
    }
}
