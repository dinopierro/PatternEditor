using System.Text.Json;
using System.Text.Json.Nodes;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using PatternEditor.Core.Models.Filters;
using Xunit;

namespace PatternEditor.Abstractions.Tests;

/// <summary>
/// Verifica che il filtro entri ed esca dal documento senza perdere niente.
///
/// <para>
/// Qui il serializzatore fa un lavoro in più rispetto al suo: l'editor tiene la copia di
/// lavoro e la cronologia di annulla/ripeti come documenti JSON, e ogni passaggio di
/// annullamento è un giro completo di scrittura e rilettura. Un campo che non sopravvive al
/// giro non si perde al salvataggio — si perde premendo Ctrl+Z.
/// </para>
/// </summary>
public class PatternFilterSerializationTests
{
    private static PatternSerializer Serializzatore() => new(AllPlugins.Registry());

    private static Pattern ConFiltro(Action<PatternFilter> regola)
    {
        var pattern = new Pattern { Name = "Prova" };
        pattern.Definition.Width = 40;
        pattern.Definition.Height = 40;
        pattern.Definition.Filter = new PatternFilter();
        regola(pattern.Definition.Filter);
        return pattern;
    }

    /// <summary>
    /// Un pattern senza filtro non deve portarsi dietro una sezione di valori predefiniti:
    /// i quattrocento documenti già in archivio devono riscriversi come prima.
    /// </summary>
    [Fact]
    public void SenzaFiltroIlDocumentoNonHaIlCampo()
    {
        var pattern = new Pattern { Name = "Senza" };
        pattern.Definition.Width = 10;
        pattern.Definition.Height = 10;

        var json = Serializzatore().Serialize(pattern);
        var definizione = JsonNode.Parse(json)!["definition"]!.AsObject();

        Assert.False(definizione.ContainsKey("filter"));
    }

    [Fact]
    public void IlFiltroSopravviveAlGiroCompleto()
    {
        var originale = ConFiltro(f =>
        {
            f.ColorSpace = FilterColorSpace.LinearRgb;
            f.X = -20;
            f.Width = 150;
            f.Primitives.Add(new GaussianBlurPrimitive { StdDeviationX = 3, StdDeviationY = 4, Result = "sfocato" });
            f.Primitives.Add(new BlendPrimitive { In = "sfocato", In2 = "SourceGraphic", Mode = BlendMode.HardLight });
        });

        var riletto = Serializzatore().Deserialize(Serializzatore().Serialize(originale));
        var filtro = riletto.Definition.Filter;

        Assert.NotNull(filtro);
        Assert.Equal(FilterColorSpace.LinearRgb, filtro!.ColorSpace);
        Assert.Equal(-20, filtro.X);
        Assert.Equal(150, filtro.Width);

        var sfocatura = Assert.IsType<GaussianBlurPrimitive>(filtro.Primitives[0]);
        Assert.Equal(3, sfocatura.StdDeviationX);
        Assert.Equal(4, sfocatura.StdDeviationY);
        Assert.Equal("sfocato", sfocatura.Result);

        var fusione = Assert.IsType<BlendPrimitive>(filtro.Primitives[1]);
        Assert.Equal(BlendMode.HardLight, fusione.Mode);
        Assert.Equal("SourceGraphic", fusione.In2);
    }

    [Fact]
    public void LeCurveDeiCanaliSopravvivonoUnaPerUna()
    {
        var livelli = new ComponentTransferPrimitive();
        livelli.R.Kind = TransferFunctionKind.Linear;
        livelli.R.Slope = 1.4;
        livelli.R.Intercept = -0.2;
        livelli.A.Kind = TransferFunctionKind.Table;
        livelli.A.TableValues = [0, 0.5, 1];

        var riletto = Serializzatore().Deserialize(
            Serializzatore().Serialize(ConFiltro(f => f.Primitives.Add(livelli))));

        var letta = Assert.IsType<ComponentTransferPrimitive>(riletto.Definition.Filter!.Primitives[0]);

        Assert.Equal(TransferFunctionKind.Linear, letta.R.Kind);
        Assert.Equal(1.4, letta.R.Slope);
        Assert.Equal(-0.2, letta.R.Intercept);
        Assert.Equal(TransferFunctionKind.Identity, letta.G.Kind);
        Assert.Equal([0, 0.5, 1], letta.A.TableValues);
    }

