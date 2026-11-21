using Themia.Sdk.WinUI;
using Themia.Core.Model;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3581] A picture a line of the settings menu shows in its icon column instead of a glyph: the
/// app's own mark and the studio's, drawn by the app from the files beside <c>Themia.exe</c>.
/// </summary>
public enum MenuPicture
{
    /// <summary><c>Assets/themia-logo.png</c>, the mark of the app's name card.</summary>
    ThemiaLogo,

    /// <summary><c>Assets/lometics.svg</c>, the studio's mark.</summary>
    Lometics,
}

/// <summary>
/// [S-3524] One line of the edit bar's settings menu, as data: what it shows and what a pick does.
/// The app draws every one with the menu engine the selection's page is drawn with ([S-3716]); the
/// rules (which lines a section has, their choices, their words, what each writes) are here, so they
/// are tested without a window. <see cref="Key"/> names the line across rebuilds and languages
/// (a path: "appearance/cards/radius"), so an open submenu stays open through a change of words.
/// </summary>
public abstract record SettingsLine(string Key)
{
    /// <summary>Shown but not clickable.</summary>
    public bool Disabled { get; init; }

    /// <summary>
    /// [S-2375] Why it is greyed, said under its name while <see cref="Disabled"/> (Karpo's <c>OptionRow.Why</c>, §309:
    /// "greyed, with why under it"); null for nothing said.
    /// </summary>
    public string? Why { get; init; }

    /// <summary>[S-3810] The colour its glyph is drawn in (<c>#RRGGBB</c>: a view's mark); null for the theme's.</summary>
    public string? GlyphColour { get; init; }

    /// <summary>[S-3581] A picture in its icon column instead of a glyph (the app's mark, the studio's).</summary>
    public MenuPicture? Picture { get; init; }

    /// <summary>
    /// [S-2317] Its glyph is its thing's own mark (a view's), not one naming the setting: a card's line keeps it where a
    /// setting's glyph is left out (Karpo's §644). A colour or a picture is always the line's own.
    /// </summary>
    public bool Marked { get; init; }

    /// <summary>
    /// [S-1862] Where what its widget waits for is set (a place, a feed, a target): the card's Set up button opens the page
    /// at this line (<c>WidgetMenuLines.SetsUp</c>). One line per widget.
    /// </summary>
    public bool SetsUp { get; init; }

    /// <summary>
    /// [S-11753] The shared setting (<c>SharedSettings</c>, by its key) a widget's line sets: the page of several finds it
    /// among a member's lines and shows it once for every member that holds the setting. Null for a line of its own.
    /// </summary>
    public string? Shares { get; init; }

    /// <summary>[S-6416] A file whose shell icon is the line's icon in place of its glyph (a shortcut's target).</summary>
    public string? IconFile { get; init; }

    /// <summary>A divider between a widget's groups of lines (its key is given by where it stands).</summary>
    public static SettingsLine Separator { get; } = new SettingsSeparator("-");
}

/// <summary>A divider between groups; leading, trailing and doubled ones are dropped.</summary>
public sealed record SettingsSeparator(string Key) : SettingsLine(Key);

/// <summary>
/// [S-3740] A heading over a group (Karpo's): its words in the 12-DIP semibold, never picked. [S-2349] Inside a card one
/// with a <see cref="Glyph"/> heads a group (Karpo's §685): its glyph and words a line of their own, the rows under it to
/// the next heading or divider a step in.
/// </summary>
public sealed record SettingsHeading(string Key, string Text) : SettingsLine(Key)
{
    /// <summary>[S-2349] The glyph of a group's head inside a card; null for a plain heading.</summary>
    public string? Glyph { get; init; }
}

/// <summary>[S-3728] Words only, greyed, never clickable; nothing at all while <see cref="Text"/> is empty.</summary>
public sealed record SettingsNote(string Key, string Text) : SettingsLine(Key)
{
    /// <summary>[S-3561] What went wrong (a try's refusal): in the critical colour.</summary>
    public bool Critical { get; init; }

    /// <summary>[S-3561] Something is being done (a sign-in waiting): a ring before the words.</summary>
    public bool Busy { get; init; }
}

