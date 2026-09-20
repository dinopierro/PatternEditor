using System.Text.Json.Nodes;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;
using Xunit;

namespace PatternEditor.Abstractions.Tests;

/// <summary>
/// La visibilità entra ed esce dal documento.
///
/// <para>
/// Un campo che non sopravvive al giro di scrittura e rilettura non si perde soltanto al
/// salvataggio: l'editor tiene la copia di lavoro e la cronologia di annulla/ripeti come
/// documenti JSON, e ogni annullamento è un giro completo. Un pattern che tornasse privato
/// premendo Ctrl+Z sarebbe un difetto difficile da attribuire.
/// </para>
/// </summary>
public class PatternVisibilitySerializationTests
{
    private static PatternSerializer Serializzatore() => new(AllPlugins.Registry());

    private static Pattern Campione(PatternVisibility visibilita)
    {
        var pattern = new Pattern { Name = "Prova", Visibility = visibilita };
        pattern.Definition.Width = 20;
        pattern.Definition.Height = 20;
        return pattern;
    }

    [Fact]
    public void UnPatternNuovoNascePrivato()
    {
        // Il valore predefinito è la metà del sistema di approvazione: se nascesse pubblico,
        // dimenticarsi di scegliere basterebbe a pubblicare.
        Assert.Equal(PatternVisibility.Privata, new Pattern().Visibility);
    }

    [Theory]
    [InlineData(PatternVisibility.Privata)]
    [InlineData(PatternVisibility.InAttesa)]
    [InlineData(PatternVisibility.Pubblica)]
    public void LoStatoSopravviveAlGiroCompleto(PatternVisibility visibilita)
    {
        var riletto = Serializzatore().Deserialize(Serializzatore().Serialize(Campione(visibilita)));

        Assert.Equal(visibilita, riletto.Visibility);
    }

    /// <summary>
    /// Per nome e non per numero, come tutte le altre enumerazioni: scritto per numero,
    /// inserire una voce in mezzo — e in mezzo ce ne sarebbe posto, fra privato e pubblico —
    /// cambierebbe il significato di ogni documento già salvato senza alcun errore.
    /// </summary>
    [Fact]
    public void LoStatoSiScrivePerNome()
    {
        var json = Serializzatore().Serialize(Campione(PatternVisibility.InAttesa));

        Assert.Contains("\"visibility\": \"InAttesa\"", json);
    }

    [Fact]
    public void IlCampoCEAnchePerLoStatoPredefinito()
    {
        // Si scrive sempre, perché è la sua assenza a significare qualcosa: un documento che
        // lo omettesse «tanto è il valore normale» verrebbe riletto come pubblico.
        var documento = JsonNode.Parse(Serializzatore().Serialize(Campione(PatternVisibility.Privata)))!;

        Assert.Equal("Privata", documento["visibility"]!.GetValue<string>());
    }

    /// <summary>
    /// Un documento più vecchio del concetto di visibilità si rilegge come pubblico.
    ///
    /// <para>
    /// È un'asimmetria voluta rispetto al valore predefinito del modello, e vale la pena
    /// dirne la ragione: prima che il concetto esistesse, ogni pattern si vedeva da chiunque
    /// — leggerlo come pubblico è dire la verità su ciò che quel documento è stato finora.
    /// Il contrario, privato per prudenza, farebbe sparire dal portale un archivio intero
    /// senza che nessuno abbia chiesto niente.
    /// </para>
    /// </summary>
    [Fact]
    public void UnDocumentoSenzaIlCampoSiLeggeComePubblico()
    {
        var json = """
        {
          "version": 1,
          "id": "0192f000-0000-7000-8000-000000000010",
          "name": "Più vecchio del concetto",
          "definition": {
            "width": 10, "height": 10, "scale": 1, "rotation": 0,
            "translateX": 0, "translateY": 0,
            "elements": []
          }
        }
        """;

        Assert.Equal(PatternVisibility.Pubblica, Serializzatore().Deserialize(json).Visibility);
    }

    /// <summary>
    /// Uno stato che questa versione non conosce vale come «pubblico» allo stesso modo: è un
    /// documento che non sappiamo leggere, non un documento da nascondere, e l'ultima parola
    /// su chi lo vede ce l'ha comunque il server.
    /// </summary>
    [Fact]
    public void UnoStatoIncomprensibileNonRendeIlleggibileIlPattern()
    {
        var json = """
        {
          "version": 1,
          "id": "0192f000-0000-7000-8000-000000000011",
          "name": "Dal futuro",
          "visibility": "InRevisioneDaDue",
          "definition": {
            "width": 10, "height": 10, "scale": 1, "rotation": 0,
            "translateX": 0, "translateY": 0,
            "elements": []
          }
        }
        """;

        var riletto = Serializzatore().Deserialize(json);

        Assert.Equal("Dal futuro", riletto.Name);
        Assert.Equal(PatternVisibility.Pubblica, riletto.Visibility);
    }

    [Fact]
    public void IlNomeDelloStatoSiLeggeComunqueSiaScritto()
    {
        // Le maiuscole di un campo scritto a mano non devono far perdere un'informazione.
        var json = """
        {
          "version": 1,
          "id": "0192f000-0000-7000-8000-000000000012",
          "name": "Scritto a mano",
          "visibility": "privata",
          "definition": {
            "width": 10, "height": 10, "scale": 1, "rotation": 0,
            "translateX": 0, "translateY": 0,
            "elements": []
          }
        }
        """;

        Assert.Equal(PatternVisibility.Privata, Serializzatore().Deserialize(json).Visibility);
    }
}
