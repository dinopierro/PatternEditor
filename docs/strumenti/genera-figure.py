# -*- coding: utf-8 -*-
"""Figure del manuale costruite sopra le schermate vere dell'applicazione.

Ogni figura è un SVG: la fotografia sta dentro, come immagine incorporata in base64, e
sopra ci vanno i richiami numerati e la legenda, che restano vettoriali e quindi nitidi in
stampa a qualunque ingrandimento.

L'immagine è INCORPORATA e non collegata perché il documento finale la carica con un tag
<img>, e un SVG dentro un <img> non ha il permesso di andare a prendere file esterni: una
figura che rimandasse al PNG accanto uscirebbe vuota. Incorporandola il file è più pesante
ma si apre correttamente ovunque, nel PDF come nell'anteprima del Markdown.

Due modi di indicare, e servono tutti e due:
  - la ZONA, un riquadro attorno a un'area intera, per dire «questa parte si chiama così»;
  - il RICHIAMO, un numero con la sua linea, per indicare un singolo comando.
Il numero non si posa mai sopra ciò che indica: coprirebbe proprio il particolare di cui
parla. Le coordinate sono frazioni del riquadro dell'immagine, e possono uscirne (valori
negativi o maggiori di 1) per mettere il numero nel margine bianco accanto.
"""
import base64
import io
import os

from PIL import Image

import sys

# La lingua delle figure. Le schermate cambiano lingua perche' l'applicazione le disegna
# tradotte; i richiami e la legenda cambiano perche' li traduce il dizionario qui sotto.
#     python docs/strumenti/genera-figure.py en
LINGUA = sys.argv[1] if len(sys.argv) > 1 else "it"

RADICE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DOCS = os.path.join(RADICE, "docs")
BASE = os.path.join(DOCS, "immagini")
SCATTI = os.path.join(BASE, "scatti") if LINGUA == "it" else os.path.join(BASE, "scatti", LINGUA)
MANUALE = os.path.join(BASE, "manuale") if LINGUA == "it" else os.path.join(BASE, "manuale", LINGUA)

try:
    from testi_figure import TESTI
except ImportError:
    TESTI = {}

MANCANTI = []


def tradotto(s):
    """La frase nella lingua richiesta, o quella italiana se manca la traduzione."""
    if LINGUA == "it" or not s:
        return s
    if s in TESTI:
        return TESTI[s]
    MANCANTI.append(s)
    return s

INCHIOSTRO = "#1b2430"
GRIGIO = "#6b7684"
ACCENTO = "#3b6ef5"
BORDO = "#ccd3db"

LARGHEZZA = 940.0            # la stessa delle altre figure: in stampa hanno tutte la stessa scala
MARGINE = 30.0


def incorpora(nome, ritaglio=None, larghezza_massima=None):
    """Il PNG come stringa base64, eventualmente ritagliato e ridotto."""
    img = Image.open(os.path.join(SCATTI, nome))
    if ritaglio:
        img = img.crop(ritaglio)
    if larghezza_massima and img.width > larghezza_massima:
        img = img.resize((larghezza_massima,
                          round(img.height * larghezza_massima / img.width)), Image.LANCZOS)
    buf = io.BytesIO()
    img.convert("RGB").save(buf, format="PNG", optimize=True)
    return base64.b64encode(buf.getvalue()).decode("ascii"), img.width, img.height


def testo(x, y, s, misura=9.5, colore=INCHIOSTRO, peso="400", ancora="start"):
    fuga = s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    return ('  <text x="%s" y="%s" font-size="%s" fill="%s" font-weight="%s" '
            'text-anchor="%s">%s</text>\n' % (x, y, misura, colore, peso, ancora, fuga))


