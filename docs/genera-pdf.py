"""Genera il PDF dell'analisi funzionale a partire dal documento Markdown.

    python docs/genera-pdf.py

Converte il Markdown in un HTML impaginato per la stampa e lo passa a Chrome in modalita'
headless, che lo rende in PDF. Chrome mantiene fedelmente CSS, tabelle e figure SVG, che
restano vettoriali nel documento finale. L'HTML intermedio viene scritto accanto al Markdown,
perche' i percorsi delle figure sono relativi, e rimosso alla fine.

I numeri di pagina non sono ottenibili dalla riga di comando di Chrome, che non accetta un
piede personalizzato, ne' dai CSS paged media, che Chrome non implementa. Vengono quindi
prodotti come secondo documento -- una pagina di solo piede per ogni pagina del testo -- e
sovrapposti al primo. Evita di dover disegnare il PDF a mano con una libreria grafica.

Requisiti: Chrome o Edge, e pypdf per la sovrapposizione (pip install pypdf).
"""
import html
import io
import os
import re
import shutil
import subprocess
import sys

BASE = os.path.dirname(os.path.abspath(__file__))

# Il documento da convertire si puo' indicare come argomento; senza, si converte l'analisi.
# Il nome del file da' anche il testo del piede: i due documenti della cartella hanno lo
# stesso impaginato e non c'e' ragione di avere due script.
_NOME = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("-")     else "Analisi funzionale - Pattern Editor SVG"
_NOME = _NOME[:-3] if _NOME.lower().endswith(".md") else _NOME

SORGENTE = os.path.join(BASE, _NOME + ".md")

# La lingua si deduce dal nome del documento invece di chiederla: un documento e il suo nome
# viaggiano insieme, un argomento in piu' si dimentica.
LINGUA = "en" if _NOME.rstrip().endswith("(English)") else "it"

# Che documento e': l'analisi parla di comportamento atteso e scelte progettuali, un
# manuale parla a chi deve usare la cosa. Dirlo uguale a tutt'e due era un refuso che si
# leggeva in copertina — «documento di analisi» sopra il manuale utente.
ANALISI = "analisi" in _NOME.lower() or "analysis" in _NOME.lower()

VOCI = {
    "it": {
        "indice": "Indice",
        "pagina": "Pagina {0} di {1}",
        "revisioni": "Revisioni",
        "occhiello": ("Pattern Editor SVG &#183; documento di analisi" if ANALISI
                      else "Pattern Editor SVG &#183; manuale utente"),
        "chiusa": ("Il presente documento descrive il comportamento atteso del componente e le "
                   "scelte progettuali da adottare. Le figure sono parte integrante della "
                   "specifica." if ANALISI
                   else "Questo manuale descrive l'applicazione com'e' oggi. Le figure fanno "
                        "parte del testo e non sono decorazione: quello che mostrano e' "
                        "l'applicazione in funzione."),
    },
    "en": {
        "indice": "Contents",
        "pagina": "Page {0} of {1}",
        "revisioni": "Revision history",
        "occhiello": ("Pattern Editor SVG &#183; functional analysis" if ANALISI
                      else "Pattern Editor SVG &#183; user manual"),
        "chiusa": ("This document describes the expected behaviour of the component and the "
                   "design decisions to adopt. The figures are part of the specification." if ANALISI
                   else "This manual describes the application as it is today. The figures are "
                        "part of the text and not decoration: what they show is the application "
                        "running."),
    },
}[LINGUA]
HTML_INTERMEDIO = os.path.join(BASE, _NOME + ".html")
PDF = os.path.join(BASE, _NOME + ".pdf")

BROWSER_POSSIBILI = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]

