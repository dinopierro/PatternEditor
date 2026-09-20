using System.Globalization;

namespace PatternEditor.Core.Localization;

/// <summary>
/// Un testo che sa come si chiama.
/// </summary>
///
/// <remarks>
/// <para>
/// Serve dove il testo <b>nasce nel modello</b> e non nell'interfaccia: il riassunto di un
/// passaggio del filtro, il messaggio di un validatore. Sono frasi che il modello sa
/// comporre — conosce i numeri, conosce i casi — ma che non può tradurre, perché di lingue
/// non sa niente e non deve saperne.
/// </para>
/// <para>
/// Porta tre cose: il <b>nome</b> con cui cercarla in un catalogo, il <b>modello</b> in
/// inglese da usare quando quel catalogo non ce l'ha, e i <b>valori</b> da sostituire. Le
/// tiene insieme perché una frase e i suoi numeri separati si ritrovano sempre, prima o poi,
/// nell'ordine sbagliato.
/// </para>
/// <para>
/// Si converte in stringa da sé, e la stringa è l'inglese: chi la usa senza sapere che
/// esistono le lingue — un test, un registro, un'applicazione ospite che mostra gli errori
/// com'è capitato — continua a funzionare come prima.
/// </para>
/// </remarks>
public sealed class TestoNominato
{
    /// <summary>
    /// Il nome con cui cercare la traduzione. Vuoto significa «non c'è niente da tradurre»:
    /// è il caso di un testo che viene da fuori — il nome di un file, un tipo sconosciuto
    /// letto da un documento — e che va mostrato com'è.
    /// </summary>
    public string Chiave { get; }

    /// <summary>
    /// La frase in inglese, con i segnaposto ancora al loro posto (<c>{0}</c>, <c>{1}</c>).
    /// È il ripiego, e serve tale e quale anche a chi traduce: è il testo di partenza.
    /// </summary>
    public string Modello { get; }

    /// <summary>I valori da sostituire ai segnaposto, nell'ordine.</summary>
    public IReadOnlyList<object?> Valori { get; }

    public TestoNominato(string chiave, string modello, params object?[] valori)
    {
        Chiave = chiave ?? string.Empty;
        Modello = modello ?? string.Empty;
        Valori = valori ?? [];
    }

    /// <summary>Un testo che non si traduce: viene da fuori e si mostra com'è.</summary>
    public static TestoNominato Fisso(string testo) => new(string.Empty, testo);

    /// <summary>
    /// Più testi in fila, separati da un segno.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Serve quando l'elenco delle parti si conosce solo a runtime — i canali regolati di
    /// una curva, i livelli di una sovrapposizione — e quindi non può stare in una frase
    /// scritta nel catalogo.
    /// </para>
    /// <para>
    /// L'insieme non ha un nome suo: da tradurre sono le parti, che restano intere fin
    /// dentro i valori. Il separatore è un segno di punteggiatura, e non cambia con la
    /// lingua.
    /// </para>
    /// </remarks>
    public static TestoNominato Unisci(string separatore, IReadOnlyList<TestoNominato> parti) =>
        new(string.Empty,
            string.Join(separatore, Enumerable.Range(0, parti.Count).Select(i => $"{{{i}}}")),
            [.. parti]);

    /// <summary>
    /// La frase in inglese, con i valori al loro posto.
    /// </summary>
    ///
    /// <remarks>
    /// I numeri si scrivono <b>alla maniera corrente</b> e non in forma invariante: questa è
    /// la frase che si legge a schermo quando nessuno ha applicato un catalogo, e un numero
    /// con il punto decimale in mezzo a un'interfaccia italiana si nota quanto una parola
    /// sbagliata.
    /// </remarks>
    public override string ToString() =>
        Valori.Count == 0
            ? Modello
            : string.Format(CultureInfo.CurrentCulture, Modello, [.. Valori]);

    /// <summary>
    /// Vale come stringa ovunque ne serva una: è quello che tiene in piedi il codice scritto
    /// prima che le lingue esistessero.
    /// </summary>
    public static implicit operator string(TestoNominato testo) => testo.ToString();
}
