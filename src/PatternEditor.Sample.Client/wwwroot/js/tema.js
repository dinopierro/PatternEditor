// Gestione del tema lato pagina: l'attributo data-theme sull'elemento radice pilota le
// variabili CSS, e la scelta viene ricordata fra una visita e l'altra.
//
// Tutti gli accessi a localStorage sono protetti: in navigazione privata, o con i dati dei
// siti bloccati, la sola lettura puo' sollevare un'eccezione. Il tema e' una comodita', non
// deve mai impedire il caricamento della pagina.

const CHIAVE = "pattern-editor-tema";

/** Tema salvato in precedenza: "light", "dark" oppure null se non e' mai stato scelto. */
export function leggi() {
    try {
        const salvato = localStorage.getItem(CHIAVE);
        return salvato === "light" || salvato === "dark" ? salvato : null;
    } catch {
        return null;
    }
}

/** Applica il tema alla pagina. Un valore diverso da "light"/"dark" torna a seguire il sistema. */
export function applica(tema) {
    const radice = document.documentElement;

    if (tema === "light" || tema === "dark") {
        radice.setAttribute("data-theme", tema);
        try { localStorage.setItem(CHIAVE, tema); } catch { /* preferenza non memorizzabile */ }
        return;
    }

    radice.removeAttribute("data-theme");
    try { localStorage.removeItem(CHIAVE); } catch { /* preferenza non memorizzabile */ }
}

/** Preferenza del sistema operativo, per sapere cosa vedrebbe l'utente senza una scelta esplicita. */
export function sistemaPreferisceScuro() {
    return window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)").matches : false;
}
