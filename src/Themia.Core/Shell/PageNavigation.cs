using Themia.Core.Settings;

namespace Themia.Core.Shell;

/// <summary>[S-2302] A row of a page as its navigation sees it: its key, its kind, the rows it opens on and its tools' keys.</summary>
public sealed record NavRow(string Key, SettingsRowKind Kind, IReadOnlyList<NavRow> Rows)
{
    /// <summary>[S-2351] The keys of its tools: a tool that asks first holds its section in this row's card.</summary>
    public IReadOnlyList<string> Tools { get; init; } = [];
}

/// <summary>[S-2301] A section of a page as its navigation sees it: its key over its rows.</summary>
public sealed record NavSection(string Key, IReadOnlyList<NavRow> Rows);

/// <summary>[S-2345] A tab of a page's strip: its key and the keys of the sections it shows.</summary>
public sealed record NavTab(string Key, IReadOnlyList<string> Sections);

/// <summary>[S-2300] A page as its navigation sees it: its sections and its tabs (none, or two and more).</summary>
public sealed record NavPage(IReadOnlyList<NavSection> Sections, IReadOnlyList<NavTab> Tabs)
{
    /// <summary>The row keyed <paramref name="key"/>, at any depth; null where it is not on the page.</summary>
    public NavRow? Find(string key)
    {
        static NavRow? In(IReadOnlyList<NavRow> rows, string key)
        {
            foreach (var row in rows)
            {
                if (row.Key == key)
                {
                    return row;
                }

                if (In(row.Rows, key) is { } found)
                {
                    return found;
                }
            }

            return null;
        }

        return Sections.Select(s => In(s.Rows, key)).FirstOrDefault(r => r is not null);
    }

    /// <summary>[S-2351] The row whose card holds the section keyed <paramref name="key"/> (its own, or the one whose tool it is), at any depth.</summary>
    public NavRow? HolderOf(string key)
    {
        static NavRow? In(IReadOnlyList<NavRow> rows, string key)
        {
            foreach (var row in rows)
            {
                if (row.Key == key || row.Tools.Contains(key))
                {
                    return row;
                }

                if (In(row.Rows, key) is { } found)
                {
                    return found;
                }
            }

            return null;
        }

        return Sections.Select(s => In(s.Rows, key)).FirstOrDefault(r => r is not null);
    }

    /// <summary>The keys from the page's own row down to the row keyed <paramref name="key"/> (the row's last); empty where it is not there.</summary>
    public IReadOnlyList<string> PathTo(string key)
    {
        static bool In(IReadOnlyList<NavRow> rows, string key, List<string> path)
        {
            foreach (var row in rows)
            {
                path.Add(row.Key);
                if (row.Key == key || In(row.Rows, key, path))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
            }

            return false;
        }

        foreach (var section in Sections)
        {
            var path = new List<string>();
            if (In(section.Rows, key, path))
            {
                return path;
            }
        }

        return [];
    }

    /// <summary>[S-2303] The page's own expander that <paramref name="key"/>'s row stands in; null for a row of a section's own.</summary>
    public NavRow? CardOf(string key)
    {
        static bool Holds(IReadOnlyList<NavRow> rows, string key) => rows.Any(r => r.Key == key || Holds(r.Rows, key));
        return Sections.SelectMany(s => s.Rows).FirstOrDefault(r => r.Kind == SettingsRowKind.Expander && r.Key != key && Holds(r.Rows, key));
    }
}

/// <summary>
/// [S-2303] [S-2307] [S-2345] [S-2351] [S-3534] Where a page stands, as a state machine its view only draws: the sub-pages
/// opened (deepest last), the one card opened on the page, the pickers unfolded inside it (each folded until clicked, owner
/// 2026-11-19), the tab chosen and the one section asked in place. Every way it moves is one method here: a way in walking a
/// path (its sub-pages and cards opened, a picker inside a card unfolded, a line that asks asked), a card's head, a tab, a
/// sub-page and back, a page built again whose sub-page went. Pure.
/// </summary>
public sealed class PageNavigation
{
    private readonly List<string> _steps = [];

    private readonly HashSet<string> _unfolded = new(StringComparer.Ordinal);

    /// <summary>The sub-pages opened, by their rows' keys, deepest last.</summary>
    public IReadOnlyList<string> Steps => _steps;

    /// <summary>The sub-page standing in the page's place; null on the page's own level.</summary>
    public string? Deepest => _steps.Count > 0 ? _steps[^1] : null;

