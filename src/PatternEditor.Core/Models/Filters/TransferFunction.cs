using System.Text.Json.Serialization;
using PatternEditor.Core.Localization;

namespace PatternEditor.Core.Models.Filters;

/// <summary>
/// La curva con cui un singolo canale viene rimappato: uno dei nodi <c>feFuncR</c>,
/// <c>feFuncG</c>, <c>feFuncB</c>, <c>feFuncA</c> dentro una <c>feComponentTransfer</c>.
///
/// <para>
/// Tutti i campi convivono anche quando la forma scelta ne usa due: provare una curva, non
/// convincersene e tornare alla precedente non deve cancellare i numeri che si erano scritti.
/// </para>
/// </summary>
public sealed class TransferFunction
{
    public TransferFunctionKind Kind { get; set; } = TransferFunctionKind.Identity;

    /// <summary>Pendenza della retta: sopra 1 aumenta il contrasto, sotto 1 lo smorza.</summary>
    public double Slope { get; set; } = 1;

    /// <summary>Scostamento della retta: alza o abbassa tutto il canale. È la luminosità.</summary>
    public double Intercept { get; set; }

    public double Amplitude { get; set; } = 1;

    public double Exponent { get; set; } = 1;

    public double Offset { get; set; }

    /// <summary>
    /// I valori della tabella. Due valori bastano per i casi frequenti: <c>0 1</c> non cambia
    /// niente, <c>1 0</c> inverte il canale. Con <see cref="TransferFunctionKind.Discrete"/>
    /// diventano i gradini della posterizzazione.
    /// </summary>
    public List<double> TableValues { get; set; } = [0, 1];

    /// <summary>Vero se questa curva lascia il canale come lo ha trovato.</summary>
    [JsonIgnore]
    public bool ELIdentita => Kind == TransferFunctionKind.Identity;

    /// <summary>Come si legge questa curva in una riga, per l'elenco dei passaggi.</summary>
    [JsonIgnore]
    public TestoNominato Sintesi => Kind switch
    {
        TransferFunctionKind.Identity => new("filtro.curva.invariato", "unchanged"),
        TransferFunctionKind.Linear =>
            new("filtro.curva.lineare", "linear {0} / {1}", Numero.Testo(Slope), Numero.Testo(Intercept)),
        TransferFunctionKind.Gamma => new("filtro.curva.gamma", "gamma {0}", Numero.Testo(Exponent)),
        TransferFunctionKind.Table => new("filtro.curva.tabella", "table of {0}", TableValues.Count),
        _ => new("filtro.curva.gradini", "{0} steps", TableValues.Count),
    };

    /// <summary>La stessa riga, già scritta in inglese.</summary>
    [JsonIgnore]
    public string Riassunto => Sintesi.ToString();

    /// <summary>Copia profonda: i modelli non si condividono fra due pattern.</summary>
    public TransferFunction Copia() => new()
    {
        Kind = Kind,
        Slope = Slope,
        Intercept = Intercept,
        Amplitude = Amplitude,
        Exponent = Exponent,
        Offset = Offset,
        TableValues = [.. TableValues],
    };
}
