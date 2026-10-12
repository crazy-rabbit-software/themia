using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>One widget's page: the shell rows every widget has, then the kind's own rows.</summary>
internal static partial class ConfigPages
{
    /// <summary>
    /// A widget's own settings.
    /// <para>
    /// Unlike the app pages these edit the live config directly and save on change, because the
    /// widget is visible on the desktop behind this window and the point is to see the effect.
    /// </para>
    /// </summary>
    private static FrameworkElement BuildWidget(AppHost host, string widgetId, Action<ConfigTarget> navigate)
    {
        if (host.Find(widgetId) is not var (_, widget) || widget is null)
        {
            return Page(Str.Settings_Style_Widget.Text(), [Note(Str.Shell_Widget_Gone.Text())]);
        }

        return WidgetPage(widget, navigate, () => host.Mutate(_ => { }));
    }

    /// <summary>
    /// [S-1542] A kind's page built over a throwaway widget, for the search index only: the rows a
    /// page of that kind has, without touching any real widget. Never shown and never saved — the
    /// widget belongs to no view and its save does nothing.
    /// </summary>
    internal static SettingsPage BuildWidgetForIndex(AppHost host, WidgetKind kind)
    {
        var widget = WidgetInstance.Create(kind, new Themia.Core.Geometry.FracRect(0, 0, 0.1, 0.1));
        return WidgetPage(widget, _ => { }, () => { });
    }

    /// <summary>
    /// [S-1750] Whether a kind's page has anything on it. Read off the rows the page builder makes
    /// for a throwaway widget of that kind (as the search index does), never a list kept beside the
    /// pages, so a kind that gains or loses its last row cannot disagree with its menus. Built once
    /// per kind and remembered: the rows of a kind's page are the same for each of its widgets.
    /// UI thread only, like the pages.
    /// </summary>
    internal static bool HasWidgetSettings(WidgetKind kind)
    {
        if (HasSettingsByKind.TryGetValue(kind, out var known))
        {
            return known;
        }

        bool has;
        try
        {
            var widget = WidgetInstance.Create(kind, new Themia.Core.Geometry.FracRect(0, 0, 0.1, 0.1));
            has = WidgetRows(widget, _ => { }, () => { }).Count > 0;
        }
        catch (Exception ex)
        {
            // Offer the page rather than hide settings that may well exist.
            Log.Error(Scope, $"could not read the rows of the {kind} settings page", ex);
            return true;
        }

        HasSettingsByKind[kind] = has;
        return has;
    }

    private static readonly Dictionary<WidgetKind, bool> HasSettingsByKind = [];

    private static SettingsPage WidgetPage(WidgetInstance widget, Action<ConfigTarget> navigate, Action save)
    {
        var definition = WidgetCatalog.Definition(widget.Kind);
        var rows = WidgetRows(widget, navigate, save);
        return Page(string.IsNullOrWhiteSpace(widget.Label) ? definition.Label : widget.Label!, rows, definition.Description);
    }

