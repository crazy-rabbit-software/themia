namespace Themia.App.Shell;

/// <summary>
/// The Segoe Fluent Icons code points this app uses, by name.
/// <para>
/// Written as code points rather than as literal characters, because a private-use character sitting
/// in a source file is invisible: you cannot read it, diff it usefully, or spot a wrong one in
/// review. A named constant with its hex value is greppable and reviewable, and the name says what
/// the icon is for rather than what it looks like.
/// </para>
/// </summary>
internal static class Glyphs
{
    public static readonly string Settings = From(0xE713);
    public static readonly string Palette = From(0xE790);

    /// <summary>[S-1556] The colour picker's "pick from the screen".</summary>
    public static readonly string Eyedropper = From(0xEF3C);
    public static readonly string Grid = From(0xE80A);
    public static readonly string Info = From(0xE946);
    public static readonly string View = From(0xE7C4);
    public static readonly string Display = From(0xE7F4);
    public static readonly string Add = From(0xE710);
    public static readonly string Delete = From(0xE74D);
    public static readonly string Edit = From(0xE70F);
    public static readonly string Done = From(0xE73E);
    public static readonly string Copy = From(0xE8C8);
    public static readonly string Cut = From(0xE8C6);
    public static readonly string Paste = From(0xE77F);
    public static readonly string Reset = From(0xE777);
    public static readonly string Rename = From(0xE8AC);

    /// <summary>[S-1740] Distinct from <see cref="Copy"/>: both sit side by side in a menu's icon row.</summary>
    public static readonly string Duplicate = From(0xF413);
    public static readonly string Move = From(0xE7C2);
    public static readonly string SelectAll = From(0xE8B3);
    public static readonly string Theme = From(0xE793);
    public static readonly string Opacity = From(0xE7E6);
    public static readonly string Surface = From(0xE8B9);
    public static readonly string Font = From(0xE8D2);
    public static readonly string TextSize = From(0xE8E9);
    public static readonly string Scale = From(0xE8A9);
    public static readonly string Motion = From(0xE9D2);
    public static readonly string Clock = From(0xE823);
    public static readonly string Timer = From(0xE916);
    public static readonly string Hours = From(0xE917);
    public static readonly string Calendar = From(0xE787);

    /// <summary>[S-3600] The week, day and year calendars and the agenda, beside the month's <see cref="Calendar"/>.</summary>
    public static readonly string CalendarWeek = From(0xE8C0);

    public static readonly string CalendarDay = From(0xE8BF);

    public static readonly string CalendarYear = From(0xEA89);

    public static readonly string Agenda = From(0xE8FD);

    public static readonly string Mail = From(0xE715);
    public static readonly string Unread = From(0xE8BD);
    public static readonly string Count = From(0xE8FD);
    public static readonly string Preview = From(0xE7C3);
    public static readonly string Startup = From(0xE7E8);
    public static readonly string Cpu = From(0xE950);
    public static readonly string Memory = From(0xE964);
    public static readonly string Disk = From(0xEDA2);
    public static readonly string Battery = From(0xE83F);
    public static readonly string Network = From(0xE968);
    public static readonly string Volume = From(0xE767);
    public static readonly string Gpu = From(0xF211);
    public static readonly string Contact = From(0xE77B);
    public static readonly string Task = From(0xE9D5);
    public static readonly string Ping = From(0xEBC3);
    public static readonly string Note = From(0xE70B);
    public static readonly string Countdown = From(0xE81C);
    public static readonly string Pomodoro = From(0xE945);
    public static readonly string Calculator = From(0xE8EF);
    public static readonly string Sun = From(0xE706);
    public static readonly string Moon = From(0xE708);
    public static readonly string Place = From(0xE707);
    public static readonly string Sort = From(0xE8CB);
    public static readonly string Play = From(0xE768);
    public static readonly string Pause = From(0xE769);
    public static readonly string Sound = From(0xE767);
    public static readonly string Link = From(0xE71B);
    public static readonly string Wrap = From(0xE751); // [S-652] E7A3 is not in the font: a return arrow
    public static readonly string Clear = From(0xE894);
    public static readonly string MoveUp = From(0xE74A);
    public static readonly string MoveDown = From(0xE74B);
    public static readonly string Folder = From(0xE8B7);
    public static readonly string File = From(0xE8A5);
    public static readonly string Hidden = From(0xE890);
    public static readonly string Open = From(0xE8E5);
    public static readonly string Power = From(0xE7E8);

