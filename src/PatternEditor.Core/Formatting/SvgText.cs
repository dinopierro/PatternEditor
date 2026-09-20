namespace PatternEditor.Core.Formatting;

/// <summary>
/// Utilità per scrivere testo dentro un documento SVG.
///
/// I valori testuali degli elementi (il tracciato di un path, un colore, il testo di un
/// &lt;text&gt;) provengono dall'utente o da un documento JSON esistente: inseriti tal quali
/// nel markup potrebbero contenere apici o parentesi angolari e produrre un SVG malformato.
/// L'escape è responsabilità di chi genera il markup, cioè dei plugin.
/// </summary>
public static class SvgText
{
    /// <summary>
    /// Protegge un valore destinato a un attributo (<c>attributo="…"</c>). Oltre ai
    /// caratteri strutturali servono anche gli apici, che chiuderebbero l'attributo.
    /// </summary>
    public static string Escape(string? value) =>
        EscapeContent(value)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);

    /// <summary>
    /// Protegge un testo destinato al contenuto di un elemento
    /// (<c>&lt;text&gt;…&lt;/text&gt;</c>). Qui gli apici non hanno alcun significato
    /// speciale e vanno lasciati intatti: sostituirli renderebbe il sorgente SVG inutilmente
    /// illeggibile senza cambiare ciò che viene visualizzato.
    /// </summary>
    public static string EscapeContent(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
}
