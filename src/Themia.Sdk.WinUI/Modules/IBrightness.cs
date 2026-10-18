using Themia.App.Services.Brightness;
using Themia.Core.Model;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-4441] Every display's brightness as a widget reaches it (<see cref="IWidgetHost.Brightness"/>):
/// the host's one worker (the power policy for a built-in panel, DDC/CI for a monitor), started by the
/// first widget that follows it and stopped by the last.
/// </summary>
public interface IBrightness
{
    /// <summary>What the displays answered last.</summary>
    BrightnessSnapshot Snapshot { get; }

    /// <summary>Follow the displays; <paramref name="changed"/> runs on the calling thread's dispatcher. Each widget coming up looks at the displays again. Dispose to let go.</summary>
    IDisposable Acquire(Action changed);

    /// <summary>Read every level again (the pointer came by: a monitor's own buttons may have moved one).</summary>
    void Refresh();

    /// <summary>[S-4442] Ask <paramref name="level"/> of every display one slider sets (<paramref name="own"/>: the widget's display); false where it sets none.</summary>
    bool Set(string? own, BrightnessScreens screens, int level);

    /// <summary>[S-4443] The level being written to a display, until it has said what it took.</summary>
    int? Asked(string device);
}
