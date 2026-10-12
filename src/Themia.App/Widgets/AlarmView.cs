using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4628]..[S-4630] The alarms of one widget (Karpo §106, for many): a row each with its time, its
/// name and what it is doing ("Tomorrow", "Weekdays", "Ringing"), and its switch — Snooze and Dismiss
/// in the switch's place while it rings. The rows are the widget's only state the view holds; what an
/// alarm is <i>doing</i> is the app's alarm service's ([S-4623]), which rings it whether or not this
/// view exists. A tile too small for a row shows the alarm that matters most: the one ringing, else
/// the next to ring.
/// </summary>
internal sealed partial class AlarmView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions
{
    private readonly ScrollViewer _scroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Padding = WidgetLayout.ListInset,
    };

    private readonly StackPanel _list = new();
    private readonly List<AlarmRow> _rows = [];
    private readonly List<IDisposable> _hovers = [];

    // The XS face: the one alarm that matters, as a readout.
    private readonly StackPanel _hero = new() { VerticalAlignment = VerticalAlignment.Center, Spacing = Fluent.Space1 };
    private readonly TextBlock _heroTime = Fluent.Text(FluentType.Title);
    private readonly TextBlock _heroHint = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly Button _heroDismiss = new();

    private ScrollFade? _fade;
    private Ticker? _ticker;
    private IDisposable? _minute;
    private IWidgetSurface? _surface;
    private AlarmConfig _config = new();
    private WidgetArea _area;
    private RowStyle _style = RowStyle.Line;
    private bool _heroMode;
    private int _focus = -1;
    private bool _detached;

    /// <summary>Debug renders: what each alarm is doing, in place of the service's answer.</summary>
#if DEBUG
    private Func<AlarmEntry, AlarmStatus>? _previewStatus;
#else
    // Release has no renders: the field's Debug-only writer (the .Preview.cs part) is not compiled.
    private Func<AlarmEntry, AlarmStatus>? _previewStatus => null;