/// <summary>
/// [S-3561] A reading: words and a value at the line's end in the text colour, never clickable (what
/// an account holds on this PC: "Mail · 1,240 messages").
/// </summary>
public sealed record SettingsReading(string Key, string Text, string? Glyph, string? Value) : SettingsLine(Key)
{
    /// <summary>[S-3580] A line under its words (Karpo's note card, §579: Themia's version, what Lometics is).</summary>
    public string? Caption { get; init; }

    /// <summary>[S-2318] A button standing in its line at its end (Karpo's <c>Beside</c>, §638: Lometics' site); null for none.</summary>
    public SettingsCommand? Beside { get; init; }
}

/// <summary>
/// [S-3561] [S-3736] A line of text typed in the menu itself (a form's field): a row of its words and
/// <see cref="Value"/>, whose level is the field with its hint, and what a change does (each letter, as it is typed).
/// A <see cref="Masked"/> field is a password's ([S-3556]): it never carries what it holds
/// (<see cref="Value"/> is null; <see cref="HasValue"/> says whether there is one), <see cref="Fill"/>
/// gives a box built again what was typed, and <see cref="Wipe"/> runs as its submenu closes.
/// </summary>
public sealed record SettingsField(string Key, string Text, string? Value, string Placeholder, Action<string> Set) : SettingsLine(Key)
{
    public bool Masked { get; init; }

    /// <summary>[S-3556] A masked field holds something (its box keeps it while it is drawn).</summary>
    public bool HasValue { get; init; }

    /// <summary>[S-3556] What a masked box built again starts with; null for nothing.</summary>
    public Func<string>? Fill { get; init; }

    /// <summary>[S-3556] [S-3557] Its form's level closed: what it held is wiped.</summary>
    public Action? Wipe { get; init; }

    /// <summary>A number (a port): the touch keyboard shows digits.</summary>
    public bool Numeric { get; init; }
}

/// <summary>A switch: ticked while on; a click turns it the other way. <see cref="Value"/> at its end (a state Windows reports).</summary>
public sealed record SettingsToggle(string Key, string Text, string? Glyph, bool On, Action<bool> Set) : SettingsLine(Key)
{
    public string? Value { get; init; }
}

/// <summary>[S-3718] One option of a choice of one: the dot while it holds. Picking the one that holds changes nothing.</summary>
public sealed record SettingsRadio(string Key, string Text, bool Holds, Action Pick) : SettingsLine(Key)
{
    /// <summary>Its glyph (a view's), or none.</summary>
    public string? Glyph { get; init; }

    /// <summary>[S-3537] Words its choice's box finds it by besides its own (a language's English name and code).</summary>
    public string? Keywords { get; init; }

    /// <summary>[S-1429] What it says of itself beside its words (a list's count); its row does not draw it yet.</summary>
    public string? Value { get; init; }
}

/// <summary>[S-3718] A choice of one: a line whose submenu is its options, the one that holds at its end.</summary>
public sealed record SettingsChoice(string Key, string Text, string? Glyph, IReadOnlyList<SettingsRadio> Options) : SettingsLine(Key)
{
    /// <summary>[S-3636] Lines its picker holds after its options (Custom…, asking for a value of its own).</summary>
    public IReadOnlyList<SettingsLine> More { get; init; } = [];

    private readonly string? _value;

    /// <summary>
    /// What the line says at its end: the option that holds, or what it is set to say in its place
    /// ([S-3536]: what a "Windows setting (…)" option gives, which fits beside the line's words).
    /// </summary>
    public string? Value
    {
        get => _value ?? Options.FirstOrDefault(o => o.Holds)?.Text;
        init => _value = value;
    }

    /// <summary>
    /// [S-3537] The hint of a box on top of its submenu that keeps the options whose words or
    /// keywords hold what is typed (<see cref="SettingsMenu.Matches"/>); null for a plain choice.
    /// </summary>
    public string? Filter { get; init; }
}

