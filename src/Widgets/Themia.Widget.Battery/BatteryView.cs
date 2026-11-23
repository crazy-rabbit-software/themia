using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Items;

namespace Themia.Widget.Battery;

/// <summary>
/// [S-17182] This PC's battery on the device family's face (<see cref="ItemFace"/>, as the Bluetooth device's; owner,
/// 2026-11-23: "battery widget should get the same base container as teh bluetooth device one and should also have the
/// percentage progress as well"): the battery glyph, its name, its state and time, its charge at 28 and its bar standing at
/// the item's end ([S-17180]), mapped by <see cref="BatteryItems"/> from the hub's battery (and the pack's rate, for the time
/// to full while there is a battery). A click opens Windows' Power &amp; battery page ([S-2022]).
/// </summary>
internal sealed partial class BatteryView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetSizeAware
{
    /// <inheritdoc />
    public CardOpen? Opens => BatteryModule.Opens;

    private readonly IWidgetHost _host;
    private readonly ItemFace _face;
    private readonly OpensLight _opensLight;
    private readonly WatchSlot<BatteryPower?> _power = new();
    private readonly WatchSlot<BatteryPack?> _pack = new();
    private IWidgetSurface? _surface;
    private BatteryConfig _config = new();
    private bool _attached;

    /// <summary>[S-11850] A render's battery in place of the hub's.</summary>
    private (BatteryPower? Power, BatteryPack? Pack)? _held;

    public BatteryView(IWidgetHost host)
    {
        _host = host;
        _face = new ItemFace(host);
        Children.Add(_face);
        _opensLight = new OpensLight(this, () => _surface?.EditMode != true);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as BatteryConfig ?? new BatteryConfig();
        _attached = true;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _opensLight.Attach();
        if (_held is null && _host.System is { } hub)
        {
            _power.Hold(hub, Sources.Battery, _ => Show());
        }

        Show();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as BatteryConfig ?? new BatteryConfig();
        Show();
    }

    /// <summary>The box only: what the card shows does not change with it ([S-2438]).</summary>
    public void ApplySize(WidgetArea area) => _face.Size(area);

    public void Detach()
    {
        _attached = false;
        Strings.Changed -= OnLanguage;
        _pack.Release();
        _power.Release();
        _opensLight.Detach();
    }

    private void OnLanguage(Locale locale) => Show();

    /// <summary>[S-17182] The battery as its item; the pack is held only while there is a battery to ask ([S-2506]).</summary>
    private void Show()
    {
        if (!_attached)
        {
            return;
        }

        bool read;
        BatteryPower? power;
        BatteryPack? pack;
        if (_held is { } held)
        {
            (read, power, pack) = (true, held.Power, held.Pack);
        }
        else
        {
            read = _power.HasValue || _host.System is null;
            power = _power.HasValue ? _power.Value : null;
            if (power is { HasBattery: true } && _host.System is { } hub)
            {
                _pack.Hold(hub, Sources.BatteryPack, _ => Show());
            }
            else
            {
                _pack.Release();
            }

            pack = _pack.HasValue ? _pack.Value : null;
        }

        var item = BatteryItems.Item(Strings.Current, read, power, pack);
        ToolTipService.SetToolTip(this, item.Tooltip);
        _face.Show(item, new ItemDraw(_config.HiddenParts, DeviceItems.Shape(_config.HiddenParts)));
    }
}
