namespace PatternEditor.Components;

/// <summary>
/// Tema cromatico del componente.
///
/// Il valore predefinito è <see cref="System"/>: il componente segue l'impostazione del
/// sistema operativo, come farebbe qualunque interfaccia che non abbia opinioni proprie.
/// Un'applicazione ospitante che offra all'utente una scelta esplicita passa invece
/// <see cref="Light"/> o <see cref="Dark"/>, che prevalgono sempre — anche quando
/// contraddicono il sistema, perché una scelta esplicita dell'utente vale più di una
/// preferenza dedotta.
/// </summary>
public enum PatternEditorTheme
{
    /// <summary>Segue la preferenza del sistema operativo.</summary>
    System,

    /// <summary>Tema chiaro, indipendentemente dal sistema.</summary>
    Light,

    /// <summary>Tema scuro, indipendentemente dal sistema.</summary>
    Dark,
}
