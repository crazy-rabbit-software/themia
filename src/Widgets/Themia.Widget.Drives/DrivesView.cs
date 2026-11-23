using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.RemovableDrives;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4660]..[S-4664] What is plugged in (Karpo §91) as a kit list ([S-7074]) over the host's drives
/// (<see cref="IDrives"/>, walked again on <c>WM_DEVICECHANGE</c>: a device plugged in or out shows at once), shaped by
/// <see cref="RemovableDrivesSpec"/>: each device's picture as the rich look's lead, its used bar in the meter's place.
/// Nothing plugged in is the kit's state row.
/// </summary>
internal sealed partial class RemovableDrivesView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    /// <summary>Nothing on a row ages: the list is redrawn when the drives change; this is only the binding's slowest tick.</summary>
    private static readonly TimeSpan Redraw = TimeSpan.FromMinutes(15);

    private readonly IWidgetHost _host;
    private readonly KitBinding _binding;
    private readonly DriveSource _source;
    private RemovableDrivesConfig _config = new();
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private bool _attached;

    public RemovableDrivesView(IWidgetHost host)
    {
        _host = host;
        _source = new DriveSource(host.Drives);
        _binding = new KitBinding(host, RemovableDrivesSpec.Spec, Glyphs.Usb, RemovableDrivesSpec.Actions, Run)
        {
            Look = new KitRichLook { Lead = Picture, LeadSize = FamilyShape.IconLarge, Trail = Bar },
            Decorate = LinkRow,
            Redrawing = ForgetRows,
            CursorMoved = PointCursor,
        };

        Children.Add(_binding.List);
        Children.Add(_binding.State);
        _binding.Status.Place(this);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as RemovableDrivesConfig ?? new RemovableDrivesConfig();
        _attached = true;
        _source.Start(ticker);
        _links.Join(_surface?.Links);
        _binding.Attach(ticker, _source, _config, Redraw);
    }

    /// <summary>[S-11027] [S-14500] The config's rows decide the natural height alone: the rows shown are the card's.</summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as RemovableDrivesConfig ?? new RemovableDrivesConfig();
        if (_attached)
        {
            _binding.Apply(_source, _config, Redraw, show: false);
        }
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        _binding.Size(area);
    }

    /// <summary>[S-4664] The kit's keys; Delete ejects the focused device as its menu's Eject does.</summary>
    public bool HandleKey(WidgetKey key)
        => key == WidgetKey.Delete && _binding.List.CursorIndex >= 0 ? Eject(_binding.List.CursorIndex) : _binding.HandleKey(key);

    /// <summary>[S-4664] E ejects the focused device.</summary>
    public bool HandleText(char character)
        => char.ToUpperInvariant(character) == 'E' && Eject(_binding.List.CursorIndex);

    public void Detach()
    {
        _attached = false;
        _binding.Dispose();
        _source.Stop();
        _links.Leave();
    }

    private bool Eject(int index)
    {
        if (index < 0 || index >= _binding.List.Items.Count)
        {
            return false;
        }

        var item = _binding.List.Items[index];
        Run(item, RemovableDrivesSpec.Actions(item)[1]);
        return true;
    }

    /// <summary>[S-4663] Open in Explorer, or the shell's own eject; a preview does neither.</summary>
    private void Run(KitItem item, KitAction action)
    {
        if (_surface?.IsPreview == true || _source.DeviceOf(item.Id) is not { } device)
        {
            return;
        }

        switch (action.Id)
        {
            case RemovableDrivesSpec.OpenId:
                _host.Launcher.Open(device.Root);
                break;

            case RemovableDrivesSpec.EjectId:
                _host.Launcher.Eject(device.Root);
                break;
        }
    }

    /// <summary>[S-4661] The device's picture, 32: the shell's own for this PC's devices, the kind's glyph in a render.</summary>
    private FrameworkElement? Picture(KitItem item)
    {
        if (_source.DeviceOf(item.Id) is not { } device)
        {
            return null;
        }

        var icon = new DriveIcon();
        icon.Show(device, FamilyShape.IconLarge, picture: _surface?.IsPreview != true && !_source.Holding);
        return icon;
    }

    /// <summary>[S-4662] The used share's bar, a third of the content at most 140, where the device says how full it is.</summary>
    private KitTrail? Bar(KitItem item)
    {
        var width = RemovableDrivesPlan.Of(_area.IsMeasured ? _area.Width : ActualWidth).Meter;
        if (width <= 0 || _source.DeviceOf(item.Id) is not { UsedFraction: not null } device)
        {
            return null;
        }

        var bar = DriveBar.Create();
        DriveBar.Show(bar, device);
        return new KitTrail(bar, width);
    }

    /// <summary>[S-4660] The host's drives as a kit source: the removable ones, in its order.</summary>
    private sealed class DriveSource(IDrives drives) : IKitSource
    {
        private IDisposable? _watch;

        /// <summary>[S-11850] The sample's devices in place of this PC's, or null.</summary>
        private IReadOnlyList<DriveEntry>? _held;

        public event Action? Changed;

        public bool Busy => false;

        public bool Holding => _held is not null;

        private IReadOnlyList<DriveEntry> Devices => _held ?? (drives.HasValue ? DriveRules.Removables(drives.Drives) : []);

        public void Start(Ticker ticker) => _watch ??= drives.Watch(ticker, () => Changed?.Invoke());

        public void Stop()
        {
            _watch?.Dispose();
            _watch = null;
        }

        public void Show(bool refresh)
        {
            // ignore: the host walks the drives again on every device change; there is nothing to ask.
        }

        public KitRead Read()
        {
            var locale = Strings.Current;
            var status = _held is not null || drives.HasValue ? FetchStatus.Ok : FetchStatus.Loading;
            return new KitRead([.. Devices.Select(device => RemovableDrivesSpec.Item(device, locale))], [new KitOutcome(null, status)]);
        }

        public DriveEntry? DeviceOf(string id) => Devices.FirstOrDefault(d => d.Root == id);

        public void HoldForPreview(IReadOnlyList<DriveEntry> devices)
        {
            _held = devices;
            Changed?.Invoke();
        }
    }
}
