# PatternEditor

> La descrizione completa del sistema — obiettivi, architettura, contratto dei plugin,
> catalogo degli elementi, formato dei documenti e scelte progettuali — sta in
> [docs/Analisi funzionale - Pattern Editor SVG.md](docs/Analisi%20funzionale%20-%20Pattern%20Editor%20SVG.md).
> Questo README resta il riepilogo operativo per chi lavora sul codice.
>
> Il PDF impaginato si rigenera dal Markdown con `python docs/genera-pdf.py`
> (richiede Chrome o Edge e `pip install pypdf`).

Implementazione della specifica "Analisi di prova per elaborazione componente blazor con AI",
seguendo l'ordine di milestone definito nel documento stesso (capitolo 128) e i comandi
`#Agent-commands` in testa alla spec.

## Struttura della solution

```
docs/                                 Analisi funzionale e tecnica, con le figure
src/                                  Libreria e applicazioni di esempio
  PatternEditor.Core                  Modello di dominio (Pattern, PatternDefinition, VectorElement, ValidationResult)
  PatternEditor.Abstractions          Contratti dei plugin (IVectorElementPlugin, IVectorElementPluginRegistry) e PatternSerializer
  PatternEditor                       Libreria riutilizzabile: componente <PatternEditor>, servizi, macchina a stati, DI
  PatternEditor.Sample.Api            Minimal API di persistenza (progetto di riferimento/test)
  PatternEditor.Sample.Client         Blazor WebAssembly di esempio (pagina di gestione pattern)

plugins/                              Un progetto per tipo di elemento vettoriale
  PatternEditor.Element.Line          Modello + validazione + rendering + editor Blazor
  PatternEditor.Element.Rect
  PatternEditor.Element.Circle
  PatternEditor.Element.Ellipse
  PatternEditor.Element.Path
  PatternEditor.Element.Polygon
  PatternEditor.Element.Polyline
  PatternEditor.Element.Text
  PatternEditor.Element.Image

tests/                                Un progetto di test per ogni progetto di src/ e plugins/
  PatternEditor.Core.Tests
  PatternEditor.Abstractions.Tests
  PatternEditor.Tests
  PatternEditor.Sample.Api.Tests
  PatternEditor.Element.Line.Tests
  PatternEditor.Element.Rect.Tests
  PatternEditor.Element.Circle.Tests
  PatternEditor.Element.Ellipse.Tests
  PatternEditor.Element.Path.Tests
  PatternEditor.Element.Polygon.Tests
  PatternEditor.Element.Polyline.Tests
  PatternEditor.Element.Text.Tests
  PatternEditor.Element.Image.Tests
```

I plugin stanno in una cartella propria perche' sono il punto di estensione del sistema, non
parte della libreria: `plugins/` dipende da `src/PatternEditor.Abstractions`, mai il contrario.
Le cartelle della soluzione in Visual Studio rispecchiano questa struttura.

## Cosa è implementato

- **M1 — Domain**: `Pattern`, `PatternDefinition`, `VectorElement`, `UnknownVectorElement`
  (preserva gli elementi il cui plugin non è disponibile, senza perdita di dati),
  `ValidationResult`.
- **M2 — Plugin infrastructure**: `IVectorElementPlugin`, `IVectorElementPluginRegistry` +
  implementazione. Il componente principale non conosce mai i tipi concreti degli elementi.
- **M3/M6 — Nove plugin di elemento** (Line, Rect, Circle, Ellipse, Path, Polygon,
  Polyline, Text, Image): modello fortemente tipizzato,
  `Create`/`Validate`/`Render`, editor Blazor specifico per ciascun tipo.
- **M4 — Rendering SVG**: `PatternSvgRenderer`, con lo stesso rendering usato da preview
  ingrandita, preview ripetuta e SVG scaricato (nessuna logica grafica duplicata).
  `patternTransform` calcolato da Scale/Rotation/TranslateX/TranslateY.
- **M5 — Componente `PatternEditor`**: API pubblica minima (`Pattern`, `OnConfirm`,
  `OnCancel`), editing transazionale su copia profonda (deep clone via round-trip di
  serializzazione), macchina a stati esplicita e testabile (`PatternEditorStateMachine`),
  toolbar con download SVG (JS interop), pannello proprietà, doppia anteprima, pannello
  elementi con riordino ↑/↓ e editor dinamico per plugin (`DynamicComponent`).
