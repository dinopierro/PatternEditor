using PatternEditor.Abstractions.Serialization;
using PatternEditor.Abstractions.Tests;
using PatternEditor.Core.Models;
using PatternEditor.Services;
using Xunit;

namespace PatternEditor.Tests;

/// <summary>
/// L'importazione di un documento SVG.
///
/// <para>
/// Il difetto che questi test cercano non è l'eccezione: è il <b>silenzio</b>. Un
/// importatore che scarta una forma senza dirlo, o che la mette nel posto sbagliato perché
/// ha ignorato la traslazione del gruppo che la contiene, produce un pattern che sembra
/// venuto male — e chi lo guarda pensa di aver sbagliato lui.
/// </para>
///
/// <para>
/// Il caso più insidioso è quello dei valori predefiniti. Un documento che non dichiara
/// l'opacità non sta dicendo «zero»: sta dicendo «quella normale». Se l'importatore
/// partisse da una struttura vuota invece che dai valori del plugin, ogni elemento
/// importato nascerebbe invisibile.
/// </para>
/// </summary>
public class SvgPatternImporterTests
{
    private static ISvgPatternImporter Importatore()
    {
        var registro = AllPlugins.Registry();
        return new SvgPatternImporter(registro, new PatternSerializer(registro));
    }

    private static Pattern Importa(string svg, string nome = "Prova")
    {
        var esito = Importatore().Import(svg, nome);
        Assert.False(esito.IsEmpty, string.Join(" · ", esito.Warnings));
        return esito.Pattern!;
    }

    // ------------------------------------------------------------------ la cella

