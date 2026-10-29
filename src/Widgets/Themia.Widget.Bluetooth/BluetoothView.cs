using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// The paired Bluetooth devices, as a Windows 11 list.
/// <para>
/// Built on the family's Row block ([S-6711]): the device's kind glyph (16) as the lead, the name
/// primary, the battery percentage as the value, and a second line (kind · connection · signal) where
/// the tier gives rows two ([S-6712]). The row carries the 16 inset as its own padding, so the
/// highlight under the pointer reaches both card edges.
/// </para>
/// <para>
/// <b>Read-only.</b> Nothing here connects, disconnects or unpairs. A surface that sits under every
/// other window, with no confirmation and no undo, is the wrong place to drop a headset mid-call.
/// </para>
/// <para>
/// <b>Ladder</b> ([S-1254], <see cref="BluetoothLayout"/>): every whole row the tile has, up to the
/// ceiling; the battery % from a 180 column; the second line from a regular tile both ways; two
/// columns on the largest. The header's informing label says "2 connected" where it has room and its
/// ring turns while a pass is out ([S-6710]). A connected device's glyph is in the primary text colour,
/// a merely paired one's in the secondary ([S-3961]). A short list on a large tile stays a short list.
/// Every empty list says which empty list it is, in the Status row at the top ([S-244], [S-6714]).
/// </para>
/// </summary>
internal sealed partial class BluetoothView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSizeAware, IWidgetContextActions
{
    private const string Scope = "bluetooth";

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

    private readonly HeaderParts _trailing = new();
    private readonly TextBlock _summary = HeaderBlock.Label(string.Empty);
    private readonly ProgressRing _busy = HeaderBlock.Busy();
    private readonly Grid _columns = new();
    private readonly ScrollViewer _scroll;
    private readonly StatusBlock _status = new() { VerticalAlignment = VerticalAlignment.Top };

    // The rows on screen: each unhooks its hover when the list is drawn again. A leaked one keeps the
    // row alive and re-lights it after the list has been redrawn.
    private readonly List<RowBlock> _rows = [];

    private Ticker? _ticker;
    private BluetoothConfig _config = new();

    /// <summary>
    /// [S-13a] A copy of the settings this view last acted on, which is what a change is measured
    /// against. Not <c>_config</c>: the properties menu edits the live config object in place, so by
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
        // [S-6710] The header's ring while a pass is out, then "2 connected" as its informing label.
        _trailing.Add(HeaderPartKind.Busy, _busy);
        _trailing.Add(HeaderPartKind.Label, _summary);
        _trailing.Want(_busy, false);
        _trailing.Want(_summary, false);

        _scroll = new ScrollViewer
        {
            Content = _columns,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };

        _status.Visibility = Visibility.Collapsed;
        Children.Add(_scroll);
        Children.Add(_status);
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
        _trailing.Want(_busy, busy);
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
            ? BluetoothRules.Arrange(_snapshot.Devices, _config.ConnectedOnly, BluetoothConfig.MaxDevices, Strings.Current)
            : [];

        var connected = devices.Count(device => device.Connected);
        var plan = BluetoothLayout.Plan(_area, devices.Count);

        // [S-1254] "2 connected" as the header's informing label: the first part to give way ([S-6710]).
        var summary = BluetoothRules.ConnectedSummary(connected, Strings.Current);
        _summary.Text = summary ?? string.Empty;
        _trailing.Want(_summary, summary is not null);

        if (devices.Count == 0)
        {
            // [S-244] Ok with no rows is "nothing paired", which StatusMessage already words correctly;
            // a read that failed is a problem, and asking again may mend it.
            var message = BluetoothRules.StatusMessage(_snapshot.Status, Strings.Current);
            if (_snapshot.Status == BluetoothStatus.Failed)
            {
                _status.Show(StatusKind.Problem, Glyphs.Bluetooth, message, Str.Common_Retry.Text(), Reload);
            }
            else
            {
                _status.Show(_snapshot.Status == BluetoothStatus.PermissionDenied ? StatusKind.Problem : StatusKind.Empty, Glyphs.Bluetooth, message);
            }

            _scroll.Visibility = Visibility.Collapsed;
            return;
        }

        _status.Hide();
        _scroll.Visibility = Visibility.Visible;

        // Two columns fill the first before the second, each as tall as the tile allows.
        var perColumn = plan.Columns == 2
            ? Math.Max(plan.VisibleRows, (devices.Count + 1) / 2)
            : devices.Count;

        _columns.ColumnDefinitions.Clear();

        for (var c = 0; c < plan.Columns; c++)
        {
            _columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var column = new StackPanel();
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
            ? rows * FamilyShape.RowHeight(plan.Lines)
            : double.NaN;
    }

    /// <summary>
    /// [S-1254] A device's row: its kind glyph (primary connected, secondary paired, [S-3961]), its name,
    /// its battery as the value from a 180 column (Critical when low, [S-3254]), and where the row has
    /// two lines its kind, its connection and its signal ([S-1255]).
    /// </summary>
    private RowBlock BuildRow(BluetoothDeviceEntry device, BluetoothRowPlan plan)
    {
        var row = new RowBlock(RowKind.List, _config.ShowIcon ? FamilyShape.Icon : 0) { Interactive = true };
        _rows.Add(row);

        // [S-1257] The kind icon is the original's "type" metric: optional. Wrapped, so the row's own
        // lead role does not repaint it: its colour is the state.
        if (_config.ShowIcon)
        {
            var glyph = Family.Glyph(KindGlyphs[(int)device.Kind], FamilyShape.Icon, device.Connected ? ColourRole.TextPrimary : ColourRole.TextSecondary);
            row.Lead(new Grid { Children = { glyph } });
        }

        var battery = _config.ShowBattery && plan.BatteryValue ? BluetoothRules.BatteryLabel(device.BatteryPercent, Strings.Current) : null;
        var second = BluetoothRules.DetailLine(device, _config.DrawsConnection, _config.ShowBattery && battery is null, _config.ShowSignal, Strings.Current);
        row.Lines = plan.Lines;
        row.Show(device.Name, battery, second);

        // [S-3254] 20 % and below is low: the percentage in the critical role, as the tray draws it.
        if (battery is not null && BluetoothRules.IsLowBattery(device.BatteryPercent))
        {
            row.ValueText.Foreground = Family.Brush(ColourRole.Critical);
        }

        // Everything the row knows, in words, for a reader who cannot see the icon's colour.
        ToolTipService.SetToolTip(row, $"{device.Name} · {BluetoothRules.DetailLine(device, true, true, true, Strings.Current)}");
        return row;
    }

    /// <summary>Drop the rows and, with them, their hover handlers.</summary>
    private void ClearRows()
    {
        foreach (var row in _rows)
        {
            row.Detach();
        }

        _rows.Clear();
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
