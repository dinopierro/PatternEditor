# -*- coding: utf-8 -*-
"""Attrezzi minimi per disegnare i diagrammi dell'analisi.

Non c'è una libreria: i diagrammi sono SVG scritti a mano, e questo modulo è solo il
pennello. Il motivo è che devono stare accanto alle otto figure già esistenti, disegnate
allo stesso modo, con lo stesso impaginato e la stessa tavolozza; una libreria di
diagrammi ne imporrebbe una sua.

Tutto è in coordinate assolute e in punti: la figura viene poi rimpicciolita dal
documento, che la porta a 168 mm di larghezza.
"""
import io
import os

RADICE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DOCS = os.path.join(RADICE, "docs")
USCITA = os.path.join(DOCS, "immagini")

INCHIOSTRO = "#1b2430"
GRIGIO = "#6b7684"
ACCENTO = "#3b6ef5"
BORDO = "#ccd3db"
BORDO_TENUE = "#e3e6ea"
FONDO = "#fbfcfd"
FONDO_ACCENTO = "#eef3ff"
FONDO_CALDO = "#fff6ec"
ARANCIO = "#c2701c"
VERDE = "#2f8f5b"
FONDO_VERDE = "#edf8f2"

FONT = "Segoe UI, system-ui, -apple-system, sans-serif"
MONO = "Consolas, ui-monospace, monospace"