    /// <summary>The one card opened on the page (an expander, a picker), by its row's key; null for none.</summary>
    public string? Open { get; private set; }

    /// <summary>[S-2303] The pickers (and lists) unfolded inside the open card, by their rows' keys; emptied as the card closes.</summary>
    public IReadOnlySet<string> Unfolded => _unfolded;

    /// <summary>[S-2345] The tab chosen, by its key, kept while the page lives; null: the first.</summary>
    public string? Tab { get; private set; }

    /// <summary>[S-2351] The one section asked in place, by the key of what asked it.</summary>
    public AskInPlace Asks { get; } = new();

    /// <summary>[S-2308] The kinds of row that open in place: an expander and the inline pickers.</summary>
    public static bool Opens(SettingsRowKind kind)
        => kind is SettingsRowKind.Expander or SettingsRowKind.Choice or SettingsRowKind.Lookup or SettingsRowKind.Colour or SettingsRowKind.Font or SettingsRowKind.Icon;

    /// <summary>[S-2303] Whether the row keyed <paramref name="key"/> stands open: inside a card unfolded, on the page the card open.</summary>
    public bool IsOpen(string key, bool inside) => inside ? _unfolded.Contains(key) : Open == key;

    /// <summary>
    /// [S-3534] A way in along <paramref name="path"/> (the rows' keys, a section's first): [S-2345] the tab its section
    /// stands on chosen, every card along the way opened (inside an opened card a picker unfolds, the card staying the one
    /// open), a sub-page stepped into, [S-2351] a line that asks asked. Returns the row it lands on (the section where no
    /// row of the path is there); null for an empty path.
    /// </summary>
    public string? Enter(NavPage page, IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(path);
        _steps.Clear();
        Fold(null);
        Asks.Cancel();

        if (path.Count > 0 && page.Tabs.FirstOrDefault(t => t.Sections.Contains(path[0])) is { } holding)
        {
            Tab = holding.Key;
        }

        var rows = page.Sections.FirstOrDefault(s => path.Count > 0 && s.Key == path[0])?.Rows;
        var target = path.Count > 0 ? path[0] : null;
        var inCard = false;
        for (var i = 1; i < path.Count && rows is not null; i++)
        {
            var row = rows.FirstOrDefault(r => r.Key == path[i]);
            if (row is null)
            {
                break;
            }

            // Every card along the way opens, the row itself too where it opens (a deep link to a choice shows its options;
            // [S-2351] to a line that asks, its section).
            target = row.Key;
            if (row.Kind == SettingsRowKind.Ask)
            {
                Asks.Show(row.Key);
                if (!inCard)
                {
                    Fold(null);
                }
            }
            else if (row.Kind == SettingsRowKind.SubPage)
            {
                _steps.Add(row.Key);
                Fold(null);
                inCard = false;
            }
            else if (Opens(row.Kind))
            {
                if (inCard)
                {
                    _unfolded.Add(row.Key);
                }
                else
                {
                    Fold(row.Key);
                    inCard = row.Kind == SettingsRowKind.Expander;
                }
            }

            rows = row.Rows;
        }

        return target;
    }

    /// <summary>[S-2307] A page of its own under this one opened (a sub-page row clicked): no card open on it yet.</summary>
    public void Push(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _steps.Add(key);
        Fold(null);
    }

    /// <summary>
    /// [S-2327] Escape and the back head, once no section is asked: the sub-page it stands on closes, else the open card.
    /// Returns what went back (the card's key for a card closed).
    /// </summary>
    public NavBack Back()
    {
        if (_steps.Count > 0)
        {
            _steps.RemoveAt(_steps.Count - 1);
            return new NavBack(NavBackKind.SubPage, null);
        }

        if (Open is { } open)
        {
            Fold(null);
            return new NavBack(NavBackKind.Card, open);
        }

        return new NavBack(NavBackKind.None, null);
    }

    /// <summary>
    /// [S-2303] [S-2308] A card's head clicked (an expander, a picker): <paramref name="inside"/> an opened card it unfolds or
    /// folds, the card staying open; on the page it opens as the one card open, or closes, and [S-2351] the section asked
    /// closes (one open at a time). Returns whether it is open now.
    /// </summary>
    public bool ToggleCard(string key, bool inside)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (inside)
        {
            if (!_unfolded.Remove(key))
            {
                _unfolded.Add(key);
                return true;
            }

            return false;
        }

