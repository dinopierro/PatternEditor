using PatternEditor.Core.Models;

namespace PatternEditor.Abstractions.Serialization;

/// <summary>
/// Serializza/deserializza un intero Pattern in un unico documento JSON (1 Pattern → 1 JSON → 1 file).
/// Il JSON è un contratto dati indipendente dai nomi di classe, namespace e assembly .NET:
/// il campo "type" di ciascun elemento è l'unico discriminatore tecnico.
/// </summary>
public interface IPatternSerializer
{
    string Serialize(Pattern pattern);

    /// <summary>
    /// Ricostruisce il Pattern dal JSON. Gli elementi il cui "type" non corrisponde ad alcun
    /// plugin registrato vengono preservati come <see cref="UnknownVectorElement"/>, senza
    /// perdita di dati e senza essere eliminati o convertiti automaticamente.
    /// </summary>
    Pattern Deserialize(string json);
}