    /// <summary>
    /// Le enumerazioni si scrivono per nome. Scritte per numero dipenderebbero dall'ordine di
    /// dichiarazione, e inserire una voce in mezzo — cosa che capiterà, perché qui seguono
    /// l'ordine della specifica — cambierebbe il significato dei documenti già salvati senza
    /// alcun errore: un «moltiplica» si riaprirebbe come «scolora».
    /// </summary>
    [Fact]
    public void LeEnumerazioniSiScrivonoPerNome()
    {
        var json = Serializzatore().Serialize(
            ConFiltro(f => f.Primitives.Add(new BlendPrimitive { Mode = BlendMode.ColorBurn })));

        Assert.Contains("\"mode\": \"colorBurn\"", json);
    }

    /// <summary>
    /// Nel documento finiscono i <b>dati</b>, non ciò che il componente ne ricava.
    ///
    /// <para>
    /// Etichette, riassunti e nomi di nodo sono proprietà calcolate, marcate perché non
    /// vengano serializzate. L’attributo però <b>non segue l’override</b>: le classi concrete
    /// ridefiniscono quei membri, e senza marcarli di nuovo finiscono nel file. È successo, e
    /// il documento di un pattern si è ritrovato tre campi in più per ogni passaggio —
    /// innocui alla rilettura, ma scritti su disco e destinati a invecchiare male il giorno
    /// in cui un’etichetta cambia.
    /// </para>
    /// </summary>
    [Fact]
    public void NelDocumentoNonFinisconoLeProprietaCalcolate()
    {
        var pattern = ConFiltro(f =>
        {
            foreach (var preset in FilterPresets.Tutti)
            {
                foreach (var passo in preset.Passaggi())
                {
                    f.Primitives.Add(passo);
                }
            }
        });

        var json = Serializzatore().Serialize(pattern);

        foreach (var calcolata in new[] { "svgName", "etichetta", "riassunto", "sconfina", "genera", "haSecondoIngresso", "lato", "produceEffetto", "qualcunoSconfina", "eLIdentita" })
        {
            Assert.DoesNotContain($"\"{calcolata}\":", json);
        }
    }

    [Fact]
    public void IlTipoNelJsonEIlNomeDelNodoSvg()
    {
        var json = Serializzatore().Serialize(
            ConFiltro(f => f.Primitives.Add(new GaussianBlurPrimitive())));

        Assert.Contains("\"type\": \"feGaussianBlur\"", json);
    }

    /// <summary>
    /// Un passaggio scritto da una versione con più primitive non fa fallire la lettura e non
    /// viene buttato: si conserva com'era e si riscrive identico. È la stessa regola degli
    /// elementi vettoriali di tipo ignoto, e serve alla stessa cosa — aprire un documento su
    /// un'installazione più vecchia e risalvarlo non deve impoverirlo in silenzio.
    /// </summary>
    [Fact]
    public void UnPassaggioIgnotoSiConservaIntatto()
    {
        var json = """
        {
          "version": 1,
          "id": "0192f000-0000-7000-8000-000000000001",
          "name": "Dal futuro",
          "definition": {
            "width": 10,
            "height": 10,
            "scale": 1,
            "rotation": 0,
            "translateX": 0,
            "translateY": 0,
            "filter": {
              "enabled": true,
              "colorSpace": "sRGB",
              "x": -10, "y": -10, "width": 120, "height": 120,
              "primitives": [
                { "type": "feDiffuseLighting", "surfaceScale": 3, "lightingColor": "#fff" }
              ]
            },
            "elements": []
          }
        }
        """;

        var riletto = Serializzatore().Deserialize(json);
        var ignota = Assert.IsType<UnknownFilterPrimitive>(riletto.Definition.Filter!.Primitives[0]);

        Assert.Equal("feDiffuseLighting", ignota.Tipo);

        var riscritto = Serializzatore().Serialize(riletto);
        var primitiva = JsonNode.Parse(riscritto)!["definition"]!["filter"]!["primitives"]![0]!.AsObject();

        Assert.Equal("feDiffuseLighting", primitiva["type"]!.GetValue<string>());
        Assert.Equal(3, primitiva["surfaceScale"]!.GetValue<double>());
        Assert.Equal("#fff", primitiva["lightingColor"]!.GetValue<string>());
    }

