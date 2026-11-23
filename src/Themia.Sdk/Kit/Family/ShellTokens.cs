namespace Themia.Sdk.Kit;

/// <summary>
/// [S-6745] The app's own chrome (the edit bar, the menus, the view bar, the notices, the pickers, the
/// layout editor's window, the card's own states) is drawn from the family's tokens like a widget;
/// what Windows or Karpo measure for a piece of chrome, and the family has no token for, is named here,
/// each with where it comes from. These are not a widget's: the source guard fails on a module or one
/// of the family's blocks that names them ([S-6747]). A spacing of the chrome's own is on the scale
/// (<see cref="FamilySpace"/>); only a measurement is a metric.
/// </summary>
public static class ShellMetrics
{
    /// <summary>Windows' overlay corner radius (<c>OverlayCornerRadius</c>): a flyout, a menu, a notice, a window's cards, the edit bar.</summary>
    public const double OverlayRadius = 8;

    /// <summary>The thin chevron Windows draws beside words in its menus and breadcrumbs (a row's ›, a trail's ›, a select's ⌄).</summary>
    public const double Chevron = 12;

    /// <summary>Windows Settings' 20 icon box: the icon grid's glyphs, the XL view bar's marks beside its 20 words.</summary>
    public const double IconBox = 20;

    /// <summary>[S-3715] A flyout's lift (elevation 32): a key light 8 DIPs down, blurred over 32; darker on the dark theme, where a lighter one would not show.</summary>
    public static class Shadow
    {
        public const float Elevation = 32;

        public const float OffsetY = 8;

        public const float Blur = 32;

        public const float OpacityLight = 0.18f;

        public const float OpacityDark = 0.42f;
    }

    /// <summary>
    /// [S-2320] The edit bar, Karpo's command bar (karpo-app, docs/widgets.md §575, §578, §608): one row 48
    /// tall (8, a 32 control, 8), 8 under the work area's top; an entry 12, its 16 icon, 8, its words, 12
    /// (40 with its icon alone), entries 4 apart; 8, the 20 hairline, 8; the selection's 256 room; a
    /// 16 gutter; the errors' 40 room; 4; Add; 8; Done (at least 80), 8 from the bar's end.
    /// </summary>
    public static class Bar
    {
        /// <summary>Around the row of controls, and between Add and Done.</summary>
        public const double Pad = 8;

        /// <summary>An entry's and a button's height.</summary>
        public const double Control = 32;

        public const double Height = Pad + Control + Pad;

        /// <summary>The bar's top under the work area's.</summary>
        public const double Top = 8;

        /// <summary>Between two entries, and between the errors' room and Add.</summary>
        public const double EntryGap = 4;

        /// <summary>Inside an entry: before its icon and after its words.</summary>
        public const double Lead = 12;

        public const double Icon = 16;

        /// <summary>Between an entry's icon and its words.</summary>
        public const double IconGap = 8;

        /// <summary>An entry with its icon alone (a narrow display; the errors' button).</summary>
        public const double IconOnly = Lead + Icon + Lead;

        /// <summary>[S-2323] The room either side of the divider's hairline.</summary>
        public const double DividerRoom = 8;

        /// <summary>The divider's hairline height.</summary>
        public const double Divider = 20;

        /// <summary>[S-2324] The selection's room, kept whether anything is selected or not.</summary>
        public const double Selection = 256;

        /// <summary>After the selection's room.</summary>
        public const double Gutter = 16;

        /// <summary>[S-2324] The least an entry's words are cut to before it shows its icon alone.</summary>
        public const double NameLeast = 40;

        /// <summary>Done's least width.</summary>
        public const double DoneLeast = 80;

        /// <summary>[S-2321] The open entry's accent pill at its foot.</summary>
        public const double PillWidth = 16;

        public const double PillHeight = 3;

        public const double PillRadius = PillHeight / 2;

        /// <summary>[S-13701] An entry's count badge (WinUI's InfoBadge): 16 tall, a pill, over its icon's top end corner.</summary>
        public const double Badge = 16;

        public const double BadgeRadius = Badge / 2;

        /// <summary>[S-13701] Inside the badge, either side of its number.</summary>
        public const double BadgePad = 4;

