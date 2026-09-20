namespace PatternEditor.Sample.Api.Auth;

/// <summary>
/// Riconosce un'immagine guardando i primi byte, invece di credere a quello che dice chi
/// la manda.
/// </summary>
///
/// <remarks>
/// <para>
/// L'estensione del file e l'intestazione <c>Content-Type</c> della richiesta le sceglie il
/// client, e un client ostile le sceglie con cura: un file che dichiara <c>image/png</c> e
/// contiene HTML, servito poi da noi, diventa una pagina che gira sul nostro dominio con i
/// gettoni dei nostri utenti a portata di mano. L'unica dichiarazione che conta è quella
/// che il file fa di sé nei propri primi byte.
/// </para>
/// <para>
/// L'SVG <b>non</b> è nell'elenco, e non è una dimenticanza: un SVG è un documento che può
/// contenere script. È l'unico formato d'immagine per cui «mostra questo file» significa
/// anche «esegui questo codice», e in un avatar caricato da chiunque non ha posto —
/// nonostante tutto il resto di questo progetto sia fatto di SVG.
/// </para>
/// </remarks>
public static class ImmagineCaricata
{
    /// <summary>
    /// Un tetto basso, e volutamente: è la foto di un cerchio da quaranta pixel. Il limite
    /// protegge il disco, la memoria e il tempo di caricamento della pagina, e nessuna
    /// immagine ragionevole per questo scopo ci si avvicina.
    /// </summary>
    public const int ByteMassimi = 2 * 1024 * 1024;

    /// <summary>
    /// Il tipo dell'immagine, oppure <c>null</c> se i byte non corrispondono a nessuno dei
    /// formati ammessi.
    /// </summary>
    public static string? Riconosci(ReadOnlySpan<byte> contenuto)
    {
        if (contenuto.Length < 12)
        {
            return null;
        }

        // PNG: la firma di otto byte della specifica, che comprende apposta un ritorno a
        // capo in due forme per accorgersi dei trasferimenti che li traducono.
        if (contenuto[0] == 0x89 && contenuto[1] == 0x50 && contenuto[2] == 0x4E && contenuto[3] == 0x47
            && contenuto[4] == 0x0D && contenuto[5] == 0x0A && contenuto[6] == 0x1A && contenuto[7] == 0x0A)
        {
            return "image/png";
        }

        // JPEG: inizia con il marcatore di inizio immagine.
        if (contenuto[0] == 0xFF && contenuto[1] == 0xD8 && contenuto[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // WebP: contenitore RIFF, con la parola "WEBP" subito dopo la lunghezza.
        if (contenuto[0] == 0x52 && contenuto[1] == 0x49 && contenuto[2] == 0x46 && contenuto[3] == 0x46
            && contenuto[8] == 0x57 && contenuto[9] == 0x45 && contenuto[10] == 0x42 && contenuto[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }
}