    [Fact]
    public void The_cell_comes_from_the_view_box()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" width="10cm" height="5cm" viewBox="0 0 80 40">
              <rect x="0" y="0" width="80" height="40" fill="#112233"/>
            </svg>
            """);

        // Non dalle misure a schermo: il viewBox è il sistema di coordinate in cui il
        // disegno è scritto, ed è quello che la cella deve riprodurre.
        Assert.Equal(80, pattern.Definition.Width);
        Assert.Equal(40, pattern.Definition.Height);
    }

    [Fact]
    public void A_view_box_that_does_not_start_at_zero_moves_the_drawing_into_the_cell()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="10 20 50 50">
              <rect x="10" y="20" width="30" height="30"/>
            </svg>
            """);

        var nodo = Assert.Single(pattern.Definition.Elements);
        Assert.Equal("rect", nodo.Type);
        Assert.Equal(0.0, Proprieta(nodo, "X"));
        Assert.Equal(0.0, Proprieta(nodo, "Y"));
    }

    [Fact]
    public void Without_a_view_box_the_measures_are_used_and_the_units_converted()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" width="1in" height="72pt">
              <circle cx="10" cy="10" r="5"/>
            </svg>
            """);

        // 1 pollice e 72 punti sono la stessa lunghezza: 96 pixel.
        Assert.Equal(96, pattern.Definition.Width);
        Assert.Equal(96, pattern.Definition.Height);
    }

    // ------------------------------------------------------------------ le forme

    [Fact]
    public void Every_shape_the_application_knows_is_imported_with_its_own_type()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="1" y="2" width="3" height="4"/>
              <circle cx="5" cy="6" r="7"/>
              <ellipse cx="8" cy="9" rx="10" ry="11"/>
              <line x1="0" y1="0" x2="10" y2="10"/>
              <path d="M 0 0 L 10 10"/>
              <polygon points="0,0 10,0 5,8"/>
              <polyline points="0,0 10,0 5,8"/>
              <text x="5" y="5">Ciao</text>
              <image x="0" y="0" width="10" height="10" href="https://esempio.invalid/a.png"/>
            </svg>
            """);

        Assert.Equal(
            new[] { "rect", "circle", "ellipse", "line", "path", "polygon", "polyline", "text", "image" },
            pattern.Definition.Elements.Select(e => e.Type));

        // L'ordine del documento è l'ordine di disegno, e va conservato: in SVG come qui,
        // chi viene dopo sta sopra.
        Assert.Equal(9, pattern.Definition.Elements.Count);
    }

    [Fact]
    public void The_geometry_of_a_shape_arrives_intact()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="12.5" y="34" width="20" height="8"/>
            </svg>
            """);

        var rect = Assert.Single(pattern.Definition.Elements);
        Assert.Equal(12.5, Proprieta(rect, "X"));
        Assert.Equal(34.0, Proprieta(rect, "Y"));
        Assert.Equal(20.0, Proprieta(rect, "Width"));
        Assert.Equal(8.0, Proprieta(rect, "Height"));
    }

    [Fact]
    public void The_text_of_a_text_element_comes_from_inside_the_tag()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <text x="5" y="20" font-size="14" text-anchor="middle">Ciao &amp; arrivederci</text>
            </svg>
            """);

        var testo = Assert.Single(pattern.Definition.Elements);
        Assert.Equal("Ciao & arrivederci", Proprieta(testo, "Content"));
        Assert.Equal(14.0, Proprieta(testo, "FontSize"));
        Assert.Equal("middle", Proprieta(testo, "TextAnchor"));
    }

    // ------------------------------------------------------------------ i valori predefiniti

    [Fact]
    public void What_the_document_does_not_say_keeps_the_value_of_the_plugin()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10"/>
            </svg>
            """);

        var rect = Assert.Single(pattern.Definition.Elements);

        // Il caso che rovina tutto se trattato male: un documento che tace sull'opacità non
        // sta chiedendo un elemento invisibile.
        Assert.Equal(1.0, Proprieta(rect, "Opacity"));
        Assert.Equal(1.0, Proprieta(rect, "FillOpacity"));
    }

    [Fact]
    public void What_the_document_does_not_say_about_paint_is_said_by_the_specification()
    {
        // Il difetto che questo test fissa si vede solo sui file veri. Un tracciato senza
        // «fill» in SVG è nero pieno; il plugin «path», che nasce per disegnare linee, ha
        // come predefiniti nessun riempimento e un bordo nero. Lasciando decidere il plugin,
        // un logo importato usciva a contorno invece che pieno — e il resoconto diceva di
        // aver preso tutto, perché in effetti nessuna forma era stata scartata.
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <path d="M 10 10 L 90 10 L 50 90 Z"/>
            </svg>
            """);

        var tracciato = Assert.Single(pattern.Definition.Elements);
        Assert.Equal("#000000", Proprieta(tracciato, "Fill"));
        Assert.Null(Proprieta(tracciato, "Stroke"));
        Assert.Equal(1.0, Proprieta(tracciato, "StrokeWidth"));
    }

    [Fact]
    public void A_shape_that_declares_its_paint_keeps_it()
    {
        // L'altra metà della regola: i valori della specifica valgono SOLO dove il documento
        // tace, e un attributo ereditato da un gruppo è già una dichiarazione.
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <g fill="#00ff00">
                <path d="M 0 0 L 10 10" stroke="#ff0000" stroke-width="3"/>
              </g>
            </svg>
            """);

        var tracciato = Assert.Single(pattern.Definition.Elements);
        Assert.Equal("#00ff00", Proprieta(tracciato, "Fill"));
        Assert.Equal("#ff0000", Proprieta(tracciato, "Stroke"));
        Assert.Equal(3.0, Proprieta(tracciato, "StrokeWidth"));
    }

    [Theory]
    [InlineData("rx=\"4\"", "rounded corners")]
    [InlineData("clip-path=\"url(#taglio)\"", "clip")]
    [InlineData("filter=\"url(#sfoca)\"", "filter")]
    public void What_is_lost_while_keeping_the_shape_is_said_too(string attributo, string atteso)
    {
        // Non sono forme scartate: la forma entra. Ma se non si dice niente, il resoconto
        // dichiara «tutto preso» mentre il rettangolo ha perso gli angoli arrotondati, e chi
        // guarda dà la colpa al disegno.
        var esito = Importatore().Import($"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" {attributo}/>
            </svg>
            """, "Prova");

        Assert.Equal(1, esito.Imported);
        Assert.Equal(0, esito.Skipped);
        Assert.Contains(esito.Warnings, a => a.Contains(atteso, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void An_attribute_the_model_does_not_have_is_ignored_without_complaining()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" rx="3" data-figma="qualcosa" shape-rendering="crispEdges"/>
            </svg>
            """);

        // Il rettangolo del modello non ha gli angoli arrotondati né sa che cosa sia Figma:
        // quelle informazioni si perdono, ma la forma entra lo stesso.
        var rect = Assert.Single(pattern.Definition.Elements);
        Assert.Equal(10.0, Proprieta(rect, "Width"));
    }

    // ------------------------------------------------------------------ colori e stile

    [Theory]
    [InlineData("#1a2b3c", "#1a2b3c")]
    [InlineData("#abc", "#aabbcc")]
    [InlineData("red", "#ff0000")]
    [InlineData("RED", "#ff0000")]
    [InlineData("rgb(255, 128, 0)", "#ff8000")]
    public void Colours_written_in_any_of_the_svg_ways_become_hexadecimal(string scritto, string atteso)
    {
        var pattern = Importa($"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" fill="{scritto}"/>
            </svg>
            """);

        Assert.Equal(atteso, Proprieta(Assert.Single(pattern.Definition.Elements), "Fill"));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("transparent")]
    [InlineData("url(#gradiente)")]
    public void A_paint_the_model_cannot_express_becomes_no_paint_at_all(string scritto)
    {
        // «none» è assenza di colore, e un gradiente il modello non lo ha: in entrambi i casi
        // l'onestà è non riempire, non inventare un colore che nell'originale non c'era.
        // Il bordo serve a tenere l'elemento visibile: senza né l'uno né l'altro sarebbe
        // invisibile e non verrebbe importato affatto (vedi il test qui sotto).
        var pattern = Importa($"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" fill="{scritto}" stroke="#000000"/>
            </svg>
            """);

        Assert.Null(Proprieta(Assert.Single(pattern.Definition.Elements), "Fill"));
    }

    [Fact]
    public void A_shape_that_would_be_invisible_is_left_out()
    {
        // Senza riempimento e senza bordo l'elemento non si vedrebbe nemmeno nell'originale:
        // importarlo lo farebbe comparire nell'elenco degli elementi e in nessun altro posto,
        // che è il modo migliore per far dubitare di aver sbagliato qualcosa.
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" fill="#123456"/>
              <rect x="0" y="0" width="10" height="10" fill="url(#gradiente)"/>
            </svg>
            """, "Prova");

        Assert.Equal(1, esito.Imported);
        Assert.Equal(1, esito.Skipped);
        Assert.Contains(esito.Warnings, a => a.Contains("gradient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_shape_without_fill_is_black_not_invisible()
    {
        // In SVG l'assenza di «fill» vuol dire nero, non trasparente: trattare i due valori
        // assenti allo stesso modo farebbe sparire mezzo documento.
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10"/>
            </svg>
            """, "Prova");

        Assert.Equal(1, esito.Imported);
    }

    [Fact]
    public void The_inline_style_wins_over_the_presentation_attribute()
    {
        // È la regola della specifica, ed è quella che conta in pratica: i programmi di
        // disegno scrivono quasi tutto dentro style.
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" fill="#000000" style="fill:#ff0000;stroke-width:3"/>
            </svg>
            """);

        var rect = Assert.Single(pattern.Definition.Elements);
        Assert.Equal("#ff0000", Proprieta(rect, "Fill"));
        Assert.Equal(3.0, Proprieta(rect, "StrokeWidth"));
    }

    [Fact]
    public void What_a_group_declares_reaches_the_shapes_inside_it()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <g fill="#00ff00" stroke="#0000ff" stroke-width="2">
                <rect x="0" y="0" width="10" height="10"/>
                <rect x="20" y="0" width="10" height="10" fill="#ff0000"/>
              </g>
            </svg>
            """);

        // Il primo eredita, il secondo dichiara il proprio e vince.
        Assert.Equal("#00ff00", Proprieta(pattern.Definition.Elements[0], "Fill"));
        Assert.Equal("#ff0000", Proprieta(pattern.Definition.Elements[1], "Fill"));
        Assert.Equal("#0000ff", Proprieta(pattern.Definition.Elements[0], "Stroke"));
        Assert.Equal(2.0, Proprieta(pattern.Definition.Elements[1], "StrokeWidth"));
    }

    // ------------------------------------------------------------------ le trasformazioni

    [Fact]
    public void A_translated_group_moves_the_shapes_it_contains()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <g transform="translate(10, 20)">
                <rect x="5" y="5" width="10" height="10"/>
                <circle cx="0" cy="0" r="4"/>
              </g>
            </svg>
            """);

        Assert.Equal(15.0, Proprieta(pattern.Definition.Elements[0], "X"));
        Assert.Equal(25.0, Proprieta(pattern.Definition.Elements[0], "Y"));
        Assert.Equal(10.0, Proprieta(pattern.Definition.Elements[1], "Cx"));
        Assert.Equal(20.0, Proprieta(pattern.Definition.Elements[1], "Cy"));
    }

    [Fact]
    public void Nested_groups_compose_their_transforms()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <g transform="translate(10, 10)">
                <g transform="scale(2)">
                  <rect x="5" y="5" width="10" height="10"/>
                </g>
              </g>
            </svg>
            """);

        // La scala interna si applica prima, poi la traslazione esterna: 5·2+10.
        var rect = Assert.Single(pattern.Definition.Elements);
        Assert.Equal(20.0, Proprieta(rect, "X"));
        Assert.Equal(20.0, Proprieta(rect, "Y"));
        Assert.Equal(20.0, Proprieta(rect, "Width"));
    }

    [Fact]
    public void A_transform_also_rewrites_the_data_of_a_path()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <g transform="translate(10,20) scale(2)">
                <path d="M 1 1 L 5 5 H 9 V 3 Z"/>
              </g>
            </svg>
            """);

        var tracciato = (string)Proprieta(Assert.Single(pattern.Definition.Elements), "D")!;

        // Comandi assoluti: scala e poi traslazione, su entrambe le coordinate.
        Assert.Contains("M 12 22", tracciato);
        Assert.Contains("L 20 30", tracciato);
        Assert.Contains("H 28", tracciato);
        Assert.Contains("V 26", tracciato);
        Assert.EndsWith("Z", tracciato);
    }

    [Fact]
    public void In_a_path_the_relative_commands_are_only_scaled()
    {
        // Sommare la traslazione anche agli spostamenti relativi sposterebbe il tracciato a
        // ogni segmento: è l'errore classico, e si vede solo su un disegno complesso.
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <g transform="translate(100,100) scale(2)">
                <path d="M 1 1 l 5 5 l -2 0"/>
              </g>
            </svg>
            """);

        var tracciato = (string)Proprieta(Assert.Single(pattern.Definition.Elements), "D")!;

        Assert.Contains("M 102 102", tracciato);
        Assert.Contains("l 10 10", tracciato);
        Assert.Contains("l -4 0", tracciato);
    }

    [Fact]
    public void A_path_that_starts_with_a_lowercase_move_is_still_placed_correctly()
    {
        // La specifica dice che il primo spostamento è assoluto anche se scritto in
        // minuscolo. Trattarlo come relativo manderebbe l'intero tracciato fuori posto.
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <g transform="translate(50,50)">
                <path d="m 1 1 l 5 5"/>
              </g>
            </svg>
            """);

        var tracciato = (string)Proprieta(Assert.Single(pattern.Definition.Elements), "D")!;

        Assert.Contains("m 51 51", tracciato);
        Assert.Contains("l 5 5", tracciato);
    }

    [Fact]
    public void A_transform_also_rewrites_the_points_of_a_polygon()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <polygon points="0,0 10,0 5,8" transform="translate(5,5)"/>
            </svg>
            """);

        Assert.Equal("5,5 15,5 10,13", Proprieta(Assert.Single(pattern.Definition.Elements), "Points"));
    }

    [Fact]
    public void A_rotation_becomes_the_rotation_of_the_element()
    {
        // Il modello ha una rotazione per elemento: una rotazione del documento non va più
        // scartata, va riscritta lì. Ignorarla darebbe un disegno sbagliato senza dirlo, che
        // resta il modo peggiore di sbagliare.
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <g transform="rotate(30)">
                <rect x="20" y="20" width="10" height="10"/>
              </g>
            </svg>
            """);

        var rect = Assert.Single(pattern.Definition.Elements);
        Assert.Equal(30, (double)Proprieta(rect, "Rotation")!, 4);
        Assert.Equal(20.0, Proprieta(rect, "X"));
        Assert.Equal(20.0, Proprieta(rect, "Y"));
    }

    [Fact]
    public void A_rotation_around_a_point_keeps_that_point()
    {
        var pattern = Importa("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="10" y="10" width="10" height="10" transform="rotate(90, 50, 50)"/>
            </svg>
            """);

        var rect = Assert.Single(pattern.Definition.Elements);

        // Ruotare di 90 gradi attorno al centro della cella porta il punto (10,10) in
        // (90,10): il disegno finale deve stare lì, comunque lo si scriva.
        Assert.Equal(90, (double)Proprieta(rect, "Rotation")!, 4);
        Assert.Equal(100.0, (double)Proprieta(rect, "OriginX")!, 4);
        Assert.Equal(0.0, (double)Proprieta(rect, "OriginY")!, 4);
    }

    [Fact]
    public void A_skew_is_still_refused_and_explained()
    {
        // Un'inclinazione cambia gli angoli fra i lati: nessuna forma del modello — che ha
        // rettangoli, cerchi e archi — resterebbe sé stessa, e non c'è un modo onesto di
        // approssimarla.
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10"/>
              <g transform="skewX(20)">
                <rect x="20" y="20" width="10" height="10"/>
              </g>
            </svg>
            """, "Prova");

        Assert.Equal(1, esito.Imported);
        Assert.Equal(1, esito.Skipped);
        Assert.Contains(esito.Warnings, a => a.Contains("skew", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_rotation_with_a_non_uniform_scale_is_refused()
    {
        // Scala diversa sui due assi e rotazione insieme non sono una similitudine: gli
        // angoli cambiano, e la forma non si conserva.
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10"/>
              <g transform="scale(2,1) rotate(30)">
                <rect x="20" y="20" width="10" height="10"/>
              </g>
            </svg>
            """, "Prova");

        Assert.Equal(1, esito.Imported);
        Assert.Equal(1, esito.Skipped);
    }

    // ------------------------------------------------------------------ che cosa resta fuori

    [Fact]
    public void What_is_only_a_definition_is_not_drawn()
    {
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <defs>
                <rect id="modello" x="0" y="0" width="5" height="5"/>
              </defs>
              <rect x="10" y="10" width="10" height="10"/>
            </svg>
            """, "Prova");

        // Il rettangolo dentro <defs> nel documento originale non si vede: importarlo
        // farebbe comparire una forma che non c'era.
        Assert.Equal(1, esito.Imported);
        Assert.Equal(10.0, Proprieta(esito.Pattern!.Definition.Elements[0], "X"));
    }

    [Fact]
    public void A_hidden_shape_stays_hidden()
    {
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10"/>
              <rect x="0" y="0" width="10" height="10" display="none"/>
              <rect x="0" y="0" width="10" height="10" style="visibility:hidden"/>
            </svg>
            """, "Prova");

        Assert.Equal(1, esito.Imported);
    }

    [Fact]
    public void A_tag_that_no_plugin_handles_is_counted_and_explained()
    {
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10"/>
              <use href="#qualcosa"/>
              <foreignObject width="10" height="10"/>
            </svg>
            """, "Prova");

        Assert.Equal(1, esito.Imported);
        Assert.Equal(2, esito.Skipped);
        Assert.Contains(esito.Warnings, a => a.Contains("<use>", StringComparison.Ordinal));
        Assert.Contains(esito.Warnings, a => a.Contains("<foreignObject>", StringComparison.Ordinal));
    }

    [Fact]
    public void Identical_reasons_are_grouped_instead_of_repeated()
    {
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10"/>
              <use href="#a"/><use href="#b"/><use href="#c"/>
            </svg>
            """, "Prova");

        // Trecento forme scartate per lo stesso motivo non devono produrre trecento righe:
        // un elenco che non si legge è un elenco che non c'è.
        var riga = Assert.Single(esito.Warnings, a => a.Contains("<use>", StringComparison.Ordinal));
        Assert.Contains("3 times", riga);
    }

    // ------------------------------------------------------------------ i documenti che non vanno

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("questo non è XML")]
    [InlineData("<html><body>ciao</body></html>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><g/></svg>")]
    public void A_document_that_cannot_be_used_says_why_instead_of_throwing(string contenuto)
    {
        var esito = Importatore().Import(contenuto, "Prova");

        Assert.True(esito.IsEmpty);
        Assert.Null(esito.Pattern);
        Assert.NotEmpty(esito.Warnings);
    }

    [Fact]
    public void A_document_that_declares_a_document_type_is_refused_not_followed()
    {
        // Un lettore XML che segue le definizioni di tipo può essere convinto a leggere file
        // locali o ad aprire connessioni. Il file arriva da fuori: non si segue niente.
        var esito = Importatore().Import("""
            <?xml version="1.0"?>
            <!DOCTYPE svg SYSTEM "http://esempio.invalid/svg.dtd">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="5" height="5"/></svg>
            """, "Prova");

        Assert.True(esito.IsEmpty);
    }

    // ------------------------------------------------------------------ il risultato

    [Fact]
    public void The_imported_pattern_is_a_new_pattern_ready_to_be_reviewed()
    {
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 40 40">
              <rect x="0" y="0" width="40" height="40" fill="#123456"/>
            </svg>
            """, "Logo aziendale");

        var pattern = esito.Pattern!;

        Assert.Equal("Logo aziendale", pattern.Name);
        Assert.NotEqual(Guid.Empty, pattern.Id);
        Assert.True(Uuid7.TryGetCreationTime(pattern.Id, out _));

        // La trasformazione parte neutra: quella del documento è già finita nella geometria.
        Assert.Equal(1, pattern.Definition.Scale);
        Assert.Equal(0, pattern.Definition.Rotation);

        // Identificativi tutti diversi, come per qualunque altro pattern.
        var identificativi = pattern.Definition.Elements.Select(e => e.Id).ToList();
        Assert.Equal(identificativi.Count, identificativi.Distinct().Count());
    }

    [Fact]
    public void The_imported_pattern_passes_its_own_validation()
    {
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 60 60">
              <rect x="0" y="0" width="60" height="60" fill="#eeeeee"/>
              <circle cx="30" cy="30" r="20" fill="none" stroke="#333333" stroke-width="2"/>
              <path d="M 10 10 L 50 50" stroke="#ff0000"/>
            </svg>
            """, "Prova");

        var esitoValidazione = new PatternValidator(AllPlugins.Registry()).Validate(esito.Pattern!);

        // Un pattern importato che nasce già in errore costringerebbe a correggere qualcosa
        // prima ancora di aver guardato che cosa è arrivato.
        Assert.True(esitoValidazione.IsValid, string.Join(" · ", esitoValidazione.Errors));
    }

    [Fact]
    public void An_imported_pattern_renders_the_shapes_it_took()
    {
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 50 50">
              <rect x="0" y="0" width="50" height="50" fill="#9f2828"/>
            </svg>
            """, "Prova");

        var markup = new PatternSvgRenderer(AllPlugins.Registry(), new SvgFilterRenderer()).RenderElementsMarkup(esito.Pattern!);

        Assert.Contains("<rect", markup);
        Assert.Contains("#9f2828", markup);
    }

    // ------------------------------------------------------------------ i file veri

    [Fact]
    public void The_private_data_of_a_drawing_program_is_not_counted_as_a_lost_shape()
    {
        // I file di Inkscape e simili portano dentro i propri dati in uno spazio dei nomi
        // tutto loro. Non sono disegno, e contarli fra le forme rimaste fuori farebbe
        // leggere come un difetto quello che non è nemmeno una forma.
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:sodipodi="http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd"
                 xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                 xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape"
                 viewBox="0 0 100 100">
              <sodipodi:namedview pagecolor="#ffffff" inkscape:zoom="2.5"/>
              <metadata><rdf:RDF/></metadata>
              <rect x="0" y="0" width="10" height="10"/>
            </svg>
            """, "Prova");

        Assert.Equal(1, esito.Imported);
        Assert.Equal(0, esito.Skipped);
        Assert.Empty(esito.Warnings);
    }

    [Fact]
    public void A_file_that_is_not_xml_is_explained_in_words_not_in_codes()
    {
        // In WebAssembly le risorse di traduzione del framework non ci sono, e il messaggio
        // di un'eccezione esce come nome interno: «Xml_InvalidRootData, 1, 1». Riga e
        // posizione invece sono numeri, e si capiscono in qualunque lingua.
        var esito = Importatore().Import("<!DOCTYPE html><html><body>pagina di errore</body></html>", "Prova");

        Assert.True(esito.IsEmpty);
        var messaggio = Assert.Single(esito.Warnings);
        Assert.DoesNotContain("Xml_", messaggio, StringComparison.Ordinal);
        Assert.Contains("line", messaggio, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_drawing_made_of_hundreds_of_paths_under_a_matrix_arrives_whole()
    {
        // È la forma dei file veri: un unico gruppo con una matrice di sola scala e
        // traslazione, e dentro centinaia di tracciati. Deve entrare tutto.
        var tracciati = string.Concat(Enumerable.Range(0, 300).Select(i =>
            $"<path d=\"M {i} 0 l 4 4 l -4 0 z\" fill=\"#{i % 10}{i % 10}3344\"/>"));

        var esito = Importatore().Import($"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 900 900">
              <g transform="matrix(1.7656463,0,0,1.7656463,324.90716,255.00942)">{tracciati}</g>
            </svg>
            """, "Molti tracciati");

        Assert.Equal(300, esito.Imported);
        Assert.Equal(0, esito.Skipped);

        // La matrice è finita nella geometria: il primo tracciato non comincia più da zero.
        var primo = (string)Proprieta(esito.Pattern!.Definition.Elements[0], "D")!;
        Assert.StartsWith("M 324.9072 255.0094", primo);
    }

    // ------------------------------------------------------------------ il giro completo

    [Fact]
    public void An_svg_exported_by_the_application_comes_back_as_the_pattern_it_was()
    {
        // È il caso che chiunque prova per primo: si scarica l'SVG di un pattern e lo si
        // riapre. Lì il disegno sta dentro <defs><pattern>, che di regola non si importa
        // perché è una definizione — ma quella definizione è esattamente il pattern, e
        // rifiutarla vorrebbe dire non saper rileggere il proprio formato.
        var originale = new Pattern { Name = "Andata" };
        originale.Definition.Width = 50;
        originale.Definition.Height = 30;
        originale.Definition.Scale = 0.5;
        originale.Definition.Rotation = 45;
        originale.Definition.TranslateX = 7;
        originale.Definition.TranslateY = 3;
        foreach (var tipo in new[] { "rect", "line" })
        {
            // Senza trasformazione propria: quella si verifica a parte, perché il giro la
            // conserva come DISEGNO ma può riscriverla in un altro modo (vedi il test dopo).
            var elemento = AllPlugins.Populated(AllPlugins.Of(tipo));
            elemento.Rotation = 0;
            elemento.FlipX = false;
            elemento.FlipY = false;
            elemento.OriginX = 0;
            elemento.OriginY = 0;
            originale.Definition.Elements.Add(elemento);
        }

        var svg = new PatternSvgRenderer(AllPlugins.Registry(), new SvgFilterRenderer()).RenderStandaloneSvg(originale);
        var tornato = Importa(svg, "Ritorno");

        Assert.Equal(50, tornato.Definition.Width);
        Assert.Equal(30, tornato.Definition.Height);
        Assert.Equal(new[] { "rect", "line" }, tornato.Definition.Elements.Select(e => e.Type));

        // La trasformazione del nodo <pattern> torna dov'era: nella definizione, non cotta
        // dentro la geometria degli elementi.
        Assert.Equal(0.5, tornato.Definition.Scale);
        Assert.Equal(45, tornato.Definition.Rotation);
        Assert.Equal(7, tornato.Definition.TranslateX);
        Assert.Equal(3, tornato.Definition.TranslateY);

        // E la geometria è quella di partenza, non una sua approssimazione.
        Assert.Equal(Proprieta(originale.Definition.Elements[0], "X"),
                     Proprieta(tornato.Definition.Elements[0], "X"));
        Assert.Equal(Proprieta(originale.Definition.Elements[0], "Fill"),
                     Proprieta(tornato.Definition.Elements[0], "Fill"));
    }

    [Fact]
    public void A_rotated_element_survives_the_export_and_the_import()
    {
        // Qui il giro conserva il DISEGNO, non la scrittura. Il renderer scrive la
        // specchiatura come una scala negativa, e una scala negativa su entrambi gli assi è
        // una rotazione di mezzo giro: rileggendo, le due cose si fondono in un solo angolo.
        // Il risultato sullo schermo è identico, i numeri no — ed è giusto chiedere il primo.
        var originale = new Pattern { Name = "Ruotato" };
        originale.Definition.Width = 60;
        originale.Definition.Height = 60;

        var rect = AllPlugins.Populated(AllPlugins.Of("rect"));
        rect.Rotation = 30;
        rect.FlipX = false;
        rect.FlipY = false;
        rect.OriginX = 30;
        rect.OriginY = 30;
        originale.Definition.Elements.Add(rect);

        var svg = new PatternSvgRenderer(AllPlugins.Registry(), new SvgFilterRenderer()).RenderStandaloneSvg(originale);
        var tornato = Importa(svg, "Ritorno");
        var elemento = Assert.Single(tornato.Definition.Elements);

        Assert.Equal("rect", elemento.Type);
        Assert.True(elemento.HasTransform);
        Assert.Equal(30, elemento.Rotation, 3);

        // Le proprietà che non c'entrano con la posizione tornano identiche.
        Assert.Equal(Proprieta(rect, "Fill"), Proprieta(elemento, "Fill"));
        Assert.Equal(Proprieta(rect, "Width"), Proprieta(elemento, "Width"));
    }

    [Fact]
    public void A_pattern_measured_in_fractions_is_not_taken_for_a_cell()
    {
        // patternUnits="objectBoundingBox" vuol dire che quelle misure sono frazioni del
        // riquadro che le usa: una cella di 0,25 × 0,25 non significa niente qui.
        var esito = Importatore().Import("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
              <defs>
                <pattern id="p" width="0.25" height="0.25" patternUnits="objectBoundingBox">
                  <rect x="0" y="0" width="1" height="1"/>
                </pattern>
              </defs>
            </svg>
            """, "Prova");

        Assert.True(esito.IsEmpty);
    }

    /// <summary>
    /// Legge una proprietà del modello concreto per riflessione.
    ///
    /// I test vivono dalla parte di chi guarda il risultato, non di chi lo costruisce: qui
    /// interessa che nel rettangolo ci sia finito 12.5, non quale classe lo rappresenti.
    /// </summary>
    private static object? Proprieta(VectorElement elemento, string nome) =>
        elemento.GetType().GetProperty(nome)?.GetValue(elemento);
}
