using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// What a widget view is allowed to ask of the surface it is drawn on.
/// <para>
/// The settings window is the writer, and the card owns input. A view that needs more — a note,
/// whose text <i>is</i> its config, a list that is typed into — gets it through <see cref="IWidgetSurfaceAware"/> rather than through a static hook or a widened
/// <see cref="IWidgetView.Attach"/>, so nothing that does not need it has to know it exists.
/// </para>
/// </summary>
public interface IWidgetSurface
{
    /// <summary>The user's own title for this widget, or null to use the catalogue label.</summary>
    string? Label { get; }

    /// <summary>
    /// Offer a title the widget knows and the catalogue cannot: the place a Sun &amp; Moon tile is
    /// computed for, the folder a listing is showing.
    /// <para>
    /// Pushed rather than pulled, because it changes when the widget's own state changes and the
    /// card has no way to know when that is. The user's own label still wins: a title they typed
    /// is never overruled by one the widget worked out.
    /// </para>
    /// </summary>
    void SetHeaderTitle(string? title);

    /// <summary>
    /// Persist the config this view has just changed. Must not re-run layout: a relayout would push
    /// the config back into the control the user is typing in.
    /// </summary>
    void RequestSave();

    /// <summary>
    /// Ask for, or give up, the keyboard.
    /// <para>
    /// Outside edit mode the surface carries <c>WS_EX_NOACTIVATE</c>, so it never becomes the
    /// foreground window and no keystroke reaches it — a text box on it would show a caret while the
    /// typing went to whatever app actually had focus. Asking here lifts that for as long as the
    /// widget is being typed into. The cost is inherent to Win32 focus and worth naming: while you
    /// type into a widget, the window you were in loses focus.
    /// </para>
    /// <para>
    /// [S-801] This is the shell's keyboard lease, held for typing: call it from a text box's
    /// GotFocus / LostFocus. A widget that navigates with keys implements <see cref="IWidgetKeys"/>
    /// instead and gets the lease on a click; when its editor closes it keeps the arrows. The lease
    /// ends by itself on Escape, a click elsewhere and the widget going away.
    /// </para>
    /// </summary>
    void RequestTextInput(bool wanted);

    /// <summary>
    /// [S-1251] True when the card is a preview (a debug render) rather than a widget on the
    /// desktop. A view whose buttons act on the machine — the power tile — must be inert there.
    /// </summary>
    bool IsPreview => false;

    /// <summary>
    /// [S-1911] Whether the desktop is in edit mode right now. There a click on a widget selects and
    /// moves it; a view whose whole tile is a button (the power widgets) leaves the press to the card.
    /// </summary>
    bool EditMode => false;

    /// <summary>
    /// [S-873] Paint nothing, or paint again: the card drops its fill, border and content and leaves
    /// the window's hit region, so the desktop under it is clickable. For a widget that has nothing
    /// to show right now (music with nothing playing). Edit mode always shows it, so a hidden widget
    /// can still be found, moved and configured. Not persisted: the view decides it from its state.
    /// </summary>
    void SetHidden(bool hidden);
}

/// <summary>Implemented by the few views that edit their own config or take typing.</summary>
public interface IWidgetSurfaceAware
{
    void Bind(IWidgetSurface surface);
}

/// <summary>
/// What a view puts on the right of the header the card draws for it — an unread badge, a count,
/// a busy ring.
/// <para>
/// The header itself belongs to the card, not to the view. Twenty-odd widgets each building their
/// own meant twenty-odd chances to disagree about its size, colour, spacing and behaviour, and it
/// left nowhere to put a setting like "draw it at the bottom instead" without asking every one of
/// them to honour it. The card knows the icon and the title already — the catalogue has one and the
/// instance has the other — so the only thing it cannot know is this.
/// </para>
/// </summary>
public interface IWidgetHeaderContent
{
    /// <summary>The trailing element, or null. Read once when the card builds its header.</summary>
    FrameworkElement? HeaderTrailing { get; }
}

/// <summary>What every widget view can do. Kept tiny on purpose: the card owns chrome and input.</summary>
public interface IWidgetView
{
    /// <summary>Bind the view to the shared ticker and its initial config.</summary>
    void Attach(Ticker ticker, WidgetConfig config);

    /// <summary>Re-read a config the user just changed.</summary>
    void ApplyConfig(WidgetConfig config);

    /// <summary>Release timers and handles. Must be safe to call more than once.</summary>
    void Detach();
}

