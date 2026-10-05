using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets.Meters;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// The face every system meter shares: the reading and its history, nothing else — the card's
/// header is the label. Where the number comes from is the kind's <see cref="MeterPart"/>
/// (<c>Meters/&lt;Kind&gt;Meter.cs</c>); this file is the layout, so the meters grow and shrink the
/// same way.
/// <para>
/// [S-664] [S-1762] The ladder (<see cref="MeterFace"/>): a tiny tile is the reading alone; from
/// there the history appears under the reading and grows to fill the tile, the reading growing by
/// ramp step up to Display; a wide strip puts the reading in a column on the left and the history
/// the full height beside it. With the history switched off the reading fills the tile.
/// </para>
/// <para>
/// [S-1824] The history sits inside the card's padding — the reading's inset from both sides and
/// the bottom — so its newest point and its fill are never cut by the card's edge or rounded corner.
/// </para>
/// </summary>
internal sealed partial class MeterView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IMeterHost
{
    private const string Scope = "meter";

    /// <summary>The graph never shrinks below this: under it a trace is a smudge, not a chart.</summary>
    private const double GraphFloor = Fluent.Space6;

    private readonly MeterPart _part;
    private readonly WidgetKind _kind;

    private readonly StackPanel _reading = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = Fluent.Space2,
    };

    private readonly TextBlock _primary = Fluent.Text(FluentType.Subtitle);
    private readonly FontIcon _badge = new();
    private readonly SparkBand _band;
    private readonly StackPanel? _absentState;

    // Looked up once. The reading swaps between them on every sample, and a resource lookup per
    // sample per widget is a dictionary hit and a possible log line for nothing.
    private readonly Brush _readingBrush = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
    private readonly Brush _unavailableBrush = FluentLayout.Themed("TextFillColorDisabledBrush", Colors.Gray);
    private readonly Brush _accentBrush = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private readonly Brush _cautionBrush = FluentLayout.Themed("SystemFillColorCautionBrush", Colors.Orange);
    private readonly Brush _criticalBrush = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.Red);
    private readonly Brush _successBrush = FluentLayout.Themed("SystemFillColorSuccessBrush", Colors.Green);

    private Ticker? _ticker;
    private WidgetConfig _config;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;

    // The ramp step currently applied. Kept so a sample that lands on the same step does not touch
    // the text block at all: re-applying a font size is a layout invalidation the tile does not need.
    private TypeStep _heroStep = FluentType.Subtitle;

    private WidgetArea _area;
    private bool _graph;
    private bool _absent;
    private bool _detached;

    public MeterView(WidgetKind kind)
    {
        // No padding on the view: the reading and the history each carry the content inset
        // ([S-1824]), the history's on its sides and bottom only, so the gap under the reading is the
        // ladder's to size.
        _kind = kind;
        _part = MeterPart.For(kind);
        _config = WidgetConfig.CreateDefault(kind);
        _band = new SparkBand(60, _part.Scale);

        _primary.Text = "--";
        _primary.Foreground = _readingBrush;
        _primary.VerticalAlignment = VerticalAlignment.Center;

        // [S-1187] The kind's badge (the battery icon) leads the reading; its width is in the kind's
        // ReadingCharacters, so the ladder never has to know it is there.
        _badge.Visibility = Visibility.Collapsed;
        _badge.VerticalAlignment = VerticalAlignment.Center;
        _reading.Children.Add(_badge);
        _reading.Children.Add(_primary);
        _reading.Margin = WidgetLayout.SideInset;

        // Two rows (the reading, the history) and two columns (the reading's, the history's on a
        // strip): each arrangement only moves the two parts between these cells.
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        Children.Add(_reading);
        Children.Add(_band);

        // Only a battery can be absent — every other meter that cannot be read is a meter that could
        // be read a moment ago, which is [S-82]'s dimmed "--" rather than an empty state.
        _absentState = _part.CanBeAbsent ? BuildAbsentState(WidgetCatalog.Definition(kind).Glyph) : null;

        if (_absentState is not null)
        {
            SetRowSpan(_absentState, 2);
            SetColumnSpan(_absentState, 2);
            Children.Add(_absentState);
        }
    }

    /// <summary>
    /// The state a machine with no battery shows: the widget's own icon and one line, centred, the
    /// shape Windows uses when a pane has nothing to show.
    /// </summary>
    private static StackPanel BuildAbsentState(string glyph)
    {
        var icon = Fluent.Icon(glyph, "TextFillColorTertiaryBrush");
        icon.HorizontalAlignment = HorizontalAlignment.Center;

        var text = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        text.Text = Str.WidgetsB_Battery_None.Text();
        text.HorizontalAlignment = HorizontalAlignment.Center;

        var panel = new StackPanel
        {
            Spacing = Fluent.Space2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        panel.Children.Add(icon);
        panel.Children.Add(text);
        return panel;
    }

    /// <summary>[S-663] The card's word on how much room the meter has.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>[S-1762] Place the reading and the history for the current area and size the reading.</summary>
    private void Lay()
    {
        if (_absentState is not null)
        {
            _absentState.Visibility = _absent ? Visibility.Visible : Visibility.Collapsed;
        }

        _reading.Visibility = _absent ? Visibility.Collapsed : Visibility.Visible;

        if (_absent)
        {
            _band.Visibility = Visibility.Collapsed;
            return;
        }

        var plan = MeterFace.Plan(_area, _graph, _part.ReadingCharacters, WidgetLayout.ContentInset, Fluent.Space2, GraphFloor);
        var side = plan.Arrangement == MeterFaceArrangement.Side;
        var alone = plan.Arrangement == MeterFaceArrangement.ReadingOnly;

        ColumnDefinitions[0].Width = side ? new GridLength(plan.ReadingColumn) : new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        // Alone, or beside the history on a strip, the reading is centred in the whole height; over
        // the history it sits at the top and the history takes everything under it.
        SetRow(_reading, 0);
        SetRowSpan(_reading, alone || side ? 2 : 1);
        _reading.VerticalAlignment = alone || side ? VerticalAlignment.Center : VerticalAlignment.Top;

        _band.Visibility = plan.Graph ? Visibility.Visible : Visibility.Collapsed;
        SetRow(_band, side ? 0 : 1);
        SetRowSpan(_band, side ? 2 : 1);
        SetColumn(_band, side ? 1 : 0);
        // Stacked, the history is the star row: exactly what the reading leaves (plan.GraphHeight).
        // [S-1824] Inside the card's padding either way; beside the reading its column's own inset
        // already separates the two.
        _band.Margin = side
            ? new Thickness(0, 0, WidgetLayout.ContentInset, WidgetLayout.ContentInset)
            : new Thickness(WidgetLayout.ContentInset, Fluent.Space2, WidgetLayout.ContentInset, WidgetLayout.ContentInset);

        if (plan.Reading != _heroStep)
        {
            _heroStep = plan.Reading;
            _primary.Apply(plan.Reading);

            // An icon's ink fills less of its em box than a figure's, so it is drawn a little larger
            // than the type to stand as tall as the digits.
            _badge.FontSize = Math.Round(plan.Reading.FontSize * 1.1);
        }
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config.Kind == _kind ? config : WidgetConfig.CreateDefault(_kind);

        var gauge = _config as GaugeConfig;
        _graph = gauge is { ShowGraph: true };

        // Before Resubscribe, which fires a tick of its own: a part that changed what it measures
        // (a ping host) must be pointed at the new thing before that tick reaches it.
        var sampleNow = _part.ApplyConfig(_config, this);

        _band.Capacity = gauge?.GraphCapacity ?? 60;

        var resubscribed = Resubscribe();
        Lay();

        // Only a change the part asked to see at once, at an unchanged cadence, still needs a push.
        if (sampleNow && !resubscribed)
        {
            Sample();
        }
    }

    /// <summary>True when the subscription was rebuilt, which also fires an immediate tick.</summary>
    private bool Resubscribe()
    {
        var interval = (_config as GaugeConfig)?.TickInterval ?? TimeSpan.FromSeconds(1);

        // Only when the cadence actually changed. Dropping and remaking the subscription on every
        // config touch made dragging a settings slider stutter.
        if (_subscription is not null && interval == _subscribedInterval)
        {
            return false;
        }

        // A new cadence makes the samples already held a different spacing from the ones to come,
        // which a fixed-slot x-axis would draw as a kink in time. Start the history again.
        if (_subscription is not null)
        {
            ClearHistory();
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, Sample);
        return _subscription is not null;
    }

    private void Sample()
    {
        try
        {
            _part.Sample(this);
        }
        catch (Exception ex)
        {
            // A meter that cannot read its counter shows that it cannot, rather than taking the
            // whole surface down from inside a tick.
            Log.Error(Scope, $"could not sample {_kind}", ex);
            Present(MeterReading.Unavailable(Str.WidgetsB_Meters_Error.Text()));
        }
    }

    /// <summary>Put one reading on screen, wherever it came from.</summary>
    public void Present(MeterReading reading)
    {
        if (_detached)
        {
            return;
        }

        var wasAbsent = _absent;

        // A desktop with no battery is not a battery meter that failed, so it gets an empty state
        // rather than a dimmed "--".
        _absent = reading.Absent;

        _primary.Text = reading.Primary;

        // Unavailable is a colour, not an opacity. Pressure colours the chart, never the number,
        // which stays legible whatever the load. Why it is unavailable is said on hover.
        _primary.Foreground = reading.Available ? _readingBrush : _unavailableBrush;
        ToolTipService.SetToolTip(_reading, reading.Available ? null : reading.Reason);

        var pressure = Pressure(_part.PressureOf(reading));
        _band.SetBrush(pressure);

        var badge = _part.Badge(reading);
        _badge.Glyph = badge ?? string.Empty;
        _badge.Foreground = pressure;
        _badge.Visibility = badge is null ? Visibility.Collapsed : Visibility.Visible;

        // [S-1172] A reading with a fraction is a sample even when it is not available: a ping that
        // went unanswered (dimmed "--") still plots, on the baseline. Every other unavailable
        // reading carries no fraction, so it is not one.
        if (reading is { Fraction: { } fraction })
        {
            // A window-scaled chart plots the value itself; a share chart plots the fraction.
            _band.Push(_part.Scale.FollowsWindow ? reading.Value ?? fraction : fraction, reading.Second);
        }

        // Only the absent state swaps the face out; every other reading keeps the layout it has.
        if (wasAbsent != _absent)
        {
            Lay();
        }
    }

    public void ClearHistory() => _band.Clear();

    /// <summary>[S-1167] Keep the surface for a kind's header title (MeterView.Part.cs).</summary>
    public void Bind(IWidgetSurface surface) => BindTitle(surface);

    private Brush Pressure(MeterPressure pressure) => pressure switch
    {
        MeterPressure.Critical => _criticalBrush,
        MeterPressure.Elevated => _cautionBrush,
        MeterPressure.Healthy => _successBrush,
        _ => _accentBrush,
    };

    public void Detach()
    {
        _detached = true;
        _subscription?.Dispose();
        _subscription = null;
        _part.Detach();
    }
}
