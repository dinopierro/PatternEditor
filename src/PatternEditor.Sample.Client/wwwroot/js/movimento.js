// Comportamenti di movimento che richiedono di leggere la posizione del puntatore o degli
// elementi: cose che i CSS da soli non sanno fare. Tutto il resto - comparse, transizioni,
// trama viva - e' animato in CSS, che e' piu' fluido e non impegna il thread principale.
//
// Ogni funzione e' idempotente e silenziosa: se qualcosa non c'e', non fa nulla. Il
// movimento e' una rifinitura, non deve mai rompere la pagina.

/** Rispetta la scelta di chi ha chiesto meno animazioni al sistema operativo. */
function movimentoRidotto() {
    return window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

/**
 * Riflettore che segue il puntatore sulla vetrina. Le coordinate finiscono in due variabili
 * CSS: e' il foglio di stile a decidere che farne, non questo file.
 */
export function riflettore() {
    const vetrina = document.querySelector(".vetrina");
    if (!vetrina || vetrina.dataset.riflettore === "1" || movimentoRidotto()) {
        return;
    }

    vetrina.dataset.riflettore = "1";

    vetrina.addEventListener("pointermove", (e) => {
        const r = vetrina.getBoundingClientRect();
        vetrina.style.setProperty("--px", ((e.clientX - r.left) / r.width * 100) + "%");
        vetrina.style.setProperty("--py", ((e.clientY - r.top) / r.height * 100) + "%");
    });

    vetrina.addEventListener("pointerleave", () => {
        vetrina.style.removeProperty("--px");
        vetrina.style.removeProperty("--py");
    });
}

/**
 * Fa nascere la finestra dell'editor dal punto in cui si e' cliccato.
 *
 * Il listener e' in fase di cattura, quindi scrive le coordinate prima che Blazor gestisca
 * il clic e disegni la modal: quando questa compare, l'origine della sua trasformazione e'
 * gia' quella giusta. Senza, la finestra crescerebbe sempre dal centro dello schermo e il
 * legame con la scheda toccata andrebbe perso.
 */
export function origineFinestra() {
    if (document.body.dataset.origineFinestra === "1") {
        return;
    }

    document.body.dataset.origineFinestra = "1";

    document.addEventListener("click", (e) => {
        const sorgente = e.target instanceof Element
            ? e.target.closest(".scheda__tela, .app-btn--primary")
            : null;

        const radice = document.documentElement;
        if (!sorgente) {
            radice.style.removeProperty("--origine-x");
            radice.style.removeProperty("--origine-y");
            return;
        }

        const r = sorgente.getBoundingClientRect();
        radice.style.setProperty("--origine-x", (r.left + r.width / 2) + "px");
        radice.style.setProperty("--origine-y", (r.top + r.height / 2) + "px");
    }, true);
}

/**
 * Porta i contatori da zero al loro valore. Il valore finale e' gia' nel testo: se questo
 * codice non venisse mai eseguito, la pagina resterebbe corretta.
 */
export function contatori() {
    const elementi = document.querySelectorAll("[data-conteggio]");

    for (const el of elementi) {
        const finale = Number(el.dataset.conteggio);
        if (!Number.isFinite(finale) || el.dataset.animato === "1") {
            continue;
        }

        el.dataset.animato = "1";

        if (movimentoRidotto() || finale === 0) {
            el.textContent = String(finale);
            continue;
        }

        const durata = 700;
        const avvio = performance.now();

        const passo = (ora) => {
            const t = Math.min(1, (ora - avvio) / durata);
            // Decelerazione: parte veloce e si posa, invece di arrivare di colpo.
            const morbido = 1 - Math.pow(1 - t, 3);
            el.textContent = String(Math.round(finale * morbido));

            if (t < 1) {
                requestAnimationFrame(passo);
            }
        };

        requestAnimationFrame(passo);
    }
}