/// <summary>One entry in the widget picker.</summary>
/// <param name="Kind">The persisted discriminator.</param>
/// <param name="LabelId">Display name, as a string id: read <see cref="Label"/> for the current language.</param>
/// <param name="DescriptionId">One line shown under the name in the picker.</param>
/// <param name="Glyph">Segoe Fluent Icons code point, so the picker matches the rest of Windows.</param>
/// <param name="DefaultWidthPx">[S-10] Default size in device-independent pixels, converted to a fraction at drop time.</param>
/// <param name="Category">[S-420] The picker column section it is listed under.</param>
internal readonly record struct WidgetDefinition(
    WidgetKind Kind,
    Str LabelId,
    Str DescriptionId,
    string Glyph,
    double DefaultWidthPx,
    double DefaultHeightPx,
    WidgetCategory Category)
{
    /// <summary>[S-1109] The display name in the current language, read each time so a language switch reaches every caller.</summary>
    public string Label => LabelId.Text();

    /// <summary>The picker's one-line description in the current language.</summary>
    public string Description => DescriptionId.Text();
}

/// <summary>
/// [S-420] The sections of the widget picker, in the order they are read. A closed set, and an
/// enum rather than a string on each definition, so a typo cannot quietly invent a tenth section.
/// </summary>
internal enum WidgetCategory
{
    Time,
    Accounts,
    System,
    Power,
    Information,
    Media,
    Launch,
    Desk,
    Trackers,
}

