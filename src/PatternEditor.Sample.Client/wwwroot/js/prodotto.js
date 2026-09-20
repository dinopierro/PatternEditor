// Il movimento della pagina di prodotto.
//
// Una regola sola, e spiega tutte le scelte qui sotto: JavaScript non anima niente. Si
// limita a misurare due cose che i CSS non sanno leggere — quanto una sezione è avanzata
// nello scorrimento, e se un elemento è entrato in vista — e a scriverle in variabili CSS.
// Che farne lo decide il foglio di stile. Il risultato è che il movimento resta fluido
// anche quando il thread principale è occupato, e che togliendo questo file la pagina
// resta leggibile e corretta: perde le animazioni, non i contenuti.

/** Chi ha chiesto meno animazioni al sistema le vede sparire tutte. */
/* Esportata perché non la usa solo questo modulo: la pagina deve sapere se può far
   alternare i pattern in scena da sola, e la preferenza sta nel browser, non in C#. */
export function movimentoRidotto() {
    return window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function fraZeroEUno(valore) {
    return Math.min(1, Math.max(0, valore));
}

/**
 * Avvia gli effetti e restituisce un oggetto con cui spegnerli.
 *
 * Il ritorno non è un vezzo: una pagina singola non ricarica il documento quando si cambia
 * schermata, e un ascoltatore di scorrimento dimenticato continuerebbe a misurare elementi
 * che non esistono più, a ogni pixel, per tutta la durata della sessione.
 */
export function avvia() {
    const ridotto = movimentoRidotto();

    // ------------------------------------------------------------------- larghezza ----
    //
    // Quanto è larga davvero l'area di lettura. Serve alle sezioni che si prendono tutta
    // la finestra: `100vw` comprende la barra di scorrimento verticale, e una sezione
    // larga così ne provoca una orizzontale — la pagina si può spostare di lato di una
    // quindicina di pixel, e su un sito che dovrebbe sembrare curato si nota subito.
    //
    // Si misura anche l'altezza della barra in cima. Serve ai pannelli agganciati: fermarsi
    // a `top: 0` significherebbe fermarsi SOTTO la barra, che è anch'essa agganciata e resta
    // sempre a schermo. Il pannello scorrerebbe quindi per l'altezza della barra prima di
    // bloccarsi -- un primo scatto all'inizio dello scorrimento -- e per tutta la sezione
    // resterebbe alto quanto la finestra intera, cioè col fondo tagliato fuori vista.
    //
    // La barra si misura invece di scriverla a mano perché cambia: sul telefono va su due
    // righe.
    let barra = 0;

    const misuraGuscio = () => {
        const radice = document.documentElement;
        radice.style.setProperty("--pro-larghezza", radice.clientWidth + "px");

        const intestazione = document.querySelector(".app-shell > header");
        barra = intestazione ? Math.round(intestazione.getBoundingClientRect().height) : 0;
        radice.style.setProperty("--pro-barra", barra + "px");
    };

    misuraGuscio();

    // La barra non ha la sua altezza definitiva al primo disegno: l'angolo dell'utente parte
    // con un segnaposto e diventa un avatar quando la sessione viene ripresa, e la riga
    // cresce di qualche pixel. Misurata una volta sola resterebbe sbagliata fino al primo
    // scorrimento, e correggerla allora si vedrebbe -- l'altezza della barra decide dove si
    // ferma il pannello agganciato.
    const barraOsservata = "ResizeObserver" in window
        ? new ResizeObserver(misuraGuscio)
        : null;

    if (barraOsservata) {
        const intestazione = document.querySelector(".app-shell > header");

        if (intestazione) {
            barraOsservata.observe(intestazione);
        }
    }

    // ------------------------------------------------------------------ avanzamento ----
    //
    // Per ogni sezione "agganciata" si calcola quanto si è percorsa, da 0 a 1, e lo si
    // scrive in --avanzamento. La misura è presa dal rettangolo rispetto alla finestra e
    // non dalla posizione di scorrimento: così funziona identica sia che a scorrere sia la
    // finestra, sia un contenitore interno, senza che questo file debba sapere quale dei due.
    const agganciate = Array.from(document.querySelectorAll("[data-avanzamento]"));

    let inAttesa = false;

    const misura = () => {
        inAttesa = false;
        misuraGuscio();

        for (const sezione of agganciate) {
            const r = sezione.getBoundingClientRect();
            // Il pannello è già fermo quando la sezione ha il bordo alto appoggiato sotto la
            // barra: è da lì che l'avanzamento deve valere zero, non da `r.top === 0`. Con lo
            // zero più in basso i primi pixel di scorrimento non muoverebbero niente, e il
            // movimento comincerebbe di scatto qualche decina di pixel dopo.
            const corsa = r.height - window.innerHeight + barra;

            const avanzamento = corsa > 0 ? fraZeroEUno((barra - r.top) / corsa) : 0;
            sezione.style.setProperty("--avanzamento", avanzamento.toFixed(4));

            // Il nastro orizzontale ha bisogno di una misura che i CSS non possono avere:
            // quanto sporge oltre il bordo dello schermo. Senza, scorrerebbe troppo o
            // troppo poco a seconda della larghezza della finestra.
            const nastro = sezione.querySelector("[data-nastro]");
            if (nastro) {
                const eccedenza = Math.max(0, nastro.scrollWidth - nastro.parentElement.clientWidth);
                nastro.style.setProperty("--eccedenza", eccedenza + "px");
            }
        }
    };

    const suScorrimento = () => {
        // Una misura per fotogramma e non una per evento: lo scorrimento ne genera
        // centinaia al secondo, e leggere il rettangolo di un elemento costa un calcolo
        // di impaginazione ogni volta.
        if (!inAttesa) {
            inAttesa = true;
            requestAnimationFrame(misura);
        }
    };

    if (!ridotto && agganciate.length > 0) {
        misura();
        window.addEventListener("scroll", suScorrimento, { passive: true });
        window.addEventListener("resize", suScorrimento, { passive: true });
    } else {
        // Anche senza effetti la larghezza va tenuta aggiornata: è impaginazione, non
        // movimento, e chi ha chiesto meno animazioni non ha chiesto una pagina storta.
        window.addEventListener("resize", misuraGuscio, { passive: true });
    }

    // ---------------------------------------------------------------------- comparse ---
    //
    // Chi entra in vista riceve una classe e compare. L'osservatore fa da sé il lavoro che
    // altrimenti andrebbe rifatto a ogni scorrimento, ed è il browser a deciderne il momento.
    const senzaOsservatore = ridotto || !("IntersectionObserver" in window);

    const osservatore = senzaOsservatore ? null : new IntersectionObserver((voci) => {
        for (const voce of voci) {
            if (!voce.isIntersecting) {
                continue;
            }

            voce.target.classList.add("e-visibile");

            // Una volta comparso resta comparso: rifare l'ingresso a ogni passaggio
            // trasformerebbe una rifinitura in un tic nervoso.
            osservatore.unobserve(voce.target);
        }
    }, { rootMargin: "0px 0px -12% 0px", threshold: 0.12 });

    // Si guarda chi non è ancora comparso, e si può richiamare.
    //
    // Serve perché una parte della pagina nasce dopo: l'archivio arriva dalla rete, e le
    // sue schede cambiano a ogni ricerca. Un osservatore registrato una volta sola al primo
    // disegno non le vedrebbe mai, e resterebbero invisibili per sempre — trasparenti, non
    // assenti, che è il modo peggiore di sparire.
    const rileggi = () => {
        const daRivelare = document.querySelectorAll("[data-rivela]:not(.e-visibile)");

        if (senzaOsservatore) {
            // Senza osservatore, o senza movimento, tutto è già visibile: la pagina non
            // deve dipendere da un effetto per mostrare il proprio contenuto.
            for (const el of daRivelare) {
                el.classList.add("e-visibile");
            }
            return;
        }

        for (const el of daRivelare) {
            osservatore.observe(el);
        }
    };

    rileggi();

    return {
        rileggi,

        dismetti() {
            window.removeEventListener("scroll", suScorrimento);
            window.removeEventListener("resize", suScorrimento);
            window.removeEventListener("resize", misuraGuscio);

            document.documentElement.style.removeProperty("--pro-larghezza");
            document.documentElement.style.removeProperty("--pro-barra");

            if (osservatore) {
                osservatore.disconnect();
            }

            if (barraOsservata) {
                barraOsservata.disconnect();
            }
        },
    };
}

/**
 * Porta i contatori da zero al loro valore quando entrano in vista.
 *
 * Il valore finale è già scritto nel documento: se questo codice non venisse mai eseguito,
 * la pagina resterebbe corretta e nessuno se ne accorgerebbe.
 */
export function contatori() {
    const elementi = Array.from(document.querySelectorAll("[data-conta]"));
    if (elementi.length === 0) {
        return;
    }

    const porta = (el) => {
        const finale = Number(el.dataset.conta);
        if (!Number.isFinite(finale) || el.dataset.contato === "1") {
            return;
        }

        el.dataset.contato = "1";

        if (movimentoRidotto() || finale === 0) {
            el.textContent = String(finale);
            return;
        }

        const durata = 1100;
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
    };

    if (!("IntersectionObserver" in window)) {
        elementi.forEach(porta);
        return;
    }

    const osservatore = new IntersectionObserver((voci) => {
        for (const voce of voci) {
            if (voce.isIntersecting) {
                porta(voce.target);
                osservatore.unobserve(voce.target);
            }
        }
    }, { threshold: 0.5 });

    for (const el of elementi) {
        osservatore.observe(el);
    }
}

/** Porta la pagina in cima: serve quando si arriva qui da un'altra schermata. */
export function inCima() {
    window.scrollTo({ top: 0, behavior: "auto" });
}
