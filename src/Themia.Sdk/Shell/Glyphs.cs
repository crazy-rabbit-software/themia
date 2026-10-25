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
public static class Glyphs
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
    public static readonly string SelectAll = From(0xE8B3);
    public static readonly string Theme = From(0xE793);
    public static readonly string Opacity = From(0xE7E6);
    public static readonly string Surface = From(0xE8B9);
    public static readonly string Font = From(0xE8D2);
    public static readonly string FontColour = From(0xE8D3);
    public static readonly string TextSize = From(0xE8E9);
    public static readonly string Scale = From(0xE8A9);
    public static readonly string Motion = From(0xE9D2);
    public static readonly string Clock = From(0xE823);
    public static readonly string Timer = From(0xE916);
    public static readonly string Hours = From(0xE917);
    public static readonly string Calendar = From(0xE787);

    public static readonly string Mail = From(0xE715);
    public static readonly string Unread = From(0xE8BD);
    public static readonly string Count = From(0xE8FD);
    public static readonly string Preview = From(0xE7C3);
    public static readonly string Startup = From(0xE7E8);

    /// <summary>
    /// [S-3525] Start with Windows: Segoe's "SpeedHigh2" speedometer, as Task Manager marks its Startup
    /// apps, not the power button Quit Themia wears.
    /// </summary>
    public static readonly string StartWithWindows = From(0xF42F);

    public static readonly string Cpu = From(0xE950);
    public static readonly string Memory = From(0xE964);
    public static readonly string Disk = From(0xEDA2);
    public static readonly string Battery = From(0xE83F);
    public static readonly string Network = From(0xE968);

    /// <summary>[S-6264] [S-6262] The network numbers' download and upload, and the battery style's charging bolt (LightningBolt).</summary>
    public static readonly string Download = From(0xE896);
    public static readonly string Upload = From(0xE898);
    public static readonly string ChargingBolt = From(0xE945);
    public static readonly string Volume = From(0xE767);
    public static readonly string Gpu = From(0xF211);
    public static readonly string Contact = From(0xE77B);
    public static readonly string Task = From(0xE9D5);
    public static readonly string Ping = From(0xEBC3);
    public static readonly string Note = From(0xE70B);
    public static readonly string Countdown = From(0xE81C);
    public static readonly string Calculator = From(0xE8EF);
    public static readonly string Sun = From(0xE706);
    public static readonly string Moon = From(0xE708);
    public static readonly string Place = From(0xE707);

    /// <summary>[S-2850] Windows' own location glyph (Segoe "Location"): the device's location, not a place picked.</summary>
    public static readonly string Location = From(0xE81D);
    public static readonly string Sort = From(0xE8CB);

    /// <summary>[S-611] To Do's importance star (Segoe "FavoriteStar"), for the Tasks importance filter.</summary>
    public static readonly string Important = From(0xE734);
    public static readonly string Play = From(0xE768);
    public static readonly string Pause = From(0xE769);
    public static readonly string Sound = From(0xE767);
    public static readonly string Link = From(0xE71B);

    /// <summary>[S-6416] Copy as path (Segoe's path box, unnamed in the icon list), Explorer's picture of it ([S-1754]).</summary>
    public static readonly string CopyAsPath = From(0xE62F);
    public static readonly string Wrap = From(0xE751); // [S-652] E7A3 is not in the font: a return arrow
    public static readonly string Clear = From(0xE894);
    public static readonly string EraseTool = From(0xE75C);
    public static readonly string FullScreen = From(0xE740);
    public static readonly string BackToWindow = From(0xE73F);
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
    public static readonly string Warning = From(0xE7BA);
    public static readonly string Undo = From(0xE7A7);
    /// <summary>[S-575] A birthday, in the calendar's time column and chips.</summary>
    public static readonly string Gift = From(0xF133);
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

    // [S-2416] [S-2433] The system details' list rows: an adapter by kind, a program without an icon.
    public static readonly string Ethernet = From(0xE839);
    public static readonly string Program = From(0xECAA);

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
    /// <summary>Headphone, not Karpo's E15D: that one draws the same speaker as the Sound toggle.</summary>
    public static readonly string MonoAudio = From(0xE7F6);
    public static readonly string ColorFilters = From(0xE2B1);
    /// <summary>Karpo's F7FC is not in Segoe Fluent Icons here (drew a box): the controller.</summary>
    public static readonly string GameMode = From(0xE7FC);
    public static readonly string Weather = From(0xE753);

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
    public static readonly string Quote = From(0xE9B2); // [S-652] E9E5 is not in the font: an opening quote mark
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

    // [S-4440] Windows' own panels, as Karpo draws them: the sun (the notifications' bell and Focus' target are their modules' own).
    public static readonly string Brightness = From(0xE706);

    /// <summary>A minus and Stop, for the focus session's stepper and its end.</summary>
    public static readonly string Minus = From(0xE738);
    public static readonly string Stop = From(0xE71A);
    public static readonly string Hide = From(0xED1A);

    /// <summary>
    /// [S-3702] Header placement on the edit bar: Segoe "DockBottom"; the top is the same glyph
    /// turned over (Segoe has no "DockTop"), hidden is <see cref="Hide"/>.
    /// </summary>
    public static readonly string HeaderBottom = From(0xE90E);

    /// <summary>[S-404] A header down the left or right edge: Segoe "DockLeft" and "DockRight".</summary>
    public static readonly string HeaderLeft = From(0xE90C);

    public static readonly string HeaderRight = From(0xE90D);

    /// <summary>[S-6584] The properties panel's Alignment choice: Segoe "AlignLeft", "AlignCenter" and "AlignRight".</summary>
    public static readonly string AlignLeft = From(0xE8E4);

    public static readonly string AlignCenter = From(0xE8E3);

    public static readonly string AlignRight = From(0xE8E2);

    // A web address wherever one is shown.
    public static readonly string Globe = From(0xE774);
    public static readonly string Rotate = From(0xE7AD);
    public static readonly string Refresh = From(0xE72C);
    public static readonly string Fit = From(0xE9A6);

    // The License page ([S-1068]).
    public static readonly string Shop = From(0xE719);
    public static readonly string Certificate = From(0xEB95);

    // The language row on the Language & region page ([S-1109]): Fluent's LocaleLanguage.
    public static readonly string Language = From(0xF2B7);

    // The Language & region page ([S-3797]): Windows Settings' "Time & language" (Segoe TimeLanguage).
    public static readonly string TimeLanguage = From(0xE775);

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

    /// <summary>[S-558] Write a new email: the pen the mail apps draw for it.</summary>
    public static readonly string MailCompose = From(0xE70F);
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

    /// <summary>[S-6416] Explorer's menu: Share (Segoe "Share").</summary>
    public static readonly string Share = From(0xE72D);

    /// <summary>[S-6416] "Compress to ZIP file" (Segoe "ZipFolder").</summary>
    public static readonly string ZipFolder = From(0xF012);

    /// <summary>[S-6416] Properties, drawn as Explorer's menu draws it: a wrench (Segoe "Repair").</summary>
    public static readonly string Properties = From(0xE90F);

    /// <summary>[S-6416] "Open in new window" (Segoe "NewWindow").</summary>
    public static readonly string NewWindow = From(0xE78B);

    /// <summary>[S-6416] "Open file location" (Segoe "FolderOpen").</summary>
    public static readonly string FolderOpen = From(0xE838);

    /// <summary>[S-1405] An app layout (Segoe "Tiles").</summary>
    public static readonly string AppLayout = From(0xECA5);

    /// <summary>[S-1411] Split a pane side by side / one above the other (Segoe "DockLeft" / "DockBottom").</summary>
    public static readonly string SplitSideBySide = From(0xE90C);

    /// <inheritdoc cref="SplitSideBySide"/>
    public static readonly string SplitStacked = From(0xE90E);

    /// <summary>[S-5320] The week planner (Segoe "MultiSelect": rows to tick, beside the calendars' pages).</summary>
    public static readonly string WeekPlanner = From(0xE762);

    /// <summary>[S-4820] Next meeting and its Join button (Segoe "VideoChat").</summary>
    public static readonly string Meeting = From(0xE8AA);

    /// <summary>[S-4860] The Recycle Bin (Segoe "Delete", the bin Windows draws).</summary>
    public static readonly string RecycleBin = From(0xE74D);

    /// <summary>[S-4660] A USB stick (Segoe "USB"): the removable drives.</summary>
    public static readonly string Usb = From(0xE88E);

    /// <summary>[S-4670] A mapped network drive (Segoe "MapDrive").</summary>
    public static readonly string NetworkDrive = From(0xE8CE);

    /// <summary>[S-4670] An optical drive (Segoe "CD").</summary>
    public static readonly string Disc = From(0xE958);

    private static string From(int codePoint) => char.ConvertFromUtf32(codePoint);
}
