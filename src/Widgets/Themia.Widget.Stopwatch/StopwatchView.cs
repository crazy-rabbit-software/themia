using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Stopwatch;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4600]..[S-4606] A stopwatch in the timer's style (Karpo §84): the time as the hero, the accent
/// start button (Start, Pause, Resume), Lap and Reset beside it — greyed, never gone, where they do
/// nothing — and the laps under it, newest first, as many as the tile holds.
/// <para>
/// The stopwatch is the widget's config ([S-4602]): written on every press, read back on attach, so
/// it keeps running across a restart. In memory it counts on the monotonic clock; the shared ticker
/// wakes it once a second only while it runs ([S-155]), so a running time is whole seconds and a
/// paused one shows its tenth ([S-4603]).
/// </para>
/// </summary>
internal sealed partial class StopwatchView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private const string Scope = "stopwatch";

    /// <summary>The laps column beside the time needs this much to say "Lap 12  0:03.9  1:02:03.4".</summary>
    private const double LapsColumnMinimum = 220;

    /// <summary>Every view of one stopwatch (the same view on two displays): one press reaches all of them.</summary>
    private static event Action<StopwatchConfig>? Acted;

    private readonly Canvas _canvas = new();
    private readonly TextBlock _clock = Fluent.Text(FluentType.Display);
    private readonly TextBlock _lapLine = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly Button _startButton = new();
    private readonly FontIcon _startIcon = new();
    private readonly Button _lapButton = new();
    private readonly Button _resetButton = new();
    private readonly StackPanel _controls;
    private readonly StackPanel _laps = new();
    private readonly TextBlock _noLaps = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");

    private Ticker? _ticker;
    private StopwatchConfig _config = new();
    private StopwatchState _state = StopwatchState.None;
    private IDisposable? _subscription;
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private int _lapRows;
    private int _hourDigits;

    public StopwatchView()
    {
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_clock, FontNumeralAlignment.Tabular);
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_lapLine, FontNumeralAlignment.Tabular);
        _clock.TextTrimming = TextTrimming.None;
        _clock.TextAlignment = TextAlignment.Center;
        _lapLine.TextAlignment = TextAlignment.Center;
        _noLaps.TextAlignment = TextAlignment.Center;

        Prepare(_startButton, _startIcon, Glyphs.Play);
        Prepare(_lapButton, new FontIcon(), StopwatchModule.LapGlyph);
        Prepare(_resetButton, new FontIcon(), Glyphs.Reset);

        _startButton.Click += (_, _) => Act(state => state.Act(Now()));
        _lapButton.Click += (_, _) => Act(state => state.Lap(Now()));
        _resetButton.Click += (_, _) => Act(_ => StopwatchState.None);

        // The primary action takes the accent, as the pomodoro's does.
        if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var accent) && accent is Style style)
        {
            _startButton.Style = style;
        }
        else
        {
            Log.Warn(Scope, "AccentButtonStyle is missing; the start button falls back to the standard style");
        }

        // Lap and reset at the left, the start button at the right, as Windows' Clock has them.
        _controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            Children = { _resetButton, _lapButton, _startButton },
        };

        _canvas.Children.Add(_clock);
        _canvas.Children.Add(_lapLine);
        _canvas.Children.Add(_controls);
        _canvas.Children.Add(_laps);
        _canvas.Children.Add(_noLaps);
        Children.Add(_canvas);

        Tooltips();

        static void Prepare(Button button, FontIcon icon, string glyph)
        {
            icon.FontSize = Fluent.IconSize;
            icon.Glyph = glyph;
            button.Content = icon;
            button.Padding = new Thickness(0);
        }
    }

    /// <summary>The monotonic clock the stopwatch counts on: setting Windows' clock never moves it.</summary>
    private static TimeSpan Now() => System.Diagnostics.Stopwatch.GetElapsedTime(0);

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        Acted -= OnActed;
        Acted += OnActed;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as StopwatchConfig ?? new StopwatchConfig();

        // Only another config is read again: the one held is this view's own writing.
        if (!ReferenceEquals(next, _config))
        {
            _config = next;
            _state = _config.Record.ToState(DateTimeOffset.UtcNow, Now());
        }

        Run();
        Render();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private void OnLanguage(Locale locale)
    {
        Tooltips();
        Render();
    }

    /// <summary>Another view of the same stopwatch pressed a button: take what it wrote.</summary>
    private void OnActed(StopwatchConfig config)
    {
        if (!ReferenceEquals(config, _config) || _ticker is null)
        {
            return;
        }

        _state = _config.Record.ToState(DateTimeOffset.UtcNow, Now());
        Run();
        Render();
    }

    /// <summary>A press: the new state, written into the config ([S-4602]) and saved, every view of it told.</summary>
    private void Act(Func<StopwatchState, StopwatchState> change)
    {
        if (_ticker is null)
        {
            return;
        }

        var next = change(_state);
        if (next == _state)
        {
            return;
        }

        _state = next;
        _config.Store(StopwatchRecord.Of(_state, DateTimeOffset.UtcNow, Now()));
        _surface?.RequestSave();
        Run();
        Render();
        Acted?.Invoke(_config);
    }

    /// <summary>[S-155] Subscribed to the second only while it runs.</summary>
    private void Run()
    {
        var wanted = _state.Running && _ticker is not null;

        if (wanted == (_subscription is not null))
        {
            return;
        }

        if (wanted)
        {
            _subscription = _ticker!.Subscribe(TimeSpan.FromSeconds(1), Render);
        }
        else
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    /// <summary>[S-4604] Space or Enter start and pause, Right a lap, Home reset.</summary>
    public bool HandleKey(WidgetKey key) => Do(StopwatchKeys.Of(key));

    /// <summary>[S-4604] L a lap, R reset.</summary>
    public bool HandleText(char character) => Do(StopwatchKeys.Letter(character));

    private bool Do(ControlKey command)
    {
        if (_ticker is null)
        {
            return false;
        }

        switch (command)
        {
            case ControlKey.Toggle:
                Act(state => state.Act(Now()));
                return true;
            case ControlKey.Lap:
                Act(state => state.Lap(Now()));
                return true;
            case ControlKey.Reset:
                Act(_ => StopwatchState.None);
                return true;
            default:
                return false;
        }
    }

    private void Tooltips()
    {
        ToolTipService.SetToolTip(_lapButton, StopwatchStr.Stopwatch_Lap.Text());
        ToolTipService.SetToolTip(_resetButton, StopwatchStr.Stopwatch_Reset.Text());
        _noLaps.Text = StopwatchStr.Stopwatch_NoLaps.Text();
    }

    // ---- drawing ----------------------------------------------------------------------------

    private string Time(TimeSpan elapsed, bool tenths)
    {
        var locale = Strings.Current;
        return LocaleFormat.Digits(locale, StopwatchText.Time(elapsed, tenths, LocaleFormat.DecimalSeparator(locale)));
    }

    private void Render()
    {
        var now = Now();
        var elapsed = _state.Elapsed(now);

        // [S-4603] Running: whole seconds, the one the ticker can keep. Stopped: the tenth too.
        _clock.Text = Time(elapsed, tenths: !_state.Running && !_state.Idle);
        _clock.Foreground = ThemeBrush.Of(_state.Phase == StopwatchPhase.Paused ? "TextFillColorSecondaryBrush" : "TextFillColorPrimaryBrush", Colors.Gray);

        var laps = _state.Splits;
        _lapLine.Text = _state.Phase == StopwatchPhase.Paused && laps.Length == 0
            ? StopwatchStr.Stopwatch_Paused.Text()
            : laps.Length == 0 ? string.Empty
            : $"{StopwatchStr.Stopwatch_LapNumber.Format(("n", LocaleFormat.Integer(laps.Length + 1)))} · {Time(_state.CurrentLap(now), tenths: !_state.Running)}";

        _startIcon.Glyph = _state.Running ? Glyphs.Pause : Glyphs.Play;
        ToolTipService.SetToolTip(_startButton, (_state.Running ? StopwatchStr.Stopwatch_Pause : _state.Idle ? StopwatchStr.Stopwatch_Start : StopwatchStr.Stopwatch_Resume).Text());

        // [S-4601] Greyed where they do nothing, never taken away: the row never moves.
        _lapButton.IsEnabled = _state.CanLap;
        _resetButton.IsEnabled = _state.CanReset;

        FillLaps();

        // From an hour up the time is three figures wider: the ladder runs again once.
        var hours = elapsed >= TimeSpan.FromHours(1) ? 1 : 0;
        if (hours != _hourDigits)
        {
            _hourDigits = hours;
            Lay();
        }
    }

    /// <summary>The newest laps that fit, each "Lap N", its length and its total.</summary>
    private void FillLaps()
    {
        var laps = _state.Splits;
        var shown = Math.Min(_lapRows, laps.Length);

        while (_laps.Children.Count < shown)
        {
            _laps.Children.Add(new LapRow().Root);
        }

        for (var i = 0; i < _laps.Children.Count; i++)
        {
            var row = (Grid)_laps.Children[i];
            row.Visibility = i < shown ? Visibility.Visible : Visibility.Collapsed;

            if (i < shown && _state.LapAt(laps.Length - 1 - i) is { } lap)
            {
                LapRow.Show(row, StopwatchStr.Stopwatch_LapNumber.Format(("n", LocaleFormat.Integer(laps.Length - i))), Time(lap.Length, true), Time(lap.Total, true));
            }
        }

        _noLaps.Visibility = _lapRows > 0 && laps.Length == 0 && _laps.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- the ladder -------------------------------------------------------------------------

    /// <summary>
    /// [S-4605] What a tile this size shows. The time and the start button never drop.
    /// <list type="bullet">
    /// <item>XS: the time over the start button.</item>
    /// <item>A short strip: the time (and the lap under way) left, the three buttons right.</item>
    /// <item>Taller: the time, the lap under way, the buttons, then as many laps as fit.</item>
    /// <item>Wide and tall: the time and buttons in a column, the laps beside them.</item>
    /// </list>
    /// </summary>
    private void Lay()
    {
        var area = _area.IsMeasured ? _area : new WidgetArea(ActualWidth, ActualHeight);
        if (!area.IsMeasured)
        {
            return;
        }

        var shortTile = area.HeightClass == SizeClass.Compact;
        var padY = !shortTile ? Fluent.Space4 : area.WidthClass == SizeClass.Compact ? Fluent.Space1 : Fluent.Space2;
        var box = area.Deflate(Fluent.Space4, padY);
        var side = area.Least >= SizeClass.Expanded ? Fluent.RowHeight : Fluent.RowHeightCompact;

        // The widest the time can read in this hour: every figure an 8, with its tenth.
        var characters = _hourDigits > 0 ? StopwatchText.Widest.Length : StopwatchText.Widest.Length - 3;

        _lapRows = 0;
        Hide(_lapLine, _laps, _noLaps);

        var all = area.WidthClass >= SizeClass.Regular || area.HeightClass >= SizeClass.Regular;
        var buttons = SetButtons(side, all);

        if (shortTile && area.WidthClass >= SizeClass.Expanded)
        {
            LayStrip(box, padY, side, buttons, characters);
        }
        else if (area.WidthClass >= SizeClass.Expanded && area.HeightClass >= SizeClass.Regular && box.Width / 2 >= LapsColumnMinimum)
        {
            LayColumns(box, side, buttons, characters);
        }
        else
        {
            LayStack(area, box, padY, side, buttons, characters);
        }

        FillLaps();
    }

    private void LayStack(WidgetArea area, WidgetArea box, double padY, double side, double buttons, int characters)
    {
        var gap = Fluent.Space2;
        var room = new Disclosure(box, gap);
        room.Reserve(side);
        room.Reserve(FluentType.Body.LineHeight);
        var lapLine = area.WidthClass >= SizeClass.Regular && room.Take(FluentType.Caption.LineHeight);

        // The time takes up to half of a tall tile; the laps the rest.
        var heroRoom = area.HeightClass >= SizeClass.Regular ? Math.Min(room.Remaining, box.Height * 0.4) : room.Remaining;
        var digits = FluentType.Hero(box.Width, heroRoom + FluentType.Body.LineHeight, characters);
        room = new Disclosure(box, gap);
        room.Reserve(side);
        room.Reserve(digits.LineHeight);
        if (lapLine)
        {
            room.Reserve(FluentType.Caption.LineHeight);
        }

        var lapRows = area.HeightClass >= SizeClass.Regular ? room.TakeRows(LapRow.Height, StopwatchState.MostLaps) : 0;

        _clock.Apply(digits);
        _clock.TextAlignment = TextAlignment.Center;
        _lapLine.Apply(FluentType.Caption);
        _lapLine.TextAlignment = TextAlignment.Center;

        var height = digits.LineHeight + gap + side
            + (lapLine ? FluentType.Caption.LineHeight + gap : 0)
            + (lapRows > 0 ? (lapRows * LapRow.Height) + gap : 0);

        var x = Fluent.Space4;
        var y = padY + (lapRows > 0 ? 0 : Math.Max(0, (box.Height - height) / 2));

        Place(_clock, x, y, box.Width, digits.LineHeight);
        y += digits.LineHeight + gap;

        if (lapLine)
        {
            Place(_lapLine, x, y, box.Width, FluentType.Caption.LineHeight);
            y += FluentType.Caption.LineHeight + gap;
        }

        Place(_controls, x + ((box.Width - buttons) / 2), y, buttons, side);
        y += side + gap;

        PlaceLaps(x, y, box.Width, lapRows);
    }

    /// <summary>A short, wider tile: the time and the lap under way left, the buttons right.</summary>
    private void LayStrip(WidgetArea box, double padY, double side, double buttons, int characters)
    {
        var left = Math.Max(0, box.Width - buttons - Fluent.Space3);
        var room = new Disclosure(left, box.Height, Fluent.Space1);
        room.Reserve(FluentType.Body.LineHeight);
        var lapLine = room.Take(FluentType.Caption.LineHeight);

        var digits = FluentType.Hero(left, room.Remaining + FluentType.Body.LineHeight, characters);
        _clock.Apply(digits);
        _clock.TextAlignment = TextAlignment.Left;
        _lapLine.Apply(FluentType.Caption);
        _lapLine.TextAlignment = TextAlignment.Left;

        var column = digits.LineHeight + (lapLine ? FluentType.Caption.LineHeight + Fluent.Space1 : 0);
        var x = Fluent.Space4;
        var y = padY + Math.Max(0, (box.Height - column) / 2);

        Place(_clock, x, y, left, digits.LineHeight);
        if (lapLine)
        {
            Place(_lapLine, x, y + digits.LineHeight + Fluent.Space1, left, FluentType.Caption.LineHeight);
        }

        Place(_controls, x + box.Width - buttons, padY + Math.Max(0, (box.Height - side) / 2), buttons, side);
    }

    /// <summary>Wide and tall: the time, the lap under way and the buttons in the left half, the laps in the right.</summary>
    private void LayColumns(WidgetArea box, double side, double buttons, int characters)
    {
        var gap = Fluent.Space3;
        var half = (box.Width - gap) / 2;
        var room = new Disclosure(half, box.Height, Fluent.Space2);
        room.Reserve(side);
        room.Reserve(FluentType.Body.LineHeight);
        var lapLine = room.Take(FluentType.Caption.LineHeight);

        var digits = FluentType.Hero(half, room.Remaining + FluentType.Body.LineHeight, characters);
        _clock.Apply(digits);
        _clock.TextAlignment = TextAlignment.Center;
        _lapLine.Apply(FluentType.Body);
        _lapLine.TextAlignment = TextAlignment.Center;

        var lapHeight = lapLine ? FluentType.Body.LineHeight + Fluent.Space2 : 0;
        var height = digits.LineHeight + lapHeight + Fluent.Space2 + side;
        var x = Fluent.Space4;
        var y = Fluent.Space4 + Math.Max(0, (box.Height - height) / 2);

        Place(_clock, x, y, half, digits.LineHeight);
        y += digits.LineHeight + Fluent.Space2;

        if (lapLine)
        {
            Place(_lapLine, x, y, half, FluentType.Body.LineHeight);
            y += lapHeight;
        }

        Place(_controls, x + ((half - buttons) / 2), y, buttons, side);

        var rows = Disclosure.WholeRows(box.Height, LapRow.Height);
        PlaceLaps(x + half + gap, Fluent.Space4, half, rows);
    }

    private void PlaceLaps(double x, double y, double width, int rows)
    {
        _lapRows = rows;
        if (rows <= 0)
        {
            return;
        }

        Place(_laps, x, y, width, rows * LapRow.Height);
        Place(_noLaps, x, y, width, FluentType.Caption.LineHeight);
    }

    /// <summary>Size the buttons; Lap and Reset only where there is room for three. Returns the row's width.</summary>
    private double SetButtons(double side, bool all)
    {
        foreach (var button in new[] { _startButton, _lapButton, _resetButton })
        {
            button.Width = side;
            button.Height = side;
        }

        _lapButton.Visibility = all ? Visibility.Visible : Visibility.Collapsed;
        _resetButton.Visibility = all ? Visibility.Visible : Visibility.Collapsed;

        var count = all ? 3 : 1;
        return (count * side) + ((count - 1) * Fluent.Space2);
    }

    private static void Place(FrameworkElement element, double x, double y, double width, double height)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
        element.Visibility = Visibility.Visible;
    }

    private static void Hide(params UIElement[] elements)
    {
        foreach (var element in elements)
        {
            element.Visibility = Visibility.Collapsed;
        }
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        Acted -= OnActed;
        _subscription?.Dispose();
        _subscription = null;

        // A key the lease delivers after the card left ([S-802]) must not press a detached stopwatch.
        _ticker = null;
    }

    /// <summary>One lap's row: its number, its length, and the total at the end in the quieter ink.</summary>
    private sealed class LapRow
    {
        public const double Height = Fluent.RowHeightCompact - 4;

        public LapRow()
        {
            var name = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            var length = Fluent.Text(FluentType.Body);
            var total = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");

            foreach (var text in new[] { length, total })
            {
                Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(text, FontNumeralAlignment.Tabular);
                text.TextAlignment = TextAlignment.Right;
                text.HorizontalAlignment = HorizontalAlignment.Right;
            }

            Root = new Grid { Height = Height, ColumnSpacing = Fluent.Space3 };
            // The name takes the slack (and gives way first); the figures are tabular, so their columns line up.
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = FluentType.EstimateWidth(FluentType.Body, 6) });

            foreach (var (text, column) in new[] { (name, 0), (length, 1), (total, 2) })
            {
                text.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(text, column);
                Root.Children.Add(text);
            }
        }

        public Grid Root { get; }

        public static void Show(Grid row, string name, string length, string total)
        {
            ((TextBlock)row.Children[0]).Text = name;
            ((TextBlock)row.Children[1]).Text = length;
            ((TextBlock)row.Children[2]).Text = total;
        }
    }
}