        /// <summary>[S-13701] How far the badge stands out past its icon's corner (inside the entry's 32: the icon stands 8 in).</summary>
        public const double BadgeOut = 6;
    }

    /// <summary>
    /// [S-2325] A page's flyout (Karpo's §575, §584): 580 wide (a 16 gutter either side of 548 of cards),
    /// 12 off the bar, its beak 8 tall and 16 across pointing at its entry, at most 70 % of the work
    /// area tall, 12 from the work area's sides where it is pushed in.
    /// </summary>
    public static class Flyout
    {
        public const double Width = 580;

        public const double Gutter = 16;

        public const double Cards = Width - (2 * Gutter);

        /// <summary>Off the bar: the beak's 8 and 4 of air before its tip.</summary>
        public const double Gap = 12;

        public const double BeakHeight = 8;

        public const double BeakWidth = 2 * BeakHeight;

        /// <summary>The most of the work area's height a flyout takes before it scrolls.</summary>
        public const double MostShare = 0.7;

        /// <summary>Kept from the work area's sides.</summary>
        public const double EdgeMargin = 12;

        /// <summary>
        /// [S-2344] How much of the ground is left over a selected widget the selection's page covers:
        /// enough for the page's words to read, little enough for the widget to be seen through it.
        /// </summary>
        public const double ShineThrough = 0.55;
    }

    /// <summary>
    /// [S-11707] [S-13600] [S-13610] The add page (Karpo's §593, §578: <c>PanelMetrics.TileLeast</c>, <c>TileTwoLines</c>;
    /// its old side list's items, <c>NavItem</c>, <c>NavPitch</c>): as wide as the bar, flush under it at a flyout's gap,
    /// no beak; the rail 240 wide (the search box over its items, each 36 tall, 4 apart, the accent's 3 × 16 pill
    /// on the chosen one, its icon at 16, its name at 48, its count 12 from its end), 48 with icons alone; tiles of at
    /// least 200 filling the list's width, 4 apart, each 52 tall (its icon 12 in, its name and one caption line 12
    /// after it), 16 off the rail; a family's head 36 tall over its stack; beside the list the 360 preview pane, 16
    /// off it, its picture box 240 tall, 16 in.
    /// </summary>
    public static class AddPage
    {
        public const double TileLeast = 200;

        public const double TileGap = 4;

        /// <summary>A tile's height: two lines of words (Karpo's <c>TileTwoLines</c>).</summary>
        public const double Tile = 52;

        /// <summary>Inside a tile: before its icon, between the icon and the words, after the words.</summary>
        public const double TilePad = 12;

        /// <summary>[S-13600] The preview pane's width beside the tiles.</summary>
        public const double Preview = 360;

        /// <summary>[S-13600] Between the tiles and the preview pane.</summary>
        public const double PreviewGap = 16;

        /// <summary>[S-13600] Inside the preview pane, round its picture box and words; and round the picture inside its box.</summary>
        public const double PreviewPad = 16;

        /// <summary>[S-13600] The preview's picture box: as wide as the pane less its padding, this tall.</summary>
        public const double PreviewPicture = 240;

        /// <summary>[S-13602] The tile's glyph in the preview's picture box until its picture is drawn.</summary>
        public const double PreviewGlyph = 48;

        /// <summary>[S-13610] The rail's width: the search box's, and its items' (Karpo's side list, NavigationView's pane).</summary>
        public const double Rail = 240;

        /// <summary>[S-13616] The rail with its icons alone (NavigationView's compact pane).</summary>
        public const double RailCompact = 48;

        /// <summary>[S-13610] Between the rail and the tiles.</summary>
        public const double RailGap = 16;

        public const double SearchWidth = Rail;

        /// <summary>[S-13610] A rail item's height (Karpo's <c>NavItem</c>), and between two (its <c>NavPitch</c> less it).</summary>
        public const double RailItem = 36;

        public const double RailItemGap = 4;

        /// <summary>[S-13610] A rail item's icon's start, and its name's (Karpo's 16 and 48, a page row's).</summary>
        public const double RailIconAt = 16;

        public const double RailNameAt = 48;

        /// <summary>[S-13610] A rail item's count, this far from its end.</summary>
        public const double RailCountEnd = 12;

        /// <summary>[S-13610] The chosen item's pill (NavigationView's selection indicator): 3 × 16 at its start.</summary>
        public const double RailPillWidth = 3;

