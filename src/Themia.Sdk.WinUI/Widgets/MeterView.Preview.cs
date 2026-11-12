#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>unavailable</c> ([S-1940]) is the kind's reading for a thing not
/// on this machine (no battery), for a kind that can be missing. A fixed reading and a full history
/// are the sample machine's (<c>--machine sample</c>, [S-1931]), drawn by the meter's own code.
/// </summary>
public sealed partial class MeterView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates => _part.AbsentReading is null ? [] : ["unavailable"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "unavailable" && _part.AbsentReading is { } absent)
        {
            // The live series, items and the part's own watch (the GPU's adapters) would put this machine's back.
            _frozen = true;
            _watch?.Dispose();
            _watch = null;
            _items?.Dispose();
            _items = null;
            _part.Unwatch();
            _standIn = false;
            Present(absent);
            Lay();
        }

        return Task.CompletedTask;
    }
}
#endif
