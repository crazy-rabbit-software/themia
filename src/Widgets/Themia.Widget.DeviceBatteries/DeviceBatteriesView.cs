using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.DeviceBatteries;

/// <summary>
/// [S-10000] The peripherals' batteries as a kit list ([S-7060]) over the host's one battery source
/// (<see cref="IWidgetHost.DeviceBatteries"/>, [S-4470]), shaped by <see cref="DeviceBatteriesSpec"/>.
/// </summary>
internal sealed partial class DeviceBatteriesView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions, IWidgetKeys
{
    private const string Scope = "device-batteries";

    /// <summary>[S-10005] Windows' Bluetooth &amp; devices page, where a device's battery is Windows' own.</summary>
    private static readonly Uri SettingsPage = new("ms-settings:bluetooth");

    /// <summary>Nothing on a row ages: the list is redrawn when the source says; this is only the binding's slowest tick.</summary>
    private static readonly TimeSpan Redraw = TimeSpan.FromMinutes(15);

    private readonly IWidgetHost _host;
    private readonly KitBinding _binding;
    private readonly BatterySource _source;

    private DeviceBatteriesConfig _config = new();
    private bool _attached;

    public DeviceBatteriesView(IWidgetHost host)
    {
        _host = host;
        _source = new BatterySource(host.DeviceBatteries, () => _config);
        _binding = new KitBinding(host, DeviceBatteriesSpec.Spec, Glyphs.Devices, DeviceBatteriesSpec.Actions, Run);

        Children.Add(_binding.List);
        Children.Add(_binding.State);
    }

    /// <summary>[S-7064] The kit's header: the ring while a read is out.</summary>
    public FrameworkElement? HeaderTrailing => _binding.Header;

    /// <summary>[S-10005] Refresh: read Windows again now.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => _binding.CardActions;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as DeviceBatteriesConfig ?? new DeviceBatteriesConfig();
        _attached = true;
        _source.Start(ticker);
        _binding.Attach(ticker, _source, _config, Redraw);
    }

    public void Bind(IWidgetSurface surface) => _binding.Surface(surface);

    public void ApplySize(WidgetArea area) => _binding.Size(area);

    /// <summary>The threshold and the charging switch only redraw what is held: Windows is not asked again.</summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as DeviceBatteriesConfig ?? new DeviceBatteriesConfig();
        if (_attached)
        {
            _binding.Apply(_source, _config, Redraw, show: false);
        }
    }

    public bool HandleKey(WidgetKey key) => _binding.HandleKey(key);

    public void Detach()
    {
        _attached = false;
        _binding.Dispose();
        _source.Stop();
    }

    /// <summary>[S-10005] Open (double-click, Enter): Windows' Bluetooth &amp; devices page; a preview opens nothing.</summary>
    private void Run(KitItem item, KitAction action)
    {
        if (action is { Kind: KitActionKind.Open, Id: DeviceBatteriesSpec.OpenSettings } && !_host.IsPreview)
        {
            var outcome = _host.Launcher.OpenWindowsSettings(SettingsPage);
            Log.Debug(Scope, $"open Bluetooth & devices: {outcome}");
        }
    }

    /// <summary>
    /// [S-10001] The host's battery read as a kit source: its last snapshot folded and ordered by the
    /// widget's rules, its read in the header's ring. The host is the one place Windows is asked.
    /// </summary>
    private sealed class BatterySource(IDeviceBatteries batteries, Func<DeviceBatteriesConfig> config) : IKitSource
    {
        private IDisposable? _watch;

        /// <summary>[S-11850] A sample's or a render's snapshot held in place of the host's.</summary>
        private DeviceBatterySnapshot? _held;

        private bool Holding => _held is not null;

        private DeviceBatterySnapshot Snapshot => _held ?? batteries.Last;

        public event Action? Changed;

        public bool Busy => !Holding && batteries.Busy;

        public void Start(Ticker ticker) => _watch ??= batteries.Watch(ticker, () => Changed?.Invoke());

        public void Stop()
        {
            _watch?.Dispose();
            _watch = null;
        }

        public void Show(bool refresh)
        {
            if (refresh && !Holding)
            {
                batteries.Refresh();
            }
        }

        public KitRead Read()
        {
            var snapshot = Snapshot;
            var settings = config();
            var status = snapshot.Status switch
            {
                DeviceBatteryStatus.Loading => FetchStatus.Loading,
                DeviceBatteryStatus.Failed => FetchStatus.BadResponse,
                _ => FetchStatus.Ok,
            };

            var rows = DeviceBatteriesRules.Arrange(snapshot.Readings, settings.ShowCharging, DeviceBatteriesConfig.MaxDevices);
            var locale = Strings.Current;
            var detail = status == FetchStatus.BadResponse ? DeviceBatteriesStr.DeviceBatteries_Failed.Text(locale) : null;
            return new KitRead([.. rows.Select(row => DeviceBatteriesSpec.Item(row, settings.EffectiveThreshold, locale))], [new KitOutcome(null, status, detail)]);
        }

        //// <summary>[S-11850] A sample or a render: hold this snapshot as if the host had read it.</summary>
        public void HoldForPreview(DeviceBatterySnapshot snapshot)
        {
            _held = snapshot;
            Changed?.Invoke();
        }
    }
}
