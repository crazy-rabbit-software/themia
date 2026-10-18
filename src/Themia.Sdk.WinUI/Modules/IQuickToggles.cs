using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-3907] The quick settings as a widget reaches them (<see cref="IWidgetHost.Toggles"/>): one source
/// per toggle for the whole app, kept by the host and shared by every widget showing it, started by
/// the first one and stopped when the last lets go. The sources themselves (radios, Core Audio, the
/// Settings handlers, the registry watches) are host code; a widget only names which toggle it follows.
/// </summary>
public interface IQuickToggles
{
    /// <summary>
    /// Follow <paramref name="toggle"/> from the UI thread; <paramref name="changed"/> is called on that
    /// thread whenever its reading moves. Dispose the source to stop (twice is once).
    /// </summary>
    IToggleSource Acquire(QuickToggle toggle, Action changed);
}

/// <summary>One widget's hold on a toggle's shared source: what it reads, a switch, a re-read.</summary>
public interface IToggleSource : IDisposable
{
    /// <summary>[S-3903] What the source last read; <see cref="ToggleReading.Pending"/> until Windows answers.</summary>
    ToggleReading Reading { get; }

    /// <summary>
    /// Ask Windows to switch it <paramref name="on"/>; true where it did. Completes once the reading
    /// after the switch has been taken. Never throws.
    /// </summary>
    Task<bool> SetAsync(bool on);

    /// <summary>Read again, for what Windows announces nowhere (the pointer coming by). Never blocks.</summary>
    void Refresh();
}
