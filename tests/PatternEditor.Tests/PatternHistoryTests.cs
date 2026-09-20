using PatternEditor.State;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// La cronologia di annullamento e ripetizione.
///
/// <para>
/// Come la macchina a stati, non dipende da Blazor: conserva testo e restituisce testo,
/// e si verifica con chiamate dirette. Quello che questi test guardano non è tanto che
/// «Annulla» torni indietro — quello è facile — quanto le regole che rendono l'annullamento
/// utile invece che esasperante: l'accorpamento delle modifiche continue, il punto di
/// partenza che resta sempre raggiungibile, e il ramo che si perde quando si riprende a
/// modificare dopo aver annullato.
/// </para>
/// </summary>
public class PatternHistoryTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Dopo(double millisecondi) => T0.AddMilliseconds(millisecondi);

    [Fact]
    public void A_new_history_has_nothing_to_undo_or_redo()
    {
        var storia = new PatternHistory();

        Assert.False(storia.PuoAnnullare);
        Assert.False(storia.PuoRipetere);
        Assert.Equal(0, storia.Conteggio);
    }

    [Fact]
    public void The_opening_state_alone_is_not_something_to_undo()
    {
        // Aprire un pattern non è una modifica: se lo fosse, il pulsante risulterebbe
        // acceso su un editor in cui non si è ancora toccato niente.
        var storia = new PatternHistory();
        storia.Inizia("A");

        Assert.False(storia.PuoAnnullare);
        Assert.False(storia.PuoRipetere);
        Assert.Equal(1, storia.Conteggio);
    }

    [Fact]
    public void Undo_gives_back_the_state_before_the_change()
    {
        var storia = new PatternHistory();
        storia.Inizia("A");
        storia.Registra("B", Dopo(1000));

        Assert.True(storia.PuoAnnullare);
        Assert.Equal("A", storia.Annulla());
        Assert.False(storia.PuoAnnullare);
        Assert.True(storia.PuoRipetere);
    }

    [Fact]
    public void Redo_gives_back_exactly_what_undo_took_away()
    {
        var storia = new PatternHistory();
        storia.Inizia("A");
        storia.Registra("B", Dopo(1000));
        storia.Annulla();

        Assert.Equal("B", storia.Ripeti());
        Assert.False(storia.PuoRipetere);
    }

    [Fact]
    public void The_very_first_change_is_never_merged_into_the_opening_state()
    {
        // Un cursore toccato nell'istante stesso in cui l'editor si apre rientrerebbe
        // nella finestra di accorpamento: accorparlo cancellerebbe il punto di partenza,
        // e «Annulla» non avrebbe più dove tornare.
        var storia = new PatternHistory();
        storia.Inizia("A");
        storia.Registra("B", Dopo(1));

        Assert.True(storia.PuoAnnullare);
        Assert.Equal("A", storia.Annulla());
    }

    [Fact]
    public void Changes_that_follow_each_other_closely_count_as_one_step()
    {
        // È il caso del cursore trascinato: quaranta valori intermedi in mezzo secondo.
        // Senza accorpamento servirebbero quaranta annullamenti per disfarne uno.
        var storia = new PatternHistory();
        storia.Inizia("A");
        storia.Registra("B", Dopo(1000));
        storia.Registra("C", Dopo(1100));
        storia.Registra("D", Dopo(1200));

        Assert.Equal(2, storia.Conteggio);
        Assert.Equal("A", storia.Annulla());
    }

    [Fact]
    public void Changes_far_apart_stay_separate_steps()
    {
        var storia = new PatternHistory();
        storia.Inizia("A");
        storia.Registra("B", Dopo(1000));
        storia.Registra("C", Dopo(1000) + PatternHistory.Finestra);

        Assert.Equal(3, storia.Conteggio);
        Assert.Equal("B", storia.Annulla());
        Assert.Equal("A", storia.Annulla());
    }

    [Fact]
    public void A_notification_that_changed_nothing_leaves_no_step_behind()
    {
        // Alcuni campi notificano anche quando il valore riscritto è identico. Un passo di
        // «Annulla» che non annulla niente sembra un guasto.
        var storia = new PatternHistory();
        storia.Inizia("A");

        Assert.False(storia.Registra("A", Dopo(1000)));
        Assert.Equal(1, storia.Conteggio);
        Assert.False(storia.PuoAnnullare);
    }

    [Fact]
    public void Changing_after_an_undo_throws_away_what_could_have_been_redone()
    {
        // Da qui in avanti la storia è un'altra. Tenere anche il ramo abbandonato
        // significherebbe un albero, e «Ripeti» dovrebbe chiedere quale.
        var storia = new PatternHistory();
        storia.Inizia("A");
        storia.Registra("B", Dopo(1000));
        storia.Registra("C", Dopo(3000));
        storia.Annulla();

        Assert.True(storia.PuoRipetere);

        storia.Registra("D", Dopo(5000));

        Assert.False(storia.PuoRipetere);
        Assert.Equal("B", storia.Annulla());
    }

    [Fact]
    public void A_change_right_after_an_undo_opens_a_step_of_its_own()
    {
        // Accorparla allo stato appena ripristinato lo farebbe sparire, e sparirebbe
        // proprio lo stato che l'utente ha appena scelto di rivedere.
        var storia = new PatternHistory();
        storia.Inizia("A");
        storia.Registra("B", Dopo(1000));
        storia.Registra("C", Dopo(3000));
        storia.Annulla();

        storia.Registra("D", Dopo(3010));

        Assert.True(storia.PuoAnnullare);
        Assert.Equal("B", storia.Annulla());
    }

    [Fact]
    public void Beyond_the_cap_the_oldest_states_are_the_ones_that_go()
    {
        // Una cronologia illimitata crescerebbe per tutta la durata della sessione. Il
        // limite si paga con i passi più lontani, che sono quelli che nessuno rifà.
        var storia = new PatternHistory();
        storia.Inizia("stato 0");

        for (var i = 1; i <= PatternHistory.Massimo + 20; i++)
        {
            storia.Registra($"stato {i}", Dopo(i * 1000));
        }

        Assert.Equal(PatternHistory.Massimo, storia.Conteggio);

        // Si torna indietro finché si può: l'ultimo stato raggiungibile non è più quello
        // di apertura, ma la cronologia resta coerente e non si rompe.
        var passi = 0;
        string? ultimo = null;
        while (storia.Annulla() is { } stato)
        {
            ultimo = stato;
            passi++;
        }

        Assert.Equal(PatternHistory.Massimo - 1, passi);
        Assert.Equal($"stato {PatternHistory.Massimo + 20 - (PatternHistory.Massimo - 1)}", ultimo);
    }

    [Fact]
    public void Opening_another_pattern_starts_from_scratch()
    {
        // Annullare dentro la sessione precedente rimetterebbe a schermo un pattern che
        // non è quello aperto.
        var storia = new PatternHistory();
        storia.Inizia("A");
        storia.Registra("B", Dopo(1000));
        storia.Registra("C", Dopo(3000));

        storia.Inizia("altro");

        Assert.Equal(1, storia.Conteggio);
        Assert.False(storia.PuoAnnullare);
        Assert.False(storia.PuoRipetere);
    }

    [Fact]
    public void Recording_without_having_opened_anything_opens_the_history()
    {
        // Difesa da un ordine di chiamate sbagliato: meglio una cronologia che parte da
        // qui che una che non parte affatto.
        var storia = new PatternHistory();

        Assert.True(storia.Registra("A", Dopo(1000)));
        Assert.Equal(1, storia.Conteggio);
        Assert.False(storia.PuoAnnullare);
    }
}