        var opening = Open != key;
        Fold(opening ? key : null);
        Asks.Cancel();
        return opening;
    }

    /// <summary>
    /// [S-2328] A click that opens the row keyed <paramref name="key"/> where it stands: a picker inside a card its card
    /// opened and it unfolded in it, a card of the page's own opened alone.
    /// </summary>
    public void Reach(NavPage page, string key)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.CardOf(key) is { } card)
        {
            Open = card.Key;
            _unfolded.Add(key);
        }
        else
        {
            Fold(key);
        }
    }

    /// <summary>[S-2345] A tab chosen: no card open, nothing asked on it.</summary>
    public void ChooseTab(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        Tab = key;
        Fold(null);
        Asks.Cancel();
    }

    /// <summary>[S-2345] The tab shown of <paramref name="page"/>'s: the one chosen where it is still there, else the first; kept as chosen.</summary>
    public NavTab? ShownTab(NavPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.Tabs.Count == 0)
        {
            return null;
        }

        var tab = page.Tabs.FirstOrDefault(t => t.Key == Tab) ?? page.Tabs[0];
        Tab = tab.Key;
        return tab;
    }

    /// <summary>
    /// [S-2351] A click on what asks (an asking row's head, a tool that asks first): its section opens in the card of
    /// <paramref name="holder"/> (on the page that card the one open: any other closes), or, open, closes. Returns whether
    /// it is open now.
    /// </summary>
    public bool ToggleAsk(string key, string holder, bool inside)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(holder);
        if (!Asks.Toggle(key))
        {
            return false;
        }

        if (!inside && Open != holder)
        {
            Fold(null);
        }

        return true;
    }

    /// <summary>
    /// [S-2351] [S-3727] A question asked after a step of its own at the row keyed <paramref name="at"/>: the cards it stands
    /// in open (a picker inside one unfolded, the card's other unfolded pickers kept where the card was open already); on the
    /// page itself it is the one card open.
    /// </summary>
    public void AskAt(NavPage page, string at)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(at);
        Asks.Show(at);
        var path = page.PathTo(at);
        if (path.Count > 1)
        {
            if (Open != path[0])
            {
                Fold(path[0]);
            }

            foreach (var key in path.Skip(1).Take(path.Count - 2))
            {
                _unfolded.Add(key);
            }
        }
        else if (Open != at)
        {
            Fold(null);
        }
    }

    /// <summary>
    /// [S-2328] A render's click on a line that asks keyed <paramref name="key"/>: the cards along the way opened as a click on
    /// each would (the first the one open, the ones under it unfolded), for its section to be asked in place. True where the
    /// line stands inside a card.
    /// </summary>
    public bool OpenAlong(NavPage page, string key)
    {
        ArgumentNullException.ThrowIfNull(page);
        var path = page.PathTo(key);
        if (path.Count <= 1)
        {
            return false;
        }

        Fold(path[0]);
        foreach (var on in path.Skip(1).Take(path.Count - 2))
        {
            _unfolded.Add(on);
        }

        return true;
    }

    /// <summary>
    /// [S-2361] A page built again: a sub-page whose row went closes (deepest first); [S-2351] a section whose row went
    /// (a view deleted, a list's item removed) goes with it. True where the section went.
    /// </summary>
    public bool Prune(NavPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        while (_steps.Count > 0 && page.Find(_steps[^1]) is null)
        {
            _steps.RemoveAt(_steps.Count - 1);
        }

        return Asks.Open is { } asked && page.HolderOf(asked) is null && Asks.Cancel();
    }

    /// <summary>[S-3557] The page goes: no sub-page, nothing asked; its card and tab are kept for its next showing.</summary>
    public void Close()
    {
        _steps.Clear();
        Asks.Cancel();
    }

    /// <summary>The one card open now <paramref name="open"/> (or none), every picker in it folded.</summary>
    private void Fold(string? open)
    {
        Open = open;
        _unfolded.Clear();
    }
}

/// <summary>[S-2327] What a step back closed.</summary>
public enum NavBackKind
{
    /// <summary>Nothing: the page was on its own level with no card open.</summary>
    None,

    /// <summary>The sub-page it stood on.</summary>
    SubPage,

    /// <summary>The open card (<see cref="NavBack.Card"/>).</summary>
    Card,
}

/// <summary>[S-2327] What a step back closed, and the card's key where it closed one.</summary>
public readonly record struct NavBack(NavBackKind Kind, string? Card);