    // [S-1910] Power widget glyphs beyond the shared ones (Power, Reset, Moon, Display).
    public static readonly string Lock = From(0xE72E);
    public static readonly string SignOut = From(0xF3B1);
    /// <summary>[S-1910] "Save to this PC": hibernation writes the session to disk. Fluent has no hibernate glyph.</summary>
    public static readonly string Hibernate = From(0xE78C);
    public static readonly string Signal = From(0xE870);
    public static readonly string Shortcut = From(0xE71B);
    public static readonly string Warning = From(0xE7BA);
    public static readonly string Undo = From(0xE7A7);
    public static readonly string Shuffle = From(0xE8B1);
    public static readonly string Mute = From(0xE74F);
    public static readonly string Volume0 = From(0xE992);
    public static readonly string Volume1 = From(0xE993);
    public static readonly string Volume2 = From(0xE994);
    public static readonly string Volume3 = From(0xE995);

    // [S-1222] An output device in the audio widgets' device picker.
    public static readonly string Speakers = From(0xE7F5);
    public static readonly string Previous = From(0xE892);
    public static readonly string Next = From(0xE893);
    public static readonly string Mouse = From(0xE962);
    public static readonly string Game = From(0xE7FC);
    public static readonly string Bluetooth = From(0xE702);

    // [S-3902] The quick-setting toggles, as Windows 11's own quick settings draw them (Karpo's set).
    public static readonly string Wifi = From(0xE701);
    public static readonly string Airplane = From(0xE709);
    public static readonly string EnergySaver = From(0xEC0A);
    public static readonly string NightLight = From(0xE708);
    public static readonly string RotationLock = From(0xE755);
    public static readonly string QuietHours = From(0xF285);
    public static readonly string Microphone = From(0xE720);
    public static readonly string MicrophoneOff = From(0xEC54);
    public static readonly string SpatialSound = From(0xED33);
    public static readonly string MonoAudio = From(0xE15D);
    public static readonly string ColorFilters = From(0xE2B1);
    /// <summary>Karpo's F7FC is not in Segoe Fluent Icons here (drew a box): the controller.</summary>
    public static readonly string GameMode = From(0xE7FC);
    public static readonly string Weather = From(0xE753);
    public static readonly string Forecast = From(0xE9CA);

    // The weather set. These code points are the least certain in this file: a wrong one renders
    // as a hollow box rather than failing, so they are checked against a screenshot rather than
    // trusted. [S-652] The earlier E9BF/E9CB/E9C4/E9C8/E9C6 are in neither Segoe Fluent Icons nor
    // MDL2 (checked against the font's cmap) and drew as boxes. Segoe Fluent has no dedicated
    // weather set, so these are its nearest shapes, each checked on a rendered sheet: a cloud,
    // wind lines, a cloud with falling drops, a snowflake, a bolt.
    public static readonly string Cloudy = From(0xE753);
    public static readonly string Fog = From(0xE7BC);
    public static readonly string Rain = From(0xEA91);
    public static readonly string Snow = From(0xEA38);
    public static readonly string Thunderstorm = From(0xE945);

    // [S-733] The weather readings and the forecast layouts. Checked against SegoeIcons.ttf's cmap
    // and a rendered sheet: a filled drop, a thermometer, wind lines, a bulleted list, three
    // vertical bars, four squares.
    public static readonly string Droplet = From(0xEB42);
    public static readonly string Thermometer = From(0xE9CA);
    public static readonly string Wind = From(0xE7BC);
    public static readonly string ListLayout = From(0xE8FD);
    public static readonly string ColumnsLayout = From(0xE8C0);
    public static readonly string GridLayout = From(0xE8A9);
    public static readonly string Stocks = From(0xEB05);
    public static readonly string Rss = From(0xE8F1);
    public static readonly string Apps = From(0xE71D);
    public static readonly string Search = From(0xE721);
    public static readonly string Clipboard = From(0xE77F);
    public static readonly string Scratchpad = From(0xE70F);
    public static readonly string Quote = From(0xE9B2); // [S-652] E9E5 is not in the font: an opening quote mark
    public static readonly string Gallery = From(0xE91B);
    public static readonly string Habit = From(0xE73E);
    public static readonly string Music = From(0xE8D6);

    /// <summary>[S-3200] The audio visualizer: Equalizer (E9E9) is the mixer's, so the sound-settings wave.</summary>
    public static readonly string Visualizer = From(0xE7F3);
    public static readonly string Group = From(0xF168);

    // The system widgets: mixer and screen time (the GPU glyph sits with the other meters).
    public static readonly string Mixer = From(0xE9E9);
    public static readonly string ScreenTime = From(0xEC92);

    /// <summary>[S-3320] The screen-time chart (AreaChart) and donut (PieSingle); the app list keeps <see cref="ScreenTime"/>.</summary>
    public static readonly string ScreenTimeChart = From(0xE9D2);
    public static readonly string ScreenTimeDonut = From(0xEB05);
    public static readonly string Bell = From(0xEA8F);
    public static readonly string Hide = From(0xED1A);

