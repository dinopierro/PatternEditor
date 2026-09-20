using System.Text.Json;

namespace PatternEditor.Core.Models;

/// <summary>
/// Rappresenta un elemento vettoriale il cui "type" non corrisponde a nessun plugin
/// attualmente registrato.
///
/// La deserializzazione non deve mai scartare silenziosamente un elemento sconosciuto:
/// il JSON originale viene preservato integralmente in <see cref="RawJson"/> così che,
/// in fase di ri-serializzazione, il documento risulti identico a quello letto
/// (nessuna perdita di dati, nessuna trasformazione implicita in un altro tipo).
/// </summary>
public sealed class UnknownVectorElement : VectorElement
{
    public override string Type { get; }

    /// <summary>
    /// Rappresentazione JSON completa e originale dell'elemento, così come letta dal documento.
    /// </summary>
    public JsonElement RawJson { get; }

    public UnknownVectorElement(Guid id, string type, JsonElement rawJson)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Il type di un elemento non può essere vuoto.", nameof(type));
        }

        Type = type;
        RawJson = rawJson.Clone();
    }
}
