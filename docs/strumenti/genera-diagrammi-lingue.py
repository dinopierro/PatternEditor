# -*- coding: utf-8 -*-
"""La figura delle lingue: i due cataloghi, chi li carica e su che cosa si ripiega."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from tela import (ACCENTO, ARANCIO, GRIGIO, INCHIOSTRO, MONO,  # noqa: E402
                  Tela, VERDE, spezza)

print("diagramma delle lingue")

# ------------------------------------------------------------------- 20 · le lingue
t = Tela("Figura 20 — Le due lingue e i due cataloghi",
         "Chi decide in che lingua si parla, da dove arrivano le parole, e che cosa succede "
         "quando ne manca una.")

# --------------------------------------------------------------- 1 · come si decide
t.scatola(30, 84, 270, 210, "1 · Come si decide la lingua", [
    "una volta sola, dopo il primo",
    "disegno: prima non c'è JavaScript",
], "accento")
t.testo(42, 158, "1.", 9, ACCENTO, "700")
t.testo(58, 158, "la scelta fatta a mano, se c'è stata", 8.8, INCHIOSTRO)
t.testo(58, 171, "localStorage — vince su tutto", 8.2, GRIGIO, famiglia=MONO)
t.testo(42, 191, "2.", 9, ACCENTO, "700")
t.testo(58, 191, "la lingua del browser, se la sappiamo", 8.8, INCHIOSTRO)
t.testo(58, 204, "navigator.languages[0] → it-IT → it", 8.2, GRIGIO, famiglia=MONO)
t.testo(42, 224, "3.", 9, ACCENTO, "700")
t.testo(58, 224, "l'inglese, che c'è sempre", 8.8, INCHIOSTRO)
t.testo(42, 250, "Il cambio dal selettore è immediato: si", 8.4, GRIGIO)
t.testo(42, 262, "sostituisce un dizionario e si ridisegna,", 8.4, GRIGIO)
t.testo(42, 274, "senza ricaricare la pagina.", 8.4, GRIGIO)

# ------------------------------------------------------------- 2 · i due cataloghi
t.scatola(345, 84, 290, 96, "2a · Catalogo dell'applicazione", [
    "`wwwroot/i18n/{en,it}.json`",
    "le pagine, l'accesso, la libreria",
])
t.testo(357, 158, "servito dall'applicazione stessa", 8.4, GRIGIO)

t.scatola(345, 198, 290, 96, "2b · Catalogo dell'editor", [
    "`_content/PatternEditor/i18n/it.json`",
    "risorsa statica della libreria",
], "verde")
t.testo(357, 272, "l'editor, i nove plugin, i filtri", 8.4, GRIGIO)

t.freccia(300, 132, 345, 132, "carica")
t.freccia(300, 246, 345, 246, "applica")

# --------------------------------------------------------- 3 · l'inglese incorporato
t.scatola(680, 84, 230, 210, "3 · L'inglese della libreria", [
    "`PatternEditor.Abstractions`",
    "risorsa incorporata nell'assembly",
], "caldo")
t.testo(692, 162, "Non una lingua privilegiata:", 8.6, ARANCIO, "700")
t.testo(692, 175, "la rete di sicurezza.", 8.6, ARANCIO, "700")
for i, riga in enumerate(spezza(
        "Un ospite che non applichi niente — perché è a lingua singola, perché il file non "
        "è arrivato — trova un editor che parla, non un editor pieno di sigle.", 34)):
    t.testo(692, 198 + i * 12, riga, 8.4, GRIGIO)

t.freccia(635, 246, 680, 246, "ripiega su", sopra=False)

# --------------------------------------------------- 4 · il ripiego, voce per voce
y = 320
t.scatola(30, y, 880, 96, "4 · Il ripiego è per chiave, non per catalogo")

passi = [
    ("il catalogo applicato", "editor.conferma → «Conferma»", VERDE),
    ("se non ce l'ha: l'inglese", "editor.conferma → «Confirm»", ACCENTO),
    ("se non c'è nemmeno lì", "si legge editor.conferma", ARANCIO),
]
x = 46
for titolo, esempio, colore in passi:
    t.testo(x, y + 44, titolo, 8.8, INCHIOSTRO, "700")
    t.testo(x, y + 60, esempio, 8.4, colore, famiglia=MONO)
    x += 292
t.testo(46, y + 82,
        "Una traduzione incompleta lascia scoperte le parole che le mancano, non l'interfaccia "
        "intera. E una chiave sbagliata si legge a schermo: è brutto, e si nota.", 8.4, GRIGIO)

# ------------------------------------------------ 5 · dove nasce il testo da tradurre
y = 436
t.scatola(30, y, 430, 128, "5a · Il testo che nasce nell'interfaccia", [
    "un'etichetta, un pulsante, una nota",
], "accento")
t.testo(42, y + 62, 'E["editor.conferma"]', 8.6, INCHIOSTRO, famiglia=MONO)
t.testo(42, y + 84, "La chiave sta scritta nel markup: il componente", 8.4, GRIGIO)
t.testo(42, y + 96, "chiede al catalogo e mostra quello che riceve.", 8.4, GRIGIO)

t.scatola(480, y, 430, 128, "5b · Il testo che nasce nel modello", [
    "un riassunto del filtro, un messaggio del validatore",
], "verde")
t.testo(492, y + 62, 'new TestoNominato("filtro.sfocatura.raggio",', 8.2, INCHIOSTRO,
        famiglia=MONO)
t.testo(492, y + 75, '                  "radius {0}", 2.5)', 8.2, INCHIOSTRO, famiglia=MONO)
t.testo(492, y + 96, "Il modello sa che cosa dire — quale caso, quali numeri —", 8.4, GRIGIO)
t.testo(492, y + 108, "e non in che lingua: quella la decide chi mostra.", 8.4, GRIGIO)

# -------------------------------------------------------------------------- la nota
t.nota(30, y + 144, 880, [
    "Le chiavi che si compongono a runtime — «tipo.rect» da un plugin, «preset.seppia» da un effetto, «filtro.feBlend» da un nome SVG —",
    "ripiegano sul nome che l'oggetto porta con sé, non sulla chiave: un plugin scritto da qualcun altro non è nei nostri cataloghi, e non",
    "ha motivo di esserci. Aggiungere una lingua è aggiungere due file e una riga nell'elenco: nessuna ricompilazione.",
])

t.salva("20-lingue.svg")