        public const double RailPillHeight = 16;

        /// <summary>[S-13610] The pill's ends are round: half its width.</summary>
        public const double RailPillRadius = RailPillWidth / 2;

        /// <summary>[S-13612] A family's head over its stack; its fill reaches this far out past the tiles' start.</summary>
        public const double FamilyHead = 36;

        public const double FamilyHeadInset = 8;

        /// <summary>[S-13612] How far a family's head stands above where a heading would: its words where a heading's stand.</summary>
        public const double FamilyHeadRise = (FamilyHead - FamilyType.BodyLine) / 2;
    }

    /// <summary>
    /// [S-14911] The widget gallery's window: four fifths of the work area each way (never under 1200 by 640 where the work
    /// area holds that: a 1280 by 720 display's well still holds eight tiles across), 24 from its sides and foot at the least, a flyout's gap under the bar; 16 inside it: the
    /// categories' rail 200 wide on the left (the add page's rail, its items 36 tall), the panel 280 wide on the right,
    /// and between them the title line (48) over the well the cards stand in, 8 inside it; a style's preview in the panel
    /// at most 132 × 96, 8 apart.
    /// </summary>
    public static class WidgetGallery
    {
        /// <summary>[S-14911] The window's share of the work area, each way.</summary>
        public const double Share = 0.8;

        /// <summary>[S-14911] The least the window is where the work area holds it.</summary>
        public const double MinWidth = 1200;

        public const double MinHeight = 640;

        /// <summary>[S-14911] Inside the window, and between its tabs' line and its well.</summary>
        public const double Pad = 16;

        /// <summary>[S-16401] [S-16559] The line of tabs, the hint and the search box over the well: a tab's own height.</summary>
        public const double Head = Page.Tabs;

        /// <summary>[S-16559] From the window's top to the tabs (owner, 2026-11-23: "a little less spacing on the top and bottom").</summary>
        public const double HeadInset = 8;

        /// <summary>[S-16559] From the tabs to the well.</summary>
        public const double HeadGap = 4;

        /// <summary>[S-16402] [S-16557] The search box at the tabs' line's end, always open; on a narrow window at most a quarter of its line.</summary>
        public const double Search = 280;

        /// <summary>[S-16566] The least the search box is narrowed to for the short hint beside the tabs.</summary>
        public const double SearchLeast = 200;

        /// <summary>[S-16404] A picked widget's words, a card of the bento: its natural box, DIPs.</summary>
        public const double AboutWidth = 320;

        public const double AboutHeight = 192;

        /// <summary>[S-16404] Inside the words' card.</summary>
        public const double AboutPad = 16;

        /// <summary>[S-14911] Inside the well, from its edge to the cards' room.</summary>
        public const double WellPad = 8;

        /// <summary>[S-14913] The scroll bar's room at the well's end.</summary>
        public const double ScrollBar = 12;

        /// <summary>From the work area's sides and foot, at the least.</summary>
        public const double Margin = 24;
    }

    /// <summary>
    /// [S-2305] A page's sections, cards and rows (Karpo's PanelMetrics): a heading 24 after the previous
    /// group and 8 over its cards; cards 4 apart and radius 4, 16 under the last; a row 48 tall, 60 with a
    /// caption, its icon at 16, its name at 48, its control ending 16 from the card's edge.
    /// </summary>
    public static class Page
    {
        public const double SectionTop = 24;

        public const double HeadingGap = 8;

        public const double CardGap = 4;

        public const double Bottom = 16;

        public const double CardRadius = 4;

        public const double OneLine = 48;

        public const double TwoLines = 60;

        public const double IconAt = 16;

        /// <summary>
        /// [S-2377] A row's icon (its icon column's, a foot button's, a button beside a row's, an option's): its glyph 16 (Karpo's
        /// <c>PanelMetrics.Icon</c>) whatever size it was built at, in a slot as large that scales down what is larger and never
        /// cuts it.
        /// </summary>
        public const double IconGlyph = FamilyShape.Icon;

        public const double IconSlot = IconGlyph;

        public const double NameAt = 48;

        public const double EndInset = 16;

