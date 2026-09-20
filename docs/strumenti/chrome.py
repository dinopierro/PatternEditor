# -*- coding: utf-8 -*-
"""Client minimo del protocollo di debug di Chrome, per catturare schermate vere.

Perché non basta `chrome --screenshot`: quell'opzione scatta al termine del caricamento
della pagina, mentre un'applicazione WebAssembly comincia a esistere dopo. Il risultato è
una schermata della scritta «Caricamento…». E `--timeout`, che nel vecchio headless
aspettava, nel nuovo non esiste più.

Serve quindi parlare con Chrome mentre lavora: navigare, ASPETTARE che una condizione sia
vera, eventualmente premere qualcosa, e solo allora scattare. Si fa con il protocollo di
debug, che viaggia su WebSocket: qui è implementato a mano, in una settantina di righe,
per non dover installare nulla.
"""
import base64
import json
import os
import socket
import struct
import subprocess
import time
import urllib.request

# Chrome o Edge: vanno bene entrambi, e nessuno dei due sta sempre nello stesso posto.
POSSIBILI = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]
CHROME = next((p for p in POSSIBILI if os.path.exists(p)), POSSIBILI[0])
PORTA = 9222


# ----------------------------------------------------------------- WebSocket essenziale
class WebSocket:
    """Quel tanto di WebSocket che serve a parlare con Chrome: testo, un frame per volta."""

    def __init__(self, url):
        senza_schema = url.split("://", 1)[1]
        host_porta, percorso = senza_schema.split("/", 1)
        host, porta = host_porta.split(":")

        self.sock = socket.create_connection((host, int(porta)))
        self.sock.settimeout(60)
        chiave = base64.b64encode(os.urandom(16)).decode()

        self.sock.sendall((
            "GET /%s HTTP/1.1\r\n"
            "Host: %s\r\n"
            "Upgrade: websocket\r\n"
            "Connection: Upgrade\r\n"
            "Sec-WebSocket-Key: %s\r\n"
            "Sec-WebSocket-Version: 13\r\n\r\n" % (percorso, host_porta, chiave)).encode())

        # Si legge finché non finisce l'intestazione della risposta.
        risposta = b""
        while b"\r\n\r\n" not in risposta:
            risposta += self.sock.recv(4096)
        assert b"101" in risposta.split(b"\r\n")[0], risposta[:200]

        self.resto = risposta.split(b"\r\n\r\n", 1)[1]
        self.contatore = 0

    def _leggi(self, quanti):
        while len(self.resto) < quanti:
            pezzo = self.sock.recv(65536)
            if not pezzo:
                raise ConnectionError("connessione chiusa da Chrome")
            self.resto += pezzo
        fuori, self.resto = self.resto[:quanti], self.resto[quanti:]
        return fuori

    def invia(self, testo):
        dati = testo.encode("utf-8")
        intestazione = bytearray([0x81])            # FIN + opcode testo
        maschera = os.urandom(4)
        n = len(dati)

        if n < 126:
            intestazione.append(0x80 | n)           # bit di maschera: il client DEVE mascherare
        elif n < 65536:
            intestazione.append(0x80 | 126)
            intestazione += struct.pack(">H", n)
        else:
            intestazione.append(0x80 | 127)
            intestazione += struct.pack(">Q", n)

        intestazione += maschera
        mascherati = bytes(b ^ maschera[i % 4] for i, b in enumerate(dati))
        self.sock.sendall(bytes(intestazione) + mascherati)

    def ricevi(self):
        """Un messaggio completo, ricomponendo gli eventuali frammenti."""
        pezzi = []
        while True:
            b1, b2 = self._leggi(2)
            fine = b1 & 0x80
            lunghezza = b2 & 0x7F

            if lunghezza == 126:
                lunghezza = struct.unpack(">H", self._leggi(2))[0]
            elif lunghezza == 127:
                lunghezza = struct.unpack(">Q", self._leggi(8))[0]

            pezzi.append(self._leggi(lunghezza))     # il server non maschera mai
            if fine:
                return b"".join(pezzi).decode("utf-8")

    def comando(self, metodo, **parametri):
        self.contatore += 1
        identificativo = self.contatore
        self.invia(json.dumps({"id": identificativo, "method": metodo, "params": parametri}))

        while True:
            messaggio = json.loads(self.ricevi())
            if messaggio.get("id") == identificativo:
                if "error" in messaggio:
                    raise RuntimeError("%s: %s" % (metodo, messaggio["error"]))
                return messaggio.get("result", {})
            # gli eventi che non interessano si scartano

    def chiudi(self):
        try:
            self.sock.close()
        except OSError:
            pass