def pallino(x, y, n, raggio=12.5):
    """Il numero dentro un cerchio pieno, con un filo bianco che lo stacca dallo sfondo."""
    return ('  <circle cx="%.1f" cy="%.1f" r="%s" fill="%s" stroke="#ffffff" '
            'stroke-width="2"/>\n' % (x, y, raggio, ACCENTO)
            + '  <text x="%.1f" y="%.1f" font-size="12" font-weight="700" fill="#ffffff" '
              'text-anchor="middle">%s</text>\n' % (x, y + 4.2, n))


def richiamo(x, y, xb, yb, n):
    """Numero staccato dal punto indicato, con la sua linea e un puntino sul bersaglio."""
    fuori = ""
    if abs(x - xb) > 0.5 or abs(y - yb) > 0.5:
        fuori += ('  <line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" '
                  'stroke-width="1.6" stroke-linecap="round" opacity=".9"/>\n'
                  % (x, y, xb, yb, ACCENTO))
        fuori += ('  <circle cx="%.1f" cy="%.1f" r="3.4" fill="%s" stroke="#ffffff" '
                  'stroke-width="1.2"/>\n' % (xb, yb, ACCENTO))
    return fuori + pallino(x, y, n)


def figura(nome_file, titolo, sottotitolo, immagini, legenda, richiami=(), zone=(),
           colonne_legenda=2, nota=None, spazio_sopra=0.0, spazio_sotto=0.0,
           rientro=0.0, distanza=14.0):
    """Compone la figura e la scrive.

    immagini: (nome_png, ritaglio, larghezza_massima, frazione_di_larghezza), affiancate.
    richiami: (indice, fx, fy, numero) mette il numero lì;
              (indice, fx, fy, bx, by, numero) lo mette in fx/fy e tira la linea fino a bx/by.
    zone:     (indice, fx, fy, fw, fh, numero): riquadro con il numero all'angolo.
    """
    utile = LARGHEZZA - 2 * MARGINE
    disposte = []
    x = MARGINE + rientro
    y_img = 84.0 + spazio_sopra
    n_img = len(immagini)

    for png, ritaglio, larghezza_massima, frazione in immagini:
        dati, w, h = incorpora(png, ritaglio, larghezza_massima)
        larghezza_disegnata = (utile - distanza * (n_img - 1)) * frazione
        altezza_disegnata = h * larghezza_disegnata / w
        disposte.append((dati, x, y_img, larghezza_disegnata, altezza_disegnata))
        x += larghezza_disegnata + distanza

    altezza_immagini = max(d[4] for d in disposte)
    y_legenda = y_img + altezza_immagini + spazio_sotto + 30

    righe_per_colonna = (len(legenda) + colonne_legenda - 1) // colonne_legenda
    altezza_legenda = righe_per_colonna * 20 + (20 if nota else 0)
    altezza = y_legenda + altezza_legenda + 12

    fuori = ['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" width="%d" '
             'height="%d" font-family="Segoe UI, system-ui, -apple-system, sans-serif">\n'
             % (LARGHEZZA, altezza, LARGHEZZA, altezza)]
    fuori.append('  <rect width="%d" height="%d" fill="#ffffff"/>\n' % (LARGHEZZA, altezza))
    fuori.append(testo(MARGINE, 34, tradotto(titolo), 17, INCHIOSTRO, "600"))
    fuori.append(testo(MARGINE, 55, tradotto(sottotitolo), 12.5, GRIGIO))

    for dati, ix, iy, iw, ih in disposte:
        fuori.append('  <image x="%.1f" y="%.1f" width="%.1f" height="%.1f" '
                     'href="data:image/png;base64,%s"/>\n' % (ix, iy, iw, ih, dati))
        fuori.append('  <rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="none" '
                     'stroke="%s" stroke-width="1.2" rx="4"/>\n' % (ix, iy, iw, ih, BORDO))

    for indice, fx, fy, fw, fh, numero in zone:
        _, ix, iy, iw, ih = disposte[indice]
        zx, zy = ix + iw * fx, iy + ih * fy
        zw, zh = iw * fw, ih * fh
        fuori.append('  <rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" rx="6" '
                     'fill="%s" fill-opacity=".06" stroke="%s" stroke-width="2"/>\n'
                     % (zx, zy, zw, zh, ACCENTO, ACCENTO))
        # Il numero sta appena FUORI dall'angolo: appoggiato dentro coprirebbe la prima
        # parola dell'area che sta delimitando.
        fuori.append(pallino(zx - 8, zy - 8, numero, 12.0))

    for voce in richiami:
        indice, fx, fy = voce[0], voce[1], voce[2]
        numero = voce[-1]
        bx, by = (voce[3], voce[4]) if len(voce) == 6 else (fx, fy)
        _, ix, iy, iw, ih = disposte[indice]
        fuori.append(richiamo(ix + iw * fx, iy + ih * fy,
                              ix + iw * bx, iy + ih * by, numero))

    colonna = utile / colonne_legenda
    for i, (numero, voce) in enumerate(legenda):
        col = i // righe_per_colonna
        riga = i % righe_per_colonna
        x = MARGINE + col * colonna
        y = y_legenda + riga * 20
        fuori.append('  <circle cx="%.1f" cy="%.1f" r="7.5" fill="%s"/>\n'
                     % (x + 7.5, y - 4, ACCENTO))
        fuori.append('  <text x="%.1f" y="%.1f" font-size="8.5" font-weight="700" '
                     'fill="#ffffff" text-anchor="middle">%s</text>\n' % (x + 7.5, y - 1, numero))
        fuori.append(testo(x + 21, y, tradotto(voce), 10.5, INCHIOSTRO))

    if nota:
        fuori.append(testo(MARGINE, y_legenda + righe_per_colonna * 20 + 10, tradotto(nota), 10,
                           GRIGIO))

    fuori.append("</svg>\n")

    os.makedirs(MANUALE, exist_ok=True)
    percorso = os.path.join(MANUALE, nome_file)
    io.open(percorso, "w", encoding="utf-8", newline="").write("".join(fuori))
    print("  %-34s %6.0f kB   %d x %d" % (nome_file, os.path.getsize(percorso) / 1024.0,
                                          LARGHEZZA, altezza))


