using Themia.Core.Geometry;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-5591] Where an item that is about to arrive should land: the view shown on the display it was
/// dropped (or pasted, or made with New ›) on, and the point there, as fractions of that display
/// (none: the next free cell of that view, a paste with no press point).
/// <paramref name="Names"/> are the file names the arrival is expected under (a drop's files); empty
/// means "whatever arrives next" (a paste or New › from the background menu, whose names the shell
/// chooses). [S-5552] <paramref name="Renames"/>: the command was one of New ›'s, so what it made
/// opens its rename box once it lands, as Explorer's New › does. [S-13005] <paramref name="Fence"/>: the fence (its
/// widget id) the arrivals go into instead of the free desktop: a drop on a fence, a paste or a new folder in it.
/// </summary>
public sealed record DesktopLandingHint(string ViewId, double? X, double? Y, IReadOnlyList<string> Names, bool Renames = false, string? Fence = null)
{
    /// <summary>[S-13102] The file operation that brings the arrivals, when its undo is the desktop's (a paste, a drop, New › Folder); null for none.</summary>
    public DesktopFileUndoKind? Undo { get; init; }

    /// <summary>[S-13102] Where a copy's or a move's files came from (full paths), matched to the arrivals by name.</summary>
    public IReadOnlyList<string> Sources { get; init; } = [];
}

/// <summary>
/// [S-5591] Where one arrival lands: the view, and the point its cell is looked for from (null: the first
/// free cell). [S-5552] <paramref name="Renames"/>: it is what New › made, to be renamed in place. [S-13005]
/// <paramref name="Fence"/>: the fence it goes into, on that view.
/// </summary>
public readonly record struct DesktopLandingSpot(string ViewId, double? X, double? Y, bool Renames = false, string? Fence = null)
{
    /// <summary>[S-13102] The hint it came from: the arrivals of one hint with an undo are one step of the log.</summary>
    public DesktopLandingHint? Hint { get; init; }
}

/// <summary>
/// [S-5591] The landings expected, as pure rules: a drop on the desktop says which names it brings and
/// where; the reconcile that sees them arrive asks here where each goes. A hint is used up by the
/// arrivals it names, or (with no names) by the next reconcile that has arrivals at all; a newer hint
/// replaces an older one, so nothing waits on a clock and no stale hint outlives the next gesture.
/// </summary>
public sealed class DesktopLandings
{
    private DesktopLandingHint? _hint;
    private HashSet<string> _waiting = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The hint waiting, or null.</summary>
    public DesktopLandingHint? Pending => _hint;

    /// <summary>A gesture expects arrivals: it replaces any earlier hint.</summary>
    public void Expect(DesktopLandingHint hint)
    {
        ArgumentNullException.ThrowIfNull(hint);
        _hint = hint;
        _waiting = new HashSet<string>(hint.Names.Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// [S-5591] A new gesture began (a press on the desktop): a hint that names nothing (a paste's, New ›'s)
    /// is dropped, so a command that brought nothing (Display settings) does not land a later download
    /// at its point; a drop's named hint stays until its files arrive.
    /// </summary>
    public void ClearOpen()
    {
        if (_hint is { Names.Count: 0 })
        {
            Clear();
        }
    }

    /// <summary>Forget the hint (the view went).</summary>
    public void Clear()
    {
        _hint = null;
        _waiting.Clear();
    }

    /// <summary>
    /// Where each of <paramref name="arrivals"/> (one reconcile's, in its order) lands: the hint's view
    /// and point for the ones it names (or for all of them, a hint with no names), null for the rest
    /// (the primary display's next free cell, [S-5520]). The hint is used up as described above.
    /// </summary>
    public IReadOnlyList<DesktopLandingSpot?> Place(IReadOnlyList<DesktopItem> arrivals)
    {
        ArgumentNullException.ThrowIfNull(arrivals);
        var spots = new DesktopLandingSpot?[arrivals.Count];
        if (_hint is not { } hint || arrivals.Count == 0)
        {
            return spots;
        }

        // [S-5552] New › makes one item: the first arrival it lands is the one renamed.
        var any = hint.Names.Count == 0;
        var renames = hint.Renames;
        for (var i = 0; i < arrivals.Count; i++)
        {
            var name = DesktopRename.FileName(arrivals[i].Key);
            if (any || _waiting.Remove(name))
            {
                spots[i] = new DesktopLandingSpot(hint.ViewId, hint.X, hint.Y, renames, hint.Fence) { Hint = hint };
                renames = false;
            }
        }

        if (any || _waiting.Count == 0)
        {
            Clear();
        }

        return spots;
    }
}

/// <summary>
/// [S-5547] Explorer's view settings the desktop follows: hidden items and protected operating-system
/// files, as the enumeration flags of the desktop's namespace (the shown names follow "File name
/// extensions" by themselves: the shell's display names do).
/// </summary>
public static class DesktopViewSettings
{
    /// <summary>SHCONTF_FOLDERS | SHCONTF_NONFOLDERS.</summary>
    public const uint Always = 0x20 | 0x40;

    /// <summary>SHCONTF_INCLUDEHIDDEN.</summary>
    public const uint IncludeHidden = 0x80;

    /// <summary>SHCONTF_INCLUDESUPERHIDDEN.</summary>
    public const uint IncludeSuperHidden = 0x10000;

    /// <summary>
    /// The flags for Explorer's settings: hidden items when "Hidden items" is on, and protected system
    /// files only when "Hide protected operating system files" is off too (Explorer shows them only then).
    /// </summary>
    public static uint EnumFlags(bool showHidden, bool showSuperHidden)
        => Always | (showHidden ? IncludeHidden : 0) | (showHidden && showSuperHidden ? IncludeSuperHidden : 0);
}