        /// <summary>[S-2349] How much further in a group's rows stand under its head in a card (Karpo's <c>IndentStep</c>).</summary>
        public const double IndentStep = 28;

        /// <summary>A control's height (a box, a button).</summary>
        public const double Control = 32;

        /// <summary>A number box's and a text box's width.</summary>
        public const double ControlWide = 240;

        /// <summary>[S-2302] A number box's − and + inside it: 28 squares, 2 from its end; the room its words leave them.</summary>
        public const double Spin = 28;

        public const double SpinInset = 2;

        public const double SpinsRoom = (2 * Spin) + (4 * SpinInset);

        public const double ActionLeast = 120;

        public const double ActionMost = 240;

        /// <summary>[S-2307] A sub-page's back head.</summary>
        public const double BackHead = 32;

        /// <summary>[S-2340] The selection page's header card, its tile and the tile's glyph.</summary>
        public const double HeaderCard = 80;

        public const double HeaderTile = 48;

        public const double HeaderGlyph = 24;

        /// <summary>The words (On, Off) before a switch.</summary>
        public const double SwitchWords = 40;

        /// <summary>[S-2302] Windows' toggle switch: a 40 × 20 track with a 12 knob, centred in its height.</summary>
        public const double SwitchWidth = 40;

        public const double SwitchHeight = 20;

        public const double Knob = 12;

        public const double KnobInset = (SwitchHeight - Knob) / 2;

        /// <summary>[S-2305] How far a row is lit when a deep link lands on it.</summary>
        public const int LitMs = 1500;

        /// <summary>
        /// [S-2317] Words to their control (Karpo's <c>PanelMetrics.Label</c>, WinUI's 12 between a control and its
        /// label): a value said before its chevron's room or its button, a button's words in from its sides.
        /// </summary>
        public const double Label = 12;

        /// <summary>[S-2317] Every chevron's middle this far in from its control's end (Karpo's <c>ChevronIn</c>, §548).</summary>
        public const double ChevronIn = 20;

        /// <summary>[S-2317] The room an expander's, a picker's or a link's chevron takes at a card's end: a 32 button, 4 in (Karpo's <c>ChevronRoom</c>).</summary>
        public const double ChevronRoom = 4 + Control;

        /// <summary>[S-2317] A chevron's end this far in from its control's end: its middle 20 in, half of its 12 after it.</summary>
        public const double ChevronOff = ChevronIn - (Chevron / 2);

        /// <summary>[S-2318] A row's button and a value's reset at most this wide, the value column's (Karpo's <c>ActionWide</c>: 120..240).</summary>
        public const double ButtonMost = ControlWide;

        /// <summary>[S-2318] Between two buttons that only act standing side by side (Karpo's §578 foot buttons, 8 apart) and under the card before them.</summary>
        public const double FootGap = 8;

        /// <summary>[S-2345] A tab strip (Karpo's §592, WinUI's SelectorBar): a control's 32 and its pill's room, 8 under the card before it and over the chosen tab's cards.</summary>
        public const double Tabs = Control + 8;

        public const double TabsGap = 8;

        /// <summary>[S-2345] Inside a tab, before and after its words; tabs 4 apart.</summary>
        public const double TabPad = 12;

        public const double TabGap = 4;

        /// <summary>
        /// [S-2317] A thing put in order's handle at its row's start (Karpo's §551 <c>Handle</c>, <c>HandleIn</c>,
        /// <c>HandleLead</c>): 16 wide, 4 in, its icon and name 12 further in.
        /// </summary>
        public const double Handle = 16;

        public const double HandleIn = 4;

        public const double HandleLead = 12;
    }

    /// <summary>
    /// [S-2308] An inline picker inside its card (Karpo's PickerMetrics, §670): 12 in, radio rows 36 tall
    /// (more than six in up to three columns, each at least 180), 36 swatches 4 apart, a lookup's
    /// eight results, the custom colour's 96 spectrum.
    /// </summary>
    public static class Inline
    {
        public const double Pad = 12;

        public const double Option = 36;

        public const double OptionLeast = 180;

        public const int Columns = 3;

        public const int InColumn = 6;

        public const int Found = 8;

        public const double Swatch = 36;

        public const double SwatchGap = 4;

        /// <summary>A swatch's corners, and the accent ring round the one that holds (its width, its gap) and its corners.</summary>
        public const double SwatchRadius = 2;