# ------------------------------------------------------------------ le figure del manuale
print("figure del manuale")

figura(
    "m01-pagina-iniziale.svg",
    "Figura 1 — La pagina iniziale",
    "L'archivio dei pattern: si ordina, si sfoglia, si apre. Schermata dell'applicazione in funzione.",
    [("s01-pagina-iniziale.png", (0, 0, 1600, 890), 1500, 1.0)],
    [(1, "L'intestazione: che cos'è il componente, quanti pattern ci sono, e il pulsante per crearne uno"),
     (2, "La barra: quanti pattern, con quale ordine e quante schede per pagina"),
     (3, "La scheda: l'anteprima è cliccabile e apre il pattern in modifica"),
     (4, "I dati della scheda: misura della cella, creazione e — se diversa — ultima modifica"),
     (5, "Tema chiaro, scuro o come il sistema")],
    zone=[(0, 0.016, 0.099, 0.969, 0.457, 1),
          (0, 0.016, 0.580, 0.969, 0.092, 2),
          (0, 0.016, 0.665, 0.234, 0.165, 3),
          (0, 0.016, 0.912, 0.234, 0.072, 4)],
    richiami=[(0, 0.880, 0.037, 0.945, 0.037, 5)],
    colonne_legenda=1,
    nota="Lo sfondo dell'intestazione non è un'immagine: è un pattern generato dallo stesso componente.")

