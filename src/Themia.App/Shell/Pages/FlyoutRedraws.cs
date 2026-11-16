using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Shell;

/// <summary>
/// [S-2346] The open flyout's drawings again: never under a press held on it, never inside the event that asked
/// (a press's release above all, whose row XAML is still handing the event to). Each is posted once to the
/// dispatcher and drawn after the event returns; at normal priority, so a busy surface (an animation, pictures
/// loading) never starves it. What the drawing throws is logged, never let out.
/// </summary>
internal sealed class FlyoutRedraws(DispatcherQueue queue, Action draw)
{
    private const string Scope = "pages";

    private readonly DispatcherQueue _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    private readonly Action _draw = draw ?? throw new ArgumentNullException(nameof(draw));
    private readonly FlyoutPress _press = new();

    /// <summary>A press landed on the flyout.</summary>
    public void Pressed() => _press.Press();

    /// <summary>The page is asked to show the state as it is now.</summary>
    public void Ask()
    {
        if (_press.Redraw())
        {
            Post();
        }
    }

    /// <summary>The press on the flyout let go: what changed while it was held is drawn after this event.</summary>
    public void LetGo()
    {
        if (_press.Release())
        {
            Post();
        }
    }

    /// <summary>The flyout closed or another opened: nothing held, owed or posted is drawn.</summary>
    public void Reset() => _press.Reset();

    private void Post()
    {
        if (!_queue.TryEnqueue(DispatcherQueuePriority.Normal, Run))
        {
            // The dispatcher is shutting down: the window goes with it, there is nothing to draw on.
            _press.Reset();
            Log.Warn(Scope, "the flyout's drawing could not be posted; the dispatcher is shutting down");
        }
    }

    private void Run()
    {
        try
        {
            if (_press.Run())
            {
                _draw();
            }
        }
        catch (Exception ex)
        {
            // A dispatcher callback: nothing may escape it. The page stays as last drawn; the next change draws it again.
            Log.Error(Scope, "the open flyout could not be drawn again", ex);
        }
    }
}
