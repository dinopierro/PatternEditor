// La lingua lato pagina: che cosa dice il browser, e che cosa ha scelto chi legge.
//
// Gli accessi a localStorage sono protetti come quelli del tema: in navigazione privata, o
// con i dati dei siti bloccati, la sola lettura puo' sollevare un'eccezione. La lingua e' una
// comodita' e non deve mai impedire il caricamento della pagina.

const CHIAVE = "pattern-editor-lingua";

/**
 * La lingua dichiarata dal browser, nella forma "it-IT" oppure "it".
 *
 * Si legge `languages[0]` prima di `language`: il primo e' l'elenco ordinato delle
 * preferenze, il secondo una sola lingua, e su un browser configurato con piu' lingue e'
 * l'elenco a dire quale viene prima.
 */
export function cultura() {
    const elenco = navigator.languages;

    if (Array.isArray(elenco) && elenco.length > 0) {
        return elenco[0];
    }

    return navigator.language || "";
}

/** La lingua scelta a mano in precedenza, oppure null se non e' mai stata scelta. */
export function leggi() {
    try {
        return localStorage.getItem(CHIAVE);
    } catch {
        return null;
    }
}

/** Ricorda la scelta. Fallire non e' grave: vale per questa visita e basta. */
export function ricorda(codice) {
    try {
        localStorage.setItem(CHIAVE, codice);
    } catch {
        /* preferenza non memorizzabile */
    }
}

/**
 * Dichiara la lingua al documento.
 *
 * Non e' un dettaglio formale: `lang` dice al browser come sillabare le parole a fine riga,
 * quali virgolette usare, e ai lettori di schermo con che pronuncia leggere. Una pagina che
 * dichiara una lingua e ne mostra un'altra si fa leggere male ad alta voce.
 */
export function dichiara(codice) {
    document.documentElement.setAttribute("lang", codice);
}
