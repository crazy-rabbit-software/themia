using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Devices;
using Themia.Widget.Bluetooth;

namespace Themia.App.Widgets;

/// <summary>
/// [S-16662] One paired Bluetooth device on the device family's face (<see cref="DeviceFace"/>, as the Drive widget's):
/// its kind's glyph and its battery at 28 over the bar, its kind, connection and signal as the line; the device the config
/// names, else the first connected. Read-only, as the list ([S-1254]). No device keeps the face: "--" and which empty pass
/// it is, or "Not connected" for one paired no more (WR-10). A click opens Windows' Bluetooth &amp; devices page.
/// </summary>
internal sealed partial class BluetoothDeviceView : Grid, IWidgetView, IWidgetOpens, IWidgetContextActions, IWidgetSurfaceAware, IWidgetSizeAware
{
    /// <inheritdoc />
    public CardOpen? Opens => BluetoothModule.Opens;

    private readonly IWidgetHost _host;
    private readonly DeviceFace _face;
    private readonly OpensLight _opensLight;
    private readonly WidgetLinks _links = new();
    private LinkCard<DeviceLink>? _card;
    private IWidgetSurface? _surface;
    private BluetoothDeviceConfig _config = new();
    private IDisposable? _watch;
    private Ticker? _ticker;
    private bool _signal;

    /// <summary>[S-11850] A sample's devices in place of the radio's.</summary>
    private BluetoothSnapshot? _held;

    public BluetoothDeviceView(IWidgetHost host)
    {
        _host = host;
        _face = new DeviceFace(host);
        Children.Add(_face);
        _opensLight = new OpensLight(this, () => _surface?.EditMode != true);
    }

    private LinkCard<DeviceLink> Card => _card ??= new LinkCard<DeviceLink>(_links, LinkTopics.HoveredDevice, this);

    /// <summary>[S-1259] Refresh: read the radios again now.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), Refresh)];

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as BluetoothDeviceConfig ?? new BluetoothDeviceConfig();
        _ticker = ticker;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _links.Join(_surface?.Links);
        Card.Publishes(this);
        _opensLight.Attach();
        Watch();
        Show();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as BluetoothDeviceConfig ?? new BluetoothDeviceConfig();
        Watch();
        Show();
    }

    /// <summary>The box only: what the card shows does not change with it ([S-2438]).</summary>
    public void ApplySize(WidgetArea area) => _face.Size(area);

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _watch?.Dispose();
        _watch = null;
        Card.Release();
        _links.Leave();
        _opensLight.Detach();
    }

    private void OnLanguage(Locale locale) => Show();

    /// <summary>[S-4474] Watch the radios (again where the signal wanted changed); a sample watches nothing.</summary>
    private void Watch()
    {
        if (_held is not null || _ticker is null || (_watch is not null && _config.ShowSignal == _signal))
        {
            return;
        }

        _watch?.Dispose();
        _signal = _config.ShowSignal;
        _watch = _host.Bluetooth.Watch(_ticker, _signal, Show);
    }

    private void Refresh()
    {
        if (_held is null)
        {
            _host.Bluetooth.Refresh();
        }
    }

    /// <summary>[S-16662] The device the config names (else the first connected) on the face; none: the face with why.</summary>
    private void Show()
    {
        var snapshot = _held ?? (_watch is null ? BluetoothSnapshot.Of(BluetoothStatus.Loading) : _host.Bluetooth.Last);
        var locale = Strings.Current;
        DeviceItem item;
        if (snapshot.Status != BluetoothStatus.Ok)
        {
            item = BluetoothItems.Empty(snapshot.Status, locale);
        }
        else if (BluetoothItems.Pick(BluetoothRules.Arrange(snapshot.Devices, connectedOnly: false, locale), _config.Device) is { } device)
        {
            item = BluetoothItems.Item(device, _config.Shows, locale);
        }
        else
        {
            var why = _config.Device.Length > 0 ? Strings.Get(locale, Str.Details_Disk_NotConnected) : BluetoothRules.StatusMessage(BluetoothStatus.Ok, locale);
            item = BluetoothItems.Empty(BluetoothStatus.Ok, locale) with { Line = why, Tooltip = why };
        }

        ToolTipService.SetToolTip(this, item.Tooltip);
        Card.Key = item.Id.Length > 0 ? LinkKeys.DeviceOfEndpoint(item.Id) : null;
        _face.Show(item, _config.HiddenParts, stock: true);
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
