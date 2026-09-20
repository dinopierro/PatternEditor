namespace PatternEditor.State;

/// <summary>
/// Stati dell'editor secondo il ciclo di vita descritto dalla specifica:
///
/// Closed --(OpenNew)--------&gt; Creating
/// Closed --(OpenExisting)---&gt; Editing
/// Creating/Editing --(modifica)--&gt; Dirty
/// (Creating/Editing/Dirty) --(Cancel)--&gt; Cancelled
/// (Creating/Editing/Dirty) --(Confirm)--&gt; Confirming --&gt; Confirmed | Invalid
///
/// Valid/Invalid rappresentano l'esito dell'ultima validazione eseguita, non uno stato
/// "di passaggio" verso cui si arriva autonomamente: sono impostati esplicitamente da
/// chi orchestra la macchina (il componente PatternEditor) dopo aver chiamato il validatore.
/// </summary>
public enum PatternEditorState
{
    Closed,
    Creating,
    Editing,
    Dirty,
    Valid,
    Invalid,
    Confirming,
    Confirmed,
    Cancelled,
}

/// <summary>
/// Implementazione esplicita e testabile della macchina a stati dell'editor. Volutamente
/// priva di qualunque dipendenza da Blazor: può essere istanziata e verificata in un test
/// xUnit "puro", senza bUnit e senza rendering di componenti.
/// </summary>
public sealed class PatternEditorStateMachine
{
    public PatternEditorState State { get; private set; } = PatternEditorState.Closed;

    /// <summary>True se il modello è stato modificato rispetto allo stato con cui l'editor è stato aperto.</summary>
    public bool IsDirty { get; private set; }

    public bool IsOpen => State is not (PatternEditorState.Closed or PatternEditorState.Cancelled or PatternEditorState.Confirmed);

    public void OpenNew()
    {
        EnsureState(PatternEditorState.Closed, nameof(OpenNew));
        State = PatternEditorState.Creating;
        IsDirty = false;
    }

    public void OpenExisting()
    {
        EnsureState(PatternEditorState.Closed, nameof(OpenExisting));
        State = PatternEditorState.Editing;
        IsDirty = false;
    }

    /// <summary>Da chiamare a ogni modifica del modello effettuata dall'utente.</summary>
    public void MarkDirty()
    {
        EnsureEditable(nameof(MarkDirty));
        IsDirty = true;
        State = PatternEditorState.Dirty;
    }

    /// <summary>
    /// Registra l'esito dell'ultima validazione eseguita (tipicamente in risposta a un
    /// tentativo di conferma, ma può essere chiamata anche per validazione "live").
    /// </summary>
    public void SetValidationResult(bool isValid)
    {
        EnsureEditable(nameof(SetValidationResult));
        State = isValid ? PatternEditorState.Valid : PatternEditorState.Invalid;
    }

    /// <summary>Annulla l'editing: nessuna modifica deve essere applicata dal chiamante.</summary>
    public void Cancel()
    {
        EnsureEditable(nameof(Cancel));
        State = PatternEditorState.Cancelled;
    }

    /// <summary>
    /// Avvia il tentativo di conferma. Il chiamante deve eseguire la validazione e poi
    /// invocare <see cref="CompleteConfirm"/> con l'esito.
    /// </summary>
    public void BeginConfirm()
    {
        EnsureEditable(nameof(BeginConfirm));
        State = PatternEditorState.Confirming;
    }

    /// <summary>
    /// Completa il tentativo di conferma avviato con <see cref="BeginConfirm"/>.
    /// Se i dati non sono validi, l'editor deve rimanere aperto (stato Invalid) e non
    /// deve chiudersi: la chiusura è permessa solo quando isValid è true.
    /// </summary>
    public void CompleteConfirm(bool isValid)
    {
        if (State != PatternEditorState.Confirming)
        {
            throw new InvalidOperationException(
                $"CompleteConfirm richiede che lo stato corrente sia {PatternEditorState.Confirming}, stato attuale: {State}.");
        }

        State = isValid ? PatternEditorState.Confirmed : PatternEditorState.Invalid;
    }

    /// <summary>Riporta la macchina allo stato iniziale, per riutilizzare la stessa istanza in una nuova sessione di editing.</summary>
    public void Reset()
    {
        State = PatternEditorState.Closed;
        IsDirty = false;
    }

    private void EnsureEditable(string operation)
    {
        if (State is PatternEditorState.Closed or PatternEditorState.Cancelled or PatternEditorState.Confirmed)
        {
            throw new InvalidOperationException($"Impossibile eseguire '{operation}' quando l'editor non è aperto (stato attuale: {State}).");
        }
    }

    private void EnsureState(PatternEditorState expected, string operation)
    {
        if (State != expected)
        {
            throw new InvalidOperationException($"'{operation}' richiede lo stato {expected}, stato attuale: {State}.");
        }
    }
}
