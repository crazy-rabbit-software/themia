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
    /// [S-3734] The menu engine's rectangle and rows, Karpo's configuration menus (Explorer's rows):
    /// one fixed 320 × 440 on every level, rows 32 apart with a 28 fill inset 5 from the edge.
    /// </summary>
    public static class Menu
    {
        /// <summary>The one width of the menu and every level at a text size of 100 % (Karpo's SettingsWidthDip).</summary>
        public const double Width = 320;

        /// <summary>The one height of the menu and every level at a text size of 100 % (Karpo's SettingsHeightDip).</summary>
        public const double Height = 440;

        /// <summary>The rows' pitch: the fill with <see cref="FillInset"/> over and under it.</summary>
        public const double Pitch = 32;

        /// <summary>The fill under a row, as Explorer's menu draws it.</summary>
        public const double Fill = 28;

        /// <summary>The room over and under a row's fill inside its pitch.</summary>
        public const double FillInset = (Pitch - Fill) / 2;

        /// <summary>Where a row's fill starts in from the menu's edge (5), less the rim the frame draws there.</summary>
        public const double RowInset = 5 - FamilyShape.Hairline;

        /// <summary>The room over the first row and under the last (Karpo's panel padding).</summary>
        public const double BandPadding = 3;

        /// <summary>Inside a row's fill: the room before its icon (which then stands 16 in from the menu's edge).</summary>
        public const double PadStart = 11;

        /// <summary>Inside a row's fill: the room after the end slot (the chevron's box ends 13 in from the menu's edge).</summary>
        public const double PadEnd = 8;

        /// <summary>[S-3741] Windows' toggle switch at a row's end: a 40 × 20 track with a 12 knob.</summary>
        public const double SwitchWidth = 40;

        public const double SwitchHeight = 20;

        public const double Knob = 12;

        /// <summary>The knob's room from the track's end: centred in the track's height.</summary>
        public const double KnobInset = (SwitchHeight - Knob) / 2;

        /// <summary>[S-3731] A slider row's compact track, the least it gives way to long words, and its value's column.</summary>
        public const double SliderWidth = 96;

        public const double SliderMinWidth = 64;

        public const double SliderValueWidth = 40;

        /// <summary>[S-3740] A heading row's height.</summary>
        public const double HeadingHeight = 32;

        /// <summary>A rule: one hairline with this much over and under it.</summary>
        public const double RuleGap = 4;

        /// <summary>[S-3722] A field in a row's fill: Windows' text box padding, held to the 28 fill (10 and 6 either side, 3 over and under).</summary>
        public const double FieldPadStart = 10;

        public const double FieldPadEnd = 6;

        public const double FieldPadY = 3;

        /// <summary>[S-3746] A note row's room under its words, so a wrapped note clears the next row's fill.</summary>
        public const double NotePadBottom = 6;

        /// <summary>A tool row's small square buttons and the room between two.</summary>
        public const double Tool = 24;

        public const double ToolGap = 2;

        /// <summary>[S-3723] A colour level's swatches: 24 in 32 cells, eight to a line (Karpo's).</summary>
        public const double Swatch = 24;

        public const double SwatchCell = 32;

        /// <summary>A swatch's room inside its cell, either side.</summary>
        public const double SwatchInset = (SwatchCell - Swatch) / 2;

        /// <summary>[S-3738] How far a level's rows slide in (Karpo's 16 over 167 ms).</summary>
        public const float SlideDip = 16;

        /// <summary>How far a notch of the mouse wheel scrolls.</summary>
        public const double WheelStep = 48;

        /// <summary>[S-3717] The properties menu's top row: 40 squares, the plate under the pointer 4 in from each side, a 20 hairline between two.</summary>
        public const double TopRow = 40;

        public const double TopPlate = 4;

        public const double TopRule = 20;
    }

    /// <summary>
    /// [S-3500] The edit bar, Karpo's: 48 tall, 36 buttons with radius 4 in a 6 padding, 6 between
    /// two, 12 either side of a 20 hairline, Done at least 80 wide.
    /// </summary>
    public static class EditBar
    {
        public const double Button = 36;

        public const double Gap = 6;

        public const double Height = Button + (2 * Gap);

        public const double Divider = 20;

        public const double DoneWidth = 80;
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
    }

    /// <summary>[S-881] A notice: Windows' toast width; an action under the detail at least this wide.</summary>
    public static class Notice
    {
        public const double Width = 364;

        public const double ActionMinWidth = 96;
    }

    /// <summary>[S-891] The error details' flyout: a menu's width, scrolling past 240.</summary>
    public static class Details
    {
        public const double Width = Menu.Width;

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

    /// <summary>[S-903] The icon grid: 32 cells, the glyph in Settings' 20 icon box; [S-6440] the font list 360 tall.</summary>
    public static class Picker
    {
        public const double Cell = 32;

        public const double Glyph = IconBox;

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
    /// <summary>A flyout's solid sheet (the menus, the properties panel).</summary>
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
    };

    /// <summary>The resource key of <paramref name="role"/> (a <c>…Brush</c> name).</summary>
    public static string Key(ShellRole role) => Keys.TryGetValue(role, out var key)
        ? key
        : throw new ArgumentOutOfRangeException(nameof(role), role, "not a shell role");

    /// <summary>Every shell role, in declaration order.</summary>
    public static IReadOnlyList<ShellRole> All { get; } = Enum.GetValues<ShellRole>();
}
