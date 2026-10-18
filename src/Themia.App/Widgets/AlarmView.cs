using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4628]..[S-4630] One alarm (Karpo §106): its time as the readout, its name and what it is doing
/// ("Tomorrow", "Weekdays", "Ringing") under it, and its switch — Snooze and Dismiss in the switch's
/// place while it rings. What the alarm is <i>doing</i> is the app's alarm service's ([S-4623]), which
/// rings it whether or not this view exists. Several alarms are several widgets.
/// </summary>
internal sealed partial class AlarmView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions
{
    /// <summary>A strip under this width keeps its switch under the time rather than beside it.</summary>
    private const double BesideWidth = 260;

    /// <summary>The buttons are worded from this width, glyphs under it.</summary>
    private const double WordedWidth = 300;

    private readonly StackPanel _text = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _time = Fluent.Text(FluentType.Title);
    private readonly TextBlock _words = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly ToggleSwitch _switch = new() { OnContent = string.Empty, OffContent = string.Empty, MinWidth = 0 };
    private readonly Button _snooze = new();
    private readonly Button _dismiss = new();
    private readonly StackPanel _ringButtons;
    private readonly Grid _controls = new() { VerticalAlignment = VerticalAlignment.Center };

    private Ticker? _ticker;
    private IDisposable? _minute;
    private readonly IWidgetHost _host;
    private IWidgetSurface? _surface;
    private AlarmConfig _config = new();
    private WidgetArea _area;
    private bool _beside;
    private bool _controlsShown = true;
    private bool _showing;
    private bool _detached;

    /// <summary>Debug renders: what the alarm is doing, in place of the service's answer.</summary>
#if DEBUG
    private AlarmStatus? _previewStatus;
#else
    // Release has no renders: the field's Debug-only writer (the .Preview.cs part) is not compiled.
    private AlarmStatus? _previewStatus => null;
#endif

    public AlarmView(IWidgetHost host)
    {
        _host = host;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_time, FontNumeralAlignment.Tabular);
        _time.MaxLines = 1;
        _time.TextTrimming = TextTrimming.Clip;
        _words.MaxLines = 1;
        _words.TextTrimming = TextTrimming.CharacterEllipsis;
        _text.Children.Add(_time);
        _text.Children.Add(_words);

        _switch.Toggled += (_, _) =>
        {
            if (!_showing)
            {
                Switch(_switch.IsOn);
            }
        };

        _snooze.Click += (_, _) => Snooze();
        _dismiss.Click += (_, _) => Dismiss();

