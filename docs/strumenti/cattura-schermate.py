# -*- coding: utf-8 -*-
"""Schermate vere dell'applicazione in funzione, per le figure dei documenti.

Le figure dei documenti erano finora disegni: fedeli come schema, ma non come aspetto.
Qui l'applicazione viene davvero avviata, pilotata e fotografata, cosi' le figure
invecchiano insieme al programma invece di raccontarne una versione immaginata.

Le immagini escono in docs/immagini/scatti a scala doppia e poi ridotte: catturare grande
e ridurre da' bordi e testi puliti, catturare piccolo no.
"""
import io
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from chrome import Browser  # noqa: E402

from PIL import Image  # noqa: E402

# L'indirizzo dell'applicazione cambia a ogni avvio del server di sviluppo: si passa da
# riga di comando, e quello scritto qui e' solo il valore di comodo.
#     python docs/strumenti/cattura-schermate.py http://localhost:5000/ "Briks"
URL = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080/"
RADICE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DOCS = os.path.join(RADICE, "docs")
USCITA = os.path.join(DOCS, "immagini", "scatti")
PATTERN = sys.argv[2] if len(sys.argv) > 2 else "Mattoni"
LINGUA = sys.argv[3] if len(sys.argv) > 3 else "it"
if LINGUA != "it":
    USCITA = os.path.join(USCITA, LINGUA)

os.makedirs(USCITA, exist_ok=True)

CARTE_PRONTE = "document.querySelectorAll('.scheda:not(.scheda--attesa)').length > 0"
EDITOR_PRONTO = "document.querySelector('.pe-editor') !== null"


# La lingua delle schermate. L'applicazione la ricorda in localStorage, quindi si apre una
# prima volta solo per scriverla e si riparte: e' l'unico modo di avere un'origine su cui
# scrivere. Le schermate italiane restano dove sono sempre state; le altre vanno in una
# sottocartella, cosi' le due serie convivono.
def imposta_lingua(b, url, codice):
    b.vai(url)
    b.valuta("(function(){try{localStorage.setItem('pattern-editor-lingua', %r);"
             "return true}catch(e){return false}})()" % codice)


def salva(b, nome, ritaglio=None, larghezza_finale=1600):
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
    """Posizione e misura di un elemento, in pixel CSS e riferite alla PAGINA.

    getBoundingClientRect misura rispetto alla finestra, il ritaglio della schermata
    rispetto al documento: senza sommare lo scorrimento si ritaglia il posto sbagliato.
    """
    v = b.valuta("(function(){var e=document.querySelector(%r);if(!e)return null;"
                 "var r=e.getBoundingClientRect();"
                 "return {x:r.x+window.scrollX,y:r.y+window.scrollY,"
                 "width:r.width,height:r.height};})()" % selettore)
    if not v:
        raise RuntimeError("elemento assente: " + selettore)
    return v


def cerca(b, nome):
    """Scrive nella casella di ricerca e lascia che il filtro si applichi.

    Il campo e' legato a Blazor, e assegnare `value` da JavaScript non basta: il componente
    non se ne accorge. L'evento `input` e' quello che ascolta.
    """
    b.valuta("""(function(){
        var c = document.querySelector('.campo__testo');
        if (!c) return false;
        var set = Object.getOwnPropertyDescriptor(
            window.HTMLInputElement.prototype, 'value').set;
        set.call(c, %r);
        c.dispatchEvent(new Event('input', {bubbles: true}));
        return true;
    })()""" % nome)
    time.sleep(1.0)


def clic(b, selettore):
    b.valuta("document.querySelector(%r).click()" % selettore)