figura(
    "m02-zone-editor.svg",
    "Figura 2 — Le tre zone dell'editor",
    "Si regola a sinistra, si osserva al centro, si compone a destra.",
    [("s03-editor.png", None, 1500, 1.0)],
    [(1, "Nome del pattern e comandi per chiudere la sessione (§3.1)"),
     (2, "Proprietà: misura della cella e trasformazione"),
     (3, "Le due anteprime: la cella singola e la ripetizione"),
     (4, "Il sorgente SVG, da copiare o scaricare"),
     (5, "Elementi: l'elenco di ciò che compone la cella"),
     (6, "Identificativo, date e stato della sessione")],
    zone=[(0, 0.026, 0.036, 0.949, 0.075, 1),
          (0, 0.028, 0.127, 0.194, 0.777, 2),
          (0, 0.240, 0.137, 0.466, 0.356, 3),
          (0, 0.240, 0.500, 0.466, 0.396, 4),
          (0, 0.719, 0.127, 0.256, 0.777, 5),
          (0, 0.026, 0.923, 0.949, 0.050, 6)])

figura(
    "m09-elenco-elementi.svg",
    "Figura 9 — Una riga dell'elenco, pezzo per pezzo",
    "Ogni riga è un elemento della cella, e l'ordine delle righe è l'ordine di disegno.",
    [("s05-elenco-elementi.png", (10, 190, 726, 278), 716, 0.90)],
    [(1, "La presa: si tiene premuta e si trascina in su o in giù per cambiare l'ordine"),
     (2, "L'icona dice il tipo di elemento"),
     (3, "Il nome del tipo; un clic in un punto qualsiasi della riga apre la sua scheda"),
     (4, "La freccia ricorda che la riga si apre"),
     (5, "Duplica l'elemento, con gli stessi valori"),
     (6, "Elimina l'elemento")],
    richiami=[(0, 0.073, -0.55, 0.073, 0.30, 1),
              (0, 0.168, -0.55, 0.168, 0.30, 2),
              (0, 0.303, -0.55, 0.303, 0.30, 3),
              (0, 0.764, 1.55, 0.764, 0.70, 4),
              (0, 0.825, 1.55, 0.825, 0.70, 5),
              (0, 0.906, 1.55, 0.906, 0.70, 6)],
    colonne_legenda=1, spazio_sopra=44, spazio_sotto=58)

figura(
    "m10-menu-tipi.svg",
    "Figura 10 — I tipi di elemento disponibili",
    "Il menù non è un elenco fisso: mostra i plugin che l'applicazione ha registrato all'avvio.",
    [("s11-menu-tipi.png", None, 860, 0.76)],
    [(1, "Accanto al nome c'è il tipo come compare nel sorgente SVG: rect, ellipse, path, …")],
    colonne_legenda=1, rientro=100)

figura(
    "m11-colore.svg",
    "Figura 11 — Come si sceglie un colore",
    "Tre strade per la stessa cosa: la pastiglia, il codice esadecimale, la trasparenza.",
    [("s06-colori.png", None, 744, 0.68)],
    [(1, "La pastiglia apre il selettore di colore del sistema"),
     (2, "Il codice esadecimale si può anche scrivere a mano: #9f2828, oppure 9f2828"),
     (3, "L'opacità del solo riempimento, da 0 a 100"),
     (4, "Senza la spunta l'elemento non viene riempito affatto")],
    richiami=[(0, -0.085, 0.490, 0.045, 0.490, 1),
              (0, 0.600, 0.490, 0.470, 0.490, 2),
              (0, -0.085, 0.830, 0.030, 0.830, 3),
              (0, 1.085, 0.196, 0.930, 0.196, 4)],
    colonne_legenda=1, rientro=90)

figura(
    "m12-telefono.svg",
    "Figura 12 — L'editor su uno schermo stretto",
    "Le stesse funzioni, disposte in altezza: anteprima in alto, schede sotto.",
    [("s08-telefono-elenco.png", None, 760, 0.38),
     ("s10-telefono-elemento.png", None, 760, 0.38)],
    [(1, "Nome e comandi stanno sopra l'anteprima, su un velo scuro"),
     (2, "La cella singola, incorniciata, resta sempre visibile"),
     (3, "Le tre schede: Proprietà, Elementi, SVG"),
     (4, "La presa per trascinare funziona anche col dito"),
     (5, "Dove c'è spazio i campi si affiancano a due a due")],
    richiami=[(0, -0.20, 0.035, 0.040, 0.035, 1),
              (0, -0.20, 0.140, 0.030, 0.140, 2),
              (0, 1.22, 0.366, 0.990, 0.366, 3),
              (0, -0.20, 0.545, 0.040, 0.545, 4),
              (1, -0.22, 0.640, 0.020, 0.640, 5)],
    colonne_legenda=1, rientro=88, distanza=150)