/// <summary>[S-3731] A number set by feel: an inline slider, stored once on the release.</summary>
public sealed record SettingsSlider(string Key, string Text, string? Glyph, double Value, double Min, double Max, double Step, Func<double, string> Format, Action<double> Set) : SettingsLine(Key)
{
    /// <summary>[S-11751] What it says at its end in place of its value as <see cref="Format"/> writes it (Mixed); null for that.</summary>
    public string? ValueText { get; init; }
}

/// <summary>
/// [S-3724] A colour (<c>#RRGGBB</c>, empty for the default): the one colour submenu. <see cref="DefaultText"/>
/// is Default's words (null where a colour is required: no Default, the first swatch while none holds);
/// <see cref="DefaultIsWindowsAccent"/> paints Default's swatch in the Windows accent, <see cref="DefaultHex"/> in a colour
/// of its own (a calendar's automatic colour).
/// </summary>
public sealed record SettingsColour(string Key, string Text, string Current, Action<string> Set, string? DefaultText, IReadOnlyList<ColorPreset> Presets) : SettingsLine(Key)
{
    public bool DefaultIsWindowsAccent { get; init; }

    /// <summary>[S-566] The colour Default's swatch is painted in (<c>#RRGGBB</c>); null for none of its own.</summary>
    public string? DefaultHex { get; init; }
}

/// <summary>[S-3724] A fill (<see cref="FillChoice"/>): the one colour submenu with Transparent and Opacity.</summary>
public sealed record SettingsFill(string Key, string Text, FillChoice Current, Action<FillChoice> Set, string DefaultText) : SettingsLine(Key);

/// <summary>[S-3528] A font: its submenu is the font picker over every installed family, then Add fonts.</summary>
public sealed record SettingsFont(string Key, string Text, string? Glyph, string Current, Action<string> Set) : SettingsLine(Key)
{
    /// <summary>What the line says at its end: the chosen font's name ([S-6441], <c>SettingsMenu.FontName</c>).</summary>
    public string? Value { get; init; }
}

/// <summary>A submenu of <see cref="Lines"/>, <see cref="Value"/> at its end.</summary>
public sealed record SettingsSubmenu(string Key, string Text, string? Glyph, IReadOnlyList<SettingsLine> Lines) : SettingsLine(Key)
{
    public string? Value { get; init; }

    /// <summary>[S-3541] The item it stands for is the one that holds (the view this display shows): the dot before its chevron.</summary>
    public bool Holds { get; init; }

    /// <summary>[S-3541] What a click does besides opening it (show the view here); null for a submenu that only opens.</summary>
    public Action? Run { get; init; }

    /// <summary>[S-3732] An item of an ordered list: its line is dragged up and down among the list's.</summary>
    public SettingsOrder? Order { get; init; }

    /// <summary>[S-2303] It opens as a page of its own behind a back head, not inside its card.</summary>
    public bool Page { get; init; }

    /// <summary>
    /// [S-3729] It opens inside its card whatever it holds (a widget's submenu: a list whose items open their own lines
    /// one level further in), never as a page of its own.
    /// </summary>
    public bool Inline { get; init; }

    /// <summary>
    /// [S-2301] [S-2310] It is one of the user's own things on its section, a card that opens in place (a view, a
    /// display, an account: Karpo's thing, §585), never a section of its own.
    /// </summary>
    public bool Card { get; init; }

    /// <summary>[S-2310] A line under its name (a thing's: where a view is shown, a display's resolution).</summary>
    public string? Caption { get; init; }

    /// <summary>[S-2310] Icon buttons at its head's end, before the chevron (a view's Duplicate and Delete, an account's Sign out).</summary>
    public IReadOnlyList<SettingsTool> Tools { get; init; } = [];
}

/// <summary>
/// [S-2311] The Appearance page's live preview (Karpo's §592, §671): real widget cards drawn in the design as it
/// stands, at the page's top, bare (no card round it). It sets nothing.
/// </summary>
public sealed record SettingsPreview(string Key) : SettingsLine(Key)
{
    /// <summary>[S-13700] What it is called (a screen reader says it); null for the Appearance's preview.</summary>
    public string? Name { get; init; }
}

