# -*- coding: utf-8 -*-
"""Schermata della finestra di conversione da immagine, per la figura del manuale.

Sta a parte dalle altre catture per una ragione pratica: le altre fotografano schermate che
esistono gia', questa deve prima **fabbricare l'immagine da convertire**. E la fabbrica sul
posto, con una tela del browser, invece di allegare un file: un file allegato va tenuto
insieme allo script, puo' sparire, e nessuno ricorda piu' perche' fosse proprio quello. Il
disegno scritto qui invece si rilegge, e chi rifa' la figura fra un anno ottiene la stessa.

Il motivo a spina di pesce non e' scelto a caso: ha un reticolo **obliquo**, cioe' esercita
la parte del riconoscimento che gli assi separati non vedrebbero, ed e' piatto, cioe' e' il
caso in cui la conversione riesce bene e la figura mostra qualcosa di leggibile.

    python docs/strumenti/cattura-conversione.py http://localhost:5080/
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from chrome import Browser  # noqa: E402

from PIL import Image  # noqa: E402

URL = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080/"
RADICE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LINGUA = sys.argv[2] if len(sys.argv) > 2 else "it"
USCITA = os.path.join(RADICE, "docs", "immagini", "scatti")
if LINGUA != "it":
    USCITA = os.path.join(USCITA, LINGUA)

os.makedirs(USCITA, exist_ok=True)

# La spina di pesce: tessere di 80x80, quattro mattoni per tessera, con la fuga chiara.
DISEGNO = """
(function () {
  var cv = document.createElement('canvas');
  cv.width = 480; cv.height = 480;
  var cx = cv.getContext('2d');
  cx.fillStyle = '#f2e6d4'; cx.fillRect(0, 0, 480, 480);
  cx.fillStyle = '#d99a5b';
  cx.strokeStyle = '#f7ece0'; cx.lineWidth = 5;
  for (var by = -80; by < 560; by += 80) {
    for (var bx = -80; bx < 560; bx += 80) {
      cx.fillRect(bx, by, 80, 40);        cx.strokeRect(bx, by, 80, 40);
      cx.fillRect(bx, by + 40, 40, 40);   cx.strokeRect(bx, by + 40, 40, 40);
      cx.fillRect(bx + 40, by + 40, 40, 40); cx.strokeRect(bx + 40, by + 40, 40, 40);
    }
  }
  return cv.toDataURL('image/png');
})()
"""

CONSEGNA = """
(async function () {
  var url = window.__disegno;
  var b = await (await fetch(url)).blob();
  var dt = new DataTransfer();
  dt.items.add(new File([b], 'spina-di-pesce.png', {type: 'image/png'}));
  var campi = document.querySelectorAll('.vetrina__file');
  campi[1].files = dt.files;
  campi[1].dispatchEvent(new Event('change', {bubbles: true}));
  return true;
})()
"""


# La lingua delle schermate. L'applicazione la ricorda in localStorage, quindi si apre una
# prima volta solo per scriverla e si riparte: e' l'unico modo di avere un'origine su cui
# scrivere. Le schermate italiane restano dove sono sempre state; le altre vanno in una
# sottocartella, cosi' le due serie convivono.
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
    return percorso


def rettangolo(b, selettore):
    v = b.valuta("(function(){var e=document.querySelector(%r);if(!e)return null;"
                 "var r=e.getBoundingClientRect();"
                 "return {x:r.x+window.scrollX,y:r.y+window.scrollY,"
                 "width:r.width,height:r.height};})()" % selettore)
    if not v:
        raise RuntimeError("elemento assente: " + selettore)
    return v


b = Browser(1440, 1100, 2)
try:
    print("conversione da immagine (%s)" % LINGUA)
    imposta_lingua(b, URL, LINGUA)
    b.vai(URL + "gestione")
    b.attendi("document.querySelectorAll('.vetrina__file').length > 1")
    time.sleep(1.5)

    b.valuta("window.__disegno = " + DISEGNO.strip())
    b.valuta(CONSEGNA.strip())

    # La conversione gira su un thread solo: finche' lavora la pagina non risponde, e l'attesa
    # va data larga.
    b.attendi("document.querySelector('.confronto') !== null", secondi=180)
    time.sleep(0.8)

    r = rettangolo(b, ".modal-backdrop .conferma")
    margine = 16
    salva(b, "s14-conversione.png", {
        "x": max(0, r["x"] - margine),
        "y": max(0, r["y"] - margine),
        "width": r["width"] + margine * 2,
        "height": r["height"] + margine * 2,
    })

    # Si esce con Annulla: niente raggiunge l'archivio.
    b.valuta("(function(){var b=[].slice.call(document.querySelectorAll('button'))"
             ".filter(function(x){return /Annulla|Cancel/i.test(x.textContent)});"
             "if(b.length) b[0].click(); return b.length;})()")
    print("  chiusa con Annulla")
finally:
    b.chiudi()
