using System.Diagnostics.CodeAnalysis;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Sample.Api;

/// <summary>
/// Lettura del corpo delle richieste che trasportano un Pattern.
///
/// Il corpo viene interpretato con <see cref="IPatternSerializer"/> e non con il model
/// binding predefinito, che non gestirebbe il polimorfismo degli elementi vettoriali.
/// Gli errori di formato arrivano quindi come eccezioni e vanno tradotti in una risposta
/// 400: un documento malformato è un errore di chi chiama, non un guasto del servizio, e
/// non deve produrre un 500.
///
/// Vengono intercettate solo le due eccezioni previste dal contratto del serializzatore:
/// FormatException per un documento malformato e ArgumentException per un testo vuoto.
/// Le eccezioni specifiche del formato JSON non arrivano fin qui, perché è il
/// serializzatore a convertirle.
/// </summary>
public static class PatternRequestReader
{
    public static bool TryRead(
        IPatternSerializer serializer,
        string json,
        [NotNullWhen(true)] out Pattern? pattern,
        [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        try
        {
            pattern = serializer.Deserialize(json);
            error = null;
            return true;
        }
        catch (FormatException e)
        {
            pattern = null;
            error = $"Il corpo della richiesta non è un Pattern valido: {e.Message}";
            return false;
        }
        catch (ArgumentException e)
        {
            pattern = null;
            error = $"Il corpo della richiesta non è un Pattern valido: {e.Message}";
            return false;
        }
    }
}