        // The switch sits at its own right edge: ToggleSwitch keeps room for its words otherwise.
        _switch.HorizontalAlignment = HorizontalAlignment.Center;
        _ringButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2, Children = { _snooze, _dismiss } };
        _ringButtons.HorizontalAlignment = HorizontalAlignment.Center;
        _controls.Children.Add(_switch);
        _controls.Children.Add(_ringButtons);

        Children.Add(_text);
        Children.Add(_controls);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        if (_host.Alarms is { } alarms)
        {
            alarms.Changed -= Render;
            alarms.Changed += Render;
        }

        // [S-4630] "Today" becomes "Tomorrow" at midnight: the minute is the slowest tick that is right.
        _minute = ticker.Subscribe(TimeSpan.FromMinutes(1), Render);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as AlarmConfig ?? new AlarmConfig();
        Lay();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private void OnLanguage(Locale locale) => Lay();

    private string WidgetId => _surface?.WidgetId ?? "preview";

    private AlarmStatus Status()
    {
        if (_previewStatus is { } preview)
        {
            return preview;
        }

        if (_host.Alarms is { } alarms)
        {
            return alarms.Status(WidgetId);
        }

        // No service (a debug render): what the plan alone says.
        var plan = _config.Plan;
        return plan.On ? new AlarmStatus(AlarmPhase.Set, AlarmSchedule.Next(plan, DateTime.UtcNow, TimeZoneInfo.Local)) : new AlarmStatus(AlarmPhase.Off);
    }

    // ---- actions ----------------------------------------------------------------------------

    private void Switch(bool on)
    {
        if (_detached || _config.On == on || (_surface?.IsPreview ?? false))
        {
            return;
        }

        _config.On = on;
        _surface?.RequestSave();
        _host.Alarms?.Sync();
        Render();
    }

    private void Snooze() => _host.Alarms?.Snooze(WidgetId);

    private void Dismiss() => _host.Alarms?.Dismiss(WidgetId);

    /// <summary>[S-4628] The card menu answers a ring too.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => Status().Phase == AlarmPhase.Ringing
        ? [new WidgetAction(Glyphs.Alarm, Str.Alarm_Snooze.Text(), Snooze), new WidgetAction(Glyphs.Dismiss, Str.Alarm_Dismiss.Text(), Dismiss)]
        : [];

    // ---- keys ([S-4630]) ----------------------------------------------------------------------

    /// <summary>Space or Enter switch the alarm, or dismiss it while it rings or is snoozed; Escape lets go.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached || key is not (WidgetKey.Space or WidgetKey.Enter))
        {
            return false;
        }

        if (Status().Phase is AlarmPhase.Ringing or AlarmPhase.Snoozed)
        {
            Dismiss();
        }
        else
        {
            Switch(!_config.On);
        }

        return true;
    }

    /// <summary>S snoozes the alarm while it rings.</summary>
    public bool HandleText(char character)
    {
        if (_detached || char.ToUpperInvariant(character) != 'S' || Status().Phase != AlarmPhase.Ringing)
        {
            return false;
        }

        Snooze();
        return true;
    }

    // ---- the ladder ([S-4629]) ------------------------------------------------------------------

    /// <summary>
    /// A strip puts the switch beside the time and its words; anything else stacks them, centred.
    /// The time is reserved, then the switch row and the words as they fit; the time takes the rest.
    /// </summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var area = _area.IsMeasured ? _area : new WidgetArea(ActualWidth, ActualHeight);
        if (!area.IsMeasured)
        {
            Render();
            return;
        }

        var box = area.Deflate(WidgetLayout.ContentInset, Fluent.Space2);
        _beside = area.Shape == AreaShape.Wide && area.Width >= BesideWidth;

        SetRow(_text, 0);
        SetRowSpan(_text, _beside ? 2 : 1);
        SetColumn(_text, 0);
        SetColumnSpan(_text, _beside ? 1 : 2);
        SetRow(_controls, _beside ? 0 : 1);
        SetRowSpan(_controls, _beside ? 2 : 1);
        SetColumn(_controls, _beside ? 1 : 0);
        SetColumnSpan(_controls, _beside ? 1 : 2);

        var centred = !_beside;
        _text.HorizontalAlignment = centred ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _time.TextAlignment = _words.TextAlignment = centred ? TextAlignment.Center : TextAlignment.Left;
        _text.Margin = new Thickness(WidgetLayout.ContentInset, 0, _beside ? Fluent.Space3 : WidgetLayout.ContentInset, 0);
        _controls.Margin = _beside
            ? new Thickness(0, 0, WidgetLayout.ContentInset, 0)
            : new Thickness(WidgetLayout.ContentInset, 0, WidgetLayout.ContentInset, Fluent.Space2);

        var controlWidth = _beside ? WordedWidth / 2 : 0;
        var room = new Disclosure(box.Width - controlWidth, box.Height, gap: 0);
        room.Reserve(FluentType.Body.LineHeight);

        // On a strip the switch is beside the time and costs no height.
        _controlsShown = _beside || room.Take(Fluent.RowHeightCompact + Fluent.Space1);
        var wordsStep = area.HeightClass >= SizeClass.Regular ? FluentType.Body : FluentType.Caption;
        var wordsShown = room.Take(wordsStep.LineHeight);

        _controls.Visibility = _controlsShown ? Visibility.Visible : Visibility.Collapsed;
        _words.Visibility = wordsShown ? Visibility.Visible : Visibility.Collapsed;
        _words.Apply(wordsStep);

        var text = AlarmWords.Time(Strings.Current, _config.Plan);
        _time.Apply(FluentType.Hero(box.Width - controlWidth, room.Remaining + FluentType.Body.LineHeight, text.Length));

        Render();
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var locale = Strings.Current;
        var now = DateTime.UtcNow;
        var zone = _host.Alarms?.Zone ?? TimeZoneInfo.Local;
        var status = Status();
        var off = status.Phase == AlarmPhase.Off;
        var ringing = status.Phase == AlarmPhase.Ringing;
        var snoozed = status.Phase == AlarmPhase.Snoozed;
        var name = AlarmWords.Name(locale, _config);

        _time.Text = AlarmWords.Time(locale, _config.Plan);
        _time.Foreground = Ink(off ? "TextFillColorTertiaryBrush" : "TextFillColorPrimaryBrush");
        _words.Text = $"{name} · {AlarmWords.Hint(locale, status, _config.Plan, now, zone)}";
        _words.Foreground = Ink(ringing ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");

        // [S-4628] While it rings (or waits out a snooze), the buttons that answer it stand where the switch was.
        _ringButtons.Visibility = ringing || snoozed ? Visibility.Visible : Visibility.Collapsed;
        _switch.Visibility = ringing || snoozed ? Visibility.Collapsed : Visibility.Visible;
        _snooze.Visibility = ringing ? Visibility.Visible : Visibility.Collapsed;

        var wide = (_area.IsMeasured ? _area.Width : ActualWidth) >= WordedWidth;
        _snooze.Content = wide ? Str.Alarm_Snooze.Text() : new FontIcon { Glyph = Glyphs.Alarm, FontSize = Fluent.IconSize };
        _dismiss.Content = wide ? Str.Alarm_Dismiss.Text() : new FontIcon { Glyph = Glyphs.Dismiss, FontSize = Fluent.IconSize };
        foreach (var button in new[] { _snooze, _dismiss })
        {
            // Narrow, square glyph buttons, so the time keeps its room.
            button.Padding = wide ? new Thickness(Fluent.Space3, Fluent.Space1, Fluent.Space3, Fluent.Space1) : new Thickness(0);
            button.Width = wide ? double.NaN : Fluent.RowHeightCompact;
            button.Height = wide ? double.NaN : Fluent.RowHeightCompact;
        }

        ToolTipService.SetToolTip(_snooze, Str.Alarm_Snooze.Text());
        ToolTipService.SetToolTip(_dismiss, Str.Alarm_Dismiss.Text());

        _showing = true;
        _switch.IsOn = _config.On;
        _showing = false;
        ToolTipService.SetToolTip(_switch, (_config.On ? Str.Alarm_SwitchOff : Str.Alarm_SwitchOn).Format(("name", name)));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_switch, $"{name} {_time.Text}");
    }

    private static Brush Ink(string key) => ThemeBrush.Of(key, Colors.Gray);

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;

        if (_host.Alarms is { } alarms)
        {
            alarms.Changed -= Render;
        }

        _minute?.Dispose();
        _minute = null;
        _ticker = null;
    }
}
