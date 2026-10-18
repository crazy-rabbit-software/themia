namespace Themia.Core.Widgets;

/// <summary>A place in the folder widget's history and how far down it was scrolled ([S-1326]).</summary>
public readonly record struct FolderPlace(string Path, double Scroll);

/// <summary>
/// [S-1326] Navigate mode's Back / Forward / Up / Home, as a browser's history: going somewhere new
/// clears Forward; Back and Forward restore the scroll position the place was left at ([S-57] in the
/// original). Pure.
/// </summary>
public sealed class FolderHistory
{
    /// <summary>How far Back reaches; older places fall off the end.</summary>
    public const int Depth = 50;

    private readonly List<FolderPlace> _back = [];
    private readonly List<FolderPlace> _forward = [];

    public FolderHistory(string home)
    {
        ArgumentNullException.ThrowIfNull(home);
        Home = home;
        Current = home;
    }

    /// <summary>The configured folder: Home goes here, and the widget starts here.</summary>
    public string Home { get; private set; }

    public string Current { get; private set; }

    public bool CanGoBack => _back.Count > 0;

    public bool CanGoForward => _forward.Count > 0;

    /// <summary>Whether Up has somewhere to go: Current has a parent.</summary>
    public bool CanGoUp => Parent(Current) is not null;

    public bool AtHome => string.Equals(Current, Home, StringComparison.OrdinalIgnoreCase);

    /// <summary>The setting changed: start over at the new folder, history gone.</summary>
    public void Reset(string home)
    {
        ArgumentNullException.ThrowIfNull(home);
        Home = home;
        Current = home;
        _back.Clear();
        _forward.Clear();
    }

    /// <summary>Open <paramref name="path"/> from <paramref name="scroll"/> where Current is scrolled to. False when it is already Current.</summary>
    public bool Go(string path, double scroll)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (string.Equals(path, Current, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _back.Add(new FolderPlace(Current, scroll));

        if (_back.Count > Depth)
        {
            _back.RemoveAt(0);
        }

        _forward.Clear();
        Current = path;
        return true;
    }

    public FolderPlace? Back(double scroll) => Step(_back, _forward, scroll);

    public FolderPlace? Forward(double scroll) => Step(_forward, _back, scroll);

    /// <summary>The parent of Current, or null at a root.</summary>
    public string? UpTarget() => Parent(Current);

    /// <summary>The parent folder of a path, or null for a drive or share root.</summary>
    public static string? Parent(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.TrimEnd('\\', '/');
        var cut = trimmed.LastIndexOfAny(['\\', '/']);

        if (cut <= 0)
        {
            return null;
        }

        // "C:\Users" → "C:\"; a share root ("\\server\share") has no parent the widget can list.
        if (trimmed.StartsWith(@"\\", StringComparison.Ordinal) && trimmed[2..].Count(c => c == '\\') <= 1)
        {
            return null;
        }

        var parent = trimmed[..cut];
        return parent.Length == 2 && parent[1] == ':' ? parent + "\\" : parent;
    }

    /// <summary>[S-1323] The breadcrumb: every folder from the root down to <paramref name="path"/>, with its label.</summary>
    public static IReadOnlyList<(string Label, string Path)> Crumbs(string path)
    {
        var crumbs = new List<(string, string)>();

        for (var at = path; at is not null; at = Parent(at))
        {
            var trimmed = at.TrimEnd('\\', '/');
            var cut = trimmed.LastIndexOfAny(['\\', '/']);
            var label = cut >= 0 && Parent(at) is not null ? trimmed[(cut + 1)..] : trimmed;
            crumbs.Insert(0, (label, at));
        }

        return crumbs;
    }

    private FolderPlace? Step(List<FolderPlace> from, List<FolderPlace> to, double scroll)
    {
        if (from.Count == 0)
        {
            return null;
        }

        var place = from[^1];
        from.RemoveAt(from.Count - 1);
        to.Add(new FolderPlace(Current, scroll));
        Current = place.Path;
        return place;
    }
}