b = Browser(1440, 980, 2)
try:
    imposta_lingua(b, URL, LINGUA)
    print("pagina iniziale (%s)" % LINGUA)
    b.vai(URL)
    b.attendi(CARTE_PRONTE)
    time.sleep(1.2)                       # il tempo delle animazioni d'ingresso
    salva(b, "s01-pagina-iniziale.png")

    # La griglia delle schede da sola, senza la vetrina: serve per la figura del capitolo
    # sulla pagina iniziale, dove interessa la scheda e non l'intestazione.
    b.valuta("document.querySelector('.barra').scrollIntoView({block:'start'})")
    time.sleep(0.8)
    salva(b, "s02-griglia-schede.png")

    print("editor")

    # Il pattern si cerca invece di scorrerlo. Prima lo si pescava dalla prima pagina, e
    # bastava che qualcuno ne salvasse dodici perche' la cattura aprisse un pattern
    # qualsiasi: succede, ed e' successo. La casella di ricerca e' anche il modo in cui lo
    # cercherebbe una persona.
    cerca(b, PATTERN)
    b.attendi(CARTE_PRONTE)
    time.sleep(0.8)

    aperto = b.valuta("""(function(){
        var schede = document.querySelectorAll('.scheda');
        for (var i = 0; i < schede.length; i++) {
            var t = schede[i].querySelector('.scheda__corpo h3, .scheda__corpo .scheda__nome, h3');
            if (t && t.textContent.trim() === %r) {
                schede[i].querySelector('.scheda__tela').click();
                return true;
            }
        }
        return false;
    })()""" % PATTERN)
    print("  pattern aperto:", aperto)
    if not aperto:
        raise RuntimeError(
            "nessun pattern si chiama %r: passane uno che esista come secondo argomento"
            % PATTERN)

    b.attendi(EDITOR_PRONTO)
    b.attendi("document.querySelectorAll('.pe-elements__apri').length > 0")
    time.sleep(1.2)
    salva(b, "s03-editor.png")

    # L'elenco PRIMA di aprire un elemento: aprendolo la colonna cambia contenuto.
    r = rettangolo(b, ".pe-panel--elements")
    print("  colonna elementi:", r)
    salva(b, "s05-elenco-elementi.png",
          {"x": r["x"] - 4, "y": r["y"] - 4, "width": r["width"] + 8, "height": r["height"] + 8},
          larghezza_finale=1100)

    # Un elemento aperto: la scheda di dettaglio con i suoi campi.
    apre = b.valuta("""(function(){
        var righe = document.querySelectorAll('.pe-elements__apri');
        if (!righe.length) return false;
        righe[0].click();
        return true;
    })()""")
    print("  elemento aperto:", apre)
    # I gruppi della scheda sono piu' di quelli delle sole proprieta': aspettare che
    # compaiano e' l'unico modo affidabile di sapere che il dettaglio si e' disegnato.
    b.attendi("document.querySelectorAll('.pe-group').length > 2")
    time.sleep(0.6)
    salva(b, "s04-editor-elemento.png")

    # Il gruppo dei colori: picker, esadecimale e opacita' sulla stessa riga.
    #
    # Le voci si cercano per TESTO, e il testo e' tradotto: ogni espressione di questo
    # script deve quindi conoscere tutt'e due le lingue. Cercando le sole parole italiane la
    # serie inglese si fermava a meta' — il gruppo non si trovava, il ritorno all'elenco non
    # avveniva, e da li' in poi mancava tutto.
    trovato = b.valuta("""(function(){
        var g = document.querySelectorAll('.pe-group');
        for (var i = 0; i < g.length; i++) {
            var t = g[i].querySelector('.pe-group__title, legend, h4, h3');
            if (t && /aspetto|colore|riempimento|fill|appearance/i.test(t.textContent)) {
                g[i].scrollIntoView({block:'center'});
                return t.textContent.trim();
            }
        }
        return null;
    })()""")
    print("  gruppo colori:", trovato)
    time.sleep(0.6)
    if trovato:
        r = b.valuta("""(function(){
            var g = document.querySelectorAll('.pe-group');
            for (var i = 0; i < g.length; i++) {
                var t = g[i].querySelector('.pe-group__title, legend, h4, h3');
                if (t && /aspetto|colore|riempimento|fill|appearance/i.test(t.textContent)) {
                    var r = g[i].getBoundingClientRect();
                    return {x:r.x+window.scrollX, y:r.y+window.scrollY,
                            width:r.width, height:r.height};
                }
            }
            return null;
        })()""")
        if r and r["height"] > 40:
            salva(b, "s06-colori.png",
                  {"x": r["x"] - 14, "y": r["y"] - 10, "width": r["width"] + 42,
                   "height": r["height"] + 20},
                  larghezza_finale=1100)

    # Il menu dei tipi disponibili: e' l'elenco dei plugin registrati, non una lista fissa.
    b.valuta("""(function(){
        var b = document.querySelectorAll('.pe-dettaglio__testa button');
        for (var i = 0; i < b.length; i++) {
            if (/elenco|list/i.test(b[i].textContent)) { b[i].click(); return true; }
        }
        return false;
    })()""")
    b.attendi("document.querySelector('.pe-add > button') !== null")
    time.sleep(0.5)
    b.valuta("document.querySelector('.pe-add > button').click()")
    b.attendi("document.querySelector('.pe-menu') !== null")
    time.sleep(0.5)
    # Il menu e' piu' largo della colonna e la sborda a sinistra: il ritaglio e' l'unione
    # dei due rettangoli, altrimenti meta' voci restano fuori dall'immagine.
    r = rettangolo(b, ".pe-panel--elements")
    m = rettangolo(b, ".pe-menu")
    x0 = min(r["x"], m["x"]) - 10
    y0 = r["y"] - 8
    x1 = max(r["x"] + r["width"], m["x"] + m["width"]) + 10
    y1 = m["y"] + m["height"] + 24
    salva(b, "s11-menu-tipi.png",
          {"x": x0, "y": y0, "width": x1 - x0, "height": y1 - y0},
          larghezza_finale=1100)
    b.valuta("var f=document.querySelector('.pe-menu__backdrop'); if(f) f.click();")
    time.sleep(0.5)

    print("schermo stretto")
    b.misura(390, 844, 3)
    time.sleep(1.5)
    salva(b, "s07-telefono-anteprima.png", larghezza_finale=780)

    # La scheda degli elementi sullo schermo stretto.
    passato = b.valuta("""(function(){
        var s = document.querySelectorAll('.pe-scheda');
        for (var i = 0; i < s.length; i++) {
            if (/element/i.test(s[i].textContent)) { s[i].click(); return s[i].textContent.trim(); }
        }
        return null;
    })()""")
    print("  scheda:", passato)
    time.sleep(1.0)

    # Se e' rimasto aperto il dettaglio si torna all'elenco: sono due figure diverse.
    indietro = b.valuta("""(function(){
        var b = document.querySelectorAll('.pe-dettaglio__testa button');
        for (var i = 0; i < b.length; i++) {
            if (/elenco|list/i.test(b[i].textContent)) { b[i].click(); return true; }
        }
        return false;
    })()""")
    print("  tornato all'elenco:", indietro)
    time.sleep(0.9)
    salva(b, "s08-telefono-elenco.png", larghezza_finale=780)

    b.valuta("var r=document.querySelectorAll('.pe-elements__apri'); if(r.length) r[0].click();")
    time.sleep(1.0)
    salva(b, "s10-telefono-elemento.png", larghezza_finale=780)

    passato = b.valuta("""(function(){
        var s = document.querySelectorAll('.pe-scheda');
        for (var i = 0; i < s.length; i++) {
            if (/propriet|propert/i.test(s[i].textContent)) { s[i].click(); return s[i].textContent.trim(); }
        }
        return null;
    })()""")
    print("  scheda:", passato)
    time.sleep(1.0)
    salva(b, "s09-telefono-proprieta.png", larghezza_finale=780)

    # Si esce con Annulla: nessuna modifica deve finire nell'archivio vero.
    #
    # Si esce PRIMA di tornare alla misura del desktop, e l'ordine non e' indifferente.
    # Cambiare le metriche di emulazione fa ridisegnare il componente, e nell'attraversare
    # quella transizione la ricerca del pulsante restituiva None: l'uscita non avveniva, e
    # restava aperto un editor con delle modifiche dentro — esattamente la cosa che queste
    # righe devono impedire. Alla misura del telefono il pulsante c'e' gia', e non serve
    # aspettare nient'altro.
    # Il pulsante puo' chiamarsi in due modi, e dipende da chi sta guardando: chi ha fatto
    # l'accesso ha «Annulla» e «Chiudi / Conferma», chi non l'ha fatto ha un solo «Chiudi»,
    # perche' non c'e' niente da confermare (§3.1 del manuale). Lo script apre l'applicazione
    # da anonimo, quindi quasi sempre trova il secondo — ma cercare solo quello si romperebbe
    # il giorno in cui la cattura venisse fatta da una sessione autenticata. Si accettano
    # entrambi, nelle due lingue, e si esclude «Chiudi / Conferma» che salverebbe.
    uscita = b.valuta("""(function(){
        var b = document.querySelectorAll('button');
        for (var i = 0; i < b.length; i++) {
            var t = b[i].textContent.trim();
            if (/conferma|confirm/i.test(t)) continue;
            if (/^(annulla|cancel|chiudi|close)$/i.test(t)) {
                b[i].click(); return t;
            }
        }
        return null;
    })()""")
    print("  uscita con:", uscita)

    if uscita is None:
        raise RuntimeError("nessun pulsante di uscita: l'editor resterebbe aperto")

    # E si verifica che sia servita. Un'uscita fallita in silenzio e' peggio di un errore:
    # lascerebbe credere che l'archivio non sia stato toccato.
    b.attendi("document.querySelector('.pe-editor') === null", secondi=30)
    print("  editor chiuso, archivio intatto")

    b.misura(1440, 980, 2)
finally:
    b.chiudi()
print("fatto")
