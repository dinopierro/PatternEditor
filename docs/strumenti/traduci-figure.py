# -*- coding: utf-8 -*-
"""Le figure disegnate a mano, tradotte.

Le figure 3-8 non sono fotografie e non le produce nessuno script: sono disegni, scritti
una volta e conservati. Rifarne una versione inglese significherebbe ridisegnarle, ed e' un
lavoro che non vale la pena fare due volte: l'unica parte che dipende dalla lingua e' il
**testo**, e il testo si puo' sostituire lasciando il disegno dov'e'.

Lo script legge gli SVG italiani, rimpiazza il contenuto di ogni <text> e <tspan> con la
traduzione presa da `testi_figure.TESTI`, e scrive il risultato accanto alle altre figure
della lingua richiesta.

    python docs/strumenti/traduci-figure.py en

Quello che non trova tradotto lo elenca alla fine invece di lasciarlo passare in silenzio:
una figura mezza italiana in un manuale inglese e' il genere di cosa che nessuno nota finche'
non la nota un lettore.
"""
import io
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from testi_figure import TESTI  # noqa: E402

LINGUA = sys.argv[1] if len(sys.argv) > 1 else "en"

RADICE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DA = os.path.join(RADICE, "docs", "immagini", "manuale")
A = os.path.join(DA, LINGUA)

# Le figure disegnate: le altre sono fotografie e le rifa' genera-figure.py.
DISEGNATE = ["m03-cella.svg", "m04-trasformazione.svg", "m05-coordinate.svg",
             "m06-colori.svg", "m07-elementi.svg", "m08-adattamento.svg"]

# Quello che non e' lingua: nomi di tag, sigle, numeri, lettere degli assi. Tradurli non
# avrebbe senso e chiederne la traduzione riempirebbe l'elenco dei buchi di rumore.
NEUTRO = re.compile(r"""^(?:
    \s* | [\d\s.,;:×()\-+/%°]* | &lt;[a-z]+&gt; |
    X | Y | Abc | Ago | meet | slice | none | \(0,0\)\s*origine
)$""", re.X)


def traduci(testo):
    nudo = testo.strip()
    if not nudo or NEUTRO.match(nudo):
        return testo, None
    if nudo in TESTI:
        return testo.replace(nudo, TESTI[nudo]), None
    return testo, nudo


def main():
    os.makedirs(A, exist_ok=True)
    mancanti = []

    print("figure disegnate (%s)" % LINGUA)
    for nome in DISEGNATE:
        percorso = os.path.join(DA, nome)
        if not os.path.exists(percorso):
            print("  %-28s assente" % nome)
            continue

        s = io.open(percorso, encoding="utf-8").read()

        def sostituisci(m):
            dentro, buco = traduci(m.group(2))
            if buco:
                mancanti.append(buco)
            return m.group(1) + dentro + m.group(3)

        fuori = re.sub(r"(>)([^<>]+)(</(?:text|tspan)>)", sostituisci, s)
        destinazione = os.path.join(A, nome)
        io.open(destinazione, "w", encoding="utf-8", newline="").write(fuori)
        print("  %-28s %6.0f kB" % (nome, os.path.getsize(destinazione) / 1024.0))

    if mancanti:
        print("\nSENZA TRADUZIONE (%d):" % len(dict.fromkeys(mancanti)))
        for voce in dict.fromkeys(mancanti):
            print("    %r," % voce)
        return 1

    print("fatto")
    return 0


if __name__ == "__main__":
    sys.exit(main())
