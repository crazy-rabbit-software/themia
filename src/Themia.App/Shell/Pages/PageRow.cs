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

    /// <summary>[S-14322] What its picker holds pictured at its head's end, before what it says (an icon picker's chosen icon).</summary>
    public Func<UIElement?>? Shows { get; init; }

    /// <summary>
    /// [S-2317] Its icon is its own colour or picture (a view's mark, an app's icon), not a glyph naming the setting: it
    /// stays on a row inside a card, where a setting's glyph does not (Karpo's §644: a card's lines no glyph).
    /// </summary>
    public bool Marked { get; init; }

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

    /// <summary>A text box's; [S-2351] an asking row's line of text (its field and its accept, in place).</summary>
    public PageEdit? Edit { get; init; }

    /// <summary>[S-2351] An asking row's question (an "are you sure?"), asked in place in its card.</summary>
    public PageQuestion? Question { get; init; }

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

    /// <summary>[S-1862] The row the card's Set up button opens the page at.</summary>
    public bool SetsUp { get; init; }

    /// <summary>
    /// [S-2318] A button standing in the row's line at its end (Karpo's <c>OptionRow.Beside</c>, §638: Lometics' site
    /// beside its name): its words, its glyph, what it does. Null for none.
    /// </summary>
    public PageRow? Beside { get; init; }
}

/// <summary>
/// [S-2302] A text box's: its hint, what it starts with, what keeping it does; why a text cannot be kept
/// (null when it can), whether an empty box may be kept, what each letter does as it is typed (a form's
/// field), whether it is a password's (its box never shows what it holds: <see cref="Fill"/> gives it
/// again, <see cref="Wipe"/> runs as its page closes), digits only, and [S-2351] where it is asked in place
/// (an asking row), the words of the button that gives it.
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

    /// <summary>[S-3721] Why <paramref name="typed"/> cannot be kept (empty words: nothing said); null when it can.</summary>
    public string? Reason(string typed) => AskInPlace.Reason(typed, AllowEmpty, Refuse);
}

/// <summary>
/// [S-2351] An "are you sure?" asked in place, in the card of what asks it: the question, the words of the button that
/// does it, what it does, whether it destroys something, and what goes with it.
/// </summary>
internal sealed record PageQuestion(string Question, string Accept, Action Done, bool Danger, string? Detail);

/// <summary>
/// [S-2302] An icon button at a thing's head or an item's end (Duplicate, Delete, Sign out), its key while the row has the
/// keyboard; [S-2351] one that asks first opens its <see cref="Question"/> in place in its card (keyed by its own key).
/// </summary>
internal sealed record PageTool(string Key, string Glyph, string Name, Windows.System.VirtualKey? Shortcut, Action Run, bool Disabled = false, PageQuestion? Question = null);

/// <summary>[S-2301] A section of a page as drawn: its heading over its rows.</summary>
internal sealed record PageSection(string Key, string? Heading, IReadOnlyList<PageRow> Rows);

/// <summary>[S-2340] The selection page's header card: the kind's glyph on a tile, the name over a caption, icon buttons at its end.</summary>
internal sealed record PageHeader(string Glyph, string Name, string Caption, IReadOnlyList<PageTool> Tools);

/// <summary>[S-2345] A tab of a page's strip: its words and the keys of the sections it shows.</summary>
internal sealed record PageTab(string Key, string Title, IReadOnlyList<string> Sections);

/// <summary>
/// [S-2300] A page as drawn: its sections, under a header card where it has one; [S-2345] where it has
/// tabs, a strip under the header card and the chosen tab's sections under it.
/// </summary>
internal sealed record PageModel(string Key, IReadOnlyList<PageSection> Sections)
{
    public PageHeader? Header { get; init; }

    /// <summary>[S-2345] Its tabs, two or more; empty: every section shows.</summary>
    public IReadOnlyList<PageTab> Tabs { get; init; } = [];
}