CSS = """
@page { size: A4; margin: 18mm 16mm 20mm 16mm; }

* { box-sizing: border-box; }

body {
  margin: 0;
  font-family: "Segoe UI", system-ui, -apple-system, sans-serif;
  font-size: 10.5pt;
  line-height: 1.55;
  color: #1b2430;
}

/* ---------- copertina ---------- */
.cover {
  height: 245mm;
  display: flex;
  flex-direction: column;
  justify-content: center;
  break-after: page;
}
.cover .occhiello {
  font-size: 9pt; letter-spacing: 2.5px; text-transform: uppercase;
  color: #3b6ef5; font-weight: 700; margin-bottom: 10mm;
}
.cover h1 {
  font-size: 30pt; line-height: 1.15; margin: 0 0 6mm 0;
  font-weight: 600; border: none; padding: 0;
}
.cover .filetto { width: 42mm; height: 3px; background: #3b6ef5; margin-bottom: 10mm; }
.cover table { width: 100%; max-width: 130mm; font-size: 10pt; }
.cover table td { border: none; padding: 2.2mm 0; }
.cover table tr { border-bottom: 1px solid #e3e6ea; }
.cover table td:first-child { color: #6b7684; width: 38mm; }
.cover .piede {
  margin-top: 14mm; font-size: 9pt; color: #6b7684;
  border-top: 1px solid #e3e6ea; padding-top: 4mm;
}

/* ---------- titoli ---------- */
h2 {
  break-before: page;
  font-size: 17pt; font-weight: 600; margin: 0 0 6mm 0; padding-bottom: 3mm;
  border-bottom: 2px solid #3b6ef5;
}
h3 { font-size: 12.5pt; font-weight: 600; margin: 8mm 0 3mm 0; break-after: avoid; }
h4 { font-size: 11pt; font-weight: 600; margin: 6mm 0 2mm 0; color: #2f3b4a; break-after: avoid; }
p { margin: 0 0 3.4mm 0; text-align: justify; hyphens: auto; }

/* ---------- tabelle ---------- */
table {
  width: 100%; border-collapse: collapse; margin: 4mm 0 6mm 0;
  font-size: 9.5pt; break-inside: avoid;
}
th {
  background: #eef2f7; text-align: left; font-weight: 600;
  padding: 2.2mm 2.5mm; border: 1px solid #d5dbe2; color: #1b2430;
}
td { padding: 2.2mm 2.5mm; border: 1px solid #e3e6ea; vertical-align: top; }
tbody tr:nth-child(even) td { background: #fafbfc; }

/* L'elenco delle revisioni e' lungo per definizione e cresce a ogni giro: deve poter
   spezzarsi fra le pagine, al contrario delle tabelle del testo che si leggono per intero.
   E la prima colonna tiene «Revisione 3.18» su una riga sola invece di spezzarlo in due. */
.revisioni table { break-inside: auto; }
.revisioni tr { break-inside: avoid; }
.revisioni td:first-child { width: 30mm; white-space: nowrap; }

/* ---------- codice ---------- */
pre {
  background: #f7f9fb; border: 1px solid #e3e6ea; border-left: 3px solid #3b6ef5;
  border-radius: 3px; padding: 3mm 4mm; margin: 4mm 0 5mm 0;
  font-family: Consolas, "Courier New", monospace; font-size: 8.8pt; line-height: 1.5;
  white-space: pre-wrap; word-break: break-word; break-inside: avoid;
}
code {
  font-family: Consolas, "Courier New", monospace; font-size: 0.92em;
  background: #f2f5f8; border: 1px solid #e6eaee; border-radius: 2px; padding: 0 1mm;
}
pre code { background: none; border: none; padding: 0; font-size: inherit; }

/* ---------- riquadri ---------- */
blockquote {
  margin: 4mm 0 5mm 0; padding: 3mm 4mm;
  background: #fdf8e8; border: 1px solid #e8d9a0; border-left: 3px solid #d0a215;
  border-radius: 3px; break-inside: avoid;
}
blockquote p { margin: 0 0 2mm 0; text-align: left; }
blockquote p:last-child { margin-bottom: 0; }

/* ---------- elenchi ---------- */
ul, ol { margin: 0 0 4mm 0; padding-left: 6mm; }
li { margin-bottom: 1.6mm; }

/* ---------- figure ---------- */
img { display: block; width: 100%; max-width: 168mm; margin: 5mm auto 6mm auto; }

hr { display: none; }
a { color: #1b2430; text-decoration: none; }

.toc ul { list-style: none; padding-left: 0; }
.toc li { margin-bottom: 2.4mm; font-size: 11pt; }
"""


