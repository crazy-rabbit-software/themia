namespace Themia.Core.Motion;

/// <summary>
/// [S-1001] Intro plays are armed one-shots, per display surface. A layout pass animates only when a
/// sanctioned moment armed it: the surface's first real layout (startup) or a change of the view the
/// display shows (transition). Everything else that rebuilds cards — a rename, a widget added, pasted
/// or restored, a settings change, a game-mode resume — lays out silently.
/// <para>
/// Replays (the settings preview and the session unlock) do not go through here: they re-run the
/// cards already on screen, and are refused while a show is still playing ([S-1012]).
/// </para>
/// </summary>
public sealed class IntroArming
{
    private bool _startupPending = true;
    private string? _shownViewId;

    /// <summary>
    /// The intro a layout pass of <paramref name="viewId"/> plays, or null for a silent pass. Called
    /// once per pass that actually laid cards out (the surface is loaded and has a usable size), so
    /// the startup arm is not spent on a pass nobody could see.
    /// </summary>
    public IntroMode? OnLayout(string viewId)
    {
        if (_startupPending)
        {
            _startupPending = false;
            _shownViewId = viewId;
            return IntroMode.Startup;
        }

        var changed = _shownViewId is not null && !string.Equals(_shownViewId, viewId, StringComparison.Ordinal);
        _shownViewId = viewId;
        return changed ? IntroMode.Transition : null;
    }

    /// <summary>A show is running on this surface: started, its end not heard yet.</summary>
    private bool _running;

    /// <summary>[S-1017] The surface started a show (after the pass that armed it placed every card).</summary>
    public void ShowStarted() => _running = true;

    /// <summary>[S-1017] The show ended: its animations completed, or it was stopped.</summary>
    public void ShowEnded() => _running = false;

    /// <summary>
    /// [S-1017] Called first in every layout pass: whether a show is still running and must end now, with
    /// every card at rest. A pass may rebuild, move or resize the cards that show animates, and a show
    /// whose compositor never reported its end (its scoped batch never completed) would otherwise hold
    /// them hidden for good; the pass that started it starts it last, so only a later pass ends it.
    /// </summary>
    public bool BeginPass()
    {
        var running = _running;
        _running = false;
        return running;
    }
}
