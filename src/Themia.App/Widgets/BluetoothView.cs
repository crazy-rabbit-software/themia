using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// The paired Bluetooth devices, as a Windows 11 list.
/// <para>
/// Built to sit next to the Bluetooth page in Settings: a Fluent list row, the Body step for the
/// device name, a 16px Segoe glyph chosen by what the device is, and no rule between rows. The row
/// carries the 16px inset as its own padding, so the highlight under the pointer reaches both card
/// edges the way a row's does.
/// </para>
/// <para>
/// <b>Read-only.</b> Nothing here connects, disconnects or unpairs. A surface that sits under every
/// other window, with no confirmation and no undo, is the wrong place to drop a headset mid-call.
/// </para>
/// <para>
/// <b>Ladder</b> ([S-1254], <see cref="BluetoothLayout"/>): every whole row the tile has, up to the
/// ceiling; the status word from 260 px; a second line (kind · state · battery · signal) on tall
/// tiles; a battery bar from 400 px; "2 connected" in the header and two columns on the largest.
/// Every empty list says which empty list it is.
/// </para>
/// </summary>
internal sealed partial class BluetoothView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSizeAware, IWidgetContextActions, IWidgetKeys
{
    private const string Scope = "bluetooth";

    /// <summary>The connection dot's diameter. Fluent's smallest meaningful mark.</summary>
    private const double DotSize = Fluent.Space2;

    /// <summary>The battery bar: a short track, the height of Fluent's progress bar.</summary>
    private const double BarWidth = 40;

    private const double BarHeight = 4;

    // Segoe Fluent Icons code points this view needs beyond the shared table.
    private static readonly string[] KindGlyphs =
    [
        From(0xE702), // unknown  - Bluetooth
        From(0xE7F6), // audio    - Headphone
        From(0xE962), // mouse    - Mouse
        From(0xE765), // keyboard - KeyboardClassic
        From(0xE8EA), // phone    - CellPhone
        From(0xE7FC), // gamepad  - Game
        From(0xE929), // pen      - PenWorkspace
        From(0xE916), // watch    - Stopwatch
    ];

    private readonly TextBlock _count = new();
    private readonly Border _badge;
    private readonly TextBlock _summary = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly ProgressRing _busy = new();
    private readonly StackPanel _trailing;
    private readonly Grid _columns = new();
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _message;
    private readonly Border _messageHost;

    // One per row, so the hover handlers come off with the rows they light. A leaked one keeps the
    // row alive and re-lights it after the list has been redrawn.
    private readonly List<IDisposable> _hovers = [];