def inline(testo):
    """Formattazione in linea: codice, immagini, collegamenti, grassetto, corsivo."""
    segnaposto = []

    def salva(m):
        segnaposto.append(m.group(1))
        return "\x00{0}\x00".format(len(segnaposto) - 1)

    testo = re.sub(r"`([^`]+)`", salva, testo)
    testo = html.escape(testo, quote=False)

    testo = re.sub(r"!\[([^\]]*)\]\(([^)]+)\)",
                   lambda m: '<img src="{0}" alt="{1}">'.format(m.group(2), m.group(1)), testo)
    testo = re.sub(r"\[([^\]]+)\]\(([^)]+)\)",
                   lambda m: '<a href="{0}">{1}</a>'.format(m.group(2), m.group(1)), testo)
    testo = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", testo)
    testo = re.sub(r"(?<![\w*])\*([^*]+)\*(?![\w*])", r"<em>\1</em>", testo)

    for i, c in enumerate(segnaposto):
        testo = testo.replace("\x00{0}\x00".format(i),
                              "<code>{0}</code>".format(html.escape(c, quote=False)))
    return testo


def converti(md):
    righe = md.split("\n")
    out = []
    i = 0
    lista_aperta = None

    def chiudi_lista():
        nonlocal lista_aperta
        if lista_aperta:
            out.append("</{0}>".format(lista_aperta))
            lista_aperta = None

    while i < len(righe):
        r = righe[i]

        if r.startswith("```"):
            chiudi_lista()
            i += 1
            corpo = []
            while i < len(righe) and not righe[i].startswith("```"):
                corpo.append(righe[i])
                i += 1
            i += 1
            out.append("<pre><code>{0}</code></pre>".format(html.escape("\n".join(corpo), quote=False)))
            continue

        if r.startswith("|") and i + 1 < len(righe) and re.match(r"^\|[\s:|-]+\|$", righe[i + 1].strip()):
            chiudi_lista()
            intestazione = [c.strip() for c in r.strip().strip("|").split("|")]
            i += 2
            corpo = []
            while i < len(righe) and righe[i].startswith("|"):
                corpo.append([c.strip() for c in righe[i].strip().strip("|").split("|")])
                i += 1
            out.append("<table>")
            if any(intestazione):
                out.append("<thead><tr>" + "".join("<th>{0}</th>".format(inline(c)) for c in intestazione) + "</tr></thead>")
            out.append("<tbody>")
            for riga in corpo:
                out.append("<tr>" + "".join("<td>{0}</td>".format(inline(c)) for c in riga) + "</tr>")
            out.append("</tbody></table>")
            continue

        if r.startswith(">"):
            chiudi_lista()
            corpo = []
            while i < len(righe) and righe[i].startswith(">"):
                corpo.append(righe[i].lstrip(">").strip())
                i += 1
            paragrafi = " ".join(corpo).split("  ")
            out.append("<blockquote>" + "".join("<p>{0}</p>".format(inline(p)) for p in paragrafi if p.strip()) + "</blockquote>")
            continue

        m = re.match(r"^(#{1,4})\s+(.*)$", r)
        if m:
            chiudi_lista()
            out.append("<h{0}>{1}</h{0}>".format(len(m.group(1)), inline(m.group(2))))
            i += 1
            continue

        m = re.match(r"^[-*]\s+(.*)$", r)
        if m:
            if lista_aperta != "ul":
                chiudi_lista()
                out.append("<ul>")
                lista_aperta = "ul"
            out.append("<li>{0}</li>".format(inline(m.group(1))))
            i += 1
            continue

        m = re.match(r"^\d+\.\s+(.*)$", r)
        if m:
            if lista_aperta != "ol":
                chiudi_lista()
                out.append("<ol>")
                lista_aperta = "ol"
            out.append("<li>{0}</li>".format(inline(m.group(1))))
            i += 1
            continue

        # Riga rientrata dentro una lista: e' la continuazione della voce precedente, non
        # un paragrafo nuovo. Senza questo caso una voce scritta su due righe chiude la
        # lista e la successiva riparte da 1.
        if lista_aperta and re.match(r"^\s+\S", r) and out and out[-1].endswith("</li>"):
            out[-1] = out[-1][:-len("</li>")] + " " + inline(r.strip()) + "</li>"
            i += 1
            continue

        if r.strip() == "---":
            chiudi_lista()
            out.append("<hr>")
            i += 1
            continue

        if not r.strip():
            chiudi_lista()
            i += 1
            continue

        chiudi_lista()
        corpo = [r]
        i += 1
        while i < len(righe) and righe[i].strip() and not re.match(r"^([-*]\s|\d+\.\s|#{1,4}\s|\||>|```|---$)", righe[i]):
            corpo.append(righe[i])
            i += 1
        out.append("<p>{0}</p>".format(inline(" ".join(corpo))))

    chiudi_lista()
    return "\n".join(out)


