using System.Text.Json.Nodes;
using PatternEditor.Abstractions.Serialization;
using PatternEditor.Core.Models;

namespace PatternEditor.Services;

/// <summary>
/// Duplica un <see cref="VectorElement"/> senza conoscerne il tipo concreto.
///
/// Il clone viene ottenuto tramite round-trip di serializzazione (la stessa logica
/// polimorfica basata sul Plugin Registry usata per il resto del Pattern): in questo modo
/// l'editor principale continua a non conoscere né i modelli concreti dei plugin né le
/// loro proprietà. Anche gli elementi non gestiti da alcun plugin
/// (<see cref="UnknownVectorElement"/>) sono duplicabili senza perdita di dati.
/// </summary>
public interface IVectorElementCloner
{
    /// <summary>
    /// Restituisce una copia profonda dell'elemento con un nuovo <see cref="VectorElement.Id"/>
    /// (UUIDv7): il duplicato è una nuova istanza, non un riferimento condiviso.
    /// </summary>
    VectorElement Clone(VectorElement element);
}

public sealed class VectorElementCloner : IVectorElementCloner
{
    private readonly IPatternSerializer _serializer;

    public VectorElementCloner(IPatternSerializer serializer)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public VectorElement Clone(VectorElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        // Pattern "contenitore" usato solo come veicolo per il round-trip: non viene
        // mai esposto all'esterno e non modifica in alcun modo l'elemento originale.
        var carrier = new Pattern();
        carrier.Definition.Elements.Add(element);

        var root = JsonNode.Parse(_serializer.Serialize(carrier))!.AsObject();
        var elementNode = root["definition"]!["elements"]!.AsArray()[0]!.AsObject();

        // L'Id identifica l'istanza, non la tipologia: il duplicato deve averne uno proprio.
        elementNode["id"] = Guid.CreateVersion7().ToString();

        return _serializer.Deserialize(root.ToJsonString()).Definition.Elements[0];
    }
}