    /// <summary>
    /// [S-3702] Header placement on the edit bar: Segoe "DockBottom"; the top is the same glyph
    /// turned over (Segoe has no "DockTop"), hidden is <see cref="Hide"/>.
    /// </summary>
    public static readonly string HeaderBottom = From(0xE90E);

    // The camera widget, and a web address wherever one is shown.
    public static readonly string Video = From(0xE714);
    public static readonly string Globe = From(0xE774);
    public static readonly string Rotate = From(0xE7AD);
    public static readonly string Refresh = From(0xE72C);
    public static readonly string Fit = From(0xE9A6);

    // The License page ([S-1068]).
    public static readonly string Shop = From(0xE719);
    public static readonly string Certificate = From(0xEB95);

    // The language row on the General page ([S-1109]): Fluent's LocaleLanguage.
    public static readonly string Language = From(0xF2B7);

    // The About page.
    public static readonly string Themia = From(0xE8A9);
    public static readonly string Home = From(0xE80F);
    public static readonly string Export = From(0xEDE1);
    public static readonly string Import = From(0xE8B5);
    public static readonly string Code = From(0xE943);
    public static readonly string OpenExternal = From(0xE8A7);

    // The Mail widget's message commands and folders.
    public static readonly string MailRead = From(0xE8C3);
    public static readonly string MailReply = From(0xE8CA);
    public static readonly string Archive = From(0xE7B8);
    public static readonly string Junk = From(0xE8F8);
    public static readonly string Send = From(0xE724);

    // The Tasks widget.
    public static readonly string Back = From(0xE72B);

    // Checked against SegoeIcons.ttf's cmap and a rendered sheet: three joined nodes (a branch, the
    // folder's git badge), a gauge (pressure), a tag, a dot.
    public static readonly string PullRequest = From(0xF003);
    public static readonly string Gauge = From(0xEC4A);
    public static readonly string Tag = From(0xE8EC);
    public static readonly string Dot = From(0xE91F);

    // [S-1365] The note formatting bar. Checked against SegoeIcons.ttf's cmap and a rendered sheet:
    // a bold B, a slanted I, a struck S, a bulleted list, a checked list. The font has no numbered
    // list and no heading mark; those two buttons are lettered ("1.", "H").
    public static readonly string Bold = From(0xE8DD);
    public static readonly string Italic = From(0xE8DB);
    public static readonly string Strikethrough = From(0xEDE0);
    public static readonly string BulletList = From(0xE8FD);
    public static readonly string Checklist = From(0xE9D5);
    public static readonly string ChevronDown = From(0xE70D);

    /// <summary>[S-913] The view bar's drag handle: upright bars on a row, flat bars on a column.</summary>
    public static readonly string GripperVertical = From(0xE784);

    public static readonly string GripperHorizontal = From(0xE76F);

    /// <summary>[S-881] A notice's close button, and its success and error marks (InfoBar's own glyphs).</summary>
    public static readonly string Dismiss = From(0xE711);

    public static readonly string Success = From(0xE930);

    public static readonly string ErrorBadge = From(0xEA39);

    /// <summary>[S-891] A widget that failed: its error card.</summary>
    public static readonly string Broken = From(0xE783);

    /// <summary>[S-1326] The folder widget's history and file commands.</summary>
    public static readonly string Forward = From(0xE72A);

    public static readonly string NewFolder = From(0xE8F4);

    public static readonly string OpenWith = From(0xE7AC);

    public static readonly string Pin = From(0xE718);

    public static readonly string Unpin = From(0xE77A);

    /// <summary>[S-1324] Cloud badges: available on this device (outline) and always kept (solid).</summary>
    public static readonly string CheckCircle = From(0xE930);

    public static readonly string CheckCircleSolid = From(0xEC61);

    /// <summary>[S-1330] "Show more options": the shell's own menu.</summary>
    public static readonly string More = From(0xE712);

    /// <summary>[S-1323] The breadcrumb's separator.</summary>
    public static readonly string ChevronRight = From(0xE76C);

    /// <summary>[S-1405] An app layout (Segoe "Tiles").</summary>
    public static readonly string AppLayout = From(0xECA5);

    /// <summary>[S-1411] Split a pane side by side / one above the other (Segoe "DockLeft" / "DockBottom").</summary>
    public static readonly string SplitSideBySide = From(0xE90C);

    /// <inheritdoc cref="SplitSideBySide"/>
    public static readonly string SplitStacked = From(0xE90E);

    private static string From(int codePoint) => char.ConvertFromUtf32(codePoint);
}
