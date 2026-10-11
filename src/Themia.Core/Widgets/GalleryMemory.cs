using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-3410] What one shared gallery source reads: a folder (with or without the folders below it), one
/// picture file, or the photo of the day. Every widget whose settings name the same one shares it:
/// one walk, one watcher, one answer. <see cref="Id"/> is the identity: the kind, the recursion and
/// the path compared as Windows compares paths (case and a trailing separator do not matter).
/// </summary>
/// <param name="Source">Which kind of source.</param>
/// <param name="Path">The validated path as configured (empty for the photo of the day).</param>
/// <param name="Recursive">A folder's subfolders are read too (folders only).</param>
public sealed record GallerySourceKey(GallerySource Source, string Path, bool Recursive)
{
    public static GallerySourceKey Bing { get; } = new(GallerySource.Bing, string.Empty, false);

    /// <summary>The source a validated configuration reads.</summary>
    public static GallerySourceKey For(GallerySource source, string path, bool recursive)
        => source switch
        {
            GallerySource.Bing => Bing,
            GallerySource.File => new(GallerySource.File, path ?? string.Empty, false),
            _ => new(GallerySource.Folder, path ?? string.Empty, recursive),
        };

    /// <summary>The identity the sources are kept under.</summary>
    public string Id => Source switch
    {
        GallerySource.Bing => "bing",
        GallerySource.File => "file|" + Normal(Path),
        _ => (Recursive ? "tree|" : "folder|") + Normal(Path),
    };

    private static string Normal(string path)
    {
        var trimmed = path.Trim().TrimEnd('\\', '/');

        // A drive root keeps its separator: "C:" alone means the current directory on that drive.
        if (trimmed.Length == 2 && trimmed[1] == ':')
        {
            trimmed += "\\";
        }

        return trimmed.Replace('/', '\\').ToUpperInvariant();
    }
}

/// <summary>Why a source was read again.</summary>
public enum GalleryRefresh
{
    /// <summary>The source's first read.</summary>
    Initial = 0,

    /// <summary>[S-3410] Its folder watcher saw a file come, go or be renamed.</summary>
    Changed = 1,

    /// <summary>[S-772] The configured refresh interval came round.</summary>
    Scheduled = 2,

    /// <summary>[S-773] "Refresh images".</summary>
    Forced = 3,
}

/// <summary>[S-3412] When a widget deals a new selection (a new shuffle seed) from its source.</summary>
public static class GalleryDeal
{
    /// <summary>
    /// "Refresh images" always deals again. The refresh interval deals again only for a shuffled
    /// folder larger than the widget shows, so over time it shows all of it. A folder change, a first
    /// read and a widget built again (a view switch) never do: the pictures stay where they were.
    /// </summary>
    public static bool ShouldReroll(GalleryRefresh reason, bool shuffle, int found, int max)
        => reason switch
        {
            GalleryRefresh.Forced => true,
            GalleryRefresh.Scheduled => shuffle && found > Math.Max(1, max),
            _ => false,
        };

    /// <summary>
    /// [S-3412] Which slide to show after the pictures changed or the widget came back: the picture
    /// it showed, wherever it is now; else the same position, kept inside the list; else the first.
    /// </summary>
    public static int Resume(IReadOnlyList<string> pictures, string? path, int index)
    {
        ArgumentNullException.ThrowIfNull(pictures);

        if (pictures.Count == 0)
        {
            return 0;
        }

        if (path is not null)
        {
            for (var i = 0; i < pictures.Count; i++)
            {
                if (string.Equals(pictures[i], path, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        return Math.Clamp(index, 0, pictures.Count - 1);
    }
}

/// <summary>[S-3412] Where one widget's gallery was: the picture shown, the deal, and whether it was paused.</summary>
/// <param name="SourceId">The <see cref="GallerySourceKey.Id"/> it was showing.</param>
/// <param name="Path">The picture on show (the slide), or null.</param>
/// <param name="Index">Its position in the selection.</param>
/// <param name="Seed">The seed its selection and its scatter / mosaic were dealt from.</param>
/// <param name="Paused">The user paused the slideshow (kept for the session, never persisted, [S-769]).</param>
public sealed record GalleryPosition(string SourceId, string? Path, int Index, int Seed, bool Paused);

/// <summary>
/// [S-3412] Each gallery widget's place, by widget id, for the app's lifetime (never saved): a view
/// switch builds the widget again, and it comes back on the picture it showed with the same deal.
/// Bounded: past <see cref="Capacity"/> the least recently remembered goes.
/// </summary>
public sealed class GalleryPositions
{
    public const int Capacity = 256;

    private readonly Dictionary<string, LinkedListNode<(string Id, GalleryPosition Position)>> _byId = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Id, GalleryPosition Position)> _order = new();

    public int Count => _byId.Count;

    public void Remember(string widgetId, GalleryPosition position)
    {
        ArgumentException.ThrowIfNullOrEmpty(widgetId);
        ArgumentNullException.ThrowIfNull(position);
        Forget(widgetId);
        _byId[widgetId] = _order.AddFirst((widgetId, position));

        while (_byId.Count > Capacity && _order.Last is { } oldest)
        {
            _byId.Remove(oldest.Value.Id);
            _order.RemoveLast();
        }
    }

    public GalleryPosition? Recall(string? widgetId)
        => widgetId is not null && _byId.TryGetValue(widgetId, out var node) ? node.Value.Position : null;

    public void Forget(string widgetId)
    {
        if (_byId.Remove(widgetId, out var node))
        {
            _order.Remove(node);
        }
    }
}
