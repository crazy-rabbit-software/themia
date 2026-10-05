namespace Themia.Core.Settings;

/// <summary>The settings pages that list one group per item: every display, every view. [S-1830]</summary>
public enum SettingsList
{
    Displays,
    Views,
}

/// <summary>
/// [S-1833] Which groups of the Displays and Views pages are open, kept by the settings window for
/// as long as it is open, so building a page again — to catch up with a change made elsewhere
/// ([S-1720]), after a view was added or moved, or on coming back from a widget's page — opens the
/// same groups the reader had open.
/// <para>
/// Only what the reader chose is stored. A group nobody has opened or closed is closed, unless it
/// is the only one in its list: a page with one display opens it, as there is nothing to choose
/// between. A group whose item is gone is forgotten, so a view id that comes back (undo) starts
/// from the default again.
/// </para>
/// </summary>
public sealed class SettingsExpansion
{
    private readonly Dictionary<(SettingsList List, string Item), bool> _chosen = [];

    /// <summary>Whether <paramref name="item"/>'s group is open, in a list of <paramref name="count"/> groups.</summary>
    public bool IsOpen(SettingsList list, string item, int count)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _chosen.TryGetValue((list, item), out var open) ? open : count == 1;
    }

    /// <summary>The reader opened or closed a group (or a link asked for it to be opened, [S-1831]).</summary>
    public void Set(SettingsList list, string item, bool open)
    {
        ArgumentNullException.ThrowIfNull(item);
        _chosen[(list, item)] = open;
    }

    /// <summary>Forget the groups of <paramref name="list"/> whose items no longer exist.</summary>
    public void Retain(SettingsList list, IEnumerable<string> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);

        var keep = existing.ToHashSet(StringComparer.Ordinal);
        foreach (var key in _chosen.Keys.Where(k => k.List == list && !keep.Contains(k.Item)).ToList())
        {
            _chosen.Remove(key);
        }
    }
}