        public const double Ring = 2;

        public const double RingRadius = SwatchRadius + Ring;

        public const double Spectrum = 96;
    }

    /// <summary>
    /// [S-911] The view bar's three sizes (M, L, XL): a chip at least this tall, and this square when it
    /// shows the mark alone. Its words are the family's Caption, Body and Title (regular), its marks the
    /// family's 16 icon (the 20 icon box at XL), its paddings on the scale.
    /// </summary>
    public static class ViewBar
    {
        public const double SquareM = 28;

        public const double SquareL = 32;

        public const double SquareXl = 40;

        /// <summary>A view's name is cut past this.</summary>
        public const double NameMaxWidth = 220;

        /// <summary>[S-13401] The dots style's dot at M, L and XL; the shown view's pill is <see cref="DotPill"/> dots long.</summary>
        public const double DotM = 6;

        public const double DotL = 8;

        public const double DotXl = 10;

        public const double DotPill = 3;
    }

    /// <summary>[S-881] A notice: Windows' toast width; an action under the detail at least this wide.</summary>
    public static class Notice
    {
        public const double Width = 364;

        public const double ActionMinWidth = 96;
    }

    /// <summary>[S-891] The error details' flyout: 320 wide, scrolling past 240.</summary>
    public static class Details
    {
        public const double Width = 320;

        public const double MaxHeight = 240;
    }

    /// <summary>
    /// The app-layout editor, Themia's one window, as Windows' settings pages: a 48 title bar, a page at
    /// most 1000 wide inset 36, sections 30 under the last and 6 over their cards (Windows'
    /// <c>SettingsSectionHeader</c>), a field 280 wide and a choice at least 200, a refusal under a
    /// field at most 300.
    /// </summary>
    public static class Window
    {
        public const double TitleBar = 48;

        public const double PageMaxWidth = 1000;

        public const double PageInset = 36;

        public const double SectionTop = 30;

        public const double SectionBottom = 6;

        public const double FieldWidth = 280;

        public const double ChoiceMinWidth = 200;

        public const double VerdictMaxWidth = 300;

        /// <summary>[S-2221] An icon command in the window (a layout cell's remove): Windows' 32 square, not a widget's button.</summary>
        public const double IconButton = 32;
    }

    /// <summary>
    /// The layout editor's picture of the panes: at most 440 tall (360 as it is first laid out), an 8
    /// divider between two panes (its grab area, with a 10 grip glyph), a pane's middle column 260 wide.
    /// </summary>
    public static class LayoutEditor
    {
        public const double CanvasHeight = 360;

        public const double MaxCanvasHeight = 440;

        public const double Divider = 8;

        public const double Grip = 10;

        public const double PaneColumn = 260;
    }

    /// <summary>
    /// [S-903] The icon grid: 32 cells, the glyph in Settings' 20 icon box; [S-6440] the font list 360 tall;
    /// [S-2221] the widget picker's rows (Karpo's 32, a heading or an entry) and its search results (48:
    /// a name over where it lives), its own and not a widget's rows.
    /// </summary>
    public static class Picker
    {
        public const double Row = 32;

        public const double Result = 48;

        /// <summary>[S-13605] An icon's square in the icon picker (Karpo's §249, §682: 40 DIP, its glyph 20).</summary>
        public const double Cell = 40;

        public const double Glyph = IconBox;

        /// <summary>[S-13605] The icon picker's one height under its search (Karpo's <c>PickerHeightDip</c>, <c>MostTall</c>): it scrolls inside.</summary>
        public const double IconsTall = 400;

        public const double FontListHeight = 360;

        /// <summary>[S-1500] A question's widest line before it wraps.</summary>
        public const double QuestionMaxWidth = 260;
    }
}

/// <summary>
/// [S-6746] The chrome's colours by what they mean: the theme brushes a flyout, the bar, the card itself,
/// a drawn switch and the accent's pressed states are painted with, which no widget may use (a widget's
/// colours are <see cref="ColourRole"/>s). The guard fails on a module or a block that names one ([S-6747]).
/// </summary>
public enum ShellRole
{
    /// <summary>A flyout's solid sheet (the menus, the pages).</summary>
    Flyout,

