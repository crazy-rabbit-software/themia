namespace Themia.Core.Shell;

/// <summary>
/// [S-3727] How an "are you sure?" draws its two buttons: the action and Cancel. A question about
/// something that cannot be taken back (Delete view, Sign out) is Windows' destructive confirm: the
/// action in the critical colour, never the accent that invites the click, and the keyboard on
/// Cancel, so Enter or Space straight after the question keeps everything. Any other question (a
/// preset over a look of one's own) offers its action as the accent button, the keyboard on it.
/// </summary>
/// <param name="Accent">The action is the accent button.</param>
/// <param name="Critical">The action is filled with the critical colour, its words on it.</param>
/// <param name="CancelFirst">The keyboard starts on Cancel rather than on the action.</param>
public readonly record struct ConfirmLook(bool Accent, bool Critical, bool CancelFirst)
{
    /// <summary>The look of a question whose action destroys something (<paramref name="danger"/>), or of any other.</summary>
    public static ConfirmLook For(bool danger) => danger
        ? new ConfirmLook(Accent: false, Critical: true, CancelFirst: true)
        : new ConfirmLook(Accent: true, Critical: false, CancelFirst: false);
}
