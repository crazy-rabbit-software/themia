using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3907] One quick setting as Windows has it: what it reads, how it hears a change, and how it is
/// switched. One per toggle for the whole app, shared by every widget showing it (<see cref="ToggleHub"/>).
/// <para>
/// <b>Nothing here polls.</b> Each source follows the notification Windows has for its setting (a radio's
/// StateChanged, a registry key's change, a power-setting notification, Core Audio's callbacks) and reads
/// again when it fires. <see cref="Refresh"/> is the one re-read a widget asks for, when the pointer
/// comes onto it, for what Windows announces nowhere (as the hibernate probe does, [S-1914]).
/// </para>
/// <para>
/// <b>Nothing here blocks the UI thread.</b> <see cref="Start"/> is called there and hands every call
/// into Windows to the pool (or the source's own thread); <see cref="Changed"/> comes from any thread.
/// </para>
/// </summary>
internal abstract class ToggleSource : IDisposable
{
    protected const string Scope = "toggles";

    private readonly Lock _gate = new();
    private ToggleReading _reading = ToggleReading.Pending;

    /// <summary>The reading moved. From any thread; the hub carries it to each widget's.</summary>
    public event Action? Changed;

    public ToggleReading Reading
    {
        get
        {
            lock (_gate)
            {
                return _reading;
            }
        }
    }

    protected volatile bool Disposed;

    /// <summary>Begin following; must not block. Called once, on the UI thread.</summary>
    public abstract void Start();

    /// <summary>
    /// Ask Windows to switch it <paramref name="on"/>; true where it did. Completes once the reading
    /// after the switch has been taken, so the tile never flickers back to the old state between the
    /// answer and the notification. Never throws.
    /// </summary>
    public abstract Task<bool> SetAsync(bool on);

    /// <summary>Read again, for a setting whose availability Windows announces nowhere. Never blocks.</summary>
    public virtual void Refresh()
    {
    }

    public void Dispose()
    {
        if (Disposed)
        {
            return;
        }

        Disposed = true;

        try
        {
            Stop();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{GetType().Name} could not let go of what it followed", ex);
        }
    }

    /// <summary>Let go of everything followed. Called once.</summary>
    protected abstract void Stop();

    /// <summary>Take a new reading; tell the widgets when it moved.</summary>
    protected void Publish(ToggleReading reading)
    {
        bool moved;
        lock (_gate)
        {
            moved = reading != _reading;
            _reading = reading;
        }

        if (moved && !Disposed)
        {
            Raise();
        }
    }

    /// <summary>Run <paramref name="work"/> on the pool; a failure is logged and read as <paramref name="otherwise"/>.</summary>
    protected Task<T> OffThread<T>(string what, Func<T> work, T otherwise) => Task.Run(() =>
    {
        try
        {
            return work();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{GetType().Name}: {what}", ex);
            return otherwise;
        }
    });

    /// <summary>Run <paramref name="work"/> on the pool, logging a failure; for reads that publish themselves.</summary>
    protected void OffThread(string what, Action work) => _ = Task.Run(() =>
    {
        try
        {
            work();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{GetType().Name}: {what}", ex);
        }
    });

    private void Raise()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            // ignore: a handler throwing must not stop the source; the hub's own handler only posts.
            Log.Error(Scope, $"{GetType().Name}: a change could not be passed on", ex);
        }
    }
}