/// <summary>
/// The widget registry. One place that maps a <see cref="WidgetKind"/> to its metadata and its view,
/// so adding a widget is one entry here plus one enum member — there is no second list to forget.
/// </summary>
internal static class WidgetCatalog
{
    public static readonly IReadOnlyList<WidgetDefinition> All =
    [
        new(WidgetKind.Clock, Str.Widget_Clock_Label, Str.WidgetsA_Catalog_Clock_Description, Glyphs.Clock, 280, 140, WidgetCategory.Time),
        new(WidgetKind.Cpu, Str.WidgetsA_Catalog_Cpu_Label, Str.WidgetsA_Catalog_Cpu_Description, Glyphs.Cpu, 200, 150, WidgetCategory.System),
        new(WidgetKind.Memory, Str.WidgetsA_Catalog_Memory_Label, Str.WidgetsA_Catalog_Memory_Description, Glyphs.Memory, 200, 150, WidgetCategory.System),
        new(WidgetKind.Disk, Str.WidgetsA_Catalog_Disk_Label, Str.WidgetsA_Catalog_Disk_Description, Glyphs.Disk, 200, 150, WidgetCategory.System),
        new(WidgetKind.Battery, Str.Widget_Battery_Label, Str.Shell_Meters_BatteryDescription, Glyphs.Battery, 200, 150, WidgetCategory.System),
        new(WidgetKind.Network, Str.WidgetsA_Catalog_Network_Label, Str.WidgetsA_Catalog_Network_Description, Glyphs.Network, 200, 150, WidgetCategory.System),
        new(WidgetKind.Ping, Str.Widget_Ping_Label, Str.WidgetsA_Catalog_Ping_Description, Glyphs.Ping, 200, 150, WidgetCategory.System),
        new(WidgetKind.Gpu, Str.WidgetsA_Catalog_Gpu_Label, Str.Shell_Meters_GpuDescription, Glyphs.Gpu, 220, 220, WidgetCategory.System),
        // [S-2400] The detail widgets, right after the meters they go with.
        new(WidgetKind.CpuDetails, Str.Details_Catalog_Cpu_Label, Str.Details_Catalog_Cpu_Desc, Glyphs.Cpu, 280, 240, WidgetCategory.System),
        new(WidgetKind.MemoryDetails, Str.Details_Catalog_Memory_Label, Str.Details_Catalog_Memory_Desc, Glyphs.Memory, 280, 240, WidgetCategory.System),
        new(WidgetKind.TopProcesses, Str.Details_Catalog_Processes_Label, Str.Details_Catalog_Processes_Desc, Glyphs.ListLayout, 300, 300, WidgetCategory.System),
        new(WidgetKind.NetworkDetails, Str.Details_Catalog_Network_Label, Str.Details_Catalog_Network_Desc, Glyphs.Network, 300, 280, WidgetCategory.System),
        new(WidgetKind.DiskSpace, Str.Details_Catalog_Disk_Label, Str.Details_Catalog_Disk_Desc, Glyphs.Disk, 300, 240, WidgetCategory.System),
        new(WidgetKind.BatteryDetails, Str.Details_Catalog_Battery_Label, Str.Details_Catalog_Battery_Desc, Glyphs.Battery, 280, 240, WidgetCategory.System),
        new(WidgetKind.PingDetails, Str.Details_Catalog_Ping_Label, Str.Details_Catalog_Ping_Desc, Glyphs.Ping, 260, 240, WidgetCategory.System),
        new(WidgetKind.GpuDetails, Str.Details_Catalog_Gpu_Label, Str.Details_Catalog_Gpu_Desc, Glyphs.Gpu, 280, 240, WidgetCategory.System),
        new(WidgetKind.Mixer, Str.WidgetsA_Catalog_Mixer_Label, Str.WidgetsA_Catalog_Mixer_Description, Glyphs.Mixer, 300, 260, WidgetCategory.System),
        new(WidgetKind.ScreenTime, Str.WidgetsA_Catalog_ScreenTime_Label, Str.WidgetsA_Catalog_ScreenTime_Description, Glyphs.ScreenTime, 260, 300, WidgetCategory.System),
        new(WidgetKind.Email, Str.WidgetsA_Catalog_Mail_Label, Str.WidgetsA_Catalog_Mail_Description, Glyphs.Mail, 380, 260, WidgetCategory.Accounts),
        new(WidgetKind.Calendar, Str.Widget_Calendar_Label, Str.WidgetsA_Catalog_Calendar_Description, Glyphs.Calendar, 340, 260, WidgetCategory.Accounts),
        new(WidgetKind.Tasks, Str.WidgetsA_Catalog_Tasks_Label, Str.WidgetsA_Catalog_Tasks_Description, Glyphs.Task, 320, 320, WidgetCategory.Accounts),
        new(WidgetKind.Contacts, Str.Widget_Contacts_Label, Str.WidgetsA_Catalog_Contacts_Description, Glyphs.Contact, 320, 260, WidgetCategory.Accounts),
        new(WidgetKind.Notes, Str.Widget_Notes_Label, Str.WidgetsA_Catalog_Notes_Description, Glyphs.Note, 300, 220, WidgetCategory.Desk),
        new(WidgetKind.Countdown, Str.Widget_Countdown_Label, Str.WidgetsA_Catalog_Countdown_Description, Glyphs.Countdown, 300, 200, WidgetCategory.Time),
        new(WidgetKind.Pomodoro, Str.Widget_Pomodoro_Label, Str.WidgetsA_Catalog_Pomodoro_Description, Glyphs.Pomodoro, 240, 200, WidgetCategory.Time),
        // [S-3100] The sun and the moon are two widgets, side by side in the Time section.
        new(WidgetKind.Sun, Str.WidgetsC_Sun_Label, Str.WidgetsC_Sun_Desc, Glyphs.Sun, 320, 240, WidgetCategory.Time),
        new(WidgetKind.Moon, Str.WidgetsC_Moon_Label, Str.WidgetsC_Moon_Desc, Glyphs.Moon, 240, 220, WidgetCategory.Time),
        new(WidgetKind.Folder, Str.Widget_Folder_Label, Str.WidgetsA_Catalog_Folder_Description, Glyphs.Folder, 340, 280, WidgetCategory.Launch),
        new(WidgetKind.Volume, Str.Widget_Volume_Label, Str.WidgetsA_Catalog_Volume_Description, Glyphs.Volume, 200, 150, WidgetCategory.System),
        // [S-1910] One widget per power action, each a single button.
        new(WidgetKind.Shutdown, Str.Power_Action_Shutdown, Str.WidgetsC_Power_Desc_Shutdown, Glyphs.Power, 120, 120, WidgetCategory.Power),
        new(WidgetKind.Restart, Str.Power_Action_Restart, Str.WidgetsC_Power_Desc_Restart, Glyphs.Reset, 120, 120, WidgetCategory.Power),
        new(WidgetKind.Sleep, Str.Power_Action_Sleep, Str.WidgetsC_Power_Desc_Sleep, Glyphs.Moon, 120, 120, WidgetCategory.Power),
        new(WidgetKind.Hibernate, Str.Power_Action_Hibernate, Str.WidgetsC_Power_Desc_Hibernate, Glyphs.Hibernate, 120, 120, WidgetCategory.Power),
        new(WidgetKind.Lock, Str.Power_Action_Lock, Str.WidgetsC_Power_Desc_Lock, Glyphs.Lock, 120, 120, WidgetCategory.Power),
        new(WidgetKind.SignOut, Str.Power_Action_SignOut, Str.WidgetsC_Power_Desc_SignOut, Glyphs.SignOut, 120, 120, WidgetCategory.Power),
        new(WidgetKind.ScreenOff, Str.Power_Action_ScreenOff, Str.WidgetsC_Power_Desc_ScreenOff, Glyphs.Display, 120, 120, WidgetCategory.Power),
        new(WidgetKind.Bluetooth, Str.Widget_Bluetooth_Label, Str.WidgetsA_Catalog_Bluetooth_Description, Glyphs.Bluetooth, 300, 240, WidgetCategory.System),
        new(WidgetKind.Music, Str.WidgetsA_Catalog_Music_Label, Str.WidgetsA_Catalog_Music_Description, Glyphs.Music, 340, 160, WidgetCategory.Media),
        // [S-3200] The spectrum, split out of Music, beside it.
        new(WidgetKind.AudioVisualizer, Str.WidgetsC_Visualizer_Label, Str.WidgetsC_Visualizer_Desc, Glyphs.Visualizer, 320, 160, WidgetCategory.Media),
        new(WidgetKind.WeatherNow, Str.Widget_WeatherCurrent_Label, Str.WidgetsA_Catalog_Weather_Description, Glyphs.Weather, 260, 200, WidgetCategory.Information),
        new(WidgetKind.WeatherForecast, Str.WidgetsA_Catalog_Forecast_Label, Str.WidgetsA_Catalog_Forecast_Description, Glyphs.Forecast, 380, 220, WidgetCategory.Information),
        new(WidgetKind.Stocks, Str.Widget_Stocks_Label, Str.WidgetsA_Catalog_Stocks_Description, Glyphs.Stocks, 320, 240, WidgetCategory.Information),
        new(WidgetKind.Rss, Str.WidgetsA_Catalog_Feed_Label, Str.WidgetsA_Catalog_Feed_Description, Glyphs.Rss, 360, 280, WidgetCategory.Information),
        new(WidgetKind.Shortcuts, Str.WidgetsA_Catalog_Shortcuts_Label, Str.WidgetsA_Catalog_Shortcuts_Description, Glyphs.Shortcut, 320, 220, WidgetCategory.Launch),
        new(WidgetKind.Clipboard, Str.WidgetsA_Catalog_Clipboard_Label, Str.WidgetsA_Catalog_Clipboard_Description, Glyphs.Clipboard, 320, 260, WidgetCategory.Desk),
        new(WidgetKind.Scratchpad, Str.Widget_Scratchpad_Label, Str.WidgetsA_Catalog_Scratchpad_Description, Glyphs.Scratchpad, 300, 260, WidgetCategory.Desk),
        new(WidgetKind.Quote, Str.WidgetsA_Catalog_Quote_Label, Str.WidgetsA_Catalog_Quote_Description, Glyphs.Quote, 320, 180, WidgetCategory.Desk),
        new(WidgetKind.Gallery, Str.Widget_Gallery_Label, Str.WidgetsA_Catalog_Gallery_Description, Glyphs.Gallery, 320, 240, WidgetCategory.Media),
        new(WidgetKind.VideoStream, Str.WidgetsA_Catalog_VideoStream_Label, Str.WidgetsA_Catalog_VideoStream_Description, Glyphs.Video, 320, 240, WidgetCategory.Media),
        new(WidgetKind.HabitTracker, Str.WidgetsA_Catalog_Habits_Label, Str.WidgetsA_Catalog_Habits_Description, Glyphs.Habit, 320, 240, WidgetCategory.Trackers),
    ];

