using PatternEditor.Core.Localization;

namespace PatternEditor.Core.Validation;

/// <summary>
/// Esito di una validazione. Usato sia a livello di Pattern (validazione del componente
/// principale) sia a livello di singolo elemento (validazione del relativo plugin).
///
/// Un elemento non valido non deve essere convertito automaticamente in un altro tipo
/// né eliminato automaticamente: l'esito della validazione serve solo a segnalare
/// all'applicazione host/editor lo stato corrente, la decisione su cosa fare è sua.
/// </summary>
///
/// <remarks>
/// I messaggi viaggiano come <see cref="TestoNominato"/> e non come stringhe: chi valida
/// conosce il caso e i numeri, ma non la lingua di chi leggerà. <see cref="Errors"/> e
/// <see cref="Warnings"/> restano l'inglese già composto, perché è quello che serve a un
/// ospite che mostri l'esito senza sapere che esistono i cataloghi.
/// </remarks>
public sealed class ValidationResult
{
    public bool IsValid => Errori.Count == 0;

    /// <summary>Errori bloccanti: impediscono la conferma/salvataggio.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Avvisi: non bloccanti, ma degni di nota per l'utente.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Gli stessi errori, ancora traducibili.</summary>
    public IReadOnlyList<TestoNominato> Errori { get; }

    /// <summary>Gli stessi avvisi, ancora traducibili.</summary>
    public IReadOnlyList<TestoNominato> Avvisi { get; }

    public ValidationResult(
        IReadOnlyList<TestoNominato>? errori = null,
        IReadOnlyList<TestoNominato>? avvisi = null)
    {
        Errori = errori ?? [];
        Avvisi = avvisi ?? [];

        // Si compongono una volta sola: sono liste corte, e ricomporle a ogni lettura
        // significherebbe rifare gli string.Format a ogni ciclo di rendering.
        Errors = [.. Errori.Select(m => m.ToString())];
        Warnings = [.. Avvisi.Select(m => m.ToString())];
    }

    /// <summary>
    /// L'esito di chi ha solo delle stringhe da dare: restano com'è, senza traduzione.
    /// </summary>
    public ValidationResult(IReadOnlyList<string>? errors, IReadOnlyList<string>? warnings)
        : this(Nominati(errors), Nominati(warnings))
    {
    }

    private static IReadOnlyList<TestoNominato>? Nominati(IReadOnlyList<string>? testi) =>
        testi is null ? null : [.. testi.Select(TestoNominato.Fisso)];

    public static ValidationResult Success() => new();

    public static ValidationResult Failure(params string[] errors) =>
        new([.. errors.Select(TestoNominato.Fisso)]);

    /// <summary>L'esito di un solo errore, già con il suo nome.</summary>
    public static ValidationResult Errore(TestoNominato messaggio) => new([messaggio]);
}