def genera_html():
    md = io.open(SORGENTE, encoding="utf-8").read()

    # La testata del documento diventa la copertina; il corpo parte dall'Indice.
    taglio = md.index("\n## " + VOCI["indice"])
    testata, corpo = md[:taglio], md[taglio:]

    titolo = re.search(r"^# (.+)$", testata, re.M).group(1)

    # La tabella di testa si divide in due, e non e' un vezzo di impaginazione.
    #
    # La copertina ha un'altezza fissa e centra il proprio contenuto: una tabella
    # piu' alta non la allarga, le trabocca da tutt'e due i lati. Con diciannove
    # righe di revisioni il titolo usciva dal bordo di sopra e la coda dell'elenco
    # finiva a pagina due SOPRA l'indice, sovrapposta. In copertina restano quindi
    # le righe che dicono che cos'e' il documento; le revisioni sono contenuto, e
    # vanno dove il contenuto puo' scorrere.
    righe_testa = [l for l in testata.split(chr(10)) if l.startswith('|')]
    intestazione, corpo_tabella = righe_testa[:2], righe_testa[2:]

    def e_revisione(riga):
        return re.match(r'\|\s*\*\*(Revisione|Revision)', riga) is not None

    identita = [r for r in corpo_tabella if not e_revisione(r)]
    revisioni = [r for r in corpo_tabella if e_revisione(r)]
    meta = converti(chr(10).join(intestazione + identita))

    copertina = """<section class="cover">
  <div class="occhiello">{2}</div>
  <h1>{0}</h1>
  <div class="filetto"></div>
  {1}
  <div class="piede">{3}</div>
</section>""".format(html.escape(titolo.replace("Pattern Editor SVG — ", "")), meta,
                     VOCI["occhiello"], VOCI["chiusa"])

    corpo_html = converti(corpo)
    corpo_html = corpo_html.replace("<h2>%s</h2>" % VOCI["indice"],
                                    '<h2>%s</h2><div class="toc">' % VOCI["indice"], 1)
    # L'elenco delle revisioni sta fra l'indice e il primo capitolo: e' la prima
    # cosa che guarda chi ha gia' letto il documento e vuole sapere cosa e' cambiato.
    storia = ('<h2>%s</h2><div class="revisioni">%s</div>' % (VOCI['revisioni'],
                                 converti(chr(10).join(intestazione + revisioni)))
              if revisioni else '')

    primo = re.search(r"^## (1\. .+)$", corpo, re.M)
    if primo:
        corpo_html = corpo_html.replace("<h2>%s</h2>" % primo.group(1),
                                        '</div>%s<h2>%s</h2>' % (storia, primo.group(1)), 1)

    documento = """<!doctype html>
<html lang="it"><head><meta charset="utf-8"><title>{0}</title><style>{1}</style></head>
<body>{2}{3}</body></html>""".format(html.escape(titolo), CSS, copertina, corpo_html)

    io.open(HTML_INTERMEDIO, "w", encoding="utf-8").write(documento)


