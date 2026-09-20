# -*- coding: utf-8 -*-
"""Le frasi delle figure, dall'italiano all'inglese.

Il dizionario e' per **frase intera** e non per figura: le stesse parole ricorrono in figure
diverse, e una chiave per figura le farebbe tradurre due volte, con il rischio che le due
traduzioni divergano. La chiave e' la frase italiana cosi' com'e' scritta in genera-figure.py:
se qualcuno la cambia li', qui smette di corrispondere e lo script lo dice invece di tacere.

Le frasi ancora senza traduzione vengono elencate a fine esecuzione di

    python docs/strumenti/genera-figure.py en

e nel frattempo escono in italiano: meglio una figura mista che una figura vuota.
"""

TESTI = {
    # --- titoli e sottotitoli -----------------------------------------------------------
    "Figura 1 — La pagina iniziale":
        "Figure 1 — The opening page",
    "L'archivio dei pattern: si ordina, si sfoglia, si apre. Schermata dell'applicazione in funzione.":
        "The pattern archive: sort it, browse it, open it. A screenshot of the running application.",

    "Figura 2 — Le tre zone dell'editor":
        "Figure 2 — The three areas of the editor",
    "Si regola a sinistra, si osserva al centro, si compone a destra.":
        "You adjust on the left, watch in the middle, compose on the right.",

    "Figura 9 — Una riga dell'elenco, pezzo per pezzo":
        "Figure 9 — A row of the list, piece by piece",
    "Ogni riga è un elemento della cella, e l'ordine delle righe è l'ordine di disegno.":
        "Each row is an element of the cell, and the order of the rows is the drawing order.",

    "Figura 10 — I tipi di elemento disponibili":
        "Figure 10 — The available element types",
    "Il menù non è un elenco fisso: mostra i plugin che l'applicazione ha registrato all'avvio.":
        "The menu is not a fixed list: it shows the plugins the application registered at start-up.",

    "Figura 11 — Come si sceglie un colore":
        "Figure 11 — How a colour is chosen",
    "Tre strade per la stessa cosa: la pastiglia, il codice esadecimale, la trasparenza.":
        "Three roads to the same thing: the swatch, the hex code, the transparency.",

    "Figura 12 — L'editor su uno schermo stretto":
        "Figure 12 — The editor on a narrow screen",
    "Le stesse funzioni, disposte in altezza: anteprima in alto, schede sotto.":
        "The same functions, stacked: preview at the top, tabs below.",

    "Figura 13 — Importare un disegno SVG":
        "Figure 13 — Importing an SVG drawing",
    "Si sceglie il file, si legge il resoconto, si decide. Niente entra nell'archivio prima di allora.":
        "You choose the file, read the report, decide. Nothing enters the archive before then.",

    "Figura 14 — Convertire un'immagine in pattern":
        "Figure 14 — Converting an image into a pattern",
    "I tre riquadri rispondono alla sola domanda che conta: somiglia? E dove no, in che modo.":
        "The three panels answer the only question that counts: does it match? And where not, how.",

    # --- legende ------------------------------------------------------------------------
    "L'intestazione: che cos'è il componente, quanti pattern ci sono, e il pulsante per crearne uno":
        "The header: what the component is, how many patterns there are, and the button to create one",
    "La barra: quanti pattern, con quale ordine e quante schede per pagina":
        "The bar: how many patterns, in what order and how many cards per page",
    "La scheda: l'anteprima è cliccabile e apre il pattern in modifica":
        "The card: the preview is clickable and opens the pattern for editing",
    "I dati della scheda: misura della cella, creazione e — se diversa — ultima modifica":
        "The card's data: cell size, creation and — if different — last change",
    "Tema chiaro, scuro o come il sistema":
        "Light theme, dark, or as the system",

    "Nome del pattern e comandi per chiudere la sessione (§3.1)":
        "Pattern name and the commands that close the session (§3.1)",
    "Proprietà: misura della cella e trasformazione":
        "Properties: cell size and transformation",
    "Le due anteprime: la cella singola e la ripetizione":
        "The two previews: the single cell and the repetition",
    "Il sorgente SVG, da copiare o scaricare":
        "The SVG source, to copy or download",
    "Elementi: l'elenco di ciò che compone la cella":
        "Elements: the list of what makes up the cell",
    "Identificativo, date e stato della sessione":
        "Identifier, dates and session state",

    "La presa: si tiene premuta e si trascina in su o in giù per cambiare l'ordine":
        "The grip: hold it and drag up or down to change the order",
    "L'icona dice il tipo di elemento":
        "The icon says the type of element",
    "Il nome del tipo; un clic in un punto qualsiasi della riga apre la sua scheda":
        "The type's name; a click anywhere on the row opens its card",
    "La freccia ricorda che la riga si apre":
        "The arrow is a reminder that the row opens",
    "Duplica l'elemento, con gli stessi valori":
        "Duplicates the element, with the same values",
    "Elimina l'elemento":
        "Deletes the element",

    "Accanto al nome c'è il tipo come compare nel sorgente SVG: rect, ellipse, path, …":
        "Next to the name is the type as it appears in the SVG source: rect, ellipse, path, …",

    "La pastiglia apre il selettore di colore del sistema":
        "The swatch opens the system colour picker",
    "Il codice esadecimale si può anche scrivere a mano: #9f2828, oppure 9f2828":
        "The hex code can also be typed by hand: #9f2828, or 9f2828",
    "L'opacità del solo riempimento, da 0 a 100":
        "The opacity of the fill alone, from 0 to 100",
    "Senza la spunta l'elemento non viene riempito affatto":
        "Without the tick the element is not filled at all",

    "Nome e comandi stanno sopra l'anteprima, su un velo scuro":
        "Name and commands sit above the preview, on a dark veil",
    "La cella singola, incorniciata, resta sempre visibile":
        "The single cell, framed, stays visible at all times",
    "Le tre schede: Proprietà, Elementi, SVG":
        "The three tabs: Properties, Elements, SVG",
    "La presa per trascinare funziona anche col dito":
        "The drag grip works with a finger too",
    "Dove c'è spazio i campi si affiancano a due a due":
        "Where there is room the fields pair up two by two",

    "Il pulsante sta accanto a «Crea un pattern»: sono i due modi di cominciare":
        "The button sits next to “Create a pattern”: they are the two ways to begin",
    "Quanti elementi sono entrati, e con quale cella":
        "How many elements came in, and with which cell",
    "Che cosa è rimasto fuori, e perché: è la parte da leggere":
        "What stayed out, and why: this is the part to read",
    "Da qui si apre l'editor, dove il pattern si guarda e si conferma — o si abbandona":
        "From here the editor opens, where the pattern is looked at and confirmed — or abandoned",

    "L'originale: la cella ritagliata dall'immagine":
        "The original: the cell cut out of the image",
    "La ricostruzione: l'SVG vero, quello che verrebbe salvato":
        "The reconstruction: the real SVG, the one that would be saved",
    "La differenza: rosso ciò che manca, blu ciò che è stato aggiunto":
        "The difference: red what is missing, blue what was added",
    "Quanto somiglia — da leggere insieme alla mappa, mai da sola":
        "How much it matches — to be read with the map, never on its own",
    "Le due manopole: quanti colori, e quanta fedeltà. Cambiarle riconverte sul posto":
        "The two knobs: how many colours, and how much fidelity. Changing them reconverts on the spot",
    "Il resoconto: la cella trovata, le bande, quante forme sono correzioni":
        "The report: the cell found, the bands, how many shapes are corrections",

    "Lo sfondo dell'intestazione non è un'immagine: è un pattern generato dallo stesso componente.":
        "The header's background is not an image: it is a pattern generated by the component itself.",

    # --- le figure disegnate (3-8), sostituite testo per testo da traduci-figure.py ---
    "Figura 3 — Che cos'è la cella":
        'Figure 3 — What the cell is',
    "Il pattern è una piastrella ripetuta all'infinito: si disegna la piastrella.":
        'A pattern is one tile repeated endlessly: you draw the tile.',
    'LA CELLA — quello che disegni':
        'THE CELL — what you draw',
    'larghezza = 100':
        'width = 100',
    'altezza = 100':
        'height = 100',
    'si ripete →':
        'repeats →',
    'IL RISULTATO — quello che ottieni':
        'THE RESULT — what you get',
    'la cella':
        'the cell',
    'Il bordo della cella non è un muro.':
        'The edge of the cell is not a wall.',
    'Un elemento può sporgere fuori dalla cella, e non viene tagliato: ricompare dal lato opposto della':
        'An element may overhang the cell, and it is not cut off: it reappears from the opposite side of the',
    'piastrella accanto. È così che si costruiscono i motivi continui, quelli in cui non si vede la griglia.':
        'tile next door. That is how continuous motifs are built, the ones where the grid cannot be seen.',
    'Nella figura il cerchio ha centro al centro della cella e raggio 60 in una cella da 100: sborda da tutti':
        'In the figure the circle is centred on the cell with a radius of 60 in a cell of 100: it overhangs on all',
    'e quattro i lati, e nel risultato i cerchi si toccano invece di restare isolati.':
        'four sides, and in the result the circles touch instead of standing apart.',
    "Cambiare la dimensione della cella non ingrandisce gli elementi: cambia quanto spazio c'è fra una":
        'Changing the size of the cell does not enlarge the elements: it changes how much space there is between',
    "ripetizione e l'altra. Per ingrandire il motivo si usa la scala.":
        'one repetition and the next. To enlarge the motif you use the scale.',
    'Figura 4 — La trasformazione':
        'Figure 4 — The transformation',
    'Agisce sulla griglia intera, non sui singoli elementi.':
        'It acts on the whole grid, not on individual elements.',
    'Nessuna':
        'None',
    'scala 100%, rotazione 0°':
        'scale 100%, rotation 0°',
    'Scala 60%':
        'Scale 60%',
    'il motivo si infittisce':
        'the motif densifies',
    'Rotazione 30°':
        'Rotation 30°',
    'la griglia si inclina':
        'the grid tilts',
    "La trasformazione è l'attributo patternTransform della specifica SVG, e si applica alla griglia":
        'The transformation is the patternTransform attribute of the SVG specification, and it applies to the grid',
    'dopo che la cella è stata composta. Ha una conseguenza che sorprende sempre la prima volta:':
        'after the cell has been composed. It has a consequence that always surprises the first time:',
    'ruotare il pattern non ruota gli elementi dentro la cella, ruota il modo in cui la cella si ripete.':
        'rotating the pattern does not rotate the elements inside the cell, it rotates the way the cell repeats.',
    'Scala: percentuale. 100% è la dimensione reale, 50% dimezza (il motivo si infittisce), 200% raddoppia.':
        'Scale: a percentage. 100% is the real size, 50% halves it (the motif densifies), 200% doubles it.',
    'Rotazione: gradi da 0 a 360, in senso orario.':
        'Rotation: degrees from 0 to 360, clockwise.',
    "Traslazione X e Y: sposta l'origine della griglia. Serve a sfalsare le file — è così che si ottiene la":
        'Translate X and Y: moves the origin of the grid. It is used to offset the rows — that is how you get the',
    'disposizione a mattoni, traslando di metà cella.':
        'brick arrangement, translating by half a cell.',
    'Figura 5 — Il sistema di coordinate':
        'Figure 5 — The coordinate system',
    'Due cose che in SVG funzionano al contrario di come ci si aspetta.':
        'Two things that in SVG work the opposite way round from what you expect.',
    "1. L'asse Y cresce verso il BASSO":
        '1. The Y axis grows DOWNWARDS',
    'x=60  y=50':
        'x=60  y=50',
    "Aumentare Y sposta l'elemento in giù, non in su.":
        'Increasing Y moves the element down, not up.',
    '2. Il testo si appoggia alla LINEA DI BASE':
        '2. Text sits on the BASELINE',
    'linea di base — è qui che punta la Y':
        'baseline — this is where Y points',
    'Con y=0 il testo finisce quasi tutto sopra il bordo.':
        'With y=0 the text ends up almost entirely above the edge.',
    'Le coordinate degli elementi sono nelle unità della cella, non in pixel dello schermo: in una cella':
        'The coordinates of elements are in the units of the cell, not in screen pixels: in a cell of',
    'da 100 per 100, un rettangolo largo 50 ne occupa metà, comunque grande venga poi mostrato.':
        '100 by 100, a rectangle 50 wide takes up half of it, however large it is later shown.',
    'Anche la dimensione del carattere è in quelle unità, non in punti tipografici: in una cella da 50, un':
        'The font size is in those units too, not in typographic points: in a cell of 50, a size',
    "corpo 12 occupa quasi un quarto dell'altezza.":
        'of 12 takes up almost a quarter of the height.',
    'Figura 6 — Riempimento, bordo e opacità':
        'Figure 6 — Fill, stroke and opacity',
    'Tre opacità distinte, e uno spessore che allarga la figura.':
        'Three distinct opacities, and a thickness that widens the figure.',
    'Solo riempimento':
        'Fill only',
    'bordo spento':
        'stroke off',
    'Solo bordo':
        'Stroke only',
    'riempimento spento':
        'fill off',
    'Entrambi':
        'Both',
    'bordo spesso 8':
        'stroke 8 thick',
    'Lo spessore sta a cavallo':
        'The thickness sits astride',
    'metà dentro, metà fuori':
        'half inside, half outside',
    'Le tre opacità non sono la stessa cosa.':
        'The three opacities are not the same thing.',
    'Opacità del riempimento    rende trasparente il solo colore interno; il bordo resta pieno.':
        'Fill opacity            makes only the inner colour transparent; the stroke stays solid.',
    'Opacità del bordo               rende trasparente il solo contorno; il riempimento resta pieno.':
        'Stroke opacity          makes only the outline transparent; the fill stays solid.',
    "Opacità complessiva          sbiadisce l'elemento già composto, come un tutt'uno. È diversa dalle":
        'Overall opacity         fades the element already composed, as one whole. It differs from the',
    'altre due: con le prime, dove bordo e riempimento si sovrappongono,':
        'other two: with those, where stroke and fill overlap,',
    'si vede il riempimento attraverso il bordo; con questa no.':
        'the fill shows through the stroke; with this one it does not.',
    'Lo spessore del bordo è misurato a cavallo del contorno: metà cade dentro la figura e metà fuori.':
        'The stroke thickness is measured astride the outline: half falls inside the figure and half outside.',
    "Un cerchio di raggio 50 con bordo spesso 20 occupa dunque un'area di raggio 60.":
        'A circle of radius 50 with a stroke 20 thick therefore occupies an area of radius 60.',
    'Figura 7 — I nove elementi':
        'Figure 7 — The nine elements',
    'Ognuno corrisponde a un elemento della specifica SVG.':
        'Each corresponds to an element of the SVG specification.',
    'Rettangolo':
        'Rectangle',
    'Cerchio':
        'Circle',
    'Ellisse':
        'Ellipse',
    'Linea':
        'Line',
    'Tracciato':
        'Path',
    'Poligono':
        'Polygon',
    'Spezzata':
        'Polyline',
    'Testo':
        'Text',
    'Immagine':
        'Image',
    "Figura 8 — L'adattamento di un'immagine":
        'Figure 8 — Fitting an image',
    'preserveAspectRatio: cosa succede quando le proporzioni non coincidono.':
        'preserveAspectRatio: what happens when the proportions do not match.',
    "L'immagine di partenza è larga e bassa; il riquadro che la ospita è quadrato.":
        'The starting image is wide and short; the frame that holds it is square.',
    '— la contiene tutta':
        '— contains all of it',
    'lascia spazio vuoto ai lati':
        'leaves empty space at the sides',
    '— riempie il riquadro':
        '— fills the frame',
    'taglia ciò che avanza':
        'cuts off what is left over',
    '— la fa combaciare':
        '— makes it match',
    'la deforma':
        'distorts it',
    'Non esiste una scelta giusta in assoluto: si sceglie cosa si è disposti a perdere. Con meet non si':
        'There is no absolutely right choice: you choose what you are willing to lose. With meet you lose',
    "perde niente dell'immagine ma si accetta dello spazio vuoto; con slice il riquadro è pieno ma i bordi":
        'nothing of the image but accept some empty space; with slice the frame is full but the edges',
    "dell'immagine escono dall'inquadratura; con none c'è tutto e il riquadro è pieno, ma le forme sono":
        'of the image fall outside the frame; with none everything is there and the frame is full, but the shapes are',
    'stirate — su un volto o su un logo si nota subito.':
        'stretched — on a face or a logo it shows immediately.',
    "Nell'editor l'anteprima dei tre modi usa apposta un riquadro non quadrato: su un riquadro quadrato,":
        'In the editor the preview of the three modes deliberately uses a non-square frame: on a square frame,',
    "con un'immagine quadrata, i tre modi darebbero lo stesso risultato e non si distinguerebbero.":
        'with a square image, the three modes would give the same result and could not be told apart.',
}