    /// <summary>The heading a picker section is shown under, in the current language.</summary>
    public static string Title(WidgetCategory category) => TitleId(category).Text();

    private static Str TitleId(WidgetCategory category) => category switch
    {
        WidgetCategory.Time => Str.WidgetsA_Catalog_Category_Time,
        WidgetCategory.Accounts => Str.WidgetsA_Catalog_Category_Accounts,
        WidgetCategory.System => Str.Category_System,
        WidgetCategory.Power => Str.WidgetsA_Catalog_Category_Power,
        WidgetCategory.Information => Str.WidgetsA_Catalog_Category_Information,
        WidgetCategory.Media => Str.WidgetsA_Catalog_Category_Media,
        WidgetCategory.Launch => Str.WidgetsA_Catalog_Category_Launch,
        WidgetCategory.Desk => Str.WidgetsA_Catalog_Category_Desk,
        _ => Str.WidgetsA_Catalog_Category_Trackers,
    };

    /// <summary>
    /// [S-943] What the picker's search matches a widget on. Name, description and section come in
    /// the current language and, when that is not English, in English as well — a German user who
    /// types "weather" still finds the Wetter widget, and the curated keywords
    /// (<see cref="WidgetKeywords"/>, English only) keep working in every language.
    /// </summary>
    public static WidgetSearchDoc SearchDoc(WidgetDefinition definition)
    {
        var locale = Strings.Current;
        var titleId = TitleId(definition.Category);
        return new WidgetSearchDoc(
            definition.Kind.ToString(),
            Both(locale, definition.LabelId),
            WidgetKeywords.For(definition.Kind),
            Both(locale, definition.DescriptionId),
            Both(locale, titleId),
            WidgetKeywords.Providers(definition.Kind));
    }

