namespace PatternEditor.Core.Models;

/// <summary>
/// Chi può vedere un pattern.
///
/// <para>
/// Come l'autore (<see cref="Pattern.AuthorId"/>), la libreria <b>trasporta</b> questo dato e
/// non lo interpreta: non sa che cosa sia un portale né chi possa approvare che cosa. A
/// decidere è l'applicazione ospite, ed è l'unica che può farlo davvero, perché è l'unica che
/// sta su un server.
/// </para>
///
/// <para>
/// I tre stati non sono tre livelli della stessa scala: <see cref="InAttesa"/> non è «un po'
/// pubblica», è una <b>richiesta</b>. La differenza conta perché è quella che impedisce a chi
/// si registra di pubblicare: fra il volere e l'essere visibile c'è una persona.
/// </para>
/// </summary>
public enum PatternVisibility
{
    /// <summary>
    /// La vede solo chi l'ha scritta. È lo stato in cui nasce ogni pattern nuovo: se una
    /// pubblicazione dev'essere approvata, il valore predefinito non può essere quello che
    /// non richiede approvazione.
    /// </summary>
    Privata,

    /// <summary>
    /// L'autore ha chiesto di pubblicarla e aspetta. Si vede come una privata — solo
    /// l'autore, più chi modera — e diventa pubblica nel momento in cui qualcuno la guarda e
    /// dice di sì.
    /// </summary>
    InAttesa,

    /// <summary>
    /// La vedono tutti, registrati e no. È l'unico stato che un client non può assegnare da
    /// sé: chiederlo significa passare da <see cref="InAttesa"/>.
    /// </summary>
    Pubblica,
}