    private readonly Brush _connectedBrush = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private readonly Brush _pairedBrush = FluentLayout.Themed("TextFillColorDisabledBrush", Colors.Gray);
    private readonly Brush _trackBrush = FluentLayout.Themed("ControlStrongFillColorDisabledBrush", Colors.Gray);
    private readonly Brush _lowBrush = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.Red);

    private Ticker? _ticker;
    private BluetoothConfig _config = new();

    /// <summary>
    /// [S-13a] A copy of the settings this view last acted on, which is what a change is measured
    /// against. Not <c>_config</c>: the settings page edits the live config object in place, so by
    /// the time a change arrives the "previous" config already holds the new values.
    /// </summary>
    private BluetoothConfig _applied = new();
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private CancellationTokenSource? _inFlight;
    private WidgetArea _area;

    private BluetoothSnapshot _snapshot = BluetoothSnapshot.Of(BluetoothStatus.Loading);

    /// <summary>
    /// Which enumeration is current. A reply carrying an older number is a radio the widget has
    /// stopped waiting on — a GATT pass that took eight seconds must not overwrite a newer one.
    /// </summary>
    private int _generation;

    private bool _detached;

    public BluetoothView()
    {
        _busy.Width = 14;
        _busy.Height = 14;
        _busy.IsActive = false;
        _busy.Visibility = Visibility.Collapsed;

        _badge = Fluent.Badge(_count);
        _badge.Visibility = Visibility.Collapsed;
        _badge.Margin = new Thickness(0, 0, Fluent.Space2, 0);

        _summary.Visibility = Visibility.Collapsed;
        _summary.VerticalAlignment = VerticalAlignment.Center;
        _summary.Margin = new Thickness(0, 0, Fluent.Space2, 0);

        // All sit in the card header's trailing slot: the ring while a pass is out, the count (or,
        // on a large tile, "2 connected") once one has landed. Built here because the card reads it
        // once, when it builds the header.
        _trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _summary, _badge, _busy },
        };

        _scroll = new ScrollViewer
        {
            Content = _columns,
            Padding = WidgetLayout.ListInset,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };

        _message = Fluent.Text(FluentType.Body, "TextFillColorTertiaryBrush");
        _message.TextWrapping = TextWrapping.Wrap;
        _message.TextTrimming = TextTrimming.None;
        _message.TextAlignment = TextAlignment.Center;

        _messageHost = new Border
        {
            Child = _message,
            Padding = WidgetLayout.SideInset,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        Children.Add(_scroll);
        Children.Add(_messageHost);
        Children.Add(_detail);
    }

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _trailing;

    /// <inheritdoc />
    public IReadOnlyList<WidgetAction> ContextActions =>
    [
        // [S-1259] The original's "Refresh": enumerate now rather than at the next tick.
        new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), Reload),
    ];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1109] Rows, states and the summary are worded again; the radios are not re-read.</summary>
    private void OnLanguage(Locale locale) => Render();

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as BluetoothConfig ?? new BluetoothConfig();
        var previous = _applied;
        _config = next;
        _applied = (BluetoothConfig)next.Clone();

        // Only the fields that change what comes off the radio are worth a re-enumeration.
        // Everything else redraws what is already in hand: dragging the row-count slider must not
        // fire a GATT pass per frame.
        var needsRead = previous.ShowBattery != next.ShowBattery || previous.ShowSignal != next.ShowSignal;

        // Subscribing fires a tick of its own, which reads with the new config already in place.
        if (Resubscribe())
        {
            return;
        }

        if (needsRead)
        {
            Reload();
        }
        else
        {
            Render();
        }
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    /// <summary>
    /// Rebuild the ticker subscription only when the cadence actually changed, and say whether it
    /// did — a new subscription ticks immediately, so the caller has nothing left to do.
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
        _subscription = _ticker?.Subscribe(interval, Reload);
        return _subscription is not null;
    }

    /// <summary>
    /// Start an enumeration. Never on the UI thread: enumerating the radios is an RPC and a GATT
    /// battery read waits on a peripheral to answer, which on the dispatcher is every widget on the
    /// desktop frozen with it.
    /// </summary>
    private void Reload()
    {
        if (_detached)
        {
            return;
        }

        // The previous pass is now for a configuration nobody is looking at.
        _inFlight?.Cancel();

        var cts = new CancellationTokenSource();
        _inFlight = cts;
        var generation = ++_generation;

        _ = LoadAsync(_config.ShowBattery, _config.ShowSignal, generation, cts);
    }

    private async Task LoadAsync(bool withBattery, bool withSignal, int generation, CancellationTokenSource cts)
    {
        try
        {
            SetBusy(true);

            var snapshot = await BluetoothInventory.ReadAsync(withBattery, withSignal, cts.Token).ConfigureAwait(true);

            // A late reply from a pass the widget has moved on from. Discarded rather than drawn.
            if (_detached || generation != _generation)
            {
                return;
            }

            _snapshot = snapshot;
            Render();
        }
        catch (OperationCanceledException)
        {
            // ignore: superseded by a newer pass, which will draw in this one's place.
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the paired-device pass failed", ex);

            if (!_detached && generation == _generation)
            {
                _snapshot = BluetoothSnapshot.Of(BluetoothStatus.Failed);
                Render();
            }
        }
        finally
        {
            if (ReferenceEquals(_inFlight, cts))
            {
                _inFlight = null;
                SetBusy(false);
            }

            cts.Dispose();
        }
    }

    private void SetBusy(bool busy)
    {
        if (_detached)
        {
            return;
        }

        _busy.IsActive = busy;
        _busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Draw the snapshot in hand at the size in hand. Every state with no rows says which state it
    /// is: a blank tile is indistinguishable from a broken one.
    /// </summary>
    private void Render()
    {
        if (_detached)
        {
            return;
        }

        ClearRows();

        List<BluetoothDeviceEntry> devices = _snapshot.Status == BluetoothStatus.Ok
            ? BluetoothRules.Arrange(_snapshot.Devices, _config.ConnectedOnly, _config.EffectiveMaxCount, Strings.Current)
            : [];

        var connected = devices.Count(device => device.Connected);

        // The list's own height: the slot less the list's top and bottom padding.
        var inset = WidgetLayout.ListInset;
        var listHeight = Math.Max(0, _area.Height - inset.Top - inset.Bottom);
        var plan = BluetoothLayout.Plan(_area, listHeight, devices.Count);

        // [S-1255] "2 connected" in words where the header has room; the bare count elsewhere.
        var summary = plan.Summary ? BluetoothRules.ConnectedSummary(connected, Strings.Current) : null;
        _summary.Text = summary ?? string.Empty;
        _summary.Visibility = summary is null ? Visibility.Collapsed : Visibility.Visible;
        _count.Text = connected.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _badge.Visibility = connected > 0 && summary is null ? Visibility.Visible : Visibility.Collapsed;

        if (devices.Count == 0)
        {
            _keyDevices = [];

            // Ok with no rows is "nothing paired", which StatusMessage already words correctly.
            _message.Text = BluetoothRules.StatusMessage(_snapshot.Status, Strings.Current);
            _messageHost.Visibility = Visibility.Visible;
            _scroll.Visibility = Visibility.Collapsed;
            ShowDetail(plan, devices, listHeight);
            return;
        }

        // [S-1569] Which device the panel shows, so its row is lit while the panel is up.
        _detailId = plan.Detail ? BluetoothRules.Selected(devices, _selectedId)?.Id : null;
        _keyDevices = plan.Detail ? [.. devices.Select(device => device.Id)] : [];

        _messageHost.Visibility = Visibility.Collapsed;
        _scroll.Visibility = Visibility.Visible;

        // Two columns fill the first before the second, each as tall as the tile allows.
        var perColumn = plan.Columns == 2
            ? Math.Max(plan.VisibleRows, (devices.Count + 1) / 2)
            : devices.Count;

        _columns.ColumnDefinitions.Clear();

        for (var c = 0; c < plan.Columns; c++)
        {
            _columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var column = new StackPanel { Spacing = 0 };
            SetColumn(column, c);
            _columns.Children.Add(column);

            foreach (var device in devices.Skip(c * perColumn).Take(perColumn))
            {
                column.Children.Add(BuildRow(device, plan));
            }
        }

        // [S-1254] The list shows whole rows only: snapped to the rows that fit, the rest a scroll
        // away, so no row is ever cut through its middle at the card's foot.
        var rows = Math.Min(perColumn, plan.VisibleRows);
        _scroll.Height = _area.IsMeasured && perColumn > plan.VisibleRows
            ? (rows * plan.RowHeight) + inset.Top + inset.Bottom
            : double.NaN;

        ShowDetail(plan, devices, listHeight);
    }

    private Border BuildRow(BluetoothDeviceEntry device, BluetoothRowPlan plan)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        // [S-1257] The kind icon is the original's "type" metric: optional.
        if (_config.ShowIcon)
        {
            var icon = Fluent.Icon(
                KindGlyphs[(int)device.Kind],
                device.Connected ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");

            icon.Margin = new Thickness(0, 0, Fluent.Space3, 0);
            SetColumn(icon, 0);
            grid.Children.Add(icon);
        }

        // On a two-line row the facts go under the name, except the ones a wide row has its own
        // column for: a battery bar reads better than "80%" buried in a sentence.
        var batteryColumn = _config.ShowBattery && plan.BatteryColumn && (!plan.TwoLine || plan.BatteryBar);
        var signalColumn = _config.ShowSignal && plan.SignalColumn && (!plan.TwoLine || plan.BatteryBar);

        var name = Fluent.Text(FluentType.Body);
        name.Text = device.Name;

        if (plan.TwoLine)
        {
            var detail = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            detail.Text = BluetoothRules.DetailLine(device, _config.ShowConnection, _config.ShowBattery && !batteryColumn, _config.ShowSignal && !signalColumn, Strings.Current);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name, detail } };
            SetColumn(stack, 1);
            grid.Children.Add(stack);
        }
        else
        {
            SetColumn(name, 1);
            grid.Children.Add(name);
        }

        // Only when the device actually reported one. A column of blanks down the list is worse
        // than no column, and "--" would read as a battery at zero.
        if (batteryColumn && BluetoothRules.BatteryLabel(device.BatteryPercent, Strings.Current) is { } battery && device.BatteryPercent is { } percent)
        {
            FrameworkElement level = plan.BatteryBar ? BatteryWithBar(percent, battery) : Detail(battery);
            SetColumn(level, 2);
            grid.Children.Add(level);
        }

        if (signalColumn && BluetoothRules.SignalLabel(device.SignalDbm) is { } signal)
        {
            var strength = Detail(signal);
            SetColumn(strength, 3);
            grid.Children.Add(strength);
        }

        if (_config.ShowConnection)
        {
            if (plan.StatusWord)
            {
                var state = Detail(BluetoothRules.ConnectionLabel(device.Connected, Strings.Current));
                SetColumn(state, 4);
                grid.Children.Add(state);
            }

            // The dot is a colour from the theme, never a literal: accent for connected, the
            // disabled text colour for merely paired, both of which follow light and dark.
            var dot = new Ellipse
            {
                Width = DotSize,
                Height = DotSize,
                Fill = device.Connected ? _connectedBrush : _pairedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(Fluent.Space2, 0, 0, 0),
            };

            SetColumn(dot, 5);
            grid.Children.Add(dot);
        }

        var row = Fluent.Row(grid);
        row.MinHeight = plan.RowHeight;
        row.Height = plan.RowHeight;

        // [S-1569] The device in the detail panel keeps its row lit (no hover, which would clear it
        // on the way out); a click on another row puts that one in the panel.
        if (_detailId is not null && device.Id == _detailId)
        {
            row.Background = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray);

            // [S-1988] A key moved the selection: its row scrolls into view once it is laid out.
            if (_revealSelected)
            {
                _revealSelected = false;
                row.Loaded += (_, _) => row.StartBringIntoView();
            }
        }
        else
        {
            _hovers.Add(Fluent.Hover(row));
        }

        if (plan.Detail)
        {
            var id = device.Id;
            row.Tapped += (_, args) =>
            {
                args.Handled = true;
                _selectedId = id;
                Render();
            };
        }

        // Everything the row knows, in words, for a reader who cannot see the dot or the bar.
        ToolTipService.SetToolTip(row, $"{device.Name} · {BluetoothRules.DetailLine(device, true, true, true, Strings.Current)}");

        return row;
    }

    /// <summary>
    /// [S-1254] The battery as a short level bar beside its percentage, on a row wide enough to
    /// carry both. The bar turns critical at 20% and below, like the system tray's.
    /// </summary>
    private StackPanel BatteryWithBar(int percent, string label)
    {
        var fill = new Rectangle
        {
            Width = BarWidth * Math.Clamp(percent, 0, 100) / 100.0,
            Height = BarHeight,
            RadiusX = BarHeight / 2,
            RadiusY = BarHeight / 2,
            Fill = percent <= 20 ? _lowBrush : _connectedBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var track = new Grid
        {
            Width = BarWidth,
            Height = BarHeight,
            CornerRadius = new CornerRadius(BarHeight / 2),
            Background = _trackBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { fill },
        };

        var text = Detail(label);
        text.MinWidth = 32;

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Fluent.Space2, 0, 0, 0),
            Children = { track, text },
        };
    }

    /// <summary>
    /// A right-aligned detail column: the secondary text colour, the Caption step, and tabular
    /// figures so the percentages line up down the list instead of jittering per row.
    /// </summary>
    private static TextBlock Detail(string text)
    {
        var block = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        block.Text = text;
        block.TextAlignment = TextAlignment.Right;
        block.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
        block.VerticalAlignment = VerticalAlignment.Center;

        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular);
        return block;
    }

    // ---- keys ([S-1988]) -------------------------------------------------------------------------

    /// <summary>[S-1988] The devices the arrows step through, in list order: only while the detail panel is up.</summary>
    private List<string> _keyDevices = [];

    /// <summary>[S-1988] Set by a key, so only the keyboard (not a tick's redraw) scrolls the list.</summary>
    private bool _revealSelected;

    /// <summary>[S-1988] Only a tile with the detail panel and more than one device takes the keyboard.</summary>
    public bool WantsKeys => !_detached && _keyDevices.Count > 1;

    /// <summary>
    /// [S-1988] With the detail panel up the lit row is the selection, so the arrows (and Page Up/Down,
    /// Home/End, <see cref="ItemCursor"/>) move it and the panel follows, as a click on a row does.
    /// There is nothing to clear on Escape: the panel always shows a device.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (!WantsKeys || key == WidgetKey.Escape)
        {
            return false;
        }

        var current = _detailId is null ? -1 : _keyDevices.IndexOf(_detailId);
        if (ItemCursor.Move(_keyDevices.Count, current < 0 ? null : current, key) is not { } next || next == current)
        {
            return key is WidgetKey.Up or WidgetKey.Down or WidgetKey.Left or WidgetKey.Right or WidgetKey.Home or WidgetKey.End or WidgetKey.PageUp or WidgetKey.PageDown;
        }

        _selectedId = _keyDevices[next];
        _revealSelected = true;
        Render();
        return true;
    }

    /// <summary>Drop the rows and, with them, their hover handlers.</summary>
    private void ClearRows()
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();
        _columns.Children.Clear();
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;

        ClearRows();

        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = TimeSpan.Zero;

        // Cancel but do not dispose: the in-flight pass owns its source and frees it on the way
        // out, so there is exactly one owner however many times this is called.
        _inFlight?.Cancel();
        _inFlight = null;

        _ticker = null;
    }

    private static string From(int codePoint) => char.ConvertFromUtf32(codePoint);
}
