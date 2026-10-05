using Microsoft.UI.Dispatching;

namespace Themia.App.Services;

/// <summary>
/// [S-1882] Hand the memory of widgets taken off a surface back as soon as they go, not whenever the
/// collector next happens to run.
/// <para>
/// A widget card is a few managed wrappers over a large native tree: the XAML elements, their
/// composition visuals, text layouts and bitmaps. The native side is released only when the wrappers
/// are collected, and the collector runs on <i>managed</i> allocation, of which a widget layer makes
/// almost none. So a view switch left every card of the old view alive natively until some later
/// collection, and switching through views piled them up: the process grew by about 100 MB per pass
/// over the showcase views and never came back down. One collection after the pass that released the
/// cards, at low priority so the new view is laid out and drawn first, frees them in one go. Requests
/// in a burst (a view switch on two displays, a multi-delete) share one collection.
/// </para>
/// </summary>
internal static class ReleaseSweep
{
    /// <summary>A sweep is queued and has not run yet. UI thread only.</summary>
    private static bool _queued;

    /// <summary>Cards were taken off a surface: collect once the dispatcher has nothing more urgent to do.</summary>
    public static void Request(DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        if (_queued)
        {
            return;
        }

        _queued = dispatcher.TryEnqueue(DispatcherQueuePriority.Low, Sweep);
    }

    private static void Sweep()
    {
        _queued = false;

        // Not blocking: a background collection, so the surface keeps drawing while it runs. The
        // finalizers it queues release the native trees.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: false);
    }
}
