export function downloadTextFile(fileName, content, mimeType) {
    const blob = new Blob([content], { type: mimeType || "image/svg+xml" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
}

/**
 * Centri verticali delle righe di un elenco, in coordinate della finestra.
 *
 * È l'unica cosa che il trascinamento non può calcolare da sé: dove cade ogni riga lo sa
 * solo il browser. Viene chiamata una volta all'inizio del trascinamento, e da lì in poi
 * il resto è aritmetica. I centri restano validi anche mentre le righe si riordinano,
 * perché descrivono le POSIZIONI dell'elenco e non gli elementi che le occupano.
 */
export function centriDelleRighe(elenco) {
    if (!elenco) {
        return [];
    }

    return Array.from(elenco.children, (riga) => {
        const r = riga.getBoundingClientRect();
        return r.top + r.height / 2;
    });
}

// Copia negli appunti. Restituisce false (senza sollevare eccezioni verso Blazor) quando
// l'API non è disponibile o l'utente ne ha negato l'uso: la UI si limita a non mostrare
// la conferma "Copiato".
export async function copyText(content) {
    try {
        await navigator.clipboard.writeText(content);
        return true;
    } catch {
        return false;
    }
}

/**
 * Ctrl+Z e Ctrl+Y sull'intero documento.
 *
 * L'ascolto sta qui e non su un elemento della pagina perché la scorciatoia deve valere
 * ovunque sia il fuoco dentro l'editor — anche sui cursori e sui selettori di colore, che
 * il fuoco se lo prendono volentieri.
 *
 * Nei campi di TESTO non interviene: lì Ctrl+Z è l'annullamento della scrittura, che il
 * browser sa fare meglio e che chi scrive si aspetta. Negli altri campi — numeri, cursori,
 * colori — non c'è nulla da annullare a livello di testo, e la scorciatoia è nostra.
 */
export function ascoltaScorciatoie(riferimento) {
    const scriveTesto = (el) => {
        if (!el) {
            return false;
        }
        if (el.isContentEditable || el.tagName === "TEXTAREA") {
            return true;
        }
        if (el.tagName !== "INPUT") {
            return false;
        }
        const tipo = (el.getAttribute("type") || "text").toLowerCase();
        return ["text", "search", "url", "email", "tel", "password"].includes(tipo);
    };

    const gestore = (e) => {
        if (!(e.ctrlKey || e.metaKey) || e.altKey) {
            return;
        }

        const tasto = (e.key || "").toLowerCase();
        if (tasto !== "z" && tasto !== "y") {
            return;
        }
        if (scriveTesto(document.activeElement)) {
            return;
        }

        // Ctrl+Y e Ctrl+Shift+Z sono la stessa richiesta: la prima è la tradizione di
        // Windows, la seconda quella del resto del mondo. Accettarle entrambe costa una riga.
        const ripeti = tasto === "y" || e.shiftKey;
        e.preventDefault();
        riferimento.invokeMethodAsync(ripeti ? "RipetiDaTastiera" : "AnnullaDaTastiera");
    };

    document.addEventListener("keydown", gestore);
    return {
        dismetti: () => document.removeEventListener("keydown", gestore),
    };
}
