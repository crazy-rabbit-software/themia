using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1240] The power tile: a grid of buttons — shut down, restart, sleep, hibernate, lock, sign out,
/// display off — with the machine's power state as a strip wherever the buttons leave room.
/// <para>
/// <b>Safety first.</b> Session-ending actions arm on the first click and run on the second
/// (<see cref="PowerArm"/>: one armed tile at a time, 5 s window, a double-click is not two clicks,
/// pointer-leave disarms). The machine is touched only through <see cref="IPowerActions"/>, which
/// starts inert and becomes the real backend only once the card says this is not a preview — so a
/// preview's buttons never run anything ([S-1251]). Actions run on a worker thread, one at a time.
/// </para>
/// <para>
/// <b>Ladder</b> ([S-1248]): every button labelled, then every button as an icon, then as many icons
/// as fit; the status strip only takes room the buttons do not need — under them, beside them on a
/// wide strip, or as a panel of facts and power plans on the largest tiles.
/// </para>
/// </summary>
internal sealed partial class PowerView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware
{
    private const string Scope = "power";

    /// <summary>A strip line's icon: Caption-sized to sit with Caption text.</summary>
    private const double StripIconSize = 12;

    private readonly Grid _buttons = new();
    private readonly StackPanel _strip = new() { Spacing = 0 };
    private readonly StackPanel _panel = new() { Spacing = 0 };
    private readonly List<IDisposable> _hovers = [];
    private readonly Dictionary<PowerAction, Tile> _tiles = [];
    private readonly DispatcherQueueTimer? _disarmTimer;
    private readonly PowerArm _arm = new();