/// <summary>
/// [S-3732] A line's place in an ordered list (the views): where it is, how many there are, and what
/// a drop or Alt+Shift+Up / Down does (move the item at the first index to the second, and save once).
/// </summary>
public sealed record SettingsOrder(int Index, int Count, Action<int, int> Move);

/// <summary>
/// [S-2302] [S-2351] A line of text typed in its box at the line's end (a view's Name, an account's Nickname): the
/// field's hint, what it starts with, why a typed text cannot be kept (null when it can) and what keeping it does.
/// Enter or leaving keeps it, Escape restores it; never a page of its own.
/// </summary>
public sealed record SettingsAsk(string Key, string Text, string? Glyph, string Placeholder, string Initial, Func<string, string?> Refuse, Action<string> Done) : SettingsLine(Key)
{
    /// <summary>[S-3551] An empty box may be given (a nickname cleared back to the account's own name).</summary>
    public bool AllowEmpty { get; init; }

    /// <summary>What the line says at its end (a nickname).</summary>
    public string? Value { get; init; }
}

/// <summary>
/// [S-3542] An icon (one glyph, or none for the default): its submenu is the icon picker over every
/// Segoe Fluent glyph, its default cell <see cref="DefaultText"/> (a view's initials) named
/// <see cref="DefaultName"/>. <see cref="Glyph"/> is the line's own glyph (the icon that holds).
/// </summary>
public sealed record SettingsIcon(string Key, string Text, string? Glyph, string? Current, string? DefaultText, string DefaultName, Action<string?> Set) : SettingsLine(Key)
{
    /// <summary>[S-14322] What its picker's head says it holds while folded (<see cref="IconHead.Of"/>).</summary>
    public IconHead Head => IconHead.Of(Current, DefaultName, DefaultText);
}

/// <summary>
/// [S-14322] What an icon picker's head shows it holds while folded (owner, 2026-11-21: "the icon in the icon selector
/// configuration section does not show when it is not extended"), at the value's place as a choice's head says its
/// choice: the chosen glyph (<see cref="Glyph"/>), or for the default its words as its cell in the picker says them
/// (<see cref="Words"/>: "Initials (WK)", "Its own icon").
/// </summary>
public readonly record struct IconHead(string? Glyph, string? Words)
{
    /// <summary>The head of a picker holding <paramref name="current"/> (null: its default, named <paramref name="defaultName"/>, drawing <paramref name="defaultText"/>).</summary>
    public static IconHead Of(string? current, string defaultName, string? defaultText)
        => !string.IsNullOrEmpty(current) ? new IconHead(current, null)
            : new IconHead(null, string.IsNullOrEmpty(defaultText) ? defaultName : $"{defaultName} ({defaultText})");
}

/// <summary>A command: a click does it, and the menu stays.</summary>
public sealed record SettingsCommand(string Key, string Text, string? Glyph, Action Run) : SettingsLine(Key)
{
    /// <summary>[S-3716] It destroys something (Delete view): drawn in the critical colour.</summary>
    public bool Danger { get; init; }

    /// <summary>[S-3580] What it says at its end (the site a link opens: "lometics.com").</summary>
    public string? Value { get; init; }

    /// <summary>[S-2312] A line under its name (what a way to add an account brings).</summary>
    public string? Caption { get; init; }

    /// <summary>
    /// [S-2351] [S-3727] It asks first, in place: its card opens on <see cref="SettingsConfirm.Question"/> and its
    /// answers, and only the answer does it (<see cref="SettingsConfirm.Done"/>). Null for a command that just does it.
    /// </summary>
    public SettingsConfirm? Confirm { get; init; }

    /// <summary>
    /// [S-2351] A command asked first, in place in its card: only <paramref name="confirm"/>'s answer does it; its own
    /// <see cref="Run"/> does nothing (refusing beats destroying something unasked).
    /// </summary>
    public static SettingsCommand Asking(string key, string text, string? glyph, SettingsConfirm confirm)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        return new SettingsCommand(key, text, glyph, static () => { }) { Confirm = confirm };
    }
}