    /// <summary>The text in <paramref name="locale"/>, then in English when that differs; the first is the shown one.</summary>
    private static string[] Both(Locale locale, Str id)
    {
        var shown = Strings.Get(locale, id);
        var english = Strings.Get(Locale.En, id);
        return string.Equals(shown, english, StringComparison.Ordinal) ? [shown] : [shown, english];
    }

    public static WidgetDefinition Definition(WidgetKind kind)
    {
        foreach (var definition in All)
        {
            if (definition.Kind == kind)
            {
                return definition;
            }
        }

        // Unreachable for a validated state, but a total function beats an exception on a render path.
        return All[0];
    }

    /// <summary>Build the view for a kind. Returns a <see cref="FrameworkElement"/> that also implements <see cref="IWidgetView"/>.</summary>
    public static FrameworkElement CreateView(WidgetKind kind) => kind switch
    {
        WidgetKind.Clock => new ClockView(),
        WidgetKind.Cpu or WidgetKind.Memory or WidgetKind.Disk
            or WidgetKind.Battery or WidgetKind.Network or WidgetKind.Ping => new MeterView(kind),
        WidgetKind.Gpu => new GpuView(),
        WidgetKind.CpuDetails or WidgetKind.MemoryDetails or WidgetKind.TopProcesses or WidgetKind.NetworkDetails
            or WidgetKind.DiskSpace or WidgetKind.BatteryDetails or WidgetKind.PingDetails or WidgetKind.GpuDetails => new DetailView(kind),
        WidgetKind.Mixer => new MixerView(),
        WidgetKind.ScreenTime => new ScreenTimeView(),
        WidgetKind.Email => new EmailView(),
        WidgetKind.Contacts => new ContactsView(),
        WidgetKind.Calendar => new CalendarView(),
        WidgetKind.Tasks => new TasksView(),
        WidgetKind.Notes => new NotesView(),
        WidgetKind.Countdown => new CountdownView(),
        WidgetKind.Pomodoro => new PomodoroView(),
        WidgetKind.Sun => new SunView(),
        WidgetKind.Moon => new MoonView(),
        WidgetKind.Folder => new FolderView(),
        WidgetKind.Volume => new VolumeView(),
        WidgetKind.Shutdown or WidgetKind.Restart or WidgetKind.Sleep or WidgetKind.Hibernate
            or WidgetKind.Lock or WidgetKind.SignOut or WidgetKind.ScreenOff => new PowerButtonView(kind),
        WidgetKind.Bluetooth => new BluetoothView(),
        WidgetKind.Music => new MusicView(),
        WidgetKind.AudioVisualizer => new AudioVisualizerView(),
        WidgetKind.WeatherNow => new WeatherNowView(),
        WidgetKind.WeatherForecast => new WeatherForecastView(),
        WidgetKind.Stocks => new StocksView(),
        WidgetKind.Rss => new RssView(),
        WidgetKind.Shortcuts => new ShortcutsView(),
        WidgetKind.Clipboard => new ClipboardView(),
        WidgetKind.Scratchpad => new ScratchpadView(),
        WidgetKind.Quote => new QuoteView(),
        WidgetKind.Gallery => new GalleryView(),
        WidgetKind.VideoStream => new VideoStreamView(),
        WidgetKind.HabitTracker => new HabitTrackerView(),
        _ => new ClockView(),
    };
}