    /// <summary>A flyout's and a notice's stroke.</summary>
    FlyoutStroke,

    /// <summary>The acrylic of the edit bar and the chrome that shares its frame.</summary>
    Acrylic,

    /// <summary>An opaque sheet a step over the base: a notice, a colour swatch with no colour.</summary>
    Raised,

    /// <summary>A window's page layer (the layout editor's assigned pane).</summary>
    Layer,

    /// <summary>The card's opaque base, under its fill (the card, its markers, the view bar's fill).</summary>
    CardBase,

    /// <summary>The card's translucent fill.</summary>
    CardFill,

    /// <summary>The card's stroke (the card, the view bar, the layout editor's panes).</summary>
    CardStroke,

    /// <summary>Accent words and glyphs on the chrome (the shown view, a picked font, a command of the accent).</summary>
    AccentText,

    /// <summary>The accent pointed at and pressed (a lit toggle), and greyed (a switch that is on but disabled).</summary>
    AccentHover,

    AccentPressed,

    AccentDisabled,

    /// <summary>Words and glyphs on a disabled accent.</summary>
    TextOnAccentDisabled,

    /// <summary>A standard control pointed at (the view bar's raised chip).</summary>
    ControlHover,

    /// <summary>A control's stroke pointed at (a swatch).</summary>
    ControlStrokeHover,

    /// <summary>A drawn switch that is off: its track and rim, and greyed.</summary>
    SwitchTrack,

    SwitchTrackDisabled,

    SwitchRim,

    SwitchRimDisabled,

    /// <summary>A strong fill (the card's resize marks).</summary>
    StrongFill,

    /// <summary>[S-2308] An opened inline picker's ground inside its card: Windows' secondary card fill.</summary>
    CardSecondary,
}

/// <summary>[S-6746] The theme resource each shell role is drawn with: the one table.</summary>
public static class ShellRoles
{
    private static readonly Dictionary<ShellRole, string> Keys = new()
    {
        [ShellRole.Flyout] = "SolidBackgroundFillColorQuarternaryBrush",
        [ShellRole.FlyoutStroke] = "SurfaceStrokeColorFlyoutBrush",
        [ShellRole.Acrylic] = "AcrylicBackgroundFillColorDefaultBrush",
        [ShellRole.Raised] = "SolidBackgroundFillColorTertiaryBrush",
        [ShellRole.Layer] = "LayerFillColorDefaultBrush",
        [ShellRole.CardBase] = ColourRoles.CardBase,
        [ShellRole.CardFill] = ColourRoles.CardFill,
        [ShellRole.CardStroke] = "CardStrokeColorDefaultBrush",
        [ShellRole.AccentText] = "AccentTextFillColorPrimaryBrush",
        [ShellRole.AccentHover] = "AccentFillColorSecondaryBrush",
        [ShellRole.AccentPressed] = "AccentFillColorTertiaryBrush",
        [ShellRole.AccentDisabled] = "AccentFillColorDisabledBrush",
        [ShellRole.TextOnAccentDisabled] = "TextOnAccentFillColorDisabledBrush",
        [ShellRole.ControlHover] = "ControlFillColorSecondaryBrush",
        [ShellRole.ControlStrokeHover] = "ControlStrokeColorSecondaryBrush",
        [ShellRole.SwitchTrack] = "ControlAltFillColorSecondaryBrush",
        [ShellRole.SwitchTrackDisabled] = "ControlAltFillColorDisabledBrush",
        [ShellRole.SwitchRim] = "ControlStrongStrokeColorDefaultBrush",
        [ShellRole.SwitchRimDisabled] = "ControlStrongStrokeColorDisabledBrush",
        [ShellRole.StrongFill] = "ControlStrongFillColorDefaultBrush",
        [ShellRole.CardSecondary] = "CardBackgroundFillColorSecondaryBrush",
    };

    /// <summary>The resource key of <paramref name="role"/> (a <c>…Brush</c> name).</summary>
    public static string Key(ShellRole role) => Keys.TryGetValue(role, out var key)
        ? key
        : throw new ArgumentOutOfRangeException(nameof(role), role, "not a shell role");

    /// <summary>Every shell role, in declaration order.</summary>
    public static IReadOnlyList<ShellRole> All { get; } = Enum.GetValues<ShellRole>();
}
