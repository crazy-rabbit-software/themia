using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.WinUI;
using Themia.Sdk.Items;
using Themia.Sdk.WinUI.Items;
using Themia.Widget.Bluetooth;
using Themia.Core.Geometry;

namespace Themia.App.Widgets;

/// <summary>
/// The paired Bluetooth devices as items (<see cref="ItemList"/>, [S-16660]) over the host's one pass over the
/// radios (<see cref="IWidgetHost.Bluetooth"/>, [S-4474]: every Bluetooth widget shares it, a connected device's battery the
/// host's one battery inventory's), each mapped by <see cref="BluetoothItems"/>.
/// <para>
/// <b>Read-only.</b> Nothing here connects, disconnects or unpairs. A surface that sits under every other window, with
/// no confirmation and no undo, is the wrong place to drop a headset mid-call.
/// </para>
/// <para>
/// Every empty list keeps the face and says which empty list it is ([S-244], WR-10): no radio, the radio off, access
/// refused, a failed pass, nothing paired, reading.
/// </para>
/// </summary>
internal sealed partial class BluetoothView : Grid, IWidgetView, IWidgetOpens, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys, IWidgetGrounds
{
    /// <inheritdoc />
    public CardOpen? Opens => BluetoothModule.Opens;

    private readonly IWidgetHost _host;
    private readonly ItemList _list;
    private BluetoothConfig _config = new();
    private IDisposable? _watch;
    private Ticker? _ticker;
    private bool _signal;
    private bool _attached;

    /// <summary>[S-11850] [S-1917] A sample's or a preview's devices in place of the radio's.</summary>
    private BluetoothSnapshot? _held;

    public BluetoothView(IWidgetHost host)
    {
        _host = host;
        _list = new ItemList(host)
        {
            Menu = _ => BluetoothItems.Menu(Refresh),
            Decorate = LinkItem,
            Redrawing = ForgetItems,
        };
        _list.GroundsChanged += () => GroundsChanged?.Invoke();
        Children.Add(_list);
    }

    /// <inheritdoc />
    public event Action? GroundsChanged;

    /// <summary>[S-16661] With the gap on, every device's box (the list stands the inset over the view's top).</summary>
    public IReadOnlyList<DipRect>? Grounds => _list.ViewGrounds;

    /// <summary>[S-1259] Refresh: read the radios again now.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), Refresh)];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as BluetoothConfig ?? new BluetoothConfig();
        _attached = true;
        _ticker = ticker;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _links.Join(_surface?.Links);
        Watch(_config.ShowSignal);
        Show();
    }

    /// <summary>[S-13a] The signal switch changes what is read off the radio; every other line redraws what is in hand.</summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as BluetoothConfig ?? new BluetoothConfig();
        if (_attached)
        {
            Watch(_config.ShowSignal);
            Show();
        }
    }

    public void ApplySize(WidgetArea area) => _list.Size(area);

    public bool HandleKey(WidgetKey key) => _list.HandleKey(key);

    public void Detach()
    {
        _attached = false;
        Strings.Changed -= OnLanguage;
        _watch?.Dispose();
        _watch = null;
        _links.Leave();
    }

    private void OnLanguage(Locale locale) => Show();

    /// <summary>[S-4474] Watch the radios (again where the signal wanted changed); a sample watches nothing.</summary>
    private void Watch(bool signal)
    {
        if (_held is not null || _ticker is null || (_watch is not null && signal == _signal))
        {
            return;
        }

        _watch?.Dispose();
        _signal = signal;
        _watch = _host.Bluetooth.Watch(_ticker, signal, Show);
    }

    private void Refresh()
    {
        if (_held is null)
        {
            _host.Bluetooth.Refresh();
        }
    }

    /// <summary>[S-16660] The devices arranged by the widget's rules as device items; none: the face with which empty list it is.</summary>
    private void Show()
    {
        var snapshot = _held ?? (_watch is null ? BluetoothSnapshot.Of(BluetoothStatus.Loading) : _host.Bluetooth.Last);
        var locale = Strings.Current;
        // [S-16661] The devices not hidden, the user's order first.
        var devices = snapshot.Status == BluetoothStatus.Ok
            ? ItemOrder.Arrange(BluetoothRules.Arrange(snapshot.Devices, _config.ConnectedOnly, locale), d => d.Id, _config.Hidden, _config.Order)
            : [];
        IReadOnlyList<Item> items = devices.Count > 0
            ? [.. devices.Select(d => BluetoothItems.Item(d, _config.Shows, locale))]
            : [BluetoothItems.Empty(snapshot.Status, locale)];
        _list.PartTitle = ItemMenuLines.DevicePartName;
        _list.Show(items, _config, DeviceItems.Parts, DeviceItems.Shape(_config.HiddenParts), stock: true);
    }

    /// <summary>Hold <paramref name="snapshot"/> in place of the radio's.</summary>
    internal void HoldSample(BluetoothSnapshot snapshot)
    {
        _watch?.Dispose();
        _watch = null;
        _held = snapshot;
        Show();
    }
}