    /// <summary>The kind's own rows, in the page's order, the danger section last.</summary>
    private static List<FrameworkElement> WidgetRows(WidgetInstance widget, Action<ConfigTarget> navigate, Action save)
    {
        var rows = new List<FrameworkElement>();

        void Save() => save();

        switch (widget.Config)
        {
            case ClockConfig clock:
                WidgetSettings.Clock(rows, clock, Save);
                break;

            case DateConfig date:
                WidgetSettings.Date(rows, date, Save);
                break;

            case DiskConfig disk:
                WidgetSettings.Disk(rows, disk, Save);
                break;

            case BatteryConfig battery:
                WidgetSettings.Battery(rows, battery, Save);
                break;

            // Before GaugeConfig: Ping derives from it, and a base-type case placed first would
            // swallow the derived one.
            case PingConfig ping:
                WidgetSettings.Ping(rows, ping, Save);
                break;

            // Also before GaugeConfig, for the same reason.
            case GpuConfig gpu:
                WidgetSettings.Gpu(rows, gpu, Save);
                break;

            case CpuConfig cpu:
                WidgetSettings.Cpu(rows, cpu, Save);
                break;

            case MemoryConfig memory:
                WidgetSettings.Memory(rows, memory, Save);
                break;

            case NetworkConfig network:
                WidgetSettings.Network(rows, network, Save);
                break;

            case EmailConfig email:
                WidgetSettings.Email(rows, email, Save, navigate);
                break;

            case MixerConfig mixer:
                WidgetSettings.Mixer(rows, mixer, Save);
                break;

            case ScreenTimeChartConfig chart:
                WidgetSettings.ScreenTimeChart(rows, chart, Save);
                break;

            case ScreenTimeDonutConfig donut:
                WidgetSettings.ScreenTimeDonut(rows, donut, Save);
                break;

            case ScreenTimeAppsConfig apps:
                WidgetSettings.ScreenTimeApps(rows, apps, Save);
                break;

            // [S-2400] Every detail widget: its own rows, then its refresh.
            case DetailConfig details:
                WidgetSettings.Details(rows, details, Save);
                break;

            case ContactsConfig contacts:
                WidgetSettings.Contacts(rows, contacts, Save);
                break;

            // Before CalendarConfig: the planner is one, with its tasks after the calendar's rows.
            case WeekPlannerConfig planner:
                WidgetSettings.WeekPlanner(rows, planner, Save);
                break;

            case CalendarConfig calendar:
                WidgetSettings.Calendar(rows, calendar, Save);
                break;

            case TasksConfig tasks:
                WidgetSettings.Tasks(rows, tasks, Save, navigate);
                break;

            case NotesConfig notes:
                WidgetSettings.Notes(rows, notes, Save);
                break;

            case CountdownConfig countdown:
                WidgetSettings.Countdown(rows, countdown, Save);
                break;

            case PomodoroConfig pomodoro:
                WidgetSettings.Pomodoro(rows, pomodoro, Save);
                break;

            case AlarmConfig alarm:
                WidgetSettings.Alarm(rows, alarm, Save);
                break;

            case DriveConfig drive:
                WidgetSettings.Drive(rows, drive, Save);
                break;

            case SunConfig sun:
                WidgetSettings.Sun(rows, sun, Save);
                break;

            case MoonConfig moon:
                WidgetSettings.Moon(rows, moon, Save);
                break;

            case FolderConfig folder:
                WidgetSettings.Folder(rows, folder, Save);
                break;

            case VolumeConfig volume:
                WidgetSettings.Volume(rows, volume, Save);
                break;

            case FocusSessionConfig focus:
                WidgetSettings.FocusSession(rows, focus, Save);
                break;

            case BrightnessConfig brightness:
                WidgetSettings.Brightness(rows, brightness, Save);
                break;

            case BluetoothConfig bluetooth:
                WidgetSettings.Bluetooth(rows, bluetooth, Save);
                break;

            case MusicConfig music:
                WidgetSettings.Music(rows, music, Save);
                break;

            case WeatherNowConfig weatherNow:
                WidgetSettings.WeatherNow(rows, weatherNow, Save);
                break;

            case WeatherForecastConfig weatherForecast:
                WidgetSettings.WeatherForecast(rows, weatherForecast, Save);
                break;

            case AirQualityConfig air:
                WidgetSettings.AirQuality(rows, air, Save);
                break;

            case RainNextHourConfig rain:
                WidgetSettings.RainNextHour(rows, rain, Save);
                break;

            case ExchangeRatesConfig rates:
                WidgetSettings.ExchangeRates(rows, rates, Save);
                break;

            case StocksConfig stocks:
                WidgetSettings.Stocks(rows, stocks, Save);
                break;

            case RssConfig rss:
                WidgetSettings.Rss(rows, rss, Save);
                break;

            case VideoStreamConfig videoStream:
                WidgetSettings.VideoStream(rows, videoStream, Save);
                break;

            case ShortcutsConfig shortcuts:
                WidgetSettings.Shortcuts(rows, shortcuts, Save);
                break;

            case ClipboardConfig clipboard:
                WidgetSettings.Clipboard(rows, clipboard, Save);
                break;

            case ScratchpadConfig scratchpad:
                WidgetSettings.Scratchpad(rows, scratchpad, Save);
                break;

            case QuoteConfig quote:
                WidgetSettings.Quote(rows, quote, Save);
                break;

            case GalleryConfig gallery:
                WidgetSettings.Gallery(rows, gallery, Save);
                break;

            case HabitTrackerConfig habitTracker:
                WidgetSettings.HabitTracker(rows, habitTracker, Save);
                break;

            default:
                break;
        }

        // Every widget page is laid out the same way: what it shows, then how it behaves, then how
        // often it updates, and destructive actions last. A row the widget built with
        // FluentLayout.DangerRow is lifted out of wherever it sat and into the one danger section
        // below, so nothing that cannot be undone is left sitting between two switches. [S-3705]
        // The card itself (header, background, title) is set on the edit bar, and the widget is
        // deleted from its card menu or the Delete key: neither has a row here.
        var danger = rows.FindAll(FluentLayout.IsDanger);
        rows.RemoveAll(FluentLayout.IsDanger);

        if (danger.Count > 0)
        {
            rows.Add(FluentLayout.Header(Str.Shell_Settings_Danger.Text()));
            rows.AddRange(danger);
        }

        return rows;
    }
}