    private readonly Brush _critical = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.Red);
    private readonly Brush _primary = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
    private readonly Brush _secondary = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);

    private Ticker? _ticker;
    private IWidgetSurface? _surface;
    private PowerConfig _config = new();
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private WidgetArea _area;

    /// <summary>Inert until the card says this is a real widget on the desktop ([S-1251]).</summary>
    private IPowerActions _actions = InertPowerActions.Instance;
    private bool _preview = true;

    private PowerSnapshot _snapshot = PowerSnapshot.Unreadable;
    private IReadOnlyList<PowerPlanEntry>? _plans;
    private PowerTimeouts _timeouts = PowerTimeouts.Unknown;
    private IReadOnlyList<PowerAction> _shownActions = [];
    private PowerTilePlan _plan;
    private string? _error;
    private bool _running;
    private bool _detached;

    public PowerView()
    {
        _buttons.HorizontalAlignment = HorizontalAlignment.Left;
        _buttons.VerticalAlignment = VerticalAlignment.Top;

        _strip.VerticalAlignment = VerticalAlignment.Bottom;
        _panel.VerticalAlignment = VerticalAlignment.Top;
        _panel.HorizontalAlignment = HorizontalAlignment.Right;

        Children.Add(_buttons);
        Children.Add(_strip);
        Children.Add(_panel);

        // [S-1247] The confirm step catches a mis-click; a tile left armed under the cursor would
        // defeat it on the user's next visit.
        PointerExited += OnPointerExited;

        _disarmTimer = DispatcherQueue.GetForCurrentThread()?.CreateTimer();

        if (_disarmTimer is not null)
        {
            _disarmTimer.IsRepeating = false;
            _disarmTimer.Interval = PowerArm.Window;
            _disarmTimer.Tick += OnDisarmTick;
        }
        else
        {
            // ignore: a design-time host. PowerArm still expires on its own clock, so safety holds;
            // only the "Confirm" label lingers until the next press or pointer exit.
            Log.Warn(Scope, "no dispatcher queue; an armed tile will not repaint when it expires");
        }
    }

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        _preview = surface.IsPreview;
        _actions = _preview ? InertPowerActions.Instance : Win32PowerActions.Instance;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1110] The fixed texts and every tile's name again, then the facts redrawn.</summary>
    private void OnLanguage(Locale locale)
    {
        foreach (var tile in _tiles.Values)
        {
            AutomationProperties.SetName(tile.Button, PowerActionRules.Label(tile.Action));
        }

        PaintArmed();
        Lay();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as PowerConfig ?? new PowerConfig();

        // A changed set of buttons is a different tile: nothing stays armed across it.
        Disarm();
        _error = null;

        if (!Resubscribe())
        {
            Lay();
        }
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>
    /// Rebuild the ticker subscription only when the cadence changed. A new subscription ticks
    /// immediately, which samples and lays out.
    /// </summary>
    private bool Resubscribe()
    {
        var interval = _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return false;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, Sample);
        return _subscription is not null;
    }

    private void Sample()
    {
        if (_detached)
        {
            return;
        }

        _snapshot = ReadSnapshot();

        // The plan list and the timeouts cost registry reads; read them only while the panel shows them.
        if (_plan.Strip == PowerStripPlacement.Panel)
        {
            _plans = ReadPlans();
            _timeouts = ReadTimeouts();
        }

        Lay();
    }

    private PowerSnapshot ReadSnapshot()
    {
        return PowerStatus.Read();
    }

    private IReadOnlyList<PowerPlanEntry> ReadPlans()
    {
        return PowerStatus.Plans();
    }

    private PowerTimeouts ReadTimeouts()
    {
        return PowerStatus.Timeouts(_snapshot.Source);
    }

    // ---- layout ------------------------------------------------------------------------------

    /// <summary>[S-1248] Decide what fits, then draw it.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var actions = _config.VisibleActions;

        if (actions.Count == 0)
        {
            // [S-1252] An empty selection is the card's Set up button ([S-1862]), not an empty card.
            _buttons.Visibility = Visibility.Collapsed;
            _strip.Visibility = Visibility.Collapsed;
            _panel.Visibility = Visibility.Collapsed;
            return;
        }

        _buttons.Visibility = Visibility.Visible;

        var compact = _area.WidthClass == SizeClass.Compact || _area.HeightClass == SizeClass.Compact;
        var inset = compact ? Fluent.Space2 : Fluent.Space3;
        var gap = compact ? Fluent.Space1 : Fluent.Space2;

        // Bottom and sides; the header above already carries its own margin.
        Padding = new Thickness(inset, 0, inset, inset);
        var width = Math.Max(0, _area.Width - (2 * inset));
        var height = Math.Max(0, _area.Height - inset);

        var facts = Facts();

        // The size classes gate where the status may go; the budget in PowerTileLayout decides
        // whether it actually does. XS and the short S tile are buttons only.
        var allowStrip = _area.IsMeasured && (_area.HeightClass >= SizeClass.Regular || _area.Shape == AreaShape.Wide)
            && _area.WidthClass >= SizeClass.Regular;
        var allowBeside = _area.Shape == AreaShape.Wide && _area.WidthClass >= SizeClass.Expanded;
        var allowPanel = _area.WidthClass >= SizeClass.Huge && _area.HeightClass >= SizeClass.Expanded;

        _plan = _area.IsMeasured
            ? PowerTileLayout.Plan(actions.Count, width, height, _config.ShowLabels, facts.Count, allowStrip, allowBeside, allowPanel, gap)
            : PowerTileLayout.Plan(actions.Count, width, height, labelsWanted: false, 0, false, false, false, gap);

        if (_plan.Strip == PowerStripPlacement.Panel && _plans is null)
        {
            _plans = ReadPlans();
            _timeouts = ReadTimeouts();
        }

        BuildButtons(actions, gap);
        PlaceButtons();
        DrawStrip(facts);
        DrawPanel(facts, height);
    }

    /// <summary>
    /// The status lines, most important first. A failed action leads, in the critical colour: the
    /// machine is still up and the tile has to say why ([S-1246]).
    /// </summary>
    private List<PowerFact> Facts()
    {
        var facts = new List<PowerFact>();

        if (_error is not null)
        {
            facts.Add(new PowerFact(PowerFactKind.Error, Str.WidgetsC_Power_Fact_Error.Text(), _error, _error));
        }

        facts.AddRange(PowerStrip.Facts(_snapshot, _config.ShowPlan, _config.ShowTimeRemaining, _config.ShowBatterySaver));
        return facts;
    }

    /// <summary>Rebuild the button grid when the set, the shape or the labels changed; repaint always.</summary>
    private void BuildButtons(IReadOnlyList<PowerAction> actions, double gap)
    {
        var grid = _plan.Grid;
        var shown = actions.Take(grid.Shown).ToList();

        var rebuild = !shown.SequenceEqual(_shownActions)
            || _buttons.ColumnDefinitions.Count != grid.Columns
            || _buttons.RowDefinitions.Count != grid.Rows
            || _tiles.Values.FirstOrDefault()?.Labelled != grid.Labels;

        if (rebuild)
        {
            ClearButtons();
            _shownActions = shown;

            for (var c = 0; c < grid.Columns; c++)
            {
                _buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            for (var r = 0; r < grid.Rows; r++)
            {
                _buttons.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            for (var i = 0; i < shown.Count; i++)
            {
                var tile = BuildTile(shown[i], grid.Labels);
                SetColumn(tile.Button, i % grid.Columns);
                SetRow(tile.Button, i / grid.Columns);
                _buttons.Children.Add(tile.Button);
                _tiles[shown[i]] = tile;
            }
        }

        _buttons.ColumnSpacing = gap;
        _buttons.RowSpacing = gap;

        // Icons scale a step with the cell: 20 where there is room for it, 16 in the smallest.
        var iconSize = grid.CellHeight >= 44 && grid.CellWidth >= 44 ? 20 : Fluent.IconSize;

        foreach (var tile in _tiles.Values)
        {
            tile.Icon.FontSize = iconSize;
        }

        PaintArmed();
    }

    private Tile BuildTile(PowerAction action, bool labelled)
    {
        var icon = Fluent.Icon(PowerGlyphs.For(action), "TextFillColorPrimaryBrush", 20);
        icon.HorizontalAlignment = HorizontalAlignment.Center;

        var label = Fluent.Text(FluentType.Caption);
        label.Text = PowerActionRules.Label(action);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.TextAlignment = TextAlignment.Center;
        label.Margin = new Thickness(0, Fluent.Space1, 0, 0);
        label.Visibility = labelled ? Visibility.Visible : Visibility.Collapsed;

        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { icon, label },
        };

        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(Fluent.Space1),
            MinWidth = 0,
            MinHeight = 0,
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
        };

        AutomationProperties.SetName(button, PowerActionRules.Label(action));
        button.Click += (_, _) => OnPress(action);

        return new Tile(action, button, icon, label, labelled);
    }

    private void PlaceButtons()
    {
        var plan = _plan;
        _buttons.Width = Math.Max(0, plan.GridWidth);
        _buttons.Height = Math.Max(0, plan.GridHeight);

        // Below: the buttons are centred in what the strip leaves. Beside / panel: centred on the
        // left. Alone: centred in the tile, so a capped cell height leaves even margins.
        var stripHeight = plan.Strip == PowerStripPlacement.Below
            ? (plan.StripLines * PowerTileLayout.StripLineHeight) + Fluent.Space2
            : 0;

        _buttons.HorizontalAlignment = HorizontalAlignment.Left;
        _buttons.VerticalAlignment = plan.Strip == PowerStripPlacement.Panel ? VerticalAlignment.Top : VerticalAlignment.Center;
        _buttons.Margin = new Thickness(0, 0, 0, stripHeight);
    }

    private void DrawStrip(List<PowerFact> facts)
    {
        _strip.Children.Clear();

        var placement = _plan.Strip;

        if (placement is not (PowerStripPlacement.Below or PowerStripPlacement.Beside))
        {
            _strip.Visibility = Visibility.Collapsed;
            return;
        }

        _strip.Visibility = Visibility.Visible;
        _strip.Width = placement == PowerStripPlacement.Beside ? _plan.StripWidth : double.NaN;
        _strip.HorizontalAlignment = placement == PowerStripPlacement.Beside ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        _strip.VerticalAlignment = placement == PowerStripPlacement.Beside ? VerticalAlignment.Center : VerticalAlignment.Bottom;

        foreach (var fact in facts.Take(_plan.StripLines))
        {
            _strip.Children.Add(StripLine(fact));
        }
    }

    private FrameworkElement StripLine(PowerFact fact)
    {
        var error = fact.Kind == PowerFactKind.Error;
        var icon = Fluent.Icon(FactGlyph(fact.Kind), "TextFillColorSecondaryBrush", StripIconSize);
        icon.Margin = new Thickness(0, 0, Fluent.Space2, 0);

        var text = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        text.Text = fact.Line;
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(text, FontNumeralAlignment.Tabular);

        if (error)
        {
            icon.Foreground = _critical;
            text.Foreground = _critical;
            ToolTipService.SetToolTip(text, fact.Line);
        }

        var line = new Grid
        {
            Height = PowerTileLayout.StripLineHeight,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Children = { icon, text },
        };

        text.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(text, 1);
        return line;
    }

    /// <summary>
    /// [S-1249] The largest tiles' side panel: every fact as a row, then the power plans with the
    /// active one marked. Read-only: switching plans is Settings' job.
    /// <para>
    /// [S-1623] On a tile Huge in height it goes on: the time since Windows started under the
    /// facts, and a "Screen and sleep" section after the plans, so the column under the plans is
    /// not left empty; its rows are then Windows' standard list height rather than compact.
    /// </para>
    /// </summary>
    private void DrawPanel(List<PowerFact> facts, double height)
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();
        _panel.Children.Clear();

        if (_plan.Strip != PowerStripPlacement.Panel)
        {
            _panel.Visibility = Visibility.Collapsed;
            return;
        }

        _panel.Visibility = Visibility.Visible;
        _panel.Width = _plan.StripWidth;

        var room = new Disclosure(_plan.StripWidth, height);
        var huge = _area.HeightClass >= SizeClass.Huge;
        var rowHeight = huge ? Fluent.RowHeight : Fluent.RowHeightCompact;
        var shownFacts = huge ? [.. facts, PowerTimeoutRules.Uptime(TimeSpan.FromMilliseconds(Environment.TickCount64))] : facts;
        var factRows = room.TakeRows(rowHeight, shownFacts.Count);

        foreach (var fact in shownFacts.Take(factRows))
        {
            _panel.Children.Add(PanelRow(FactGlyph(fact.Kind), fact.Label, fact.Value, fact.Kind == PowerFactKind.Error, compact: !huge));
        }

        var plans = _plans ?? [];

        if (plans.Count > 0 && room.Take(FluentType.Caption.LineHeight + Fluent.Space3))
        {
            _panel.Children.Add(PanelHeading(Str.WidgetsC_Power_Plans.Text()));

            var planRows = room.TakeRows(rowHeight, plans.Count);

            foreach (var plan in plans.Take(planRows))
            {
                _panel.Children.Add(PanelRow(plan.Active ? Glyphs.Done : Glyphs.Lightning, plan.Name, plan.Active ? Str.WidgetsC_Power_PlanActive.Text() : string.Empty, false, compact: !huge, accent: plan.Active));
            }
        }

        var timeouts = huge ? PowerTimeoutRules.Facts(_timeouts) : [];

        if (timeouts.Count > 0
            && Disclosure.WholeRows(room.Remaining - FluentType.Caption.LineHeight - Fluent.Space3, rowHeight) >= timeouts.Count
            && room.Take(FluentType.Caption.LineHeight + Fluent.Space3))
        {
            _panel.Children.Add(PanelHeading(Str.WidgetsC_Power_ScreenAndSleep.Text()));

            foreach (var fact in timeouts.Take(room.TakeRows(rowHeight, timeouts.Count)))
            {
                _panel.Children.Add(PanelRow(FactGlyph(fact.Kind), fact.Label, fact.Value, false, compact: false));
            }
        }
    }

    private static TextBlock PanelHeading(string text)
    {
        var heading = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        heading.Text = text;
        heading.Margin = new Thickness(0, Fluent.Space3, 0, 0);
        return heading;
    }

    private Border PanelRow(string glyph, string label, string value, bool critical, bool compact, bool accent = false)
    {
        var icon = Fluent.Icon(glyph, accent ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        icon.Margin = new Thickness(0, 0, Fluent.Space3, 0);

        var name = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        name.Text = label;

        var detail = Fluent.Text(FluentType.Body);
        detail.Text = value;
        detail.TextAlignment = TextAlignment.Right;
        detail.Margin = new Thickness(Fluent.Space2, 0, 0, 0);

        if (critical)
        {
            icon.Foreground = _critical;
            detail.Foreground = _critical;
            ToolTipService.SetToolTip(detail, value);
        }

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Children = { icon, name, detail },
        };

        SetColumn(name, 1);
        SetColumn(detail, 2);

        // The panel sits inside the tile's inset, so its rows pad themselves less than a list's.
        var row = Fluent.Row(grid, compact);
        row.Padding = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0);
        _hovers.Add(Fluent.Hover(row));
        return row;
    }

    private static string FactGlyph(PowerFactKind kind) => kind switch
    {
        PowerFactKind.Source => Glyphs.Power,
        PowerFactKind.Plan => Glyphs.Lightning,
        PowerFactKind.Remaining => Glyphs.Timer,
        PowerFactKind.Saver => Glyphs.BatterySaver,
        PowerFactKind.Uptime => Glyphs.Hours,
        PowerFactKind.ScreenOff => Glyphs.Display,
        PowerFactKind.Sleep => Glyphs.Moon,
        PowerFactKind.Hibernate => Glyphs.Hibernate,
        _ => Glyphs.Warning,
    };

    // ---- pressing ----------------------------------------------------------------------------

    /// <summary>[S-1245][S-1247] One press. The arm state machine decides; this only follows.</summary>
    private void OnPress(PowerAction action)
    {
        if (_detached || _running)
        {
            return;
        }

        switch (_arm.Press(action, _config.Confirm, DateTimeOffset.UtcNow))
        {
            case PowerPress.Armed:
                _disarmTimer?.Stop();
                _disarmTimer?.Start();
                PaintArmed();
                break;

            case PowerPress.Run:
                _disarmTimer?.Stop();
                PaintArmed();
                _ = RunAsync(action);
                break;

            default:
                // ignore: the tail of a double-click on an armed tile.
                break;
        }
    }

    /// <summary>
    /// Run one action on a worker thread — sleep blocks until the machine resumes — and show a
    /// failure on the tile ([S-1246]). One at a time: presses while one runs are dropped.
    /// </summary>
    private async Task RunAsync(PowerAction action)
    {
        _running = true;
        var hadError = _error is not null;
        _error = null;

        if (hadError)
        {
            Lay();
        }

        var runner = _actions;
        PowerOutcome outcome;

        try
        {
            outcome = await Task.Run(() => runner.Run(action)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{action} could not be started", ex);
            outcome = PowerOutcome.Error(0);
        }
        finally
        {
            _running = false;
        }

        if (_detached)
        {
            return;
        }

        if (PowerActionRules.FailureMessage(action, outcome) is { } message)
        {
            Log.Warn(Scope, $"{message} (Win32 {outcome.Win32Error})");
            _error = message;
            Lay();
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => Disarm();

    private void OnDisarmTick(DispatcherQueueTimer sender, object args)
    {
        // The window is over whether or not the clock agrees to the millisecond.
        _arm.Disarm();
        PaintArmed();
    }

    private void Disarm()
    {
        _disarmTimer?.Stop();

        if (_arm.Armed is null)
        {
            return;
        }

        _arm.Disarm();
        PaintArmed();
    }

    /// <summary>"Confirm" in the critical colour on the armed tile; every other tile as it was.</summary>
    private void PaintArmed()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var tile in _tiles.Values)
        {
            var armed = _arm.IsArmed(tile.Action, now);
            var name = PowerActionRules.Label(tile.Action);

            tile.Label.Text = armed ? PowerActionRules.ConfirmLabel : name;
            tile.Label.Foreground = armed ? _critical : _secondary;
            tile.Icon.Foreground = armed ? _critical : _primary;
            if (armed)
            {
                tile.Button.BorderBrush = _critical;
            }
            else
            {
                tile.Button.ClearValue(Control.BorderBrushProperty);
            }

            ToolTipService.SetToolTip(tile.Button, armed ? PowerActionRules.ConfirmHint(tile.Action) : name);
            AutomationProperties.SetItemStatus(tile.Button, armed ? Str.WidgetsC_Power_ArmedStatus.Text() : string.Empty);
        }
    }

    private void ClearButtons()
    {
        _tiles.Clear();
        _buttons.Children.Clear();
        _buttons.ColumnDefinitions.Clear();
        _buttons.RowDefinitions.Clear();
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;
        _arm.Disarm();

        if (_disarmTimer is not null)
        {
            _disarmTimer.Stop();
            _disarmTimer.Tick -= OnDisarmTick;
        }

        PointerExited -= OnPointerExited;

        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();

        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = TimeSpan.Zero;
        _ticker = null;
    }

    /// <summary>A built button and the parts the painter writes into.</summary>
    private sealed record Tile(PowerAction Action, Button Button, FontIcon Icon, TextBlock Label, bool Labelled);
}

/// <summary>[S-1242] The icon for each power action, shared by the tile and its settings page.</summary>
internal static class PowerGlyphs
{
    public static string For(PowerAction action) => action switch
    {
        PowerAction.Shutdown => Glyphs.Power,
        PowerAction.Restart => Glyphs.Reset,
        PowerAction.Sleep => Glyphs.Moon,
        PowerAction.Hibernate => Glyphs.Hibernate,
        PowerAction.Lock => Glyphs.Lock,
        PowerAction.SignOut => Glyphs.SignOut,
        PowerAction.ScreenOff => Glyphs.Display,
        _ => Glyphs.Power,
    };
}
