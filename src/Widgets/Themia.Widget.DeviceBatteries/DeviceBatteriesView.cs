using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk.Devices;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Items;
using Themia.Core.Geometry;

namespace Themia.Widget.DeviceBatteries;

/// <summary>
/// [S-10000] [S-16665] The peripherals' batteries as items (<see cref="ItemList"/>) over the host's one battery
/// source (<see cref="IWidgetHost.DeviceBatteries"/>, [S-4470]), folded and ordered by <see cref="DeviceBatteriesRules"/>,
/// each mapped by <see cref="DeviceBatteriesItems"/>. A click opens Windows' Bluetooth &amp; devices page.
/// </summary>
internal sealed partial class DeviceBatteriesView : Grid, IWidgetView, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions, IWidgetKeys, IWidgetGrounds
{
    private const string Scope = "device-batteries";

    private readonly IWidgetHost _host;
    private readonly ItemList _list;
    private DeviceBatteriesConfig _config = new();
    private IDisposable? _watch;
    private bool _attached;

    /// <summary>[S-11850] A sample's or a render's snapshot held in place of the host's.</summary>
    private DeviceBatterySnapshot? _held;

    public DeviceBatteriesView(IWidgetHost host)
    {
        _host = host;
        _list = new ItemList(host)
        {
            Opened = _ => OpenSettings(),
            CanOpen = () => _surface is not { EditMode: true } && !_host.IsPreview,
            Menu = _ => DeviceBatteriesItems.Menu(OpenSettings, Refresh),
            Decorate = LinkItem,
            Redrawing = ForgetItems,
        };
        _list.GroundsChanged += () => GroundsChanged?.Invoke();
        Children.Add(_list);
    }

    /// <inheritdoc />
    public event Action? GroundsChanged;

    /// <summary>[S-16666] With the gap on, every device's box (the list stands the inset over the view's top).</summary>
    public IReadOnlyList<DipRect>? Grounds => _list.ViewGrounds;

    /// <summary>[S-10005] Refresh: read Windows again now.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), Refresh)];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as DeviceBatteriesConfig ?? new DeviceBatteriesConfig();
        _attached = true;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        if (_held is null)
        {
            _watch ??= _host.DeviceBatteries.Watch(ticker, Show);
        }

        _links.Join(_surface?.Links);
        Show();
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void ApplySize(WidgetArea area) => _list.Size(area);

    /// <summary>The threshold and the charging switch only redraw what is held: Windows is not asked again.</summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as DeviceBatteriesConfig ?? new DeviceBatteriesConfig();
        if (_attached)
        {
            Show();
        }
    }

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

    /// <summary>[S-10001] The last read folded and ordered as device items; none: the face with why.</summary>
    private void Show()
    {
        var snapshot = _held ?? _host.DeviceBatteries.Last;
        var locale = Strings.Current;
        var rows = snapshot.Status == DeviceBatteryStatus.Ok
            ? DeviceBatteriesRules.Arrange(snapshot.Readings, _config.ShowCharging, DeviceBatteriesConfig.MaxDevices)
            : [];
        IReadOnlyList<Item> items = rows.Count > 0
            ? [.. rows.Select(row => DeviceBatteriesItems.Item(row, _config.EffectiveThreshold, locale))]
            : [DeviceBatteriesItems.Empty(snapshot.Status, locale)];
        _list.PartTitle = ItemMenuLines.DevicePartName;
        _list.Show(items, _config, DeviceItems.Parts, DeviceItems.Shape(_config.HiddenParts), stock: true);
    }

    /// <summary>[S-10005] Windows' Bluetooth &amp; devices page; a preview opens nothing.</summary>
    private void OpenSettings()
    {
        if (!_host.IsPreview)
        {
            var outcome = _host.Launcher.OpenWindowsSettings(DeviceBatteriesItems.SettingsPage);
            Log.Debug(Scope, $"open Bluetooth & devices: {outcome}");
        }
    }

    private void Refresh()
    {
        if (_held is null)
        {
            _host.DeviceBatteries.Refresh();
        }
    }

    /// <summary>[S-11850] A sample or a render: hold this snapshot as if the host had read it.</summary>
    private void Hold(DeviceBatterySnapshot snapshot)
    {
        _watch?.Dispose();
        _watch = null;
        _held = snapshot;
        Show();
    }
}
