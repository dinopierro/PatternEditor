namespace PatternEditor.Services.Riconoscimento;

/// <summary>
/// Una superficie su cui ridisegnare le forme riconosciute, a colori.
/// </summary>
///
/// <remarks>
/// <para>
/// Serve a rispondere alla sola domanda che conta: <b>somiglia?</b> Con una maschera di sì e
/// no si può sapere quanto inchiostro è stato coperto, e non basta — un retino di mattoni
/// arancioni su fughe bianche si può leggere in due modi opposti, e tutti e due «coprono»
/// tutto l'inchiostro che si sono scelti. A distinguerli è solo il colore.
/// </para>
/// <para>
/// Si dipinge nell'ordine in cui si scrive il documento, perché è l'ordine in cui dipinge
/// anche un lettore SVG: chi viene dopo sta sopra.
/// </para>
/// </remarks>
public sealed class Tela
{
    private readonly byte[] _rgb;

    public Tela(int larghezza, int altezza, (byte R, byte G, byte B) fondo)
    {
        Larghezza = larghezza;
        Altezza = altezza;
        _rgb = new byte[larghezza * altezza * 3];

        for (var i = 0; i < larghezza * altezza; i++)
        {
            _rgb[i * 3] = fondo.R;
            _rgb[i * 3 + 1] = fondo.G;
            _rgb[i * 3 + 2] = fondo.B;
        }
    }

    public int Larghezza { get; }

    public int Altezza { get; }

    /// <summary>
    /// Dipinge un punto, riportandolo dentro la cella se ne è uscito.
    /// </summary>
    ///
    /// <remarks>
    /// L'avvolgimento non è una comodità: una cella è una tessera, e una forma che esce a
    /// destra rientra a sinistra. Senza, ogni tratto inclinato risulterebbe dipinto a metà e
    /// la misura direbbe male di una ricostruzione giusta.
    /// </remarks>
    public void Dipingi(int x, int y, (byte R, byte G, byte B) colore)
    {
        var cx = (x % Larghezza + Larghezza) % Larghezza;
        var cy = (y % Altezza + Altezza) % Altezza;
        var i = (cy * Larghezza + cx) * 3;

        _rgb[i] = colore.R;
        _rgb[i + 1] = colore.G;
        _rgb[i + 2] = colore.B;
    }

    public (byte R, byte G, byte B) Colore(int x, int y)
    {
        var i = (y * Larghezza + x) * 3;
        return (_rgb[i], _rgb[i + 1], _rgb[i + 2]);
    }
}