def trova_browser():
    for p in BROWSER_POSSIBILI:
        if os.path.exists(p):
            return p
    trovato = shutil.which("chrome") or shutil.which("msedge")
    if trovato:
        return trovato
    raise SystemExit("Nessun browser basato su Chromium trovato: serve Chrome o Edge.")


# Il piede riprende il titolo del documento, con il nome del prodotto davanti.
PIEDE_SINISTRA = ("Pattern Editor SVG &#183; " +
                  _NOME.replace("Pattern Editor SVG", "").replace("(English)", "").strip(" -").strip())

CSS_PIEDE = """
@page { size: A4; margin: 0; }
body { margin: 0; font-family: "Segoe UI", system-ui, sans-serif; }
/* Ogni riquadro occupa esattamente una pagina: il piede si posiziona rispetto al bordo. */
.p { position: relative; width: 210mm; height: 297mm; break-after: page; }
.p:last-child { break-after: auto; }
.piede {
  position: absolute; left: 16mm; right: 16mm; bottom: 11mm;
  display: flex; justify-content: space-between; align-items: baseline;
  font-size: 8pt; color: #6b7684;
  border-top: 0.6pt solid #e3e6ea; padding-top: 2mm;
}
"""


def numera_pagine(browser):
    """Sovrappone al documento un piede con la numerazione. La copertina resta senza."""
    from pypdf import PdfReader, PdfWriter

    documento = PdfReader(PDF)
    totale = len(documento.pages)

    riquadri = []
    for n in range(1, totale + 1):
        piede = "" if n == 1 else (
            '<div class="piede"><span>{0}</span><span>{1}</span></div>'
            .format(PIEDE_SINISTRA, VOCI["pagina"].format(n, totale)))
        riquadri.append('<div class="p">{0}</div>'.format(piede))

    html_piede = os.path.join(BASE, "_piede.html")
    pdf_piede = os.path.join(BASE, "_piede.pdf")
    io.open(html_piede, "w", encoding="utf-8").write(
        '<!doctype html><html lang="it"><head><meta charset="utf-8"><style>{0}</style></head>'
        '<body>{1}</body></html>'.format(CSS_PIEDE, "".join(riquadri)))

    subprocess.run(
        [browser, "--headless=new", "--disable-gpu", "--no-pdf-header-footer",
         "--virtual-time-budget=5000", "--print-to-pdf=" + pdf_piede,
         "file:///" + html_piede.replace("\\", "/").replace(" ", "%20")],
        check=True)

    numerazione = PdfReader(pdf_piede)
    if len(numerazione.pages) != totale:
        raise SystemExit(
            "La numerazione ha prodotto {0} pagine invece di {1}: impaginazione non allineata."
            .format(len(numerazione.pages), totale))

    scrittore = PdfWriter()
    for pagina, piede in zip(documento.pages, numerazione.pages):
        pagina.merge_page(piede)
        aggiunta = scrittore.add_page(pagina)

        # La fusione riscrive il flusso di contenuto senza comprimerlo: senza questa
        # ricompressione il documento triplica di peso per una riga di testo.
        # Va fatta dopo add_page, perche' la pagina deve appartenere allo scrittore.
        aggiunta.compress_content_streams()

    scrittore.compress_identical_objects()

    with open(PDF, "wb") as f:
        scrittore.write(f)

    os.remove(html_piede)
    os.remove(pdf_piede)
    return totale


def main():
    genera_html()
    browser = trova_browser()

    url = "file:///" + HTML_INTERMEDIO.replace("\\", "/").replace(" ", "%20")
    subprocess.run(
        [browser, "--headless=new", "--disable-gpu", "--no-pdf-header-footer",
         "--virtual-time-budget=10000", "--print-to-pdf=" + PDF, url],
        check=True)

    os.remove(HTML_INTERMEDIO)
    pagine = numera_pagine(browser)

    print("PDF generato:", PDF)
    print("pagine:", pagine, "| dimensione:", round(os.path.getsize(PDF) / 1024), "kB")


if __name__ == "__main__":
    sys.exit(main())
