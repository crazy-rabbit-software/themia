using Microsoft.UI.Xaml;
using Themia.Core.Settings;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-2302] One row of a page as the page draws it: the settings' lines and the selection's entries both
/// come to this, so a row that does the same is drawn the same whoever built it. Its
/// <see cref="Kind"/> is Core's (<see cref="SettingsRows.KindOf"/> for a settings line).
/// </summary>
internal sealed record PageRow(string Key, SettingsRowKind Kind)
{
    public string Text { get; init; } = string.Empty;

    /// <summary>A line under the name (a row 60 tall).</summary>
    public string? Caption { get; init; }

    /// <summary>What it holds, at its end or on its picker's head.</summary>
    public string? Value { get; init; }

    /// <summary>Its icon, built as the row is; null for none.</summary>
    public Func<UIElement?>? Icon { get; init; }

    /// <summary>A toggle's state.</summary>
    public bool On { get; init; }

    /// <summary>A toggle's flip.</summary>
    public Action<bool>? Toggle { get; init; }

    /// <summary>A radio's: it holds.</summary>
    public bool Holds { get; init; }

    /// <summary>What a click does (an action, a link, a radio's pick).</summary>
    public Action? Run { get; init; }

    public bool Disabled { get; init; }

    /// <summary>Destroys something: in the critical colour.</summary>
    public bool Danger { get; init; }

    /// <summary>Something is being done (a ring before a note's words).</summary>
    public bool Busy { get; init; }

    /// <summary>An expander's or a sub-page's rows, a choice's options.</summary>
    public IReadOnlyList<PageRow>? Rows { get; init; }

    /// <summary>A number's range, grain, words and store.</summary>
    public MenuSlider? Number { get; init; }

    /// <summary>A text box's.</summary>
    public PageEdit? Edit { get; init; }

    /// <summary>A colour's or a fill's.</summary>
    public MenuColour? Colour { get; init; }

    /// <summary>A font's or an icon's picker, built as it opens.</summary>
    public Func<FrameworkElement>? Picker { get; init; }

    /// <summary>A lookup that asks (a place): its box, its answers.</summary>
    public MenuSearch? Search { get; init; }

    /// <summary>A lookup over its own options: the box's hint.</summary>
    public string? Filter { get; init; }

    /// <summary>Words a lookup finds an option by besides its own.</summary>
    public string? Keywords { get; init; }

    /// <summary>An item's icon buttons.</summary>
    public IReadOnlyList<PageTool>? Tools { get; init; }

    /// <summary>An item's place in its list.</summary>
    public MenuOrder? Order { get; init; }

    /// <summary>A link's owner (its tooltip: "Opens Accounts").</summary>
    public string? Owner { get; init; }

    /// <summary>Its click asks first (a line of text, an "are you sure?"): a chevron at its end.</summary>
    public bool Asks { get; init; }

    /// <summary>[S-1862] The row the card's Set up button opens the page at.</summary>
    public bool SetsUp { get; init; }

    /// <summary>[S-11755] A sub-page that is a whole page of its own (a group's member: its header card and sections) rather than rows.</summary>
    public PageModel? Page { get; init; }
}

/// <summary>
/// [S-2302] A text box's: its hint, what it starts with, what keeping it does; why a text cannot be kept
/// (null when it can), whether an empty box may be kept, what each letter does as it is typed (a form's
/// field), whether it is a password's (its box never shows what it holds: <see cref="Fill"/> gives it
/// again, <see cref="Wipe"/> runs as its page closes), digits only, and the words of the button that keeps
/// it and the title of the page it is asked on, where it is asked on a page of its own.
/// </summary>
internal sealed record PageEdit(string Placeholder, string Initial, Action<string> Commit)
{
    public Func<string, string?>? Refuse { get; init; }

    public bool AllowEmpty { get; init; } = true;

    public Action<string>? Live { get; init; }

    public bool Masked { get; init; }

    public Func<string>? Fill { get; init; }

    public Action? Wipe { get; init; }

    public bool Numeric { get; init; }

    public string? Accept { get; init; }

    public string? Title { get; init; }

    /// <summary>Why <paramref name="typed"/> cannot be kept; null when it can.</summary>
    public string? Reason(string typed) => !AllowEmpty && string.IsNullOrWhiteSpace(typed) ? string.Empty : Refuse?.Invoke(typed);
}

/// <summary>[S-2302] An "are you sure?" on a page of its own: the question, the words of the button that does it, what it does.</summary>
internal sealed record PageQuestion(string Question, string Accept, Action Done, bool Danger, string? Detail);

/// <summary>[S-2302] An icon button at an item's end (Rename, Delete), its key while the item has the keyboard.</summary>
internal sealed record PageTool(string Key, string Glyph, string Name, Windows.System.VirtualKey? Shortcut, Action Run, bool Disabled = false);

/// <summary>[S-2301] A section of a page as drawn: its heading over its rows.</summary>
internal sealed record PageSection(string Key, string? Heading, IReadOnlyList<PageRow> Rows);

/// <summary>[S-2340] The selection page's header card: the kind's glyph on a tile, the name over a caption, icon buttons at its end.</summary>
internal sealed record PageHeader(string Glyph, string Name, string Caption, IReadOnlyList<PageTool> Tools)
{
    /// <summary>[S-11754] The name as a box that renames (a group's): its hint, what it holds, what keeping it does. Null: words only.</summary>
    public PageEdit? Rename { get; init; }
}

/// <summary>[S-2300] A page as drawn: its sections, under a header card where it has one.</summary>
internal sealed record PageModel(string Key, IReadOnlyList<PageSection> Sections)
{
    public PageHeader? Header { get; init; }
}
