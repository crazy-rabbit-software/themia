using Themia.Core.SystemData;

namespace Themia.App.Services.SystemData;

/// <summary>
/// One watch a widget holds on a hub feed, watched again only when the feed it names changes (a new
/// host), so a config touch that changes nothing the feed depends on keeps the watch.
/// </summary>
/// <typeparam name="T">The feed's snapshot.</typeparam>
internal sealed class WatchSlot<T>
{
    private FeedWatch<T>? _watch;

    /// <summary>The watch, or null while nothing is held.</summary>
    public FeedWatch<T>? Current => _watch;

    /// <summary>Whether the held feed has been read yet.</summary>
    public bool HasValue => _watch is { HasValue: true };

    /// <summary>The newest snapshot; meaningful only when <see cref="HasValue"/>.</summary>
    public T Value => _watch is { } watch ? watch.Value : default!;

    /// <summary>Hold <paramref name="source"/>, unless that is held already. True when it watched anew.</summary>
    public bool Hold(SystemHub hub, SourceId<T> source, Action<T> onSample, string? target = null)
    {
        var key = FeedKey.Create(source.Source, target);

        if (_watch is not null && _watch.Key == key)
        {
            return false;
        }

        _watch?.Dispose();
        _watch = hub.Watch(source, onSample, target);
        return true;
    }

    /// <summary>Let go; safe when nothing is held.</summary>
    public void Release()
    {
        _watch?.Dispose();
        _watch = null;
    }
}
