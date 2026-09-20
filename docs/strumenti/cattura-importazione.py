# -*- coding: utf-8 -*-
"""Le due schermate dell'importazione di un SVG, per la figura 13 del manuale.

Esistevano gia' come file ma non le produceva nessuno script: erano state fatte a mano, e
quindi non si potevano rifare — ne' quando l'interfaccia cambiava, ne' per una seconda lingua.
Questo le rifa' tutte e due.

Il disegno da importare si **scrive qui**, e non e' un file allegato: cosi' si rilegge, e chi
rifa' la figura fra un anno ottiene lo stesso resoconto. Contiene apposta anche due cose che
l'importazione non sa rappresentare — un rettangolo ruotato e una forma riempita con un
gradiente — perche' la meta' interessante di quella finestra e' l'elenco di cio' che resta
fuori, e su un disegno che entra tutto quell'elenco sarebbe vuoto.

    python docs/strumenti/cattura-importazione.py http://localhost:5080/ [it|en]
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from chrome import Browser  # noqa: E402

from PIL import Image  # noqa: E402

URL = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080/"
LINGUA = sys.argv[2] if len(sys.argv) > 2 else "it"

RADICE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
USCITA = os.path.join(RADICE, "docs", "immagini", "scatti")
if LINGUA != "it":
    USCITA = os.path.join(USCITA, LINGUA)

os.makedirs(USCITA, exist_ok=True)

DISEGNO = """<svg xmlns="http://www.w3.org/2000/svg" width="60" height="60" viewBox="0 0 60 60">
  <defs>
    <linearGradient id="sfumatura"><stop offset="0" stop-color="#fff"/><stop offset="1" stop-color="#000"/></linearGradient>
  </defs>
  <rect x="0" y="0" width="60" height="30" fill="#c1502e"/>
  <circle cx="15" cy="45" r="9" fill="#3b6ef5"/>
  <circle cx="45" cy="45" r="9" fill="#3b6ef5"/>
  <rect x="24" y="36" width="12" height="18" fill="#ecc94b" transform="rotate(20 30 45)"/>
  <ellipse cx="30" cy="15" rx="10" ry="6" fill="url(#sfumatura)"/>
</svg>"""

CONSEGNA = """
(function () {
  var b = new Blob([%s], {type: 'image/svg+xml'});
  var dt = new DataTransfer();
  dt.items.add(new File([b], 'mattoni-e-pois.svg', {type: 'image/svg+xml'}));
  var campi = document.querySelectorAll('.vetrina__file');
  campi[0].files = dt.files;
  campi[0].dispatchEvent(new Event('change', {bubbles: true}));
  return true;
})()
"""


def imposta_lingua(b, url, codice):
    b.vai(url)
    b.valuta("(function(){try{localStorage.setItem('pattern-editor-lingua', %r);"
             "return true}catch(e){return false}})()" % codice)


def salva(b, nome, ritaglio, larghezza_finale=1600):
    grezzo = os.path.join(USCITA, "_grezzo.png")
    b.scatta(grezzo, ritaglio)
    img = Image.open(grezzo)
    if img.width > larghezza_finale:
        altezza = round(img.height * larghezza_finale / img.width)
        img = img.resize((larghezza_finale, altezza), Image.LANCZOS)
    img = img.convert("RGB")
    percorso = os.path.join(USCITA, nome)
    img.save(percorso, optimize=True)
    os.remove(grezzo)
    print("  %-28s %5d x %-5d %6.0f kB" % (nome, img.width, img.height,
                                           os.path.getsize(percorso) / 1024.0))


def rettangolo(b, selettore):
    v = b.valuta("(function(){var e=document.querySelector(%r);if(!e)return null;"
                 "var r=e.getBoundingClientRect();"
                 "return {x:r.x+window.scrollX, y:r.y+window.scrollY,"
                 "width:r.width, height:r.height};})()" % selettore)
    if not v:
        raise RuntimeError("elemento assente: " + selettore)
    return v


b = Browser(1440, 1100, 2)
try:
    print("importazione di un SVG (%s)" % LINGUA)
    imposta_lingua(b, URL, LINGUA)
    b.vai(URL + "gestione")
    b.attendi("document.querySelectorAll('.vetrina__file').length > 1")
    time.sleep(1.5)

    # 1. La fila dei comandi, dove sta il pulsante.
    r = rettangolo(b, ".vetrina__azioni")
    salva(b, "s12-importa-pulsante.png",
          {"x": max(0, r["x"] - 14), "y": max(0, r["y"] - 14),
           "width": r["width"] + 28, "height": r["height"] + 28},
          larghezza_finale=1200)

    # 2. Il resoconto.
    b.valuta(CONSEGNA % ("`" + DISEGNO.replace("`", "") + "`"))
    b.attendi("document.querySelector('.modal-backdrop .conferma') !== null", secondi=60)
    time.sleep(0.8)

    r = rettangolo(b, ".modal-backdrop .conferma")
    salva(b, "s13-importa-esito.png",
          {"x": max(0, r["x"] - 16), "y": max(0, r["y"] - 16),
           "width": r["width"] + 32, "height": r["height"] + 32},
          larghezza_finale=1200)

    # Si esce senza aprire l'editor: la finestra dell'importazione ha il suo Annulla, e
    # nessun pattern deve arrivare all'archivio.
    uscita = b.valuta("""(function(){
        var b = document.querySelectorAll('.conferma button');
        for (var i = 0; i < b.length; i++) {
            var t = b[i].textContent.trim();
            if (/^(annulla|cancel|chiudi|close)$/i.test(t)) { b[i].click(); return t; }
        }
        return null;
    })()""")
    print("  uscita con:", uscita)

    if uscita is None:
        raise RuntimeError("nessun pulsante di uscita: la finestra resterebbe aperta")

    b.attendi("document.querySelector('.modal-backdrop') === null", secondi=30)
    print("  finestra chiusa, archivio intatto")
finally:
    b.chiudi()