    /// <summary>
    /// Tipo conosciuto ma contenuto illeggibile: si conserva allo stesso modo invece di
    /// rendere illeggibile l'intero pattern. Un disegno resta valido anche quando una sua
    /// rifinitura non lo è.
    /// </summary>
    [Fact]
    public void UnValoreIncomprensibileNonRendeIlleggibileIlPattern()
    {
        var json = """
        {
          "version": 1,
          "id": "0192f000-0000-7000-8000-000000000002",
          "name": "Storto",
          "definition": {
            "width": 10, "height": 10, "scale": 1, "rotation": 0,
            "translateX": 0, "translateY": 0,
            "filter": {
              "enabled": true,
              "primitives": [ { "type": "feBlend", "mode": "arcobaleno" } ]
            },
            "elements": []
          }
        }
        """;

        var riletto = Serializzatore().Deserialize(json);

        Assert.Equal("Storto", riletto.Name);
        Assert.IsType<UnknownFilterPrimitive>(riletto.Definition.Filter!.Primitives[0]);
    }

    [Fact]
    public void UnDocumentoSenzaLaSezioneFiltroSiLeggeComePrima()
    {
        var json = """
        {
          "version": 1,
          "id": "0192f000-0000-7000-8000-000000000003",
          "name": "Vecchio",
          "definition": {
            "width": 10, "height": 10, "scale": 1, "rotation": 0,
            "translateX": 0, "translateY": 0,
            "elements": []
          }
        }
        """;

        Assert.Null(Serializzatore().Deserialize(json).Definition.Filter);
    }

    /// <summary>
    /// Ogni effetto pronto deve sopravvivere al giro: sono il punto di partenza consigliato,
    /// e uno che si sfasciasse al primo annulla sarebbe peggio di non averlo.
    /// </summary>
    [Fact]
    public void OgniEffettoProntoSopravviveAlGiro()
    {
        foreach (var preset in FilterPresets.Tutti)
        {
            var pattern = ConFiltro(f =>
            {
                foreach (var passo in preset.Passaggi())
                {
                    f.Primitives.Add(passo);
                }
            });

            var prima = Serializzatore().Serialize(pattern);
            var dopo = Serializzatore().Serialize(Serializzatore().Deserialize(prima));

            Assert.Equal(prima, dopo);
            Assert.DoesNotContain(
                Serializzatore().Deserialize(prima).Definition.Filter!.Primitives,
                p => p is UnknownFilterPrimitive);
        }
    }

    /// <summary>
    /// Il documento resta leggibile come JSON anche con un filtro dentro: sembra ovvio, ed è
    /// il controllo che scopre una virgola di troppo prima di chiunque altro.
    /// </summary>
    [Fact]
    public void IlDocumentoConFiltroRestaJsonValido()
    {
        var json = Serializzatore().Serialize(ConFiltro(f =>
        {
            f.Primitives.Add(new ColorMatrixPrimitive { Kind = ColorMatrixKind.Matrix });
            f.Primitives.Add(new MergePrimitive());
        }));

        var documento = JsonDocument.Parse(json);

        Assert.Equal(
            2,
            documento.RootElement.GetProperty("definition").GetProperty("filter")
                .GetProperty("primitives").GetArrayLength());
    }
}
