# Come si rifanno le figure e i PDF

Le figure della documentazione non sono disegni fatti a mano una volta sola: si rigenerano.
Quelle che mostrano l'interfaccia sono **fotografie dell'applicazione in funzione**, e vanno
rifatte quando l'interfaccia cambia; i diagrammi sono SVG prodotti da uno script, e vanno
rifatti quando cambia ciò che descrivono.

Serve Python 3 con **Pillow** (`pip install pillow`) e **pypdf** (`pip install pypdf`), più
Chrome o Edge installato. Nient'altro: il dialogo con il browser è scritto qui dentro.

## 1. Le schermate vere

Prima si avvia l'applicazione. È **una sola**: l'host serve sia le risposte sia le pagine.

```
dotnet run --project src/PatternEditor.Sample.Api --urls http://localhost:5080
```

```
python docs/strumenti/cattura-schermate.py http://localhost:5080/gestione "Briks"
```

Il terzo argomento, facoltativo, e' la **lingua**: `en` scrive la scelta in `localStorage`
prima di aprire l'applicazione, e le schermate finiscono in `docs/immagini/scatti/en`. Senza
argomento si resta in italiano, e le schermate restano dove sono sempre state.

Il secondo argomento e' il **nome del pattern** da aprire nell'editor, e non deve piu' stare
nella prima pagina: lo script lo cerca con la casella di ricerca, come farebbe una persona.
Prima lo pescava dalla prima pagina, e bastava che qualcuno ne salvasse dodici perche' la
cattura aprisse un pattern qualsiasi — e' successo.

> **Le voci si cercano per testo, e il testo e' tradotto.** Ogni espressione regolare dello
> script conosce percio' tutt'e due le lingue: cercando le sole parole italiane la serie
> inglese si fermava a meta'. Lo stesso vale per il pulsante di uscita, che per chi non ha
> fatto l'accesso non e' «Annulla» ma un solo «Chiudi».

## 2. Le figure del manuale

```
python docs/strumenti/genera-figure.py
python docs/strumenti/genera-figure.py en
```

La seconda riga produce le figure inglesi in `docs/immagini/manuale/en`, leggendo le schermate
da `scatti/en` e le frasi dal dizionario `testi_figure.py` (`TESTI`, italiano -> inglese). Le
frasi che il dizionario non copre vengono elencate alla fine invece di passare in silenzio.

Due figure hanno uno script proprio, perche' devono prima **fabbricare** quello che mostrano —
un'immagine da convertire e un disegno da importare — e lo disegnano su una tela del browser
invece di allegare un file:

```
python docs/strumenti/cattura-conversione.py http://localhost:5080/ [it|en]
python docs/strumenti/cattura-importazione.py http://localhost:5080/ [it|en]
```

Le figure 3-8 non sono fotografie ma disegni, e non le produce nessuno script: esistono come
file, scritte una volta. Per l'inglese non si ridisegnano, si **traduce il testo dentro**:

```
python docs/strumenti/traduci-figure.py en
```

Prende le schermate del passo 1, le ritaglia e ci disegna sopra i richiami numerati e la
legenda. Le figure escono in `docs/immagini/manuale`.

Le fotografie sono **incorporate** dentro l'SVG, in base64. Non è uno spreco: il documento
finale carica le figure con un tag `<img>`, e un SVG dentro un `<img>` non ha il permesso di
andare a prendere file esterni — una figura che rimandasse al PNG accanto uscirebbe vuota.

## 3. I diagrammi dell'analisi

```
python docs/strumenti/genera-diagrammi.py
```

Non dipendono dall'applicazione in esecuzione: sono disegni, e descrivono l'architettura.
Escono in `docs/immagini`. Lo strumento di disegno è `tela.py`.

## 4. I PDF

```
python docs/genera-pdf.py "Analisi funzionale - Pattern Editor SVG"
python docs/genera-pdf.py "Manuale utente - Pattern Editor SVG"
python docs/genera-pdf.py "User manual - Pattern Editor SVG (English)"
```

La lingua si deduce dal **nome del documento**: quelli che finiscono per `(English)` prendono
l'indice «Contents» e il piede «Page N of M». Un documento e il suo nome viaggiano insieme, un
argomento in piu' si dimentica.

I due manuali vanno poi copiati dove l'applicazione li serve, perche' il pulsante **Manuale**
nella barra in alto li scarica da li':

```
cp "docs/Manuale utente - Pattern Editor SVG.pdf" src/PatternEditor.Sample.Client/wwwroot/manuale/
cp "docs/User manual - Pattern Editor SVG (English).pdf" "src/PatternEditor.Sample.Client/wwwroot/manuale/User manual - Pattern Editor SVG.pdf"
```

## I file

| File | Che cos'è |
|---|---|
| `chrome.py` | Il dialogo con Chrome: un client WebSocket e i comandi del protocollo di debug |
| `cattura-schermate.py` | Pilota l'applicazione e scatta le schermate |
| `genera-figure.py` | Le figure del manuale, costruite sopra le schermate |
| `tela.py` | Gli attrezzi di disegno: riquadri, frecce, diagrammi di sequenza |
| `genera-diagrammi.py` | I diagrammi dell'analisi |
| `cattura-conversione.py` | La schermata della conversione da immagine, con il disegno fabbricato sul posto |
| `cattura-importazione.py` | Le due schermate dell'importazione di un SVG, con il disegno fabbricato sul posto |
| `traduci-figure.py` | Le figure disegnate (3-8), con il testo sostituito nella lingua richiesta |
| `testi_figure.py` | Le frasi delle figure tradotte, per la serie inglese |

## Perché non `chrome --screenshot`

Quell'opzione scatta al termine del caricamento della pagina, e un'applicazione WebAssembly
comincia a esistere dopo: si ottiene una fotografia della scritta «Caricamento…». L'opzione
`--timeout`, che nel vecchio headless permetteva di aspettare, nel nuovo non esiste più.
Serve quindi parlare con il browser mentre lavora — navigare, **aspettare che una condizione
sia vera**, premere qualcosa, e solo allora scattare — ed è quello che fa `chrome.py`.