def impila(sopra, sotto, uscita, distanza=18):
    """Due schermate in una sola immagine, una sopra l'altra.

    Servono impilate e non affiancate: hanno proporzioni molto diverse — una striscia e un
    riquadro alto — e messe fianco a fianco la prima resterebbe un filo e la seconda
    sborderebbe. La figura le tratta poi come un'immagine sola.
    """
    a = Image.open(os.path.join(SCATTI, sopra))
    b = Image.open(os.path.join(SCATTI, sotto))
    larghezza = max(a.width, b.width)
    tela = Image.new("RGB", (larghezza, a.height + distanza + b.height), "white")
    tela.paste(a, ((larghezza - a.width) // 2, 0))
    tela.paste(b, ((larghezza - b.width) // 2, a.height + distanza))
    tela.save(os.path.join(SCATTI, uscita), optimize=True)
    return tela.height


altezza = impila("s12-importa-pulsante.png", "s13-importa-esito.png", "s14-importazione.png")

figura(
    "m13-importazione.svg",
    "Figura 13 — Importare un disegno SVG",
    "Si sceglie il file, si legge il resoconto, si decide. Niente entra nell'archivio prima di allora.",
    [("s14-importazione.png", None, 900, 0.78)],
    [(1, "Il pulsante sta accanto a «Crea un pattern»: sono i due modi di cominciare"),
     (2, "Quanti elementi sono entrati, e con quale cella"),
     (3, "Che cosa è rimasto fuori, e perché: è la parte da leggere"),
     (4, "Da qui si apre l'editor, dove il pattern si guarda e si conferma — o si abbandona")],
    richiami=[(0, 1.07, 0.062, 0.52, 0.062, 1),
              (0, 1.07, 0.250, 0.42, 0.250, 2),
              (0, 1.07, 0.560, 0.78, 0.560, 3),
              (0, 1.07, 0.930, 0.86, 0.930, 4)],
    colonne_legenda=1)

# La figura della conversione: la schermata la fabbrica cattura-conversione.py, che disegna
# da se' l'immagine di partenza invece di allegarne una.
figura(
    "m14-conversione.svg",
    "Figura 14 — Convertire un'immagine in pattern",
    "I tre riquadri rispondono alla sola domanda che conta: somiglia? E dove no, in che modo.",
    [("s14-conversione.png", None, 880, 0.72)],
    [(1, "L'originale: la cella ritagliata dall'immagine"),
     (2, "La ricostruzione: l'SVG vero, quello che verrebbe salvato"),
     (3, "La differenza: rosso ciò che manca, blu ciò che è stato aggiunto"),
     (4, "Quanto somiglia — da leggere insieme alla mappa, mai da sola"),
     (5, "Le due manopole: quanti colori, e quanta fedeltà. Cambiarle riconverte sul posto"),
     (6, "Il resoconto: la cella trovata, le bande, quante forme sono correzioni")],
    richiami=[(0, -0.075, 0.290, 0.085, 0.290, 1),
              (0, 0.500, 0.120, 0.500, 0.185, 2),
              (0, 1.075, 0.290, 0.905, 0.290, 3),
              (0, -0.075, 0.429, 0.060, 0.429, 4),
              (0, -0.075, 0.487, 0.070, 0.487, 5),
              (0, 1.075, 0.600, 0.800, 0.585, 6)],
    colonne_legenda=1, rientro=70)

if MANCANTI:
    print("\nSENZA TRADUZIONE (%d):" % len(MANCANTI))
    for voce in dict.fromkeys(MANCANTI):
        print("    %r," % voce)
else:
    print("fatto")
