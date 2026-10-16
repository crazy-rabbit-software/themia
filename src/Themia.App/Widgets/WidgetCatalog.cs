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
    /// [S-3412] The placed widget's id, for state a view keeps across being built again (a view
    /// switch): the gallery's slide. Null where there is no placed widget (a debug render).
    /// </summary>
    string? WidgetId => null;

    /// <summary>
    /// [S-3300] The view links of the view this widget is on: what the widgets there hover and
    /// select, shared. Read in Attach (a card can move to another view between attaches), join with
    /// <see cref="Themia.Core.Links.LinkBoard.Join"/>, dispose the member in Detach. Null where there
    /// is no view (a host without one).
    /// </summary>
    Themia.Core.Links.LinkBoard? Links => null;

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
/// <remarks>Where the add menu lists it is the add tree's ([S-3511], Core <c>AddTree</c>), not the catalogue's.</remarks>
internal readonly record struct WidgetDefinition(
    WidgetKind Kind,
    Str LabelId,
    Str DescriptionId,
    string Glyph,
    double DefaultWidthPx,
    double DefaultHeightPx)
{
    /// <summary>[S-1109] The display name in the current language, read each time so a language switch reaches every caller.</summary>
    public string Label => LabelId.Text();

    /// <summary>The picker's one-line description in the current language.</summary>
    public string Description => WidgetCatalog.Describe(Strings.Current, DescriptionId);
}

