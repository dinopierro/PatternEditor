// Decodifica di un'immagine in punti.
//
// Sta in JavaScript per una ragione sola: il browser sa già leggere PNG, JPEG, WebP, GIF e
// BMP, e portarsi dentro una libreria di immagini in WebAssembly vorrebbe dire scaricare
// qualche megabyte per usarne una funzione. Qui si apre l'immagine, la si disegna su una tela
// e si restituiscono i punti; tutto il ragionamento sta dall'altra parte, in C#.

let ultima = null;

/**
 * Apre l'immagine e ne tiene i punti da parte.
 * @param {string} indirizzo un data URI dell'immagine
 * @returns {Promise<{larghezza: number, altezza: number}>}
 */
export async function analizza(indirizzo) {
    const immagine = await new Promise((risolvi, rifiuta) => {
        const i = new Image();
        i.onload = () => risolvi(i);
        i.onerror = () => rifiuta(new Error("immagine illeggibile"));
        i.src = indirizzo;
    });

    const larghezza = immagine.naturalWidth;
    const altezza = immagine.naturalHeight;

    const tela = document.createElement("canvas");
    tela.width = larghezza;
    tela.height = altezza;

    // willReadFrequently: si legge tutta la tela una volta sola, e senza questo avviso il
    // browser la tiene sulla scheda grafica — da dove rileggerla costa molto più che
    // disegnarla.
    const contesto = tela.getContext("2d", { willReadFrequently: true });
    contesto.drawImage(immagine, 0, 0);

    ultima = contesto.getImageData(0, 0, larghezza, altezza);
    return { larghezza, altezza };
}

/**
 * I punti dell'ultima immagine aperta, quattro byte per punto.
 *
 * Viaggiano come array di byte e non dentro l'oggetto di sopra: Blazor sa passare un
 * Uint8Array così com'è, mentre un oggetto con dentro un milione di numeri passerebbe da JSON
 * — e sarebbe un secondo abbondante di attesa per niente.
 *
 * @returns {Uint8Array}
 */
export function pixel() {
    if (ultima === null) {
        return new Uint8Array(0);
    }

    const dati = new Uint8Array(ultima.data.buffer.slice(0));

    // Un'immagine per volta: tenerla sarebbe qualche megabyte che nessuno rilegge più.
    ultima = null;
    return dati;
}

/**
 * Un'immagine a partire dai punti: l'operazione inversa di quella qui sopra.
 *
 * Serve al confronto, che si fa a schermo: il C# sa quali punti ha capito e quali no, ma per
 * mostrarli servono due immagini vere. Passa i byte, e torna indietro un indirizzo che un
 * riquadro può usare come sfondo — ripetuto, così la tessera si giudica come si
 * giudica una trama, cioè ripetuta.
 *
 * @param {Uint8Array} punti quattro byte per punto
 * @param {number} larghezza
 * @param {number} altezza
 * @returns {string} un data URI PNG
 */
export function daPunti(punti, larghezza, altezza) {
    const tela = document.createElement("canvas");
    tela.width = larghezza;
    tela.height = altezza;

    const contesto = tela.getContext("2d");
    const dati = contesto.createImageData(larghezza, altezza);
    dati.data.set(punti);
    contesto.putImageData(dati, 0, 0);

    return tela.toDataURL("image/png");
}
