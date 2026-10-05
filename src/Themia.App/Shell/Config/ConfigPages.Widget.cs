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
        if (host.Find(widgetId) is not var (view, widget) || widget is null)
        {
            return Page(Str.Settings_Style_Widget.Text(), [Note(Str.Shell_Widget_Gone.Text())]);
        }

        return WidgetPage(host, view, widget, navigate, () => host.Mutate(_ => { }));
    }

    /// <summary>
    /// [S-1542] A kind's page built over a throwaway widget, for the search index only: the rows a
    /// page of that kind has, without touching any real widget. Never shown and never saved — the
    /// widget belongs to no view and its save does nothing.
    /// </summary>
    internal static SettingsPage BuildWidgetForIndex(AppHost host, WidgetKind kind)
    {
        var widget = WidgetInstance.Create(kind, new Themia.Core.Geometry.FracRect(0, 0, 0.1, 0.1));
        return WidgetPage(host, ViewState.Create(string.Empty), widget, _ => { }, () => { });
    }

    private static SettingsPage WidgetPage(AppHost host, ViewState view, WidgetInstance widget, Action<ConfigTarget> navigate, Action save)
    {
        var widgetId = widget.Id;
        var definition = WidgetCatalog.Definition(widget.Kind);
        var rows = new List<FrameworkElement>();

        void Save() => save();

        switch (widget.Config)
        {
            case ClockConfig clock:
                WidgetSettings.Clock(rows, clock, Save);
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

            case ScreenTimeConfig screenTime:
                WidgetSettings.ScreenTime(rows, screenTime, Save);
                break;

            case ContactsConfig contacts:
                WidgetSettings.Contacts(rows, contacts, Save);
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

            case SunMoonConfig sunMoon:
                WidgetSettings.SunMoon(rows, sunMoon, Save);
                break;

            case FolderConfig folder:
                WidgetSettings.Folder(rows, folder, Save);
                break;

            case VolumeConfig volume:
                WidgetSettings.Volume(rows, volume, Save);
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
        // often it updates, then the card itself, and destructive actions last. The widget's own
        // rows cover the first three; a row it built with FluentLayout.DangerRow is lifted out of
        // wherever it sat and into the one danger section below, so nothing that cannot be undone
        // is left sitting between two switches.
        var danger = rows.FindAll(FluentLayout.IsDanger);
        rows.RemoveAll(FluentLayout.IsDanger);

        rows.Add(FluentLayout.Header(Str.Settings_Section_Appearance.Text()));

        rows.Add(FluentLayout.Row(Glyphs.View, Str.App_ConfigHeaderLabel.Text(), Str.Shell_Widget_HeaderDesc.Text(),
            FluentLayout.Choice(
                [
                    (HeaderPlacement.Top, Str.Group_HeaderSide_Top.Text()),
                    (HeaderPlacement.Bottom, Str.Group_HeaderSide_Bottom.Text()),
                    (HeaderPlacement.Hidden, Str.Toggle_Hidden.Text()),
                ],
                widget.Header,
                value => host.Mutate(_ => widget.Header = value))));

        rows.Add(FluentLayout.Row(Glyphs.Surface, Str.Settings_Style_BgLabel.Text(), Str.Shell_Widget_BackgroundDesc.Text(),
            FluentLayout.Choice(
                [
                    (WidgetSurface.Card, Str.Shell_Widget_Card.Text()),
                    (WidgetSurface.Translucent, Str.Shell_Widget_Translucent.Text()),
                    (WidgetSurface.Transparent, Str.Settings_Style_None.Text()),
                ],
                widget.Surface,
                value => host.Mutate(_ => widget.Surface = value))));

        var label = new TextBox
        {
            Text = widget.Label ?? string.Empty,
            MinWidth = 220,
            PlaceholderText = definition.Label,
        };

        label.LostFocus += (_, _) => host.Mutate(_ =>
            widget.Label = string.IsNullOrWhiteSpace(label.Text) ? null : label.Text.Trim());

        rows.Add(FluentLayout.Row(Glyphs.Rename, Str.Group_Title_Label.Text(), Str.Shell_Widget_TitleDesc.Text(), label));

        rows.Add(FluentLayout.Header(Str.Shell_Settings_Danger.Text()));
        rows.AddRange(danger);

        var delete = new Button { Content = Str.CtxMenu_DeleteWidget.Text() };
        delete.Click += (_, _) =>
        {
            host.Select(widgetId);
            host.DeleteSelected();
            navigate(ConfigTarget.ForView(view.Id));
        };

        rows.Add(FluentLayout.Row(Glyphs.Delete, Str.Common_Delete.Text(), Str.Shell_Widget_DeleteDesc.Format(("view", view.Name)), delete));

        return Page(string.IsNullOrWhiteSpace(widget.Label) ? definition.Label : widget.Label!, rows, definition.Description);
    }
}
