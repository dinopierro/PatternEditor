namespace PatternEditor.Core.Models;

/// <summary>
/// Lettura dell'istante di creazione contenuto in un UUIDv7.
///
/// Un UUID versione 7 (RFC 9562) inizia con un timestamp Unix in millisecondi su 48 bit:
/// da un Id generato con <see cref="Guid.CreateVersion7()"/> si può quindi ricavare quando
/// è stato creato, senza bisogno di alcun dato aggiuntivo. È ciò che permette di ricostruire
/// la data di creazione dei pattern salvati prima che venisse introdotta esplicitamente.
/// </summary>
public static class Uuid7
{
    private const int Version7 = 7;

    /// <summary>
    /// Estrae l'istante di creazione da un UUIDv7. Restituisce false (e
    /// <see cref="DateTimeOffset.MinValue"/>) se il Guid non è di versione 7, ad esempio
    /// perché generato altrove o scritto a mano in un documento JSON.
    /// </summary>
    public static bool TryGetCreationTime(Guid id, out DateTimeOffset creationTime)
    {
        creationTime = DateTimeOffset.MinValue;

        // La rappresentazione big-endian è quella "di rete" definita dall'RFC: i primi
        // sei byte sono il timestamp, indipendentemente dall'ordinamento interno di Guid.
        Span<byte> bytes = stackalloc byte[16];
        if (!id.TryWriteBytes(bytes, bigEndian: true, out _))
        {
            return false;
        }

        // Il numero di versione occupa i quattro bit alti del settimo byte.
        var version = (bytes[6] & 0xF0) >> 4;
        if (version != Version7)
        {
            return false;
        }

        long milliseconds = 0;
        for (var i = 0; i < 6; i++)
        {
            milliseconds = (milliseconds << 8) | bytes[i];
        }

        creationTime = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        return true;
    }
}