/// <summary>
/// The widget registry. One place that maps a <see cref="WidgetKind"/> to its metadata and its view,
/// so adding a widget is one entry here plus one enum member — there is no second list to forget.
/// </summary>
internal static class WidgetCatalog
{
    public static readonly IReadOnlyList<WidgetDefinition> All =
    [
        new(WidgetKind.Clock, Str.Widget_Clock_Label, Str.WidgetsA_Catalog_Clock_Description, Glyphs.Clock, 280, 140),
        // [S-5300] The date without the time, beside the clock.
        new(WidgetKind.Date, Str.DateWidget_Label, Str.DateWidget_Desc, Glyphs.Date, 240, 200),
        new(WidgetKind.Cpu, Str.WidgetsA_Catalog_Cpu_Label, Str.WidgetsA_Catalog_Cpu_Description, Glyphs.Cpu, 200, 150),
        new(WidgetKind.Memory, Str.WidgetsA_Catalog_Memory_Label, Str.WidgetsA_Catalog_Memory_Description, Glyphs.Memory, 200, 150),
        new(WidgetKind.Disk, Str.WidgetsA_Catalog_Disk_Label, Str.WidgetsA_Catalog_Disk_Description, Glyphs.Disk, 200, 150),
        new(WidgetKind.Battery, Str.Widget_Battery_Label, Str.Shell_Meters_BatteryDescription, Glyphs.Battery, 200, 150),
        new(WidgetKind.Network, Str.WidgetsA_Catalog_Network_Label, Str.WidgetsA_Catalog_Network_Description, Glyphs.Network, 200, 150),
        new(WidgetKind.Ping, Str.Widget_Ping_Label, Str.WidgetsA_Catalog_Ping_Description, Glyphs.Ping, 200, 150),
        new(WidgetKind.Gpu, Str.WidgetsA_Catalog_Gpu_Label, Str.Shell_Meters_GpuDescription, Glyphs.Gpu, 220, 220),
        // [S-2400] The detail widgets, right after the meters they go with.
        new(WidgetKind.CpuDetails, Str.Details_Catalog_Cpu_Label, Str.Details_Catalog_Cpu_Desc, Glyphs.Cpu, 280, 240),
        new(WidgetKind.MemoryDetails, Str.Details_Catalog_Memory_Label, Str.Details_Catalog_Memory_Desc, Glyphs.Memory, 280, 240),
        new(WidgetKind.TopProcesses, Str.Details_Catalog_Processes_Label, Str.Details_Catalog_Processes_Desc, Glyphs.ListLayout, 300, 300),
        new(WidgetKind.NetworkDetails, Str.Details_Catalog_Network_Label, Str.Details_Catalog_Network_Desc, Glyphs.Network, 300, 280),
        // [S-4670] [S-4660] One drive, and what is plugged in.
        new(WidgetKind.Drive, Str.Drives_Drive_Label, Str.Drives_Drive_Desc, Glyphs.Disk, 280, 160),
        new(WidgetKind.RemovableDrives, Str.Drives_Removable_Label, Str.Drives_Removable_Desc, Glyphs.Usb, 300, 200),
        new(WidgetKind.BatteryDetails, Str.Details_Catalog_Battery_Label, Str.Details_Catalog_Battery_Desc, Glyphs.Battery, 280, 240),
        new(WidgetKind.PingDetails, Str.Details_Catalog_Ping_Label, Str.Details_Catalog_Ping_Desc, Glyphs.Ping, 260, 240),
        new(WidgetKind.GpuDetails, Str.Details_Catalog_Gpu_Label, Str.Details_Catalog_Gpu_Desc, Glyphs.Gpu, 280, 240),
        new(WidgetKind.Mixer, Str.WidgetsA_Catalog_Mixer_Label, Str.WidgetsA_Catalog_Mixer_Description, Glyphs.Mixer, 300, 260),
        // [S-3320] Screen time is three widgets that light the same app together on a view ([S-3310]).
        new(WidgetKind.ScreenTimeChart, Str.WidgetsC_ScreenTime_Chart_Label, Str.WidgetsC_ScreenTime_Chart_Desc, Glyphs.ScreenTimeChart, 320, 220),
        new(WidgetKind.ScreenTimeDonut, Str.WidgetsC_ScreenTime_Donut_Label, Str.WidgetsC_ScreenTime_Donut_Desc, Glyphs.ScreenTimeDonut, 240, 260),
        new(WidgetKind.ScreenTimeApps, Str.WidgetsC_ScreenTime_Apps_Label, Str.WidgetsC_ScreenTime_Apps_Desc, Glyphs.ScreenTime, 280, 300),
        new(WidgetKind.Email, Str.WidgetsA_Catalog_Mail_Label, Str.WidgetsA_Catalog_Mail_Description, Glyphs.Mail, 380, 260),
        // [S-3600] The five calendars, one per layout (one family of the add menu, [S-3511]).
        new(WidgetKind.MonthCalendar, Str.WidgetsC_CalMonth_Label, Str.WidgetsC_CalMonth_Desc, Glyphs.Calendar, 340, 260),
        new(WidgetKind.WeekCalendar, Str.WidgetsC_CalWeek_Label, Str.WidgetsC_CalWeek_Desc, Glyphs.CalendarWeek, 480, 300),
        new(WidgetKind.DayCalendar, Str.WidgetsC_CalDay_Label, Str.WidgetsC_CalDay_Desc, Glyphs.CalendarDay, 280, 340),
        new(WidgetKind.YearCalendar, Str.WidgetsC_CalYear_Label, Str.WidgetsC_CalYear_Desc, Glyphs.CalendarYear, 420, 300),
        new(WidgetKind.Agenda, Str.WidgetsC_Agenda_Label, Str.WidgetsC_Agenda_Desc, Glyphs.Agenda, 320, 300),
        new(WidgetKind.NextMeeting, Str.Meeting_Label, Str.Meeting_Desc, Glyphs.Meeting, 320, 200),
        // [S-5320] The days ahead and today's tasks, beside the calendars.
        new(WidgetKind.WeekPlanner, Str.Planner_Label, Str.Planner_Desc, Glyphs.WeekPlanner, 480, 280),
        new(WidgetKind.Tasks, Str.WidgetsA_Catalog_Tasks_Label, Str.WidgetsA_Catalog_Tasks_Description, Glyphs.Task, 320, 320),
        new(WidgetKind.Contacts, Str.Widget_Contacts_Label, Str.WidgetsA_Catalog_Contacts_Description, Glyphs.Contact, 320, 260),
        new(WidgetKind.Notes, Str.Widget_Notes_Label, Str.WidgetsA_Catalog_Notes_Description, Glyphs.Note, 300, 220),
        new(WidgetKind.Countdown, Str.Widget_Countdown_Label, Str.WidgetsA_Catalog_Countdown_Description, Glyphs.Countdown, 300, 200),
        new(WidgetKind.Pomodoro, Str.Widget_Pomodoro_Label, Str.WidgetsA_Catalog_Pomodoro_Description, Glyphs.Pomodoro, 240, 200),
        // [S-4600] [S-4620] The stopwatch and the alarm, in the timers' style, beside them.
        new(WidgetKind.Stopwatch, Str.Stopwatch_Label, Str.Stopwatch_Desc, Glyphs.Stopwatch, 260, 200),
        new(WidgetKind.Alarm, Str.Alarm_Label, Str.Alarm_Desc, Glyphs.Alarm, 300, 220),
        // [S-4420] A focus session that holds Windows' do not disturb, beside the pomodoro.
        new(WidgetKind.FocusSession, Str.Panels_Focus_Label, Str.Panels_Focus_Desc, Glyphs.Focus, 240, 200),
        // [S-3100] The sun and the moon are two widgets.
        new(WidgetKind.Sun, Str.WidgetsC_Sun_Label, Str.WidgetsC_Sun_Desc, Glyphs.Sun, 320, 240),
        new(WidgetKind.Moon, Str.WidgetsC_Moon_Label, Str.WidgetsC_Moon_Desc, Glyphs.Moon, 240, 220),
        new(WidgetKind.YearProgress, Str.YearProgress_Label, Str.YearProgress_Desc, Glyphs.YearProgress, 280, 220),
        // [S-4120] The folder layouts are four widgets (one family of the add menu, [S-3511]).
        new(WidgetKind.Folder, Str.Variants_Folder_List_Label, Str.Variants_Folder_List_Desc, Glyphs.Folder, 300, 280),
        new(WidgetKind.FolderDetails, Str.Variants_Folder_Details_Label, Str.Variants_Folder_Details_Desc, Glyphs.FolderDetails, 440, 280),
        new(WidgetKind.FolderGrid, Str.Variants_Folder_Grid_Label, Str.Variants_Folder_Grid_Desc, Glyphs.FolderGrid, 380, 300),
        new(WidgetKind.FolderIcons, Str.Variants_Folder_Icons_Label, Str.Variants_Folder_Icons_Desc, Glyphs.FolderIcons, 300, 200),
        new(WidgetKind.RecentFiles, Str.RecentFiles_Label, Str.RecentFiles_Desc, Glyphs.RecentFiles, 320, 300),
        new(WidgetKind.RecycleBin, Str.WidgetsC_Location_RecycleBin, Str.RecycleBin_Desc, Glyphs.RecycleBin, 200, 200),
        new(WidgetKind.Volume, Str.Widget_Volume_Label, Str.WidgetsA_Catalog_Volume_Description, Glyphs.Volume, 200, 150),
        // [S-4440] [S-4400] Windows' own brightness and notification list, beside the volume.
        new(WidgetKind.Brightness, Str.Panels_Brightness_Label, Str.Panels_Brightness_Desc, Glyphs.Brightness, 200, 150),
        new(WidgetKind.Notifications, Str.Panels_Notifications_Label, Str.Panels_Notifications_Desc, Glyphs.Notifications, 320, 320),
        // [S-1910] One widget per power action, each a single button.
        new(WidgetKind.Shutdown, Str.Power_Action_Shutdown, Str.WidgetsC_Power_Desc_Shutdown, Glyphs.Power, 120, 120),
        new(WidgetKind.Restart, Str.Power_Action_Restart, Str.WidgetsC_Power_Desc_Restart, Glyphs.Reset, 120, 120),
        new(WidgetKind.Sleep, Str.Power_Action_Sleep, Str.WidgetsC_Power_Desc_Sleep, Glyphs.Moon, 120, 120),
        new(WidgetKind.Hibernate, Str.Power_Action_Hibernate, Str.WidgetsC_Power_Desc_Hibernate, Glyphs.Hibernate, 120, 120),
        new(WidgetKind.Lock, Str.Power_Action_Lock, Str.WidgetsC_Power_Desc_Lock, Glyphs.Lock, 120, 120),
        new(WidgetKind.SignOut, Str.Power_Action_SignOut, Str.WidgetsC_Power_Desc_SignOut, Glyphs.SignOut, 120, 120),
        new(WidgetKind.ScreenOff, Str.Power_Action_ScreenOff, Str.WidgetsC_Power_Desc_ScreenOff, Glyphs.Display, 120, 120),
        // [S-3900] One widget per quick setting, each a single switch.
        new(WidgetKind.WifiToggle, Str.Toggles_Wifi_Label, Str.Toggles_Wifi_Desc, Glyphs.Wifi, 120, 120),
        new(WidgetKind.BluetoothToggle, Str.Toggles_Bluetooth_Label, Str.Toggles_Bluetooth_Desc, Glyphs.Bluetooth, 120, 120),
        new(WidgetKind.AirplaneModeToggle, Str.Toggles_AirplaneMode_Label, Str.Toggles_AirplaneMode_Desc, Glyphs.Airplane, 120, 120),
        new(WidgetKind.EnergySaverToggle, Str.Toggles_EnergySaver_Label, Str.Toggles_EnergySaver_Desc, Glyphs.EnergySaver, 120, 120),
        new(WidgetKind.NightLightToggle, Str.Toggles_NightLight_Label, Str.Toggles_NightLight_Desc, Glyphs.NightLight, 120, 120),
        new(WidgetKind.RotationLockToggle, Str.Toggles_RotationLock_Label, Str.Toggles_RotationLock_Desc, Glyphs.RotationLock, 120, 120),
        new(WidgetKind.DoNotDisturbToggle, Str.Toggles_DoNotDisturb_Label, Str.Toggles_DoNotDisturb_Desc, Glyphs.QuietHours, 120, 120),
        new(WidgetKind.SoundToggle, Str.Toggles_Sound_Label, Str.Toggles_Sound_Desc, Glyphs.Volume3, 120, 120),
        new(WidgetKind.MicrophoneToggle, Str.Toggles_Microphone_Label, Str.Toggles_Microphone_Desc, Glyphs.Microphone, 120, 120),
        new(WidgetKind.SpatialSoundToggle, Str.Toggles_SpatialSound_Label, Str.Toggles_SpatialSound_Desc, Glyphs.SpatialSound, 120, 120),
        new(WidgetKind.MonoAudioToggle, Str.Toggles_MonoAudio_Label, Str.Toggles_MonoAudio_Desc, Glyphs.MonoAudio, 120, 120),
        new(WidgetKind.ColorFiltersToggle, Str.Toggles_ColorFilters_Label, Str.Toggles_ColorFilters_Desc, Glyphs.ColorFilters, 120, 120),
        new(WidgetKind.GameModeToggle, Str.Toggles_GameMode_Label, Str.Toggles_GameMode_Desc, Glyphs.GameMode, 120, 120),
        new(WidgetKind.Bluetooth, Str.Widget_Bluetooth_Label, Str.WidgetsA_Catalog_Bluetooth_Description, Glyphs.Bluetooth, 300, 240),
        new(WidgetKind.Music, Str.WidgetsA_Catalog_Music_Label, Str.WidgetsA_Catalog_Music_Description, Glyphs.Music, 340, 160),
        // [S-3200] [S-3210] The spectrum, split out of Music, beside it: six designs, six widgets.
        new(WidgetKind.AudioVisualizer, Str.Variants_Visualizer_Bars_Label, Str.Variants_Visualizer_Bars_Desc, Glyphs.Visualizer, 320, 160),
        new(WidgetKind.VisualizerMirror, Str.Variants_Visualizer_Mirror_Label, Str.Variants_Visualizer_Mirror_Desc, Glyphs.VisualizerMirror, 320, 160),
        new(WidgetKind.VisualizerWave, Str.Variants_Visualizer_Wave_Label, Str.Variants_Visualizer_Wave_Desc, Glyphs.VisualizerWave, 320, 160),
        new(WidgetKind.VisualizerDots, Str.Variants_Visualizer_Dots_Label, Str.Variants_Visualizer_Dots_Desc, Glyphs.VisualizerDots, 320, 160),
        new(WidgetKind.VisualizerRadial, Str.Variants_Visualizer_Radial_Label, Str.Variants_Visualizer_Radial_Desc, Glyphs.VisualizerRadial, 240, 240),
        new(WidgetKind.VisualizerScope, Str.Variants_Visualizer_Scope_Label, Str.Variants_Visualizer_Scope_Desc, Glyphs.VisualizerScope, 320, 160),
        new(WidgetKind.WeatherNow, Str.Widget_WeatherCurrent_Label, Str.WidgetsA_Catalog_Weather_Description, Glyphs.Weather, 260, 200),
        new(WidgetKind.WeatherForecast, Str.WidgetsA_Catalog_Forecast_Label, Str.WidgetsA_Catalog_Forecast_Description, Glyphs.Forecast, 380, 220),
        new(WidgetKind.Stocks, Str.Widget_Stocks_Label, Str.WidgetsA_Catalog_Stocks_Description, Glyphs.Stocks, 320, 240),
        // [S-5100] [S-5120] [S-5140] The newer online widgets, beside their weather and market siblings.
        new(WidgetKind.AirQuality, Str.Online_Air_Label, Str.Online_Air_Desc, Glyphs.AirQuality, 260, 200),
        new(WidgetKind.RainNextHour, Str.Online_Rain_Label, Str.Online_Rain_Desc, Glyphs.Rain, 300, 180),
        new(WidgetKind.ExchangeRates, Str.Online_Rates_Label, Str.Online_Rates_Desc, Glyphs.ExchangeRates, 300, 220),
        new(WidgetKind.Rss, Str.WidgetsA_Catalog_Feed_Label, Str.WidgetsA_Catalog_Feed_Description, Glyphs.Rss, 360, 280),
        new(WidgetKind.Shortcuts, Str.WidgetsA_Catalog_Shortcuts_Label, Str.WidgetsA_Catalog_Shortcuts_Description, Glyphs.Shortcut, 320, 220),
        new(WidgetKind.Clipboard, Str.WidgetsA_Catalog_Clipboard_Label, Str.WidgetsA_Catalog_Clipboard_Description, Glyphs.Clipboard, 320, 260),
        new(WidgetKind.Scratchpad, Str.Widget_Scratchpad_Label, Str.WidgetsA_Catalog_Scratchpad_Description, Glyphs.Scratchpad, 300, 260),
        new(WidgetKind.Quote, Str.WidgetsA_Catalog_Quote_Label, Str.WidgetsA_Catalog_Quote_Description, Glyphs.Quote, 320, 180),
        new(WidgetKind.Gallery, Str.Widget_Gallery_Label, Str.WidgetsA_Catalog_Gallery_Description, Glyphs.Gallery, 320, 240),
        new(WidgetKind.VideoStream, Str.WidgetsA_Catalog_VideoStream_Label, Str.WidgetsA_Catalog_VideoStream_Description, Glyphs.Video, 320, 240),
        new(WidgetKind.HabitTracker, Str.WidgetsA_Catalog_Habits_Label, Str.WidgetsA_Catalog_Habits_Description, Glyphs.Habit, 320, 240),
    ];

