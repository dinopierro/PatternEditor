# -*- coding: utf-8 -*-
"""I diagrammi nuovi dell'analisi: la parte architetturale, passaggio per passaggio."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from tela import (ACCENTO, ARANCIO, BORDO, BORDO_TENUE, FONDO, FONDO_ACCENTO,  # noqa: E402
                  GRIGIO, INCHIOSTRO, MONO, Tela, VERDE, codice, sequenza, spezza)

print("diagrammi dell'analisi")

# --------------------------------------------------------------------------- 09 · DI
t = Tela("Figura 9 — Composizione a runtime",
         "Chi registra che cosa, e in che ordine, quando l'applicazione parte.")

t.scatola(30, 84, 260, 246, "1 · L'applicazione ospitante", [
    "Program.cs — l'unico file che",
    "conosce i tipi concreti",
], "accento")
t.testo(42, 158, "builder.Services", 9, INCHIOSTRO, "400", famiglia=MONO)
for i, riga in enumerate([".AddPatternEditor()",
                          ".AddPatternEditorPlugin<LinePlugin>()",
                          ".AddPatternEditorPlugin<RectPlugin>()",
                          ".AddPatternEditorPlugin<CirclePlugin>()",
                          "… una riga per tipo, nove in tutto"]):
    t.testo(50, 176 + i * 15, riga, 8.6, GRIGIO if i == 4 else INCHIOSTRO, "400",
            famiglia=None if i == 4 else MONO)
t.testo(42, 268, "L'ordine delle righe è l'ordine", 8.8, GRIGIO)
t.testo(42, 280, "delle voci nel menù «Aggiungi».", 8.8, GRIGIO)
t.testo(42, 304, "La libreria non referenzia", 8.8, ARANCIO)
t.testo(42, 316, "nessuno di questi assembly.", 8.8, ARANCIO)

t.scatola(345, 84, 250, 246, "2 · Contenitore dei servizi", [
    "tutti singleton: nessuno tiene",
    "stato della singola sessione",
])
for i, riga in enumerate(["IVectorElementPlugin × 9",
                          "IVectorElementPluginRegistry",
                          "IPatternSerializer",
                          "IPatternFactory",
                          "IPatternValidator",
                          "IPatternSvgRenderer",
                          "IVectorElementCloner",
                          "IPatternCloner"]):
    t.testo(357, 168 + i * 16, "• " + riga, 8.8, INCHIOSTRO)
t.testo(357, 312, "Lo stato della modifica vive nel componente, non qui.", 8.4, GRIGIO)

t.scatola(650, 84, 260, 246, "3 · Il registro", [
    "costruito una volta sola, alla",
    "prima richiesta del registro",
])
t.testo(662, 172, "Dictionary<string, IVectorElementPlugin>", 8.2, INCHIOSTRO, famiglia=MONO)
t.testo(662, 192, 'confronto Ordinal: "rect" è "rect"', 8.8, GRIGIO)
t.testo(662, 204, "in qualunque lingua", 8.8, GRIGIO)
t.testo(662, 232, "Type duplicato → eccezione all'avvio,", 8.8, ARANCIO)
t.testo(662, 244, "non «vince l'ultimo».", 8.8, ARANCIO)
t.testo(662, 272, "Da qui in poi il registro si legge", 8.8, GRIGIO)
t.testo(662, 284, "soltanto: nessun lucchetto, perché", 8.8, GRIGIO)
t.testo(662, 296, "nessuno scrive più.", 8.8, GRIGIO)

t.freccia(290, 190, 345, 190)
t.testo(317, 182, "registra", 8.2, ACCENTO, "600", "middle")
t.freccia(595, 190, 650, 190)
t.testo(622, 182, "GetServices", 8.2, ACCENTO, "600", "middle")

t.nota(30, 352, 880, [
    "Le tre fasi avvengono all'avvio e non si ripetono. Da quel momento il componente chiede al registro il plugin di un tipo e non sa altro:",
    "non conosce le classi RectElement o RectEditor, non le referenzia, e aggiungere un decimo tipo non lo tocca.",
])
t.salva("09-composizione-di.svg")

# ------------------------------------------------------------------- 10 · componenti
t = Tela("Figura 10 — L'albero dei componenti",
         "Chi contiene chi, e che cosa si scambiano padre e figlio.")

t.scatola(30, 84, 250, 76, "PatternsPage", [
    "l'applicazione ospitante",
    "tiene l'elenco e parla con l'API",
], "accento")

t.scatola(345, 84, 250, 76, "PatternEditor", [
    "il componente della libreria",
    "possiede la copia in modifica",
], "accento")
t.freccia(280, 122, 345, 122)
t.testo(312, 114, "Pattern", 8.2, ACCENTO, "600", "middle")
t.percorso("M 345 142 L 280 142", ACCENTO, 1.6, tratteggio="5 4")
t.testo(312, 178, "OnConfirm / OnCancel", 8.2, GRIGIO, "600", "middle")

colonne = [
    (30, "PatternEditorToolbar", ["nome, Annulla,", "Chiudi / Conferma"]),
    (243, "PatternPropertiesEditor", ["cella e", "trasformazione"]),
    (456, "PatternPreviewPanel", ["due anteprime,", "sorgente, copia,", "scarica"]),
    (669, "VectorElementsPanel", ["elenco, dettaglio,", "riordino, duplica,", "elimina"]),
]
for x, nome, righe in colonne:
    t.scatola(x, 230, 241, 84, nome, righe)
    t.percorso("M 470 160 L 470 200 L %d 200 L %d 230" % (x + 120, x + 120), ACCENTO, 1.5)
t.testo(250, 192, "parametri in giù, EventCallback in su", 8.4, GRIGIO, "600", "middle")

t.scatola(669, 350, 241, 72, "DynamicComponent", [
    "istanzia il tipo dichiarato dal",
    "plugin, senza conoscerlo",
], "caldo")
t.freccia(789, 314, 789, 350)

t.scatola(560, 452, 160, 60, "RectEditor", ["plugin Rect"], "verde")
t.scatola(730, 452, 160, 60, "LineEditor", ["plugin Line"], "verde")
t.testo(900, 500, "…", 14, GRIGIO, "700", "middle")
t.percorso("M 789 422 L 789 436 L 640 436 L 640 452", ACCENTO, 1.5)
t.percorso("M 789 422 L 789 436 L 810 436 L 810 452", ACCENTO, 1.5)

t.nota(30, 350, 600, [
    "I quattro pannelli ricevono la stessa istanza di Pattern e la modificano in casa: non ci sono copie intermedie da tenere allineate.",
    "Ogni modifica risale al PatternEditor con un EventCallback, che è il solo punto in cui si decide che qualcosa è cambiato — e quindi",
    "l'unico punto in cui si rivalida e si ridisegna.",
    "",
    "L'ultima riga è il confine dell'estensibilità: da DynamicComponent in giù, il codice è quello dei plugin, e la libreria non lo conosce.",
])
t.salva("10-albero-componenti.svg")

# --------------------------------------------------------------- 11 · flusso modifica
t = Tela("Figura 11 — Che cosa succede a ogni tasto premuto",
         "Dal campo di un plugin all'anteprima ridisegnata: il giro completo, senza salvataggi.")
sequenza(t, ["Utente", "RectEditor\n(plugin)", "VectorElements\nPanel", "PatternEditor",
             "PatternValidator", "PatternSvgRenderer"],
         [(0, 1, "scrive 50 in «Larghezza»"),
          (1, 1, "Rect.Width = 50 — scrive nel modello vero, non in una copia", "auto"),
          (1, 2, "OnChanged.InvokeAsync()"),
          (2, 3, "OnModelChanged"),
          (3, 3, "stato → Dirty (una volta sola)", "auto"),
          (3, 4, "Validate(pattern)"),
          (4, 3, "ValidationResult: errori e avvisi", "rit"),
          (3, 3, "StateHasChanged()", "auto"),
          (3, 5, "RenderStandaloneSvg(pattern)"),
          (5, 3, "markup dell'SVG", "rit"),
          (3, 0, "anteprime, sorgente e avvisi aggiornati", "rit")])
t.nota(40, t.basso + 16, 860, [
    "Nessun passaggio tocca l'archivio: fino a «Chiudi / Conferma» tutto avviene sulla copia che vive dentro il componente.",
    "Il renderer viene invocato dal rendering di Blazor, non dall'evento: se in un colpo solo cambiano dieci proprietà, il disegno si rifà una volta.",
])
t.salva("11-flusso-modifica.svg")

# ------------------------------------------------------------------- 12 · la sessione
t = Tela("Figura 12 — Il ciclo di una sessione di modifica",
         "La modifica è transazionale: o si conferma tutta, o non è mai avvenuta.")
sequenza(t, ["PatternsPage\n(host)", "PatternEditor", "PatternSerializer",
             "Macchina\na stati", "API"],
         [(0, 1, "Pattern = l'originale dell'elenco"),
          (1, 2, "Serialize(originale) → Deserialize"),
          (2, 1, "copia profonda, indipendente", "rit"),
          (1, 3, "OpenExisting()"),
          (1, 1, "… n modifiche: MarkDirty, Validate, ridisegna …", "auto"),
          (0, 1, "Chiudi / Conferma"),
          (1, 3, "BeginConfirm()"),
          (1, 1, "Validate(copia): se non è valida si ferma qui", "auto"),
          (1, 3, "CompleteConfirm(valido)"),
          (1, 0, "OnConfirm(copia modificata)", "rit"),
          (0, 4, "PUT /api/patterns/{id}"),
          (4, 0, "204 · l'elenco si ricarica", "rit")])
t.nota(40, t.basso + 16, 860, [
    "Annulla percorre lo stesso tracciato fino a OnCancel: la copia viene buttata e l'originale non è mai stato toccato, perché nessun",
    "passaggio ha mai scritto su di lui. È questo che rende «Annulla» una promessa e non una speranza.",
    "",
    "La copia si ottiene serializzando e rideserializzando l'originale. Sembra un giro largo rispetto a un metodo Copia(), e invece è il modo",
    "più sicuro: passa dallo stesso codice della persistenza, quindi copia anche gli elementi di tipi che questa installazione non conosce.",
])
t.salva("12-sessione.svg")

# ------------------------------------------------------------- 13 · generazione SVG
t = Tela("Figura 13 — Come nasce l'SVG, riga per riga",
         "Lo stesso codice produce l'anteprima e il file scaricato: non esistono due rendering.")

t.scatola(30, 84, 262, 128, "1 · Che cosa entra", [
    "Pattern.Definition",
])
for i, riga in enumerate(["Width = 50   Height = 50",
                          "Scale = 1    Rotation = 0",
                          "TranslateX = 0  TranslateY = 0",
                          "Elements = [ rect, line × 5 ]"]):
    t.testo(42, 140 + i * 16, riga, 8.4, INCHIOSTRO, famiglia=MONO)

t.scatola(339, 84, 262, 128, "2 · Ogni elemento, da solo", [
    "per ciascun elemento in ordine:",
])
t.testo(351, 140, "registry.TryGet(el.Type)", 8.4, INCHIOSTRO, famiglia=MONO)
t.testo(351, 158, "trovato  → plugin.Render(el)", 8.4, VERDE, famiglia=MONO)
t.testo(351, 176, "assente  → si salta il disegno", 8.4, ARANCIO, famiglia=MONO)
t.testo(351, 196, "Il plugin produce un tag e basta: niente", 8.2, GRIGIO)
t.testo(351, 206, "<svg>, niente <pattern>, niente trasformazioni.", 8.2, GRIGIO)

t.scatola(648, 84, 262, 128, "3 · La trasformazione", [
    "scale, rotate e translate, in quest'ordine",
])
t.testo(660, 146, 'patternTransform="scale(1)', 8.2, INCHIOSTRO, famiglia=MONO)
t.testo(660, 160, '  rotate(0) translate(0,0)"', 8.2, INCHIOSTRO, famiglia=MONO)
t.testo(660, 184, "Se è l'identità l'attributo non viene", 8.2, GRIGIO)
t.testo(660, 194, "scritto affatto: un documento pulito", 8.2, GRIGIO)
t.testo(660, 204, "non porta in giro istruzioni inutili.", 8.2, GRIGIO)

t.freccia(292, 148, 339, 148)
t.freccia(601, 148, 648, 148)

t.scatola(30, 246, 880, 206, "4 · L'assemblaggio finale", [])
sorgente = [
    '<svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%">',
    '  <defs>',
    '    <pattern id="p-9f24…e91c" width="50" height="50" patternUnits="userSpaceOnUse">',
    '      <rect x="0" y="0" width="50" height="50" fill="#9f2828" fill-opacity="1" />',
    '      <line x1="0" y1="25" x2="50" y2="25" stroke="#ffffff" stroke-width="2" />',
    '      …',
    '    </pattern>',
    '  </defs>',
    '  <rect width="100%" height="100%" fill="url(#p-9f24…e91c)" />',
    '</svg>',
]
codice(t, 44, 286, sorgente, colore=lambda i: GRIGIO if i == 5 else INCHIOSTRO)

t.nota(30, 470, 880, [
    "patternUnits=\"userSpaceOnUse\" dice che le misure della cella sono in unità del disegno, non frazioni del riquadro: è ciò che permette",
    "di ragionare in pixel. L'identificativo del nodo <pattern> contiene l'identificativo del pattern perché url(#…) si risolve sull'intero",
    "documento: due anteprime nella stessa pagina con lo stesso id mostrerebbero entrambe il primo dei due disegni.",
])
t.salva("13-generazione-svg.svg")

# ---------------------------------------------------------------- 14 · serializzazione
t = Tela("Figura 14 — Dal JSON al modello e ritorno",
         "Un tipo che questa installazione non conosce attraversa il sistema intatto.")

t.scatola(30, 84, 258, 250, "Documento JSON", ["come sta sul disco"])
documento = ['{', '  "version": 1,', '  "id": "01a09078-…",',
             '  "name": "Briks",', '  "createdAt": "2026-…Z",',
             '  "definition": {', '    "width": 50, "height": 50,',
             '    "elements": [', '      { "type": "rect", … },',
             '      { "type": "runa", … }', '    ] } }']
codice(t, 42, 140, documento,
       colore=lambda i: ARANCIO if "runa" in documento[i] else INCHIOSTRO)

t.scatola(341, 84, 258, 250, "PatternSerializer", ["l'unico punto di traduzione"])
t.testo(353, 140, "Deserialize", 9.5, INCHIOSTRO, "700")
t.testo(353, 158, "per ogni elemento legge \"type\" e", 8.4, GRIGIO)
t.testo(353, 170, "chiede al registro chi lo gestisce:", 8.4, GRIGIO)
t.testo(353, 190, "trovato → il tipo CLR del plugin", 8.4, VERDE, famiglia=MONO)
t.testo(353, 206, "assente → UnknownVectorElement", 8.4, ARANCIO, famiglia=MONO)
t.testo(353, 218, "          che conserva il JSON grezzo", 8.4, ARANCIO, famiglia=MONO)
t.testo(353, 244, "Serialize", 9.5, INCHIOSTRO, "700")
t.testo(353, 262, "riscrive l'elemento sconosciuto", 8.4, GRIGIO)
t.testo(353, 274, "esattamente com'era arrivato.", 8.4, GRIGIO)
t.testo(353, 296, "Date sempre in UTC, formato \"O\";", 8.4, GRIGIO)
t.testo(353, 308, "numeri con la cultura invariante.", 8.4, GRIGIO)

t.scatola(652, 84, 258, 250, "Modello in memoria", ["quello che l'editor manipola"])
t.testo(664, 140, "Pattern", 9, INCHIOSTRO, "700", famiglia=MONO)
t.testo(664, 158, " └ PatternDefinition", 8.4, INCHIOSTRO, famiglia=MONO)
t.testo(664, 174, "     ├ RectElement", 8.4, VERDE, famiglia=MONO)
t.testo(664, 190, "     ├ LineElement × 5", 8.4, VERDE, famiglia=MONO)
t.testo(664, 206, "     └ UnknownVectorElement", 8.4, ARANCIO, famiglia=MONO)
t.testo(664, 226, "L'elemento sconosciuto non si può", 8.4, GRIGIO)
t.testo(664, 238, "modificare e non viene disegnato,", 8.4, GRIGIO)
t.testo(664, 250, "ma resta nell'elenco e viene", 8.4, GRIGIO)
t.testo(664, 262, "risalvato: un'installazione senza", 8.4, GRIGIO)
t.testo(664, 274, "il plugin «runa» non distrugge il", 8.4, GRIGIO)
t.testo(664, 286, "lavoro di chi ce l'ha.", 8.4, GRIGIO)

t.freccia(288, 170, 341, 170)
t.testo(314, 162, "legge", 8.2, ACCENTO, "600", "middle")
t.freccia(599, 170, 652, 170)
t.percorso("M 652 250 L 599 250", ACCENTO, 1.6)
t.percorso("M 341 250 L 288 250", ACCENTO, 1.6)
t.testo(314, 268, "riscrive", 8.2, ACCENTO, "600", "middle")

t.nota(30, 352, 880, [
    "In lettura ogni campo assente ha un valore di ripiego: un documento più povero si apre, non esplode. Un documento senza \"id\" invece",
    "viene rifiutato, perché senza identificativo non si saprebbe che cosa si sta aprendo né dove risalvarlo.",
])
t.salva("14-serializzazione.svg")

# ------------------------------------------------------------------- 15 · interop JS
t = Tela("Figura 15 — I quattro punti in cui si scende a JavaScript",
         "Tutto il resto è C#. Si chiama JavaScript solo per ciò che il browser non espone altrimenti.")

righe = [
    ("PatternPreviewPanel", "_content/PatternEditor/patternEditorInterop.js",
     ["downloadTextFile(nome, contenuto, tipo)", "copyText(contenuto) → true/false"],
     "scaricare un file e scrivere negli appunti sono gesti che solo il browser può compiere"),
    ("VectorElementsPanel", "_content/PatternEditor/patternEditorInterop.js",
     ["centriDelleRighe(elenco) → double[]"],
     "misurare dove sono le righe sullo schermo: la posizione la conosce solo il documento"),
    ("MainLayout (host)", "js/tema.js",
     ["sistemaPreferisceScuro() · leggi() · applica(t)"],
     "il tema scelto va ricordato fra una visita e l'altra, e sta nel browser"),
    ("PatternsPage (host)", "js/movimento.js",
     ["riflettore() · origineFinestra() · contatori()"],
     "le animazioni della vetrina, che non hanno nulla a che vedere con la libreria"),
]
y = 84
for nome, modulo, funzioni, perche in righe:
    h = 46 + len(funzioni) * 15
    t.scatola(30, y, 230, h, nome, [], "accento")
    t.testo(42, y + 38, "componente Blazor", 8.4, GRIGIO)
    t.scatola(300, y, 300, h, "", [])
    t.testo(312, y + 20, modulo, 8.2, INCHIOSTRO, famiglia=MONO)
    for i, f in enumerate(funzioni):
        t.testo(312, y + 40 + i * 15, f, 8.2, VERDE, famiglia=MONO)
    t.testo(620, y + 22, "perché:", 8.4, GRIGIO, "700")
    for i, riga in enumerate(spezza(perche, 58)):
        t.testo(620, y + 38 + i * 12, riga, 8.4, GRIGIO)
    t.freccia(260, y + h / 2, 300, y + h / 2)
    y += h + 18

t.nota(30, y + 4, 880, [
    "Ogni modulo si importa una volta sola, alla prima occasione, e viene rilasciato in DisposeAsync: un import per clic lascerebbe dietro",
    "riferimenti che nessuno chiude. I moduli sono ES: le loro funzioni non finiscono su window, e due componenti non possono darsi fastidio.",
])
t.salva("15-interop-js.svg")

# ---------------------------------------------------------------- 16 · trascinamento
t = Tela("Figura 16 — Il riordino per trascinamento",
         "Eventi puntatore, non drag-and-drop HTML: il secondo, sul telefono, non esiste.")
sequenza(t, ["Dito o mouse", "La presa ≡", "VectorElements\nPanel", "JavaScript", "Elenco"],
         [(0, 1, "pointerdown sulla presa"),
          (1, 2, "IniziaTrascinamento(indice)"),
          (2, 3, "centriDelleRighe(righe)"),
          (3, 2, "la metà verticale di ogni riga", "rit"),
          (0, 1, "pointermove — il dito scorre"),
          (1, 2, "Trascina(y del puntatore)"),
          (2, 2, "posizione più vicina: se è cambiata, sposta e ridisegna", "auto"),
          (0, 1, "pointerup"),
          (1, 2, "FineTrascinamento → OnChanged"),
          (2, 4, "l'ordine nuovo è l'ordine di disegno")])
t.nota(40, t.basso + 16, 860, [
    "Sul touch il browser assegna la cattura del puntatore da solo: il dito può uscire dalla riga e gli eventi continuano ad arrivare.",
    "La presa dichiara touch-action: none, altrimenti il primo movimento verticale verrebbe interpretato come scorrimento della pagina",
    "e il trascinamento non comincerebbe mai. Senza puntatore resta la tastiera: si raggiunge la presa col tabulatore e si usano ↑ e ↓.",
])
t.salva("16-trascinamento.svg")

# ------------------------------------------------------------------ 17 · duplicazione
t = Tela("Figura 17 — Le due duplicazioni",
         "Copiare un pattern intero e copiare un elemento sono lo stesso gesto a due livelli.")

t.scatola(30, 84, 430, 40, "Duplicare un pattern — dalla pagina iniziale", [], "accento")
passi = [
    ("IPatternCloner.Clone(originale, \"Copia di Briks\")", "il nome lo decide il chiamante"),
    ("identificativo nuovo, date nuove", "è un altro oggetto, non una versione"),
    ("le sei misure della definizione, copiate una per una", "non un MemberwiseClone: un campo nuovo va copiato a mano, e si vede"),
    ("ogni elemento passa da IVectorElementCloner", "compresi quelli di tipo sconosciuto"),
    ("POST /api/patterns → la copia compare in cima", "ordinata per creazione, la più recente è la prima"),
]
y = 140
for titolo, dettaglio in passi:
    t.scatola(30, y, 430, 46, "", [])
    t.testo(42, y + 19, titolo, 8.6, INCHIOSTRO, "600")
    t.testo(42, y + 34, dettaglio, 8.2, GRIGIO)
    y += 56

t.scatola(500, 84, 410, 40, "Duplicare un elemento — dentro l'editor", [], "accento")
passi = [
    ("IVectorElementCloner.Clone(elemento)", "lo stesso servizio del caso sopra"),
    ("identificativo nuovo, tutto il resto identico", "due elementi non possono condividere l'id"),
    ("inserito subito sotto l'originale", "dove lo si cerca con gli occhi"),
    ("la scheda NON si apre", "si duplica spesso più volte di seguito"),
    ("l'anteprima si aggiorna: c'è una forma in più", "sovrapposta all'originale, finché non la si sposta"),
]
y = 140
for titolo, dettaglio in passi:
    t.scatola(500, y, 410, 46, "", [])
    t.testo(512, y + 19, titolo, 8.6, INCHIOSTRO, "600")
    t.testo(512, y + 34, dettaglio, 8.2, GRIGIO)
    y += 56

t.nota(30, y + 6, 880, [
    "La regola comune: una copia è un oggetto nuovo con un'identità nuova, e tutto il resto uguale. Il validatore lo verifica dopo il fatto —",
    "due elementi con lo stesso identificativo sono un errore segnalato, non un difetto che si scopre mesi dopo in un documento salvato.",
])
t.salva("17-duplicazione.svg")

# -------------------------------------------------------------------- 18 · rotte dati
t = Tela("Figura 18 — Il percorso dei dati, dal campo al file",
         "Chi tiene lo stato, chi lo trasporta, chi lo conserva.")

t.scatola(30, 84, 270, 160, "Browser — WebAssembly", [
    "PatternsPage: elenco, ordine, pagina",
    "PatternEditor: la copia in modifica",
    "",
    "Nessuno scrive sul disco da qui:",
    "il browser non ne ha il permesso.",
], "accento")

t.scatola(355, 84, 230, 160, "HTTP · JSON", [
    "GET    /api/patterns",
    "GET    /api/patterns/{id}",
    "POST   /api/patterns",
    "PUT    /api/patterns/{id}",
    "DELETE /api/patterns/{id}",
    "DELETE /api/patterns  (elenco di id)",
])
t.scatola(640, 84, 270, 160, "API — PatternEditor.Sample.Api", [
    "JsonFilePatternRepository",
    "un file per pattern, in App_Data",
    "",
    "Usa lo STESSO PatternSerializer",
    "del client: un solo formato, non due.",
], "accento")

t.freccia(300, 150, 355, 150)
t.percorso("M 640 190 L 585 190", ACCENTO, 1.6)
t.freccia(585, 150, 640, 150)
t.percorso("M 355 190 L 300 190", ACCENTO, 1.6)

t.scatola(640, 280, 270, 106, "Disco", [
    "App_Data/patterns/{id}.json",
    "un documento per pattern: si riscrive",
    "solo quello toccato",
    "",
    "Le date le decide questo livello, e",
    "CreatedAt non si riscrive mai.",
], "tenue")
t.freccia(775, 244, 775, 280)

t.nota(30, 280, 580, [
    "Il client non conosce il formato del file, e l'API non conosce l'interfaccia: si parlano",
    "attraverso il documento JSON, che è l'unico contratto fra i due. Sostituire l'archiviazione",
    "su file con un database non tocca una riga del componente.",
    "",
    "La validazione avviene in due punti e non è una ripetizione inutile: nell'editor per",
    "accompagnare chi lavora, nell'API perché una richiesta può arrivare anche da altrove.",
])
t.salva("18-rotte-dati.svg")

# ------------------------------------------------------------------ 19 · importazione
t = Tela("Figura 19 — Dall'SVG al modello: l'importazione",
         "Il percorso inverso della generazione, e le tre decisioni che lo rendono onesto.")

t.scatola(30, 84, 200, 150, "1 · Il documento", [
    "letto con le definizioni di",
    "tipo DISABILITATE: il file",
    "arriva da fuori e non deve",
    "poter far aprire niente",
], "accento")
t.testo(42, 206, "La cella viene dal viewBox,", 8.4, GRIGIO)
t.testo(42, 218, "non dalle misure a schermo.", 8.4, GRIGIO)

t.scatola(268, 84, 200, 150, "2 · L'albero, percorso", [
    "gruppi attraversati",
    "<defs> saltati: definiscono,",
    "non disegnano",
    "stile in linea che vince",
    "sugli attributi",
])
t.testo(280, 218, "Trasformazioni composte.", 8.4, GRIGIO)

t.scatola(506, 84, 200, 150, "3 · Che cosa entra", [
    "un tag entra se il REGISTRO",
    "ha un plugin con quel nome",
])
t.testo(518, 158, "<rect> → plugin «rect»", 8.4, VERDE, famiglia=MONO)
t.testo(518, 174, "<use>  → nessuno: fuori", 8.4, ARANCIO, famiglia=MONO)
t.testo(518, 196, "Nessun elenco di tipi scritto", 8.4, GRIGIO)
t.testo(518, 208, "nell'importatore: un decimo", 8.4, GRIGIO)
t.testo(518, 220, "elemento entra da solo.", 8.4, GRIGIO)

t.scatola(744, 84, 166, 150, "4 · Il pattern", [
    "non salvato:",
    "si apre nell'editor,",
    "si guarda,",
    "si conferma",
], "accento")

for x in (230, 468, 706):
    t.freccia(x, 150, x + 38, 150)

t.scatola(30, 262, 430, 180, "Come si scrivono le proprietà senza conoscere i tipi", [])
codice(t, 44, 296, [
    "plugin.Create()        → i valori predefiniti del tipo",
    "serializza             → { \"type\": \"rect\", \"x\": 0, \"fill\": \"#3b6ef5\", … }",
    "sovrapponi gli attributi del documento, PER NOME",
    "  x=\"12.5\"           → \"x\": 12.5",
    "  stroke-width=\"2\"   → \"strokeWidth\": 2",
    "  rx=\"3\"             → la chiave non esiste: si ignora",
    "deserializza           → l'elemento concreto del plugin",
])
t.testo(44, 412, "Ciò che il documento non dice resta al valore del plugin: un SVG che tace", 8.4, GRIGIO)
t.testo(44, 424, "sull'opacità non sta chiedendo un elemento invisibile.", 8.4, GRIGIO)

t.scatola(490, 262, 420, 180, "Le tre cose che restano fuori, e perché si dicono", [])
for i, (titolo, motivo) in enumerate([
    ("Rotazioni e inclinazioni",
     "cambierebbero la forma: un rettangolo ruotato non lo è più"),
    ("Gradienti, motivi, ritagli, filtri",
     "il modello non li ha; senza bordo l'elemento sarebbe invisibile"),
    ("Fogli di stile, <use>, <symbol>",
     "richiedono di risolvere riferimenti che il modello non conserva"),
]):
    y = 314 + i * 44
    t.testo(504, y, titolo, 9, INCHIOSTRO, "700")
    t.testo(504, y + 14, motivo, 8.4, GRIGIO)

t.nota(30, 464, 880, [
    "Il documento SVG prodotto dall'applicazione si rilegge: il disegno sta dentro <defs><pattern>, che di regola non si importa perché",
    "è una definizione — ma lì quella definizione È il pattern, e da lì tornano anche la cella e la trasformazione. Andata e ritorno",
    "restituiscono lo stesso pattern, ed è quello che rende l'SVG un formato di scambio e non solo di uscita.",
])
t.salva("19-importazione.svg")

print("fatto")
