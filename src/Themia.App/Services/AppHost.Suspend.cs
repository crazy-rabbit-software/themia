using Microsoft.UI.Dispatching;
using Themia.App.Interop;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Services;

/// <summary>
/// [S-920..S-929] Game mode: while a full-screen game, video or presentation has the screen, the
/// widgets are suspended — every surface hidden, every widget detached, the shared ticker paused —
/// and when it is gone they come back, rebuilt from the state.
/// <para>
/// The point is resource conservation as much as getting out of the way: a desktop nobody can see
/// should cost nothing. Detaching is what guarantees that, because every widget already stops its
/// own timers, samplers and network feeds on detach; the shared services they used go idle with
/// their last subscriber. What is deliberately <b>not</b> suspended is screen time ([S-459]): it is a
/// recorder, not a display, and the game is exactly the time it exists to count.
/// </para>
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>The event source, alive only while the switch is on.</summary>
    private FullscreenWatch? _fullscreenWatch;

    /// <summary>Whether the widgets are suspended right now.</summary>
    private bool _suspended;

    /// <summary>A check is already queued, so the next event need not queue another.</summary>
    private bool _gameModeCheckQueued;

    /// <summary>Whether game mode has the widgets suspended right now — the settings page's "paused now".</summary>
    public bool Suspended => _suspended;

    /// <summary>Raised on the UI thread when the widgets are suspended or resumed.</summary>
    public event Action<bool>? SuspendedChanged;

    /// <summary>[S-921] Flip the game-mode switch. Applies at once: off while a game runs brings the widgets back.</summary>
    public void SetGameModeSuspend(bool enabled)
    {
        Mutate(state => state.Preferences.GameModeSuspend = enabled, relayout: false);
        SyncGameModeWatch();
    }

    private void StartGameMode()
    {
        // An import or a reset can change the switch too, and it arrives as a state change.
        StateChanged += SyncGameModeWatch;
        SyncGameModeWatch();
    }

    private void StopGameMode()
    {
        StateChanged -= SyncGameModeWatch;
        _fullscreenWatch?.Dispose();
        _fullscreenWatch = null;
    }

    /// <summary>
    /// [S-927] Install the watch when the switch is on and remove it when it is off, then decide
    /// straight away. Synchronous on purpose: at startup it runs before the first surface is built,
    /// so a launch under a running game holds its surfaces instead of flashing them.
    /// </summary>
    private void SyncGameModeWatch()
    {
        var wanted = State.Preferences.GameModeSuspend && !_disposed;
        if (wanted == (_fullscreenWatch is not null))
        {
            return;
        }

        if (wanted)
        {
            var watch = new FullscreenWatch();
            if (!watch.Start())
            {
                watch.Dispose();
                Log.Error(Scope, "game mode is on but cannot watch for full-screen apps; widgets stay up");
                return;
            }

            watch.Changed += RequestGameModeCheck;
            _fullscreenWatch = watch;
            Log.Info(Scope, "game mode on: widgets pause while a full-screen app has the screen");
        }
        else
        {
            _fullscreenWatch?.Dispose();
            _fullscreenWatch = null;
            Log.Info(Scope, "game mode off");
        }

        CheckGameMode();
    }

    /// <summary>
    /// Something may have changed: check once, soon. Coalesced, not timed — one alt-tab produces a
    /// foreground event and a shell notification, and the first queues a check at low priority behind
    /// the rest of the burst. Each check reads everything fresh, so a later event queues the next.
    /// </summary>
    private void RequestGameModeCheck()
    {
        if (_disposed || _gameModeCheckQueued)
        {
            return;
        }

        _gameModeCheckQueued = true;
        if (!_dispatcher.TryEnqueue(DispatcherQueuePriority.Low, RunQueuedGameModeCheck))
        {
            _gameModeCheckQueued = false;
            Log.Error(Scope, "could not queue a game-mode check; the widgets keep their state until the next change");
        }
    }

    private void RunQueuedGameModeCheck()
    {
        _gameModeCheckQueued = false;
        if (!_disposed)
        {
            CheckGameMode();
        }
    }

    /// <summary>[S-920] The one decision, from a fresh read of the shell, and the one place it is applied.</summary>
    private void CheckGameMode()
    {
        var enabled = _fullscreenWatch is not null;
        var attention = enabled ? UserNotification.Query() : UserNotificationState.Unknown;
        var foreground = enabled ? UserNotification.Foreground() : ForegroundKind.None;
        var next = GameMode.Decide(enabled, attention, foreground, _suspended);

        if (next == _suspended)
        {
            return;
        }

        Log.Info(Scope, next
            ? $"a full-screen app has the screen ({attention}); suspending the widgets"
            : $"the screen is free again ({attention}, {foreground} in front); resuming the widgets");

        if (next)
        {
            SuspendWidgets();
        }
        else
        {
            ResumeWidgets();
        }

        try
        {
            SuspendedChanged?.Invoke(next);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a suspend subscriber threw", ex);
        }
    }

    /// <summary>[S-928] Hide every surface with its widgets detached, and stop every wake-up the shell itself owns.</summary>
    private void SuspendWidgets()
    {
        _suspended = true;

        // Edit mode on a hidden surface would hold a lease and a placement nobody can see.
        SetEditMode(false);

        foreach (var window in _windows)
        {
            window.Suspend();
        }

        // The z-order controller's backstop wakes every second to look after visible surfaces;
        // with none visible it has nothing to do.
        DesktopZOrder.Stop();
        Ticker.SetPaused(true);

        // [S-3411] The cards have let go: decoded pictures nobody shows are memory a game wants.
        _gallery.Trim();
    }

    /// <summary>[S-929] Bring everything back, then catch up on display changes the game made while it had the screen.</summary>
    private void ResumeWidgets()
    {
        _suspended = false;

        DesktopZOrder.Start(_dispatcher);
        Ticker.SetPaused(false);

        foreach (var window in _windows)
        {
            window.Resume();
        }

        // An exclusive full-screen game changes the display mode and back; a monitor may have been
        // plugged in or out meanwhile. The display pass is idempotent, so asking costs nothing.
        RequestDisplayPass();
    }

    /// <summary>
    /// [S-928] A surface the display pass has just built: shown, unless game mode is holding the
    /// desktop — then it starts suspended, so a monitor connected mid-game stays dark too.
    /// </summary>
    private void ShowOrHold(DesktopWindow window)
    {
        if (_suspended)
        {
            window.Suspend();
        }
        else
        {
            window.ShowSurface();
        }
    }
}