# ----------------------------------------------------------------- pilotaggio di Chrome
class Browser:
    def __init__(self, larghezza=1440, altezza=900, scala=2):
        self.profilo = os.path.join(os.environ["TEMP"], "chrome-scatti-%d" % time.time())
        self.processo = subprocess.Popen([
            CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars",
            "--no-first-run", "--no-default-browser-check",
            "--disable-features=Translate,MediaRouter",
            "--remote-debugging-port=%d" % PORTA,
            "--user-data-dir=" + self.profilo,
            "--window-size=%d,%d" % (larghezza, altezza),
            "--force-device-scale-factor=%d" % scala,
            "about:blank",
        ], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

        indirizzo = None
        for _ in range(60):
            try:
                with urllib.request.urlopen("http://127.0.0.1:%d/json/version" % PORTA, timeout=2) as r:
                    indirizzo = json.loads(r.read())["webSocketDebuggerUrl"]
                break
            except Exception:
                time.sleep(0.5)
        if not indirizzo:
            raise RuntimeError("Chrome non ha aperto la porta di debug")

        self.browser = WebSocket(indirizzo)
        bersaglio = self.browser.comando("Target.createTarget", url="about:blank")["targetId"]
        with urllib.request.urlopen("http://127.0.0.1:%d/json" % PORTA, timeout=5) as r:
            schede = json.loads(r.read())
        ws = next(s["webSocketDebuggerUrl"] for s in schede if s["id"] == bersaglio)

        self.pagina = WebSocket(ws)
        self.pagina.comando("Page.enable")
        self.pagina.comando("Runtime.enable")
        self.pagina.comando("Emulation.setDeviceMetricsOverride",
                            width=larghezza, height=altezza,
                            deviceScaleFactor=scala, mobile=False)

    def vai(self, url):
        self.pagina.comando("Page.navigate", url=url)

    def valuta(self, espressione):
        esito = self.pagina.comando("Runtime.evaluate", expression=espressione,
                                    returnByValue=True, awaitPromise=True)
        return esito.get("result", {}).get("value")

    def attendi(self, condizione, secondi=90, passo=0.4):
        scadenza = time.time() + secondi
        while time.time() < scadenza:
            try:
                if self.valuta(condizione):
                    return True
            except Exception:
                pass
            time.sleep(passo)
        raise TimeoutError("condizione mai verificata: " + condizione)

    def misura(self, larghezza, altezza, scala=2):
        self.pagina.comando("Emulation.setDeviceMetricsOverride",
                            width=larghezza, height=altezza,
                            deviceScaleFactor=scala, mobile=False)

    def scatta(self, percorso, ritaglio=None):
        parametri = {"format": "png", "captureBeyondViewport": False}
        if ritaglio:
            parametri["clip"] = dict(ritaglio, scale=1)
        dati = self.pagina.comando("Page.captureScreenshot", **parametri)["data"]
        with open(percorso, "wb") as f:
            f.write(base64.b64decode(dati))
        return os.path.getsize(percorso)

    def chiudi(self):
        try:
            self.pagina.chiudi()
            self.browser.chiudi()
        finally:
            self.processo.terminate()
