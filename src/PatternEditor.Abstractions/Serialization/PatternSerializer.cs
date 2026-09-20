using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PatternEditor.Abstractions.Plugins;
using PatternEditor.Core.Models;
using PatternEditor.Core.Models.Filters;

namespace PatternEditor.Abstractions.Serialization;

/// <summary>
/// Implementazione di <see cref="IPatternSerializer"/>.
///
/// La deserializzazione polimorfica degli elementi avviene tramite il Plugin Registry:
/// per ogni elemento del JSON si legge il campo "type", si risolve il plugin corrispondente
/// e si usa <see cref="IVectorElementPlugin.ElementClrType"/> per deserializzare l'oggetto
/// nel tipo concreto corretto. Questo evita che Core o Abstractions debbano referenziare
/// direttamente gli assembly dei singoli plugin (Line, Rect, ...).
///
/// Se nessun plugin è registrato per un dato "type", l'elemento viene preservato come
/// <see cref="UnknownVectorElement"/> mantenendo il JSON originale, per evitare perdita di dati.
/// </summary>
public sealed class PatternSerializer : IPatternSerializer
{
    private readonly IVectorElementPluginRegistry _registry;

    private static readonly JsonSerializerOptions ElementOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Le opzioni per il filtro. Differiscono da quelle degli elementi in un punto: le
    /// enumerazioni si scrivono per nome e non per numero.
    ///
    /// <para>
    /// Non è un vezzo di leggibilità. I valori numerici dipendono dall'ordine in cui le voci
    /// sono dichiarate nel codice, e inserirne una in mezzo — cosa che capiterà, perché le
    /// enumerazioni qui seguono l'ordine della specifica — cambierebbe il significato di tutti
    /// i documenti già scritti: un filtro salvato come «moltiplica» si riaprirebbe come
    /// «scolora», senza alcun errore. Scritto per nome, questo non può succedere.
    /// </para>
    /// </summary>
    private static readonly JsonSerializerOptions FilterOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public PatternSerializer(IVectorElementPluginRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public string Serialize(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var elements = new JsonArray();
        foreach (var element in pattern.Definition.Elements)
        {
            elements.Add(SerializeElement(element));
        }

        var definizione = new JsonObject
        {
            ["width"] = pattern.Definition.Width,
            ["height"] = pattern.Definition.Height,
            ["scale"] = pattern.Definition.Scale,
            ["rotation"] = pattern.Definition.Rotation,
            ["translateX"] = pattern.Definition.TranslateX,
            ["translateY"] = pattern.Definition.TranslateY,
        };

        // Il filtro compare solo quando c'è, e il campo si aggiunge invece di assegnarlo
        // dentro l'inizializzatore: assegnare null a una chiave non la omette, la scrive
        // valorizzata a null. I quattrocento documenti già in archivio devono continuare a
        // riscriversi identici a com'erano, e una chiave in più li cambierebbe tutti.
        if (SerializeFilter(pattern.Definition.Filter) is { } filtro)
        {
            definizione["filter"] = filtro;
        }

        // Gli elementi per ultimi: sono la parte lunga, e le proprietà della cella lette
        // prima di duecento righe di forme si trovano senza scorrere.
        definizione["elements"] = elements;

        var root = new JsonObject
        {
            ["version"] = pattern.Version,
            ["id"] = pattern.Id.ToString(),
            ["name"] = pattern.Name,
            // Formato ISO 8601 con offset, in UTC: ordinabile come testo e non ambiguo
            // rispetto al fuso orario della macchina che ha scritto il documento.
            ["createdAt"] = pattern.CreatedAt.ToUniversalTime().ToString("O"),
            ["modifiedAt"] = pattern.ModifiedAt.ToUniversalTime().ToString("O"),

            // L'autore compare solo se c'è: un ospite che di utenti non sa niente non deve
            // trovarsi due campi nulli in cima a ogni documento che scrive.
            ["authorId"] = pattern.AuthorId?.ToString(),
            ["authorName"] = pattern.AuthorName,

            // La visibilità si scrive sempre, anche quando è quella predefinita: è la sua
            // assenza a significare qualcosa (vedi la lettura), e un documento che la omette
            // perché «tanto è il valore normale» direbbe il contrario di quello che intende.
            ["visibility"] = pattern.Visibility.ToString(),
            ["definition"] = definizione,
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Un documento malformato è un dato non valido, non un guasto del programma, e viene
    /// sempre segnalato con <see cref="FormatException"/>. Le eccezioni specifiche di
    /// System.Text.Json non escono da questa classe: che il formato sia JSON è un dettaglio
    /// implementativo del serializzatore, e chi lo usa ha un solo caso da gestire.
    /// </summary>
    public Pattern Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Il JSON non può essere vuoto.", nameof(json));
        }

        try
        {
            return DeserializeDocument(json);
        }
        catch (JsonException e)
        {
            throw new FormatException("Il documento non è JSON valido.", e);
        }
        catch (InvalidOperationException e)
        {
            // Sollevata da JsonNode quando un valore è del tipo sbagliato, ad esempio una
            // larghezza scritta come testo invece che come numero.
            throw new FormatException("Il documento contiene un valore di tipo non atteso.", e);
        }
    }

    private Pattern DeserializeDocument(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new FormatException("JSON del Pattern non valido.");

        var version = root["version"]?.GetValue<int>() ?? Pattern.CurrentVersion;

        if (root["id"] is not JsonValue idValue
            || !idValue.TryGetValue<string>(out var idText)
            || !Guid.TryParse(idText, out var id))
        {
            throw new FormatException("Il documento non contiene un identificativo valido.");
        }

        if (id == Guid.Empty)
        {
            throw new FormatException("L'identificativo del Pattern non può essere vuoto.");
        }

        var name = root["name"]?.GetValue<string>() ?? string.Empty;

        // Documenti scritti prima dell'introduzione delle date: createdAt viene ricostruita
        // dal timestamp dell'UUIDv7 e modifiedAt, non ricavabile da nulla, la eguaglia.
        // Entrambe vengono poi scritte al primo salvataggio successivo.
        var createdAt = ReadTimestamp(root["createdAt"]) ?? Pattern.DefaultCreatedAtFor(id);
        var modifiedAt = ReadTimestamp(root["modifiedAt"]) ?? createdAt;

        // Un autore scritto male vale come autore assente, non come documento rotto: il
        // disegno è leggibile lo stesso, e chiudere fuori un intero pattern per un campo
        // accessorio sarebbe sproporzionato.
        var authorId = root["authorId"] is JsonValue autore
                       && autore.TryGetValue<string>(out var autoreTesto)
                       && Guid.TryParse(autoreTesto, out var autoreId)
            ? autoreId
            : (Guid?)null;

        var authorName = root["authorName"]?.GetValue<string>();

        // Un documento SENZA il campo è più vecchio del concetto di visibilità, e prima che
        // il concetto esistesse ogni pattern si vedeva da chiunque: leggerlo come pubblico
        // è dire la verità su che cosa quel documento è stato finora. Il contrario —
        // privato per prudenza — farebbe sparire dal portale un archivio intero senza che
        // nessuno abbia chiesto niente.
        //
        // La prudenza sta altrove, ed è dove serve: un pattern NUOVO nasce privato, e il
        // campo c'è sempre in quello che scrive questa classe.
        var visibility = root["visibility"] is JsonValue vista
                         && vista.TryGetValue<string>(out var vistaTesto)
                         && Enum.TryParse<PatternVisibility>(vistaTesto, ignoreCase: true, out var letta)
            ? letta
            : PatternVisibility.Pubblica;

        if (root["definition"] is not JsonObject definitionNode)
        {
            throw new FormatException("Il documento non contiene la definizione del Pattern.");
        }

        var definition = new PatternDefinition
        {
            Width = definitionNode["width"]?.GetValue<double>() ?? 0,
            Height = definitionNode["height"]?.GetValue<double>() ?? 0,
            Scale = definitionNode["scale"]?.GetValue<double>() ?? 1.0,
            Rotation = definitionNode["rotation"]?.GetValue<double>() ?? 0,
            TranslateX = definitionNode["translateX"]?.GetValue<double>() ?? 0,
            TranslateY = definitionNode["translateY"]?.GetValue<double>() ?? 0,
            Filter = DeserializeFilter(definitionNode["filter"] as JsonObject),
        };

        if (definitionNode["elements"] is JsonArray elementsArray)
        {
            foreach (var elementNode in elementsArray)
            {
                if (elementNode is JsonObject elementObject)
                {
                    definition.Elements.Add(DeserializeElement(elementObject));
                }
            }
        }

        return new Pattern(id, name, definition, version, createdAt, modifiedAt, authorId, authorName, visibility);
    }

    /// <summary>
    /// Legge una data ISO 8601. Un valore assente o non interpretabile viene trattato come
    /// assente (null) anziché far fallire l'intera lettura del pattern: una data
    /// malformata non deve rendere illeggibile un documento per il resto valido.
    /// </summary>
    private static DateTimeOffset? ReadTimestamp(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
                ? parsed
                : null;
    }

    /// <summary>
    /// Il filtro come oggetto JSON, oppure null quando non c'è: un nodo nullo viene omesso
    /// dal documento invece di comparire come <c>"filter": null</c>.
    /// </summary>
    private static JsonNode? SerializeFilter(PatternFilter? filter)
    {
        if (filter is null)
        {
            return null;
        }

        var primitive = new JsonArray();
        foreach (var primitiva in filter.Primitives)
        {
            primitive.Add(SerializePrimitive(primitiva));
        }

        return new JsonObject
        {
            ["enabled"] = filter.Enabled,
            ["colorSpace"] = filter.ColorSpace == FilterColorSpace.Srgb ? "sRGB" : "linearRGB",
            ["x"] = filter.X,
            ["y"] = filter.Y,
            ["width"] = filter.Width,
            ["height"] = filter.Height,
            ["primitives"] = primitive,
        };
    }

    /// <summary>
    /// Una primitiva come oggetto JSON. Il campo <c>type</c> è il nome SVG del nodo, cioè la
    /// stessa parola che comparirà nel documento generato: un documento in cui il passaggio
    /// si chiama <c>feGaussianBlur</c> in entrambi i posti si legge senza tradurre.
    /// </summary>
    private static JsonObject SerializePrimitive(FilterPrimitive primitiva)
    {
        if (primitiva is UnknownFilterPrimitive ignota)
        {
            // Riscritta com'era: non la si sa disegnare, ma non la si perde.
            return JsonObject.Create(ignota.JsonOriginale) ?? new JsonObject();
        }

        var json = JsonSerializer.SerializeToNode(primitiva, primitiva.GetType(), FilterOptions)?.AsObject()
            ?? new JsonObject();

        json["type"] = primitiva.SvgName;
        return json;
    }

    /// <summary>Il filtro letto dal documento, o null quando il documento non ne ha.</summary>
    private static PatternFilter? DeserializeFilter(JsonObject? node)
    {
        if (node is null)
        {
            return null;
        }

        var filtro = new PatternFilter
        {
            Enabled = node["enabled"]?.GetValue<bool>() ?? true,

            // Il confronto è sul nome scritto nella specifica, non su quello
            // dell'enumerazione: è il valore che si legge anche nell'SVG generato.
            ColorSpace = string.Equals(node["colorSpace"]?.GetValue<string>(), "linearRGB", StringComparison.OrdinalIgnoreCase)
                ? FilterColorSpace.LinearRgb
                : FilterColorSpace.Srgb,

            X = node["x"]?.GetValue<double>() ?? -10,
            Y = node["y"]?.GetValue<double>() ?? -10,
            Width = node["width"]?.GetValue<double>() ?? 120,
            Height = node["height"]?.GetValue<double>() ?? 120,
        };

        if (node["primitives"] is JsonArray primitive)
        {
            foreach (var voce in primitive)
            {
                if (voce is JsonObject oggetto)
                {
                    filtro.Primitives.Add(DeserializePrimitive(oggetto));
                }
            }
        }

        return filtro;
    }

    /// <summary>
    /// Una primitiva letta dal documento.
    ///
    /// <para>
    /// Un passaggio che qui non si conosce — scritto da una versione con più primitive, o
    /// con un valore che questa non sa interpretare — non fa fallire la lettura dell'intero
    /// pattern: diventa un passaggio conservato com'era. La regola è la stessa degli elementi
    /// vettoriali di tipo ignoto, e il motivo pure: un disegno resta leggibile anche quando
    /// una sua rifinitura non lo è.
    /// </para>
    /// </summary>
    private static FilterPrimitive DeserializePrimitive(JsonObject oggetto)
    {
        var tipo = oggetto["type"]?.GetValue<string>() ?? string.Empty;
        var clr = FilterPrimitiveTypes.TipoDi(tipo);

        if (clr is not null)
        {
            try
            {
                if (oggetto.Deserialize(clr, FilterOptions) is FilterPrimitive letta)
                {
                    return letta;
                }
            }
            catch (JsonException)
            {
                // Tipo giusto, contenuto no: si conserva senza interpretarlo.
            }
        }

        using var documento = JsonDocument.Parse(oggetto.ToJsonString());
        return new UnknownFilterPrimitive(tipo, documento.RootElement.Clone());
    }

    private static JsonObject SerializeElement(VectorElement element)
    {
        if (element is UnknownVectorElement unknown)
        {
            // Riscrive il JSON originale così com'era: nessuna trasformazione, nessuna perdita.
            return JsonObject.Create(unknown.RawJson) ?? new JsonObject();
        }

        var json = JsonSerializer.SerializeToNode(element, element.GetType(), ElementOptions);
        return json?.AsObject() ?? new JsonObject();
    }

    private VectorElement DeserializeElement(JsonObject elementObject)
    {
        var type = elementObject["type"]?.GetValue<string>()
            ?? throw new FormatException("Un elemento del Pattern non contiene il campo \"type\".");

        if (elementObject["id"] is not JsonValue idValue
            || !idValue.TryGetValue<string>(out var idText)
            || !Guid.TryParse(idText, out var id))
        {
            throw new FormatException($"L'elemento di tipo \"{type}\" non ha un identificativo valido.");
        }

        if (_registry.TryGet(type, out var plugin) && plugin is not null)
        {
            var deserialized = elementObject.Deserialize(plugin.ElementClrType, ElementOptions) as VectorElement;
            return deserialized
                ?? throw new FormatException($"Impossibile deserializzare l'elemento di tipo \"{type}\".");
        }

        // Plugin non disponibile: l'elemento viene preservato integralmente, non eliminato.
        using var document = JsonDocument.Parse(elementObject.ToJsonString());
        return new UnknownVectorElement(id, type, document.RootElement.Clone());
    }
}
