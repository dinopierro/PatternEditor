using PatternEditor.State;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// Verifica la macchina a stati dell'editor.
///
/// <para>
/// La macchina è deliberatamente priva di dipendenze da Blazor, e questi test sono la
/// ragione per cui ne vale la pena: il ciclo di vita si verifica con chiamate dirette,
/// senza montare un componente né simulare eventi. I passaggi che contano sono quelli che
/// **non** devono avvenire — dagli stati finali non si torna indietro, e una conferma con
/// dati non validi non chiude l'editor.
/// </para>
/// </summary>
public class PatternEditorStateMachineTests
{
    [Fact]
    public void OpenNew_moves_from_Closed_to_Creating()
    {
        var sm = new PatternEditorStateMachine();

        sm.OpenNew();

        Assert.Equal(PatternEditorState.Creating, sm.State);
        Assert.False(sm.IsDirty);
    }

    [Fact]
    public void OpenExisting_moves_from_Closed_to_Editing()
    {
        var sm = new PatternEditorStateMachine();

        sm.OpenExisting();

        Assert.Equal(PatternEditorState.Editing, sm.State);
    }

    [Fact]
    public void MarkDirty_sets_dirty_flag_and_state()
    {
        var sm = new PatternEditorStateMachine();
        sm.OpenExisting();

        sm.MarkDirty();

        Assert.True(sm.IsDirty);
        Assert.Equal(PatternEditorState.Dirty, sm.State);
    }

    [Fact]
    public void Cancel_from_dirty_state_moves_to_Cancelled()
    {
        var sm = new PatternEditorStateMachine();
        sm.OpenNew();
        sm.MarkDirty();

        sm.Cancel();

        Assert.Equal(PatternEditorState.Cancelled, sm.State);
        Assert.False(sm.IsOpen);
    }

    [Fact]
    public void Confirm_with_valid_data_moves_to_Confirmed()
    {
        var sm = new PatternEditorStateMachine();
        sm.OpenNew();
        sm.MarkDirty();

        sm.BeginConfirm();
        sm.CompleteConfirm(isValid: true);

        Assert.Equal(PatternEditorState.Confirmed, sm.State);
        Assert.False(sm.IsOpen);
    }

    [Fact]
    public void Confirm_with_invalid_data_moves_to_Invalid_and_editor_stays_open()
    {
        var sm = new PatternEditorStateMachine();
        sm.OpenNew();
        sm.MarkDirty();

        sm.BeginConfirm();
        sm.CompleteConfirm(isValid: false);

        Assert.Equal(PatternEditorState.Invalid, sm.State);
        Assert.True(sm.IsOpen);
    }

    [Fact]
    public void OpenNew_when_already_open_throws()
    {
        var sm = new PatternEditorStateMachine();
        sm.OpenNew();

        Assert.Throws<InvalidOperationException>(() => sm.OpenNew());
    }

    [Fact]
    public void MarkDirty_when_closed_throws()
    {
        var sm = new PatternEditorStateMachine();

        Assert.Throws<InvalidOperationException>(() => sm.MarkDirty());
    }

    [Fact]
    public void Reset_allows_reopening_after_confirm()
    {
        var sm = new PatternEditorStateMachine();
        sm.OpenNew();
        sm.BeginConfirm();
        sm.CompleteConfirm(isValid: true);

        sm.Reset();
        sm.OpenNew();

        Assert.Equal(PatternEditorState.Creating, sm.State);
    }
}
