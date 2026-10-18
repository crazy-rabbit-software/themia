using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.SystemData;

namespace Themia.Sdk;

/// <summary>
/// [S-2500] [S-2506] The system figures as a widget reaches them (<c>IWidgetHost.System</c>): the
/// host's one hub, which reads each source once per tick however many widgets show it and keeps
/// every charted series' history ([S-2504]). A view watches in Attach and lets go in Detach, and never
/// reads a counter or sends a probe itself. UI thread only: every delivery comes on the dispatcher.
/// </summary>
public interface ISystemFeeds
{
    /// <summary>
    /// [S-2506] Watch a source's snapshots (<paramref name="target"/>: a ping host). Never calls back
    /// from inside this call: the current value, when there is one, is <see cref="IFeedWatch{T}.Value"/>;
    /// every read after this is <paramref name="onSample"/>. Dispose to let go.
    /// </summary>
    IFeedWatch<T> Watch<T>(SourceId<T> source, Action<T> onSample, string? target = null);

    /// <summary>
    /// [S-2504] [S-2506] Watch one charted series. The reading and the history so far are on the watch
    /// at once; each read after this is <paramref name="onUpdate"/>, with the point it added.
    /// </summary>
    ISeriesWatch WatchSeries(SeriesInterest series, Action<SeriesUpdate> onUpdate);
}

/// <summary>[S-2506] One widget's hold on a feed's snapshots. Dispose to let go; twice is once.</summary>
public interface IFeedWatch<out T> : IDisposable
{
    FeedKey Key { get; }

    /// <summary>Whether the feed has been read yet.</summary>
    bool HasValue { get; }

    /// <summary>The newest snapshot; meaningful only when <see cref="HasValue"/>.</summary>
    T Value { get; }

    /// <summary>[S-2510] Read now (a Refresh command).</summary>
    void Refresh();
}

/// <summary>[S-2504] [S-2506] One widget's hold on a charted series. Dispose to let go; twice is once.</summary>
public interface ISeriesWatch : IDisposable
{
    SeriesInterest Interest { get; }

    /// <summary>The newest reading, or null before the first read has come back.</summary>
    MeterReading? Latest { get; }

    /// <summary>How many points the history holds.</summary>
    int Count { get; }

    /// <summary>How many points were ever added: the newest point's absolute position ([S-1121]).</summary>
    long Added { get; }

    /// <summary>The newest <paramref name="max"/> points, oldest first (a copy).</summary>
    SeriesPoint[] History(int max);

    /// <summary>[S-2510] Read now: a probe unless one is out.</summary>
    void Refresh();
}

/// <summary>The drive Windows runs from: what a disk meter with no drive chosen reads.</summary>
public static class SystemDrive
{
    /// <summary>The system drive's root (<c>C:\</c>).</summary>
    public static string Root => Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
}