    /// <summary>
    /// [S-943] What the picker's search matches a widget on. Name, description and the add menu's
    /// folders it is in (<paramref name="path"/>, [S-3513]) come in the current language and, when
    /// that is not English, in English as well — a German user who types "weather" still finds the
    /// Wetter widget, and the curated keywords (<see cref="WidgetKeywords"/>, English only) keep
    /// working in every language.
    /// </summary>
    public static WidgetSearchDoc SearchDoc(WidgetDefinition definition, IReadOnlyList<AddShelf> path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var locale = Strings.Current;
        return new WidgetSearchDoc(
            definition.Kind.ToString(),
            Both(locale, definition.LabelId),
            WidgetKeywords.For(definition.Kind),
            Both(locale, definition.DescriptionId),
            [.. path.SelectMany(shelf => Both(locale, shelf.Title))],
            WidgetKeywords.Providers(definition.Kind));
    }

    /// <summary>
    /// [S-1115] A catalogue text with its facts filled: a number in a description ("15-minute steps")
    /// is a placeholder, so it is written in the language's own digits. A text without one is as is.
    /// </summary>
    public static string Describe(Locale locale, Str id)
        => Strings.Format(locale, id, ("minutes", (long)RainRules.Step.TotalMinutes));

    /// <summary>The text in <paramref name="locale"/>, then in English when that differs; the first is the shown one.</summary>
    private static string[] Both(Locale locale, Str id)
    {
        var shown = Describe(locale, id);
        var english = Describe(Locale.En, id);
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
        WidgetKind.Date => new DateView(),
        WidgetKind.Cpu or WidgetKind.Memory or WidgetKind.Disk
            or WidgetKind.Battery or WidgetKind.Network or WidgetKind.Ping => new MeterView(kind),
        WidgetKind.Gpu => new GpuView(),
        WidgetKind.CpuDetails or WidgetKind.MemoryDetails or WidgetKind.TopProcesses or WidgetKind.NetworkDetails
            or WidgetKind.BatteryDetails or WidgetKind.PingDetails or WidgetKind.GpuDetails => new DetailView(kind),
        WidgetKind.Mixer => new MixerView(),
        WidgetKind.ScreenTimeChart => new ScreenTimeChartView(),
        WidgetKind.ScreenTimeDonut => new ScreenTimeDonutView(),
        WidgetKind.ScreenTimeApps => new ScreenTimeAppsView(),
        WidgetKind.Email => new EmailView(),
        WidgetKind.Contacts => new ContactsView(),
        WidgetKind.MonthCalendar => new CalendarView(CalendarLayout.Month),
        WidgetKind.WeekCalendar => new CalendarView(CalendarLayout.Week),
        WidgetKind.DayCalendar => new CalendarView(CalendarLayout.Day),
        WidgetKind.YearCalendar => new CalendarView(CalendarLayout.Year),
        WidgetKind.Agenda => new CalendarView(CalendarLayout.Agenda),

        WidgetKind.Tasks => new TasksView(),
        WidgetKind.Notes => new NotesView(),
        WidgetKind.Countdown => new CountdownView(),
        WidgetKind.Pomodoro => new PomodoroView(),
        WidgetKind.Stopwatch => new StopwatchView(),
        WidgetKind.Alarm => new AlarmView(),
        WidgetKind.Drive => new DriveView(),
        WidgetKind.RemovableDrives => new RemovableDrivesView(),
        WidgetKind.Sun => new SunView(),
        WidgetKind.Moon => new MoonView(),
        WidgetKind.YearProgress => new YearProgressView(),
        WidgetKind.NextMeeting => new NextMeetingView(),
        WidgetKind.WeekPlanner => new WeekPlannerView(),
        WidgetKind.RecentFiles => new RecentFilesView(),
        WidgetKind.RecycleBin => new RecycleBinView(),
        WidgetKind.Folder or WidgetKind.FolderDetails or WidgetKind.FolderGrid or WidgetKind.FolderIcons => new FolderView(kind),
        WidgetKind.Volume => new VolumeView(),
        WidgetKind.Brightness => new BrightnessView(),
        WidgetKind.Notifications => new NotificationsView(),
        WidgetKind.FocusSession => new FocusSessionView(),
        WidgetKind.Shutdown or WidgetKind.Restart or WidgetKind.Sleep or WidgetKind.Hibernate
            or WidgetKind.Lock or WidgetKind.SignOut or WidgetKind.ScreenOff => new PowerButtonView(kind),
        _ when QuickToggleRules.ForKind(kind) is not null => new QuickToggleView(kind),
        WidgetKind.Bluetooth => new BluetoothView(),
        WidgetKind.Music => new MusicView(),
        _ when VisualizerLayout.StyleOf(kind) is not null => new AudioVisualizerView(kind),
        WidgetKind.WeatherNow => new WeatherNowView(),
        WidgetKind.WeatherForecast => new WeatherForecastView(),
        WidgetKind.Stocks => new StocksView(),
        WidgetKind.AirQuality => new AirQualityView(),
        WidgetKind.RainNextHour => new RainNextHourView(),
        WidgetKind.ExchangeRates => new ExchangeRatesView(),
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