- **M7 — Persistenza**: `PatternSerializer` (deserializzazione polimorfica basata sul
  registry, con preservazione degli elementi sconosciuti) e, lato applicazione di test,
  `IPatternRepository`/`JsonFilePatternRepository` (un file JSON per pattern, nominato con
  l'UUIDv7, operazioni isolate per singolo file).
- **M8 — Sample application**: `PatternEditor.Sample.Api` (minimal API con gli endpoint
  `GET/POST/PUT/DELETE /api/patterns[...]` indicati dalla spec) e
  `PatternEditor.Sample.Client` (pagina con elenco/checkbox/anteprime/eliminazione multipla
  e apertura del `PatternEditor` in una modal per creazione/modifica).

Tutti i progetti di libreria/servizio hanno test xUnit associati (dominio, plugin,
registry+serializzatore, macchina a stati, validatore, renderer SVG, repository).

## Correzioni successive alla prima verifica sul campo

- **Anteprima che non seguiva Scale/Rotate/Translate.** Il renderer emetteva sempre
  `<pattern id="p">`. Con l'editor aperto sopra l'elenco, nella stessa pagina esistevano due
  nodi con lo stesso id e `fill="url(#p)"` viene risolto sull'intero documento HTML (non
  all'interno del singolo `<svg>`): entrambe le anteprime mostravano quindi la prima
  definizione incontrata, cioe' quella - immutabile - della riga di elenco. Il modello e il
  markup generato erano corretti fin dall'inizio, era la risoluzione del riferimento a essere
  sbagliata. Ora l'id del nodo `<pattern>` e' univoco: derivato dall'Id del Pattern per le
  anteprime incorporate, scelto dal componente (uno per istanza) per l'anteprima dell'editor,
  lasciato a `p` solo nell'SVG scaricato, che e' un documento a se stante. Lo stesso difetto
  faceva si' che, con piu' pattern in elenco, tutte le righe mostrassero il primo.
- **Tendina "Nuovo elemento" da riportare ogni volta sul placeholder.** L'inserimento
  avveniva sull'evento `change` della `<select>`, che non si ripete se si riseleziona la voce
  gia' selezionata. Ora la tendina sceglie soltanto il tipo (e mantiene la scelta) mentre
  l'inserimento avviene con il pulsante **Aggiungi**: piu' elementi dello stesso tipo si
  aggiungono con clic ripetuti. Aggiunto anche **Duplica** su ogni elemento
  (`IVectorElementCloner`, clone profondo con nuovo Id via round-trip di serializzazione,
  quindi indipendente dai tipi concreti dei plugin).
- **Numeri formattati con la cultura del browser.** In Blazor WebAssembly la cultura corrente
  e' quella del browser: con it-IT `value="@x"` produceva "1,5", che un `<input type="number">`
  considera non valido (campo vuoto). Formattazione e parsing passano ora da
  `PatternEditor.Core.Formatting.InvariantNumber`.
- **Rifinitura grafica** di libreria e applicazione di esempio: layout a tre colonne, pannelli,
  cursori per scala/rotazione/traslazioni con valore a fianco, sfondo a scacchiera nelle
  anteprime (per vedere le trasparenze), riquadro "SVG generato", tema chiaro/scuro
  automatico via `prefers-color-scheme` e variabili CSS ridefinibili dall'host.

## Seconda passata di rifinitura

- **Allineamento dei campi affiancati.** La regola `.pe-field + .pe-field` aggiungeva un
  margine superiore anche dentro la griglia a due colonne, facendo scendere il secondo campo
  (Larghezza/Altezza, X/Y disallineati). Ora il margine vale solo per i campi impilati.
- **Scala in percentuale.** L'editor mostra 100 al posto di 1; il modello e l'attributo SVG
  continuano a usare il fattore moltiplicativo, la conversione resta dentro
  `PatternPropertiesEditor`. Ogni etichetta porta l'unita' di misura: `[%]`, `[°]`, `[px]`.
- **Rimossa la ridondanza** fra l'etichetta e il campo numerico nella sezione Trasformazione:
  il valore corrente e' mostrato una volta sola. Resta accanto ai soli cursori che non hanno
  un campo numerico (le opacita').
- **Testi tutti in italiano**, comprese le etichette dei campi e i messaggi di validazione,
  che ora usano la stessa terminologia della UI ("La larghezza deve essere maggiore di zero"
  invece di "Width deve essere...").
- **Riquadro "SVG generato" leggibile senza scorrimento orizzontale**: `SvgSourceFormatter`
  rientra i tag e mette ogni attributo su una riga a se'. Riguarda la sola visualizzazione;
  l'SVG scaricato e copiato resta quello prodotto dal renderer.
- **Cartella dei dati stabile.** `Storage:Directory` era un percorso relativo risolto sulla
  directory di lavoro del processo, diversa a seconda di come si avvia l'API: i pattern
  finivano in due cartelle distinte. Inoltre l'SDK trattava `App_Data` come contenuto del
  progetto, quindi la build ne ricopiava la versione "di progetto" sopra quella scritta a
  runtime e `Clean`/`Rebuild` la cancellava dall'output. Ora il percorso relativo si risolve
  sulla content root e `App_Data` e' escluso dagli item di Content.

## Elementi aggiunti dopo la prima consegna

Aggiunti seguendo esattamente la struttura di Line e Rect: un progetto per plugin, modello
fortemente tipizzato + `Create`/`Validate`/`Render` + editor Blazor dedicato, nessuna modifica
alla libreria o al componente `PatternEditor`. L'unico punto in cui compaiono e' la
registrazione nelle applicazioni host (`AddPatternEditorPlugin<CirclePlugin>()` e
`AddPatternEditorPlugin<PathPlugin>()` in API e Client), come previsto dall'architettura.

- **Circle** (`type: "circle"`, "Cerchio"): `cx`, `cy`, `r` piu' riempimento, bordo e opacita'
  con le stesse convenzioni di Rect. Il raggio deve essere maggiore di zero; centro e raggio
  non sono normalizzati rispetto alla cella, quindi il cerchio puo' sporgere di proposito.
- **Path** (`type: "path"`, "Tracciato"): la geometria e' l'attributo `d` della specifica SVG,
  conservato come testo senza reinterpretarlo (il Pattern Editor non e' un editor di curve e
  non deve riscrivere il tracciato dell'utente). La validazione controlla che non sia vuoto,
  che inizi con un comando di spostamento (`M`/`m`) e che contenga solo caratteri ammessi nei
  path data; l'editor offre una textarea monospaziata con il promemoria dei comandi.
  Estensioni naturali non implementate: `fill-rule`, `stroke-linecap`, `stroke-linejoin`.
- **Ellipse** (`type: "ellipse"`, "Ellisse"): come Circle ma con due semiassi indipendenti;
  un cerchio e' il caso particolare in cui coincidono. Restano due elementi distinti perche'
  tali sono nella specifica SVG, e perche' un cerchio con un raggio solo e' piu' comodo da
  modificare.
- **Polygon** (`type: "polygon"`, "Poligono") e **Polyline** (`type: "polyline"`, "Spezzata"):
  geometria data dall'elenco dei vertici (attributo `points`), conservato come testo senza
  normalizzarlo. Differiscono per la specifica SVG, e la differenza si riflette in modello,
  validazione e valori predefiniti: il poligono chiude il contorno fra ultimo e primo vertice,
  quindi racchiude una superficie, richiede almeno 3 vertici ed e' riempito di default; la
  spezzata resta aperta, bastano 2 vertici ed e' tracciata e non riempita. La lettura di
  `points` e' condivisa da `PatternEditor.Core.Formatting.SvgPoints`, perche' due plugin non
  possono e non devono conoscersi.
- **Text** (`type: "text"`, "Testo"): e' l'unico elemento in cui la parte principale non e'
  un attributo ma il **contenuto** del nodo, e questo ha due conseguenze. Il rendering non
  produce un tag autochiuso, e il testo va protetto con le regole del contenuto (`&`, `<`,
  `>`) e non con quelle di un attributo: dentro un nodo gli apici non chiudono nulla, e
  sostituirli renderebbe il sorgente illeggibile senza cambiare cio' che si vede. Da qui la
  distinzione fra `SvgText.Escape` (attributi) e `SvgText.EscapeContent` (contenuto).
  Proprieta': testo, posizione, allineamento rispetto a X, famiglia, dimensione e spessore
  del carattere, colore, contorno, opacita'. Il carattere **non viene incorporato** nell'SVG:
  indicarne uno non generico produce un avviso, perche' chi apre il documento potrebbe non
  averlo installato.
- **Image** (`type: "image"`, "Immagine"): riquadro `x`/`y`/`width`/`height`, `preserveAspectRatio`
  (contenuta / riempi e ritaglia / deforma) e opacita'. La sorgente e' un data URI oppure un
  indirizzo http/https; l'editor permette di scegliere un file locale, che viene letto e
  incorporato come data URI (limite 2 MB). Sono ammesse solo sorgenti che rappresentano
  davvero un'immagine: un `data:text/html`, un `javascript:` o un percorso locale vengono
  rifiutati dalla validazione. Un'immagine incorporata oltre 512 kB produce un avviso, non un
  errore: e' una scelta legittima, ma il pattern salvato pesa di conseguenza. Viene generato
  l'attributo `href` di SVG 2 (il vecchio `xlink:href` richiederebbe la dichiarazione del
  namespace sul nodo radice, che spetta al renderer e non al plugin).

Poiche' `d` e' testo libero che finisce dentro un attributo XML, il markup viene generato
passando i valori testuali per `PatternEditor.Core.Formatting.SvgText.Escape`. L'anteprima si
rigenera a ogni battuta anche mentre il tracciato non e' ancora valido, quindi il rendering
deve restare ben formato in ogni caso; per uniformita' l'escape e' usato anche dai colori di
Line, Rect e Circle.

## Icone e inserimento degli elementi

L'icona di ogni tipo di elemento arriva dal plugin, che e' l'unico a sapere cosa rappresenta:
`IVectorElementPlugin.IconSvg` restituisce il contenuto di un `<svg>` con viewBox "0 0 16 16",
disegnato con `stroke="currentColor"` cosi' da adattarsi a colore e dimensione decisi da chi
lo mostra. E' un **membro con implementazione predefinita**: un plugin gia' esistente che non
la ridefinisce continua a compilare e funzionare, mostrando un'icona generica. L'icona e' una
rifinitura dell'UI, non parte del contratto funzionale, e il contratto lo dice.

L'inserimento avviene con un solo pulsante **Aggiungi elemento** che apre il menu dei tipi
disponibili, ciascuno con la propria icona: scegliere il tipo *e'* l'inserimento, senza il
passaggio intermedio "seleziona il tipo, poi premi Aggiungi". Il menu si chiude con Esc o con
un clic fuori, gestito da una superficie trasparente sotto al menu invece che da un gestore
globale in JavaScript.

## Date di creazione e ultima modifica

Ogni pattern porta con se' `createdAt` e `modifiedAt`, scritte nel JSON in ISO 8601 UTC e
mostrate in orario locale nell'elenco e nella testata dell'editor.

Le due date sono gestite dal livello di persistenza, non dall'editor: aprire un pattern,
modificarlo e poi annullare non le tocca. Alla creazione coincidono; a ogni salvataggio
successivo si sposta solo `modifiedAt`. La data di creazione appartiene al documento gia'
salvato: se un client ne rimandasse indietro una diversa, il repository la ignora e mantiene
quella registrata.

**Recupero dai file precedenti.** I pattern salvati prima di questa modifica non hanno le due
date, ma hanno un Id UUIDv7, che per definizione (RFC 9562) inizia con il timestamp Unix in
millisecondi della propria generazione: `PatternEditor.Core.Models.Uuid7` lo estrae e il
serializzatore lo usa come `createdAt` quando il campo manca. `modifiedAt`, non ricavabile da
alcun dato esistente, viene fatta coincidere con la creazione. Le due date vengono poi scritte
nel file al primo salvataggio successivo, quindi la ricostruzione avviene una sola volta e non
richiede alcuna migrazione manuale. Se l'Id non fosse un UUIDv7 (possibile solo con un file
scritto a mano) si ricade sull'istante corrente.

## Errori e avvisi

La validazione distingue due livelli, e ora l'editor li mostra entrambi separatamente.
Gli **errori** impediscono la conferma (un tracciato vuoto, un raggio nullo, un'opacita' fuori
scala). Gli **avvisi** no: segnalano una scelta legittima con una conseguenza che si potrebbe
non aspettare, e finora venivano calcolati dai plugin ma scartati dall'interfaccia. I due casi
attuali sono il carattere non generico di Text e l'immagine incorporata oltre 512 kB di Image:
in entrambi il documento resta valido, ma qualcosa cambia fuori dall'applicazione — un font
che potrebbe mancare, un file che diventa pesante.

## Test di integrazione dell'API

`PatternApiEndpointsTests` avvia l'applicazione vera con `WebApplicationFactory`: le
richieste attraversano binding, serializzatore polimorfico e repository su filesystem.
Coprono elenco, lettura, creazione, conflitto su Id duplicato, aggiornamento, cancellazione
singola e multipla, ordinamento per UUIDv7, corpi malformati, e la sopravvivenza attraverso
HTTP sia dei tipi concreti degli elementi sia di un elemento privo di plugin.

`PatternApiFactory` redirige lo storage su una cartella temporanea per ogni istanza. E' il
punto piu' importante di quella classe, e un test lo verifica esplicitamente: senza
l'override i test scriverebbero e cancellerebbero nella cartella `App_Data` reale.

**Un difetto trovato subito da questi test.** Il controllo su `Guid.Empty` nell'endpoint POST
era codice irraggiungibile: `serializer.Deserialize` veniva chiamato una riga prima e il
costruttore di `Pattern` sollevava un'eccezione, che risaliva fino al gestore predefinito.
Il client riceveva **500 invece di 400**, e lo stesso valeva per qualunque corpo malformato
(con `id` assente addirittura una `NullReferenceException`, per via di un `root["id"]!`).
Nessun test unitario poteva accorgersene: il difetto stava nella composizione fra
serializzatore ed endpoint. Ora `PatternSerializer` segnala i difetti di formato con
`FormatException` e un messaggio leggibile, e `PatternRequestReader` li traduce in 400.

## Un vincolo dell'ambiente: niente `catch (JsonException)` nel progetto API

Su questa postazione l'antivirus aziendale (Bitdefender Endpoint Security Tools) blocca in
lettura l'assembly compilato di `PatternEditor.Sample.Api` quando il codice contiene un
`catch (JsonException)`. Il compilatore scrive il file, l'antivirus lo rende illeggibile e la
build fallisce con "Access denied" sul .dll in `obj\`, in qualunque cartella e con qualunque
nome di assembly. Verificato per bisezione: gli stessi tre catch senza `JsonException`
compilano, aggiungerlo blocca. Negli altri progetti la stessa clausola non da' problemi.

Non e' un vincolo che serva aggirare, perche' la forma corretta e' comunque un'altra: le
eccezioni di System.Text.Json non devono uscire da `PatternSerializer`, che e' l'unico
componente a sapere che il formato e' JSON. Il serializzatore converte ogni guasto di
parsing in `FormatException`, e chi lo usa intercetta solo quella.

## Cosa NON e' (ancora) coperto

- Elementi grafici oltre i nove implementati (per esempio `use`, `tspan`, i gradienti):
  l'architettura a plugin li supporta senza modifiche al componente principale, ma non
  sono implementati.
- Test automatici di rendering dei componenti Blazor (bUnit): la macchina a stati, il
  renderer, il validatore e il cloner sono coperti da test xUnit puri, mentre il
  comportamento dei componenti e' stato verificato manualmente sull'applicazione di esempio.

## Come compilare e testare

Requisito: .NET 10 SDK.

```bash
dotnet restore PatternEditor.sln
dotnet build PatternEditor.sln
dotnet test PatternEditor.sln
```

Per eseguire l'applicazione di esempio (due terminali):

```bash
dotnet run --project src/PatternEditor.Sample.Api
dotnet run --project src/PatternEditor.Sample.Client
```

L'API è configurata di default su `http://localhost:5080` (vedi
`src/PatternEditor.Sample.Api/Properties/launchSettings.json`); se cambi porta, aggiorna
`PatternApiBaseAddress` in `src/PatternEditor.Sample.Client/Program.cs` o passalo come
configurazione.

