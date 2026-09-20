using PatternEditor.Core.Models;
using Xunit;

namespace PatternEditor.Core.Tests;

/// <summary>
/// Verifica l'estrazione della data da un identificativo UUIDv7.
///
/// <para>
/// La versione 7 (RFC 9562) mette nei primi 48 bit i millisecondi trascorsi dal 1970: da un
/// identificativo si ricava quindi quando è stato generato, ed è questo che permette di
/// attribuire una data di creazione ai documenti salvati prima che le date esistessero. I
/// test coprono anche il rifiuto di un UUID di versione diversa, dove quei bit non
/// significano niente e leggerli darebbe una data inventata.
/// </para>
/// </summary>
public class Uuid7Tests
{
    [Fact]
    public void The_creation_time_of_a_freshly_generated_uuid_is_now()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        var id = Guid.CreateVersion7();
        var after = DateTimeOffset.UtcNow.AddSeconds(2);

        Assert.True(Uuid7.TryGetCreationTime(id, out var creationTime));
        Assert.InRange(creationTime, before, after);
    }

    [Fact]
    public void The_creation_time_matches_the_timestamp_the_uuid_was_built_with()
    {
        var expected = new DateTimeOffset(2024, 3, 15, 10, 30, 0, TimeSpan.Zero);

        var id = Guid.CreateVersion7(expected);

        Assert.True(Uuid7.TryGetCreationTime(id, out var creationTime));
        Assert.Equal(expected, creationTime);
    }

    [Fact]
    public void A_uuid_of_another_version_has_no_readable_creation_time()
    {
        // Guid.NewGuid() genera un UUIDv4: casuale, senza timestamp.
        Assert.False(Uuid7.TryGetCreationTime(Guid.NewGuid(), out var creationTime));
        Assert.Equal(DateTimeOffset.MinValue, creationTime);
    }

    [Fact]
    public void An_empty_guid_has_no_readable_creation_time()
    {
        Assert.False(Uuid7.TryGetCreationTime(Guid.Empty, out _));
    }

    [Fact]
    public void A_new_pattern_takes_its_creation_date_from_its_own_id()
    {
        var pattern = new Pattern();

        Assert.True(Uuid7.TryGetCreationTime(pattern.Id, out var fromId));
        Assert.Equal(fromId, pattern.CreatedAt);
        Assert.Equal(pattern.CreatedAt, pattern.ModifiedAt);
    }

    // ------------------------------------------------------------------ l'unicità
    //
    // Un UUIDv7 non è unico perché qualcuno tiene il conto: è unico perché dopo il timestamp
    // ci sono 74 bit tirati a sorte da un generatore crittografico. Non c'è un contatore da
    // condividere fra thread, e quindi non c'è niente da rendere atomico: due chiamate nello
    // stesso millisecondo differiscono per quei 74 bit, e la probabilità che coincidano è
    // dello stesso ordine di quella di indovinare una chiave.
    //
    // I due test che seguono lo mettono nero su bianco, e servono soprattutto a distinguere
    // questo meccanismo da un generatore *riproducibile* — quello di uno script che rigioca
    // la propria sequenza pseudo-casuale per riottenere lo stesso disegno, e che nello stesso
    // millisecondo riottiene anche lo stesso identificativo.

    [Fact]
    public void A_hundred_thousand_identifiers_generated_in_a_row_are_all_different()
    {
        const int quanti = 100_000;

        var identificativi = new HashSet<Guid>(quanti);
        for (var i = 0; i < quanti; i++)
        {
            Assert.True(identificativi.Add(Guid.CreateVersion7()), "Identificativo ripetuto.");
        }

        Assert.Equal(quanti, identificativi.Count);
    }

    [Fact]
    public void Identifiers_born_in_the_very_same_millisecond_are_different_anyway()
    {
        // Generati il più in fretta possibile: molti condividono il millisecondo, ed è
        // proprio il caso in cui un contatore mal protetto produrrebbe un doppione.
        var identificativi = Enumerable.Range(0, 20_000).Select(_ => Guid.CreateVersion7()).ToList();

        var perMillisecondo = identificativi
            .GroupBy(id => Uuid7.TryGetCreationTime(id, out var istante) ? istante : DateTimeOffset.MinValue)
            .ToList();

        // Il test sarebbe inutile se ogni identificativo avesse un millisecondo tutto suo:
        // si verifica che la situazione interessante si sia davvero verificata.
        Assert.Contains(perMillisecondo, gruppo => gruppo.Count() > 1);
        Assert.Equal(identificativi.Count, identificativi.Distinct().Count());
    }

    [Fact]
    public void Identifiers_generated_in_sequence_do_not_go_back_in_time()
    {
        var istanti = new List<DateTimeOffset>();
        for (var i = 0; i < 5_000; i++)
        {
            Assert.True(Uuid7.TryGetCreationTime(Guid.CreateVersion7(), out var istante));
            istanti.Add(istante);
        }

        // È la proprietà che rende utile la versione 7 rispetto alla 4: ordinare per
        // identificativo è ordinare per momento di creazione, ed è così che l'elenco dei
        // pattern mostra per primi quelli nuovi anche senza guardare le date.
        for (var i = 1; i < istanti.Count; i++)
        {
            Assert.True(istanti[i] >= istanti[i - 1], "Un identificativo è nato prima del precedente.");
        }
    }

    [Fact]
    public void The_generated_identifier_declares_version_seven_and_the_rfc_variant()
    {
        Span<byte> bytes = stackalloc byte[16];
        Assert.True(Guid.CreateVersion7().TryWriteBytes(bytes, bigEndian: true, out _));

        // Quattro bit di versione nel settimo byte, due bit di variante nel nono: sono i
        // campi che permettono a chi legge di sapere che i primi sei byte sono una data.
        Assert.Equal(7, (bytes[6] & 0xF0) >> 4);
        Assert.Equal(0b10, (bytes[8] & 0xC0) >> 6);
    }
}