def _fuga(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


class Tela:
    """Un foglio su cui si depositano forme, che alla fine sa quanto è alto."""

    def __init__(self, titolo, sottotitolo="", larghezza=940, margine=30):
        self.larghezza = larghezza
        self.margine = margine
        self.pezzi = []
        self.fondo = []
        self.basso = 84.0
        self.titolo = titolo
        self.sottotitolo = sottotitolo

    # ------------------------------------------------------------------ primitive
    def testo(self, x, y, s, misura=10, colore=INCHIOSTRO, peso="400",
              ancora="start", famiglia=None, corsivo=False):
        self.pezzi.append(
            '  <text x="%.1f" y="%.1f" font-size="%s" fill="%s" font-weight="%s" '
            'text-anchor="%s"%s%s>%s</text>\n'
            % (x, y, misura, colore, peso, ancora,
               ' font-family="%s"' % famiglia if famiglia else "",
               ' font-style="italic"' if corsivo else "", _fuga(s)))
        self.basso = max(self.basso, y + 6)

    def rettangolo(self, x, y, w, h, riempimento="#ffffff", tratto=BORDO,
                   spessore=1.4, raggio=8, tratteggio=None, opacita=None):
        self.pezzi.append(
            '  <rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" rx="%s" fill="%s" '
            'stroke="%s" stroke-width="%s"%s%s/>\n'
            % (x, y, w, h, raggio, riempimento, tratto, spessore,
               ' stroke-dasharray="%s"' % tratteggio if tratteggio else "",
               ' fill-opacity="%s"' % opacita if opacita is not None else ""))
        self.basso = max(self.basso, y + h)

    def linea(self, x1, y1, x2, y2, colore=BORDO, spessore=1.2, tratteggio=None):
        self.pezzi.append(
            '  <line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="%s" '
            'stroke-width="%s"%s/>\n'
            % (x1, y1, x2, y2, colore, spessore,
               ' stroke-dasharray="%s"' % tratteggio if tratteggio else ""))
        self.basso = max(self.basso, y1, y2)

    def percorso(self, d, tratto=ACCENTO, spessore=1.6, riempimento="none",
                 punta=True, tratteggio=None):
        self.pezzi.append(
            '  <path d="%s" fill="%s" stroke="%s" stroke-width="%s"%s%s/>\n'
            % (d, riempimento, tratto, spessore,
               ' marker-end="url(#punta)"' if punta else "",
               ' stroke-dasharray="%s"' % tratteggio if tratteggio else ""))

    # ------------------------------------------------------------------ composti
    def scatola(self, x, y, w, h, titolo, righe=(), tono="chiaro", misura_titolo=11):
        """Un riquadro con un titolo in grassetto e qualche riga di dettaglio sotto."""
        riempimenti = {
            "chiaro": ("#ffffff", BORDO),
            "tenue": (FONDO, BORDO_TENUE),
            "accento": (FONDO_ACCENTO, ACCENTO),
            "caldo": (FONDO_CALDO, ARANCIO),
            "verde": (FONDO_VERDE, VERDE),
        }
        riempimento, tratto = riempimenti[tono]
        self.rettangolo(x, y, w, h, riempimento, tratto, 1.5)
        self.testo(x + 12, y + 20, titolo, misura_titolo, INCHIOSTRO, "700")
        for i, riga in enumerate(righe):
            famiglia = None
            testo_riga = riga
            if riga.startswith("`") and riga.endswith("`"):
                famiglia, testo_riga = MONO, riga[1:-1]
            self.testo(x + 12, y + 38 + i * 14, testo_riga, 9, GRIGIO, "400",
                       famiglia=famiglia)

    def pastiglia(self, x, y, testo, colore=ACCENTO, fondo=FONDO_ACCENTO, misura=9):
        larghezza = 10 + len(testo) * misura * 0.56
        self.rettangolo(x, y, larghezza, 18, fondo, colore, 1.2, raggio=9)
        self.testo(x + larghezza / 2, y + 12.5, testo, misura, colore, "600", "middle")
        return larghezza

    def freccia(self, x1, y1, x2, y2, etichetta=None, tratteggio=None,
                colore=ACCENTO, sopra=True, misura=8.5):
        self.percorso("M %.1f %.1f L %.1f %.1f" % (x1, y1, x2, y2),
                      colore, 1.6, tratteggio=tratteggio)
        if etichetta:
            mx, my = (x1 + x2) / 2, (y1 + y2) / 2
            self.testo(mx, my - 5 if sopra else my + 12, etichetta, misura, colore,
                       "600", "middle")
        self.basso = max(self.basso, y1, y2)

    def gomito(self, x1, y1, x2, y2, etichetta=None, colore=ACCENTO, tratteggio=None):
        """Freccia a due tratti: prima in orizzontale, poi in verticale."""
        self.percorso("M %.1f %.1f L %.1f %.1f L %.1f %.1f" % (x1, y1, x2, y1, x2, y2),
                      colore, 1.6, tratteggio=tratteggio)
        if etichetta:
            self.testo((x1 + x2) / 2, y1 - 6, etichetta, 8.5, colore, "600", "middle")
        self.basso = max(self.basso, y1, y2)

    def nota(self, x, y, w, righe, tono="tenue"):
        h = 14 + len(righe) * 13
        riempimento, tratto = (FONDO, BORDO_TENUE) if tono == "tenue" else (FONDO_CALDO, ARANCIO)
        self.rettangolo(x, y, w, h, riempimento, tratto, 1.2, raggio=6)
        for i, riga in enumerate(righe):
            self.testo(x + 10, y + 17 + i * 13, riga, 8.8, GRIGIO)
        return h

    # ------------------------------------------------------------------ chiusura
    def salva(self, nome, altezza=None):
        h = altezza or (self.basso + 24)
        testa = ('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" width="%d" '
                 'height="%d" font-family="%s">\n' % (self.larghezza, h, self.larghezza, h, FONT))
        testa += '  <rect width="%d" height="%d" fill="#ffffff"/>\n' % (self.larghezza, h)
        testa += ('  <defs><marker id="punta" viewBox="0 0 10 10" refX="9" refY="5" '
                  'markerWidth="6" markerHeight="6" orient="auto-start-reverse">'
                  '<path d="M 0 0 L 10 5 L 0 10 z" fill="%s"/></marker></defs>\n' % ACCENTO)
        testa += ('  <text x="%s" y="34" font-size="17" font-weight="600" fill="%s">%s</text>\n'
                  % (self.margine, INCHIOSTRO, _fuga(self.titolo)))
        if self.sottotitolo:
            testa += ('  <text x="%s" y="55" font-size="12.5" fill="%s">%s</text>\n'
                      % (self.margine, GRIGIO, _fuga(self.sottotitolo)))

        percorso = os.path.join(USCITA, nome)
        io.open(percorso, "w", encoding="utf-8", newline="").write(
            testa + "".join(self.fondo) + "".join(self.pezzi) + "</svg>\n")
        print("  %-30s %5d x %-5d %5.0f kB" % (nome, self.larghezza, h,
                                               os.path.getsize(percorso) / 1024.0))


def sequenza(tela, attori, messaggi, x0=40, larghezza_attore=None, y0=84, passo=42):
    """Diagramma di sequenza: attori in cima, linee di vita, messaggi numerati.

    messaggi: (indice_da, indice_a, testo) oppure (indice_da, indice_a, testo, 'rit')
              per una risposta (tratteggiata), o (i, i, testo, 'auto') per un'azione
              che l'attore compie su se stesso.
    """
    utile = tela.larghezza - 2 * x0
    n = len(attori)
    larghezza_attore = larghezza_attore or min(150, utile / n - 10)
    passo_x = utile / n
    centri = [x0 + passo_x * i + passo_x / 2 for i in range(n)]

    # L'etichetta dell'attore puo' essere su due righe: <text> non manda a capo da solo,
    # quindi un a-capo nell'etichetta diventa due <text>, uno sotto l'altro.
    for i, nome in enumerate(attori):
        x = centri[i] - larghezza_attore / 2
        tela.rettangolo(x, y0, larghezza_attore, 34, FONDO_ACCENTO, ACCENTO, 1.4, raggio=7)
        parti = nome.split(chr(10))
        if len(parti) == 1:
            tela.testo(centri[i], y0 + 22, nome, 9.5, INCHIOSTRO, "700", "middle")
        else:
            tela.testo(centri[i], y0 + 15, parti[0], 9, INCHIOSTRO, "700", "middle")
            tela.testo(centri[i], y0 + 27, parti[1], 9, INCHIOSTRO, "700", "middle")

    # Le linee di vita si disegnano PRIMA dei messaggi, altrimenti passerebbero sopra
    # le frecce: in un SVG vince chi viene dopo. Quindi prima si conta quanto sara' lungo
    # il diagramma, poi si tracciano, poi si scrivono i messaggi.
    def altezza_di(messaggio):
        genere = messaggio[3] if len(messaggio) > 3 else "chiamata"
        if genere == "nota":
            return 34
        return passo - 6 if messaggio[0] == messaggio[1] else passo

    y_inizio = y0 + 34 + 26
    y_fine = y_inizio + sum(altezza_di(m) for m in messaggi)
    for i in range(n):
        tela.linea(centri[i], y0 + 34, centri[i], y_fine - passo + 20, BORDO, 1.1, "4 4")

    y = y_inizio
    for messaggio in messaggi:
        da, a, testo = messaggio[0], messaggio[1], messaggio[2]
        genere = messaggio[3] if len(messaggio) > 3 else "chiamata"

        if genere == "nota":
            tela.nota(centri[da] - 150, y - 14, 300, [testo])
            y += 34
            continue

        if da == a:
            x = centri[da]
            tela.percorso("M %.1f %.1f L %.1f %.1f L %.1f %.1f L %.1f %.1f"
                          % (x, y - 6, x + 34, y - 6, x + 34, y + 8, x + 4, y + 8))
            tela.testo(x + 42, y + 2, testo, 9, INCHIOSTRO)
            y += passo - 6
            continue

        verso = 1 if a > da else -1
        x1 = centri[da] + verso * 4
        x2 = centri[a] - verso * 4
        tela.freccia(x1, y, x2, y, None,
                     tratteggio="5 4" if genere == "rit" else None,
                     colore=GRIGIO if genere == "rit" else ACCENTO)
        tela.testo((x1 + x2) / 2, y - 7, testo, 9,
                   GRIGIO if genere == "rit" else INCHIOSTRO,
                   "600" if genere != "rit" else "400", "middle")
        y += passo

    tela.basso = max(tela.basso, y)
    return centri, y

def spezza(testo, massimo):
    """Manda a capo sulle PAROLE. <text> non sa andare a capo, e tagliare a lunghezza
    fissa spezzerebbe le parole a meta'."""
    righe, corrente = [], ""
    for parola in testo.split(" "):
        prova = (corrente + " " + parola).strip()
        if len(prova) <= massimo:
            corrente = prova
        else:
            if corrente:
                righe.append(corrente)
            corrente = parola
    if corrente:
        righe.append(corrente)
    return righe


def codice(tela, x, y, righe, passo=15, misura=8.2, colore=INCHIOSTRO):
    """Righe di codice con l'indentazione conservata.

    In SVG gli spazi iniziali di un <text> vengono ignorati, e un frammento di JSON senza
    rientri non si legge. Si sostituiscono con spazi unificatori, che nessuno collassa.
    """
    for i, riga in enumerate(righe):
        testo_riga = riga
        conteggio = len(riga) - len(riga.lstrip(" "))
        if conteggio:
            testo_riga = " " * conteggio + riga.lstrip(" ")
        tela.testo(x, y + i * passo, testo_riga, misura,
                   colore(i) if callable(colore) else colore, famiglia=MONO)