Verificato su .NET 10.0.401: `dotnet build` senza avvisi ed errori, `dotnet test` con 215 test
superati; flusso elenco -> editor -> anteprime -> annulla provato sull'applicazione di esempio.

## Manutenzione dalla riga di comando

L'API riconosce due comandi, passati dopo `--`: li esegue e termina senza mettersi in ascolto.
Non esiste un endpoint corrispondente per nessuno dei due, ed e' voluto.

```bash
# concede a un utente gia' registrato il permesso di approvare le pubblicazioni
dotnet run --project src/PatternEditor.Sample.Api -- amministratore <nome utente>

# lo toglie
dotnet run --project src/PatternEditor.Sample.Api -- amministratore <nome utente> revoca

# assegna un autore ai pattern che non ne hanno (archivi nati prima degli account)
dotnet run --project src/PatternEditor.Sample.Api -- assegna-autore <nome utente>
```

**Il primo amministratore si nomina per forza cosi'**: un permesso ottenibile via rete e' un
permesso che prima o poi qualcuno si prende, e comunque non ci sarebbe nessuno che possa
concederlo. L'utente va **registrato prima** dall'applicazione: il comando concede un
permesso, non crea account.

Non serve riavviare l'API (l'account viene riletto dal disco a ogni richiesta) e chi ha la
pagina aperta vede la voce "Moderazione" ricaricandola.

Due avvertenze:

- **con l'API avviata** `dotnet run` fallisce con `MSB3027`, perche' il processo tiene bloccati
  i propri file: aggiungere `--no-build`, oppure fermare l'API prima;
- **i comandi usano le cartelle dati configurate** (`Storage:Directory`, `Auth:Directory`):
  lanciarli con una configurazione diversa da quella del servizio significa lavorare su un
  altro archivio.

Dettagli e casistica completa nell'appendice E dell'analisi funzionale.

Segnalami eventuali errori di compilazione o comportamenti inattesi: li correggo.