#endif

    public AlarmView()
    {
        _scroll.Content = _list;
        Children.Add(_scroll);
        _fade = new ScrollFade(_scroll, _list);

        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_heroTime, FontNumeralAlignment.Tabular);
        _heroTime.TextAlignment = TextAlignment.Center;
        _heroHint.TextAlignment = TextAlignment.Center;
        _heroHint.TextTrimming = TextTrimming.CharacterEllipsis;
        _heroDismiss.HorizontalAlignment = HorizontalAlignment.Center;
        _heroDismiss.Click += (_, _) => DismissRinging();
        _hero.Children.Add(_heroTime);
        _hero.Children.Add(_heroHint);
        _hero.Children.Add(_heroDismiss);
        _hero.Visibility = Visibility.Collapsed;
        Children.Add(_hero);
    }

    private enum RowStyle
    {
        /// <summary>One line: time, hint, switch. Short tiles.</summary>
        Line,

        /// <summary>The time over its name and hint.</summary>
        Stacked,

        /// <summary>A large time over its name and hint: few alarms on a big tile.</summary>
        Large,
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        if (AppServices.Alarms is { } alarms)
        {
            alarms.Changed -= Render;
            alarms.Changed += Render;
        }

        // "Today" becomes "Tomorrow" at midnight: the minute is the slowest tick that is right.
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

    private void OnLanguage(Locale locale) => Render();

    private string WidgetId => _surface?.WidgetId ?? "preview";

    private AlarmStatus StatusOf(AlarmEntry alarm)
    {
        if (_previewStatus is { } preview)
        {
            return preview(alarm);
        }

        if (AppServices.Alarms is { } alarms)
        {
            return alarms.Status(WidgetId, alarm);
        }

        // No service (a debug render): what the plan alone says.
        var plan = alarm.Plan;
        return plan.On ? new AlarmStatus(AlarmPhase.Set, AlarmSchedule.Next(plan, DateTime.UtcNow, TimeZoneInfo.Local)) : new AlarmStatus(AlarmPhase.Off);
    }

    // ---- actions ----------------------------------------------------------------------------

    private void Switch(AlarmEntry alarm, bool on)
    {
        if (_detached || alarm.On == on || (_surface?.IsPreview ?? false))
        {
            return;
        }

        alarm.On = on;
        _surface?.RequestSave();
        AppServices.Alarms?.Sync();
        Render();
    }

    private void Snooze(AlarmEntry alarm) => AppServices.Alarms?.Snooze(AlarmWords.Key(WidgetId, alarm.Id));

    private void Dismiss(AlarmEntry alarm) => AppServices.Alarms?.Dismiss(AlarmWords.Key(WidgetId, alarm.Id));

    private void DismissRinging()
    {
        foreach (var alarm in _config.Alarms)
        {
            if (StatusOf(alarm).Phase is AlarmPhase.Ringing or AlarmPhase.Snoozed)
            {
                Dismiss(alarm);
            }
        }
    }

    public IReadOnlyList<WidgetAction> ContextActions
    {
        get
        {
            var actions = new List<WidgetAction>();
            foreach (var alarm in _config.Alarms)
            {
                if (StatusOf(alarm).Phase == AlarmPhase.Ringing)
                {
                    var name = AlarmWords.Name(Strings.Current, alarm);
                    actions.Add(new WidgetAction(Glyphs.Alarm, $"{Str.Alarm_Snooze.Text()} · {name}", () => Snooze(alarm)));
                    actions.Add(new WidgetAction(Glyphs.Dismiss, $"{Str.Alarm_Dismiss.Text()} · {name}", () => Dismiss(alarm)));
                }
            }

            return actions;
        }
    }

    // ---- keys ([S-4630]) ----------------------------------------------------------------------

    public bool WantsKeys => !_detached && !_heroMode && _config.Alarms.Count > 0;

    /// <summary>Up/Down walk the alarms; Space or Enter switch the focused one, or dismiss it while it rings; Escape lets go.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        if (key == WidgetKey.Space)
        {
            key = WidgetKey.Enter;
        }

        var decision = ListKeys.Decide(key, _focus, _config.Alarms.Count);

        switch (decision.Command)
        {
            case ListCommand.Focus:
                Focus(decision.Index);
                return true;

            case ListCommand.Open when decision.Index >= 0 && decision.Index < _config.Alarms.Count:
                var alarm = _config.Alarms[decision.Index];
                if (StatusOf(alarm).Phase is AlarmPhase.Ringing or AlarmPhase.Snoozed)
                {
                    Dismiss(alarm);
                }
                else
                {
                    Switch(alarm, !alarm.On);
                }

                return true;

            case ListCommand.Release:
                var had = _focus >= 0;
                Focus(-1);
                return had;

            default:
                return false;
        }
    }

    /// <summary>S snoozes the focused alarm while it rings.</summary>
    public bool HandleText(char character)
    {
        if (_detached || char.ToUpperInvariant(character) != 'S' || _focus < 0 || _focus >= _config.Alarms.Count)
        {
            return false;
        }

        var alarm = _config.Alarms[_focus];
        if (StatusOf(alarm).Phase != AlarmPhase.Ringing)
        {
            return false;
        }

        Snooze(alarm);
        return true;
    }

    private void Focus(int index)
    {
        _focus = index;
        for (var i = 0; i < _rows.Count; i++)
        {
            RowFocus.Mark(_rows[i].Root, i == index);
        }
    }

    // ---- the ladder ([S-4629]) ------------------------------------------------------------------

    /// <summary>
    /// The XS tile is the hero alone; a short tile one-line rows; a taller one the time over its name;
    /// a big tile with few alarms large rows that use its height.
    /// </summary>
    private void Lay()
    {
        var area = _area.IsMeasured ? _area : new WidgetArea(ActualWidth, ActualHeight);
        if (!area.IsMeasured)
        {
            Render();
            return;
        }

        _heroMode = area.WidthClass == SizeClass.Compact && area.HeightClass == SizeClass.Compact;

        var count = Math.Max(1, _config.Alarms.Count);
        var room = area.Height - WidgetLayout.ListInset.Top - WidgetLayout.ListInset.Bottom;
        _style = area.HeightClass == SizeClass.Compact || area.WidthClass == SizeClass.Compact ? RowStyle.Line
            : area.Least >= SizeClass.Expanded && room / count >= AlarmRow.LargeHeight ? RowStyle.Large
            : RowStyle.Stacked;

        _scroll.Visibility = _heroMode ? Visibility.Collapsed : Visibility.Visible;
        _hero.Visibility = _heroMode ? Visibility.Visible : Visibility.Collapsed;
        _hero.Margin = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0);

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
        var zone = AppServices.Alarms?.Zone ?? TimeZoneInfo.Local;

        if (_heroMode)
        {
            RenderHero(locale, now, zone);
            return;
        }

        while (_rows.Count < _config.Alarms.Count)
        {
            var row = new AlarmRow(this);
            _hovers.Add(Fluent.Hover(row.Root));
            _rows.Add(row);
            _list.Children.Add(row.Root);
        }

        var wide = (_area.IsMeasured ? _area.Width : ActualWidth) >= 300;

        for (var i = 0; i < _rows.Count; i++)
        {
            var shown = i < _config.Alarms.Count;
            _rows[i].Root.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;

            if (shown)
            {
                var alarm = _config.Alarms[i];
                _rows[i].Show(alarm, StatusOf(alarm), _style, wide, locale, now, zone);
            }
        }

        _fade?.Refresh();
    }

    /// <summary>The alarm that matters most: the one ringing, else the soonest to ring, else the first.</summary>
    private void RenderHero(Locale locale, DateTime now, TimeZoneInfo zone)
    {
        AlarmEntry? best = null;
        AlarmStatus bestStatus = default;

        foreach (var alarm in _config.Alarms)
        {
            var status = StatusOf(alarm);
            var better = best is null
                || status.Phase == AlarmPhase.Ringing && bestStatus.Phase != AlarmPhase.Ringing
                || bestStatus.Phase != AlarmPhase.Ringing && status.Next is { } next && (bestStatus.Next is not { } other || next < other);

            if (better)
            {
                (best, bestStatus) = (alarm, status);
            }
        }

        if (best is null)
        {
            _hero.Visibility = Visibility.Collapsed;
            return;
        }

        var box = _area.IsMeasured ? _area.Deflate(Fluent.Space2, Fluent.Space1) : new WidgetArea(100, 60);
        var ringing = bestStatus.Phase == AlarmPhase.Ringing;
        var room = box.Height - FluentType.Caption.LineHeight - (ringing ? Fluent.RowHeightCompact + Fluent.Space1 : 0) - Fluent.Space1;
        _heroTime.Apply(FluentType.Fit(box.Width, room, AlarmWords.Time(locale, best.Plan).Length, FluentType.Body, FluentType.TitleLarge));
        _heroTime.Text = AlarmWords.Time(locale, best.Plan);
        _heroTime.Foreground = Ink(bestStatus.Phase == AlarmPhase.Off ? "TextFillColorTertiaryBrush" : "TextFillColorPrimaryBrush");
        _heroHint.Text = AlarmWords.Hint(locale, bestStatus, best.Plan, now, zone);
        _heroHint.Foreground = Ink(ringing ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        _heroDismiss.Content = Str.Alarm_Dismiss.Text();
        _heroDismiss.Visibility = ringing ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Brush Ink(string key) => FluentLayout.Themed(key, Colors.Gray);

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;

        if (AppServices.Alarms is { } alarms)
        {
            alarms.Changed -= Render;
        }

        _minute?.Dispose();
        _minute = null;
        _fade?.Dispose();
        _fade = null;

        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();
        _ticker = null;
    }

    /// <summary>One alarm's row: its time and words at the left, its switch (or Snooze and Dismiss) at the right.</summary>
    private sealed class AlarmRow
    {
        public const double LargeHeight = 88;

        private readonly AlarmView _owner;
        private readonly TextBlock _time = Fluent.Text(FluentType.BodyStrong);
        private readonly TextBlock _words = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        private readonly ToggleSwitch _switch = new() { OnContent = string.Empty, OffContent = string.Empty, MinWidth = 0 };
        private readonly Button _snooze = new();
        private readonly Button _dismiss = new();
        private readonly StackPanel _ringButtons;
        private readonly Grid _grid = new() { ColumnSpacing = Fluent.Space3 };
        private AlarmEntry? _alarm;
        private bool _showing;

        public AlarmRow(AlarmView owner)
        {
            _owner = owner;
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_time, FontNumeralAlignment.Tabular);
            _words.TextTrimming = TextTrimming.CharacterEllipsis;
            _time.TextTrimming = TextTrimming.CharacterEllipsis;

            _switch.Toggled += (_, _) =>
            {
                if (!_showing && _alarm is { } alarm)
                {
                    _owner.Switch(alarm, _switch.IsOn);
                }
            };

            _snooze.Click += (_, _) =>
            {
                if (_alarm is { } alarm)
                {
                    _owner.Snooze(alarm);
                }
            };

            _dismiss.Click += (_, _) =>
            {
                if (_alarm is { } alarm)
                {
                    _owner.Dismiss(alarm);
                }
            };

            // The switch sits at its own right edge: ToggleSwitch keeps room for its words otherwise.
            _switch.HorizontalAlignment = HorizontalAlignment.Right;
            _switch.Margin = new Thickness(0, 0, -Fluent.Space2, 0);
            _ringButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2, Children = { _snooze, _dismiss } };

            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            foreach (var control in new FrameworkElement[] { _switch, _ringButtons })
            {
                Grid.SetColumn(control, 2);
                Grid.SetRowSpan(control, 2);
                control.VerticalAlignment = VerticalAlignment.Center;
            }

            _grid.Children.Add(_time);
            _grid.Children.Add(_words);
            _grid.Children.Add(_switch);
            _grid.Children.Add(_ringButtons);
            _grid.VerticalAlignment = VerticalAlignment.Center;

            Root = Fluent.Row(_grid);
            RowFocus.Prepare(Root);
            Root.PointerPressed += OnPressed;
        }

        public Border Root { get; }

        private void OnPressed(object sender, PointerRoutedEventArgs args)
        {
            var index = _owner._rows.IndexOf(this);
            if (index >= 0)
            {
                _owner.Focus(index);
            }
        }

        /// <summary>The words beside the time (one line) or under it (two), the time spanning where they are under it.</summary>
        private void Place(int row, int column, int span)
        {
            Grid.SetRow(_words, row);
            Grid.SetColumn(_words, column);
            Grid.SetColumnSpan(_words, span);
            Grid.SetColumnSpan(_time, row == 0 ? 1 : 2);
            _words.VerticalAlignment = VerticalAlignment.Center;
            _time.VerticalAlignment = VerticalAlignment.Center;
        }

        public void Show(AlarmEntry alarm, AlarmStatus status, RowStyle style, bool wide, Locale locale, DateTime now, TimeZoneInfo zone)
        {
            _alarm = alarm;
            var off = status.Phase == AlarmPhase.Off;
            var ringing = status.Phase == AlarmPhase.Ringing;
            var snoozed = status.Phase == AlarmPhase.Snoozed;

            var hint = AlarmWords.Hint(locale, status, alarm.Plan, now, zone);
            var name = AlarmWords.Name(locale, alarm);

            _time.Text = AlarmWords.Time(locale, alarm.Plan);
            _time.Foreground = Ink(off ? "TextFillColorTertiaryBrush" : "TextFillColorPrimaryBrush");
            _words.Foreground = Ink(ringing ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");

            switch (style)
            {
                case RowStyle.Line:
                    // Time, then the name and the hint on the same line, at the list's text.
                    _time.Apply(FluentType.BodyStrong);
                    _words.Apply(FluentType.Body);
                    _words.Text = wide ? $"{name} · {hint}" : hint;
                    Place(row: 0, column: 1, span: 1);
                    Root.MinHeight = Fluent.RowHeight;
                    break;

                case RowStyle.Large:
                    _time.Apply(FluentType.Title);
                    _words.Apply(FluentType.Body);
                    _words.Text = $"{name} · {hint}";
                    Place(row: 1, column: 0, span: 2);
                    Root.MinHeight = LargeHeight;
                    break;

                default:
                    _time.Apply(FluentType.Subtitle);
                    _words.Apply(FluentType.Caption);
                    _words.Text = $"{name} · {hint}";
                    Place(row: 1, column: 0, span: 2);
                    Root.MinHeight = 60;
                    break;
            }

            // While it rings (or waits out a snooze), the buttons that answer it stand where the switch was.
            _ringButtons.Visibility = ringing || snoozed ? Visibility.Visible : Visibility.Collapsed;
            _switch.Visibility = ringing || snoozed ? Visibility.Collapsed : Visibility.Visible;
            _snooze.Visibility = ringing ? Visibility.Visible : Visibility.Collapsed;
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
            _switch.IsOn = alarm.On;
            _showing = false;
            ToolTipService.SetToolTip(_switch, (alarm.On ? Str.Alarm_SwitchOff : Str.Alarm_SwitchOn).Format(("name", name)));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_switch, $"{name} {_time.Text}");
        }
    }
}
