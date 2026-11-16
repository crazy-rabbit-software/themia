namespace Themia.Core.Shell;

/// <summary>
/// [S-2346] The edit bar's open flyout and a press held on it. A page is drawn again from the state whenever
/// something changes it (an undo, a widget's config written by its view, a line that finished reading, [S-3531],
/// [S-3746]), and drawing it again replaces every row: a row pressed and released across that lost its click, so
/// a section the user clicked did not open (owner, 2026-11-15: "sometimes i cannot open a section in the
/// configuration … the mouse click does not reach the component"). While a press is held on the flyout the page is
/// not drawn again; what asked for it is drawn once, after the press lets go.
/// <para>
/// A drawing is never done inside the event that asked for it, the release's above all: XAML is still handing
/// that pointer event to the row under it, and taking the rows away then threw out of the event (owner's log,
/// 2026-11-15 18:01: a NullReferenceException from the panel's <c>Clear</c> under <c>PointerReleased</c>). Every
/// drawing is posted to run after the event, one at a time: asks while one is posted are that one.
/// </para>
/// </summary>
public sealed class FlyoutPress
{
    /// <summary>A press is held on the flyout.</summary>
    public bool Held { get; private set; }

    /// <summary>The page was asked to be drawn again while the press was held.</summary>
    public bool Pending { get; private set; }

    /// <summary>A drawing is posted to run after the event that asked for it.</summary>
    public bool Posted { get; private set; }

    /// <summary>A press landed on the flyout.</summary>
    public void Press() => Held = true;

    /// <summary>
    /// The page is asked to be drawn again: true to post one drawing (it runs after this event, through
    /// <see cref="Run"/>); false when one is posted already or it waits for the press to let go.
    /// </summary>
    public bool Redraw()
    {
        if (Held)
        {
            Pending = true;
            return false;
        }

        return Post();
    }

    /// <summary>
    /// The press let go (released, lost, or the pointer seen with no button down): true to post the drawing it
    /// owes. Never a drawing now: the release is still being handed to the row pressed.
    /// </summary>
    public bool Release()
    {
        var owed = Held && Pending;
        Held = false;
        Pending = false;
        return owed && Post();
    }

    /// <summary>
    /// The posted drawing's turn, after the event: true to draw now. A press that landed since it was posted holds
    /// it again until it lets go; one closed flyout's drawing is gone.
    /// </summary>
    public bool Run()
    {
        if (!Posted)
        {
            return false;
        }

        Posted = false;
        if (Held)
        {
            Pending = true;
            return false;
        }

        return true;
    }

    /// <summary>The flyout closed: nothing is held, owed or posted.</summary>
    public void Reset()
    {
        Held = false;
        Pending = false;
        Posted = false;
    }

    private bool Post()
    {
        if (Posted)
        {
            return false;
        }

        Posted = true;
        return true;
    }
}
