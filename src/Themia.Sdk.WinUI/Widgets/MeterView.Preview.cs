#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>unavailable</c> ([S-1940]) is the kind's reading for a thing not
/// on this machine (no battery), for a kind that can be missing; <c>waiting</c> ([S-15161]) the face before the hub's first
/// read, for a kind that draws items (its items as they will stand, each "--"). A fixed reading and a full history
/// are the sample machine's (<c>--machine sample</c>, [S-1931]; [S-15744] the <c>sample</c> state, first, so every style is
/// drawn with readings), drawn by the meter's own code.
/// </summary>
public sealed partial class MeterView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates
        => ["sample", .. _part.AbsentReading is null ? [] : new[] { "unavailable" }, .. _part.WaitingItems.Count == 0 ? [] : new[] { "waiting" }];

    public Task ShowPreviewStateAsync(string state)
    {
        // [S-15744] "sample": the sample machine's reading and items, which the render hands the card's host for this state
        // (as --machine sample): the meter draws them through its own code, nothing of its own to set.
        if (state == "sample")
        {
            return Task.CompletedTask;
        }

        if (state == "waiting" && _part.WaitingItems.Count > 0)
        {
            // Nothing read yet: the live series and items let go, so this machine's first read never lands over it.
            _frozen = true;
            _watch?.Dispose();
            _watch = null;
            _items?.Dispose();
            _items = null;
            _part.Unwatch();
            _band.Clear();
            Present(_part.Waiting);
            ShowItems(_part.WaitingItems, point: false);
            Lay();
            return Task.CompletedTask;
        }

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