/// <summary>
/// [S-3549] One thing of a list one manages (a view of Manage views): its icon and words, tools at its
/// end (Rename, Delete) whose keys work on it as it has the keyboard (F2, Delete), and its place in
/// the list's order (dragged, or Ctrl+Up / Down). A click on it does nothing of its own.
/// </summary>
public sealed record SettingsItem(string Key, string Text, string? Glyph, IReadOnlyList<SettingsTool> Tools) : SettingsLine(Key)
{
    /// <summary>[S-3732] Its place in the list, one move shared by the list's items.</summary>
    public SettingsOrder? Order { get; init; }
}

/// <summary>[S-3549] The key a tool of an item answers while the item has the keyboard.</summary>
public enum SettingsToolKey
{
    None,

    /// <summary>F2, as Explorer renames.</summary>
    Rename,

    /// <summary>Delete.</summary>
    Delete,
}

/// <summary>
/// [S-3549] A tool at a thing's head (a view's Duplicate and Delete, an account's Sign out): its glyph and its name (the
/// tooltip), its key, and what it does: a command (<see cref="Run"/>), or [S-2351] a question asked first in place
/// (<see cref="Confirm"/>: its card opens on the question, keyed by the tool's <see cref="Key"/>; only the answer does it).
/// </summary>
public sealed record SettingsTool(string Key, string Glyph, string Name, SettingsToolKey Shortcut)
{
    public Action? Run { get; init; }

    /// <summary>[S-2351] [S-3727] Asked first, in its thing's card; null for a tool that just does it.</summary>
    public SettingsConfirm? Confirm { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>
/// [S-3727] [S-2351] An "are you sure?" asked in place, in the card of what asks it: the question, the words of the
/// button that does it (the critical colour where <paramref name="Danger"/>), what it does, and what goes with it.
/// Cancel, Escape or a press elsewhere do nothing.
/// </summary>
public sealed record SettingsConfirm(string Question, string Accept, Action Done, string? Detail = null, bool Danger = false);

/// <summary>
/// [S-3749] A link row: somewhere the user must go to do something (settings › Accounts, to add or manage an account),
/// <see cref="Owner"/> naming it (the tooltip says "Opens Accounts"), Windows' "open in new window" mark at its end;
/// <see cref="Run"/> closes the page and opens it. Never a copy of a setting it leads to.
/// </summary>
public sealed record SettingsLink(string Key, string Text, string? Glyph, string Owner, Action Run) : SettingsLine(Key);

/// <summary>
/// [S-2351] [S-3721] A line that asks for one line of text in place (a web address, a path, Rename…, Add…): its row says its
/// words and <see cref="Value"/> with the expand chevron, and opens in its own card on <see cref="Ask"/>'s field and accept.
/// Never a page of its own.
/// </summary>
public sealed record SettingsPrompt(string Key, string Text, string? Glyph, WidgetTextAsk Ask) : SettingsLine(Key)
{
    /// <summary>What it holds, at its end.</summary>
    public string? Value { get; init; }

    /// <summary>It destroys something: in the critical colour.</summary>
    public bool Danger { get; init; }

    /// <summary>[S-7513] Among a choice's lines (Custom…), it holds the choice: what it gave is in force; its row does not draw it yet.</summary>
    public bool Holds { get; init; }
}

/// <summary>
/// [S-3722] A search (a place, a symbol, a zone): its line shows <see cref="Value"/> (what is chosen) and opens on a box,
/// the answer for what is typed (the empty text included, asked as it opens) under it and <see cref="Fixed"/> after the
/// answers; typing settles for <see cref="Settle"/> before it is asked (zero for an answer in hand).
/// </summary>
public sealed record SettingsSearch(
    string Key,
    string Text,
    string? Glyph,
    string Placeholder,
    Func<string, CancellationToken, Task<SettingsSearchAnswer>> Find,
    IReadOnlyList<SettingsLine> Fixed,
    TimeSpan Settle = default) : SettingsLine(Key)
{
    /// <summary>What is chosen now, at the end of its line.</summary>
    public string? Value { get; init; }
}

/// <summary>[S-3722] What a search found: its lines (a pick chooses), or a word in their place (searching, no match, offline).</summary>
public sealed record SettingsSearchAnswer(IReadOnlyList<SettingsLine> Lines, string? Note = null);
