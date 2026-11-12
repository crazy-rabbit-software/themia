using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
/// [S-4660]..[S-4664] What is plugged in (Karpo §91), on the family's Row block: a row per USB stick,
/// card, disc or external disk with its picture, its name as This PC names it, the used share, and by
/// the tier what it holds and its kind (<see cref="RemovableDrivesPlan"/>). Eject is the row's menu and
/// the keys (Delete, E), never a button in the row: the shell's own eject, which says how it went in
/// Windows' own notice. Live: a device plugged in or out shows at once (the host's walk,
/// <see cref="IDrives"/>, runs again on <c>WM_DEVICECHANGE</c>). Nothing plugged in is the Status row.
/// </summary>
internal sealed partial class RemovableDrivesView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private readonly ScrollViewer _scroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        HorizontalScrollMode = ScrollMode.Disabled,
        VerticalAlignment = VerticalAlignment.Top,
    };

    private readonly StackPanel _list = new();
    private readonly List<DeviceRow> _rows = [];
    private readonly StatusBlock _empty = new();
    private ScrollFade? _fade;
    private IWidgetSurface? _surface;
    private IDisposable? _watch;
    private IReadOnlyList<DriveEntry> _devices = [];
    /// <summary>[S-11850] The sample's devices in place of this PC's (<see cref="ShowSampleAsync"/>), or null.</summary>
    private IReadOnlyList<DriveEntry>? _previewDevices;
    private WidgetArea _area;
    private double _lead = -1;
    private int _focus = -1;
    private bool _detached;

    /// <summary>[S-6913] What the view asks of the host: the launcher, the drives.</summary>
    private readonly IWidgetHost _widgetHost;

    public RemovableDrivesView(IWidgetHost host)
    {
        _widgetHost = host;
        _scroll.Content = _list;
        Children.Add(_scroll);
        _fade = new ScrollFade(_scroll, _list);

        _empty.Hide();
        Children.Add(_empty);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _rowCount = (config as RemovableDrivesConfig)?.Rows ?? RemovableDrivesPlan.DefaultRows;
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _watch?.Dispose();
        _watch = _widgetHost.Drives.Watch(ticker, Show);
        _links.Join(_surface?.Links);
        Show();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _rowCount = (config as RemovableDrivesConfig)?.Rows ?? RemovableDrivesPlan.DefaultRows;
        Show();
    }

    /// <summary>[S-11027] The config's rows: the list's height.</summary>
    private int _rowCount = RemovableDrivesPlan.DefaultRows;

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private void OnLanguage(Locale locale) => Lay();

    private void Show()
    {
        if (_previewDevices is null && !_widgetHost.Drives.HasValue)
        {
            return;
        }

        _devices = _previewDevices ?? DriveRules.Removables(_widgetHost.Drives.Drives);
        Lay();
    }

    /// <summary>[S-4662] The rows the tier gives, whole rows only; nothing plugged in, the Status row.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var look = RemovableDrivesPlan.Of(_area.IsMeasured ? _area.Width : ActualWidth);

        if (_devices.Count == 0)
        {
            _empty.Show(StatusKind.Empty, Glyphs.Usb, RemovableDrivesStr.Drives_Nothing.Text());
            _scroll.Visibility = Visibility.Collapsed;
        }
        else
        {
            _empty.Hide();
            _scroll.Visibility = Visibility.Visible;
        }

        // A row's lead is fixed when it is built: a tile crossing 180 builds its rows again.
        if (look.Lead != _lead)
        {
            _lead = look.Lead;
            foreach (var row in _rows)
            {
                row.Dispose();
            }

            _rows.Clear();
            _list.Children.Clear();
            Drives.Clear();
        }

        while (_rows.Count < _devices.Count)
        {
            var row = new DeviceRow(this, look.Lead);
            _rows.Add(row);
            _list.Children.Add(row.Block);
        }

        var locale = Strings.Current;
        var picture = !(_surface?.IsPreview ?? false) && _previewDevices is null;
        for (var i = 0; i < _rows.Count; i++)
        {
            var shown = i < _devices.Count;
            _rows[i].Block.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            if (shown)
            {
                _rows[i].Show(_devices[i], look, picture, locale);
            }
        }

        _scroll.Height = RemovableDrivesPlan.ListHeight(_rowCount);
        LinkRows();

        if (_focus >= _devices.Count)
        {
            Focus(ListKeys.AfterRemoval(_focus, _devices.Count));
        }
        else
        {
            Focus(_focus);
        }

        _fade?.Refresh();
    }

    private void Open(DriveEntry device)
    {
        if (_surface?.IsPreview ?? false)
        {
            return;
        }

        _widgetHost.Launcher.Open(device.Root);
    }

    private void Eject(DriveEntry device)
    {
        if (_surface?.IsPreview ?? false)
        {
            return;
        }

        _widgetHost.Launcher.Eject(device.Root);
    }

    // ---- keys ([S-4664]) ----------------------------------------------------------------------

    public bool WantsKeys => !_detached && _devices.Count > 0;

    /// <summary>Up/Down walk the devices, Enter opens one, Delete ejects it, Escape lets go.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        var decision = ListKeys.Decide(key, _focus, _devices.Count);

        switch (decision.Command)
        {
            case ListCommand.Focus:
                Focus(decision.Index);
                PointCursor();
                return true;
            case ListCommand.Open when decision.Index >= 0 && decision.Index < _devices.Count:
                Open(_devices[decision.Index]);
                return true;
            case ListCommand.Delete when decision.Index >= 0 && decision.Index < _devices.Count:
                Eject(_devices[decision.Index]);
                return true;
            case ListCommand.Release:
                var had = _focus >= 0;
                Focus(-1);
                PointCursor();
                return had;
            default:
                return false;
        }
    }

    /// <summary>E ejects the focused device.</summary>
    public bool HandleText(char character)
    {
        if (_detached || char.ToUpperInvariant(character) != 'E' || _focus < 0 || _focus >= _devices.Count)
        {
            return false;
        }

        Eject(_devices[_focus]);
        return true;
    }

    /// <summary>[S-1288] The keyboard's ring on the focused row (the Row block's own, moving nothing).</summary>
    private void Focus(int index)
    {
        _focus = index;
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Block.Focused = i == index;
        }
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;
        _watch?.Dispose();
        _watch = null;
        _fade?.Dispose();
        _fade = null;

        foreach (var row in _rows)
        {
            row.Dispose();
        }

        _rows.Clear();
        _list.Children.Clear();
        Drives.Clear();
        _links.Leave();
        _lead = -1;
    }

    /// <summary>One device: its picture, name, used share, and by the tier its space line and kind; Open and Eject on its menu.</summary>
    private sealed class DeviceRow : IDisposable
    {
        private readonly RemovableDrivesView _owner;
        private readonly DriveIcon? _icon;
        private readonly ChartBar _bar = DriveBar.Create();
        private readonly IDisposable _menu;
        private DriveEntry? _device;

        public DeviceRow(RemovableDrivesView owner, double lead)
        {
            _owner = owner;
            Block = new RowBlock(RowKind.List, lead) { Interactive = true };
            if (lead > 0)
            {
                _icon = new DriveIcon();
                Block.Lead(_icon);
            }

            Block.PointerPressed += OnPressed;
            Block.DoubleTapped += OnDoubleTapped;
            _menu = ItemMenu.Attach(Block, Entries);
            WidgetItems.Mark(Block);
        }

        public RowBlock Block { get; }

        private IEnumerable<ItemMenuEntry> Entries()
        {
            if (_device is not { } device || _owner._detached)
            {
                return [];
            }

            return
            [
                new(Glyphs.Open, Str.Common_Open.Text(), () => _owner.Open(device)),
                new(Glyphs.Usb, Str.Drives_Eject.Text(), () => _owner.Eject(device)),
            ];
        }

        private void OnPressed(object sender, PointerRoutedEventArgs args)
        {
            var index = _owner._rows.IndexOf(this);
            if (index >= 0)
            {
                _owner.Focus(index);
            }
        }

        private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
        {
            if (_device is { } device)
            {
                _owner.Open(device);
            }
        }

        public void Show(DriveEntry device, RemovableDrivesLook look, bool picture, Locale locale)
        {
            _device = device;
            var name = DriveRules.Title(locale, device);
            var share = device.UsedFraction is { } used ? Formatting.Percent(locale, used) : null;
            Block.Lines = look.Lines;
            Block.Show(name, share, DriveRules.SpaceLine(locale, device), DriveRules.Description(locale, device));
            _icon?.Show(device, FamilyShape.IconLarge, picture);

            DriveBar.Show(_bar, device);
            Block.Meter(look.Meter > 0 && device.UsedFraction is not null ? _bar : null, look.Meter);
            ToolTipService.SetToolTip(Block, $"{name} · {DriveRules.SpaceLine(locale, device)}");
        }

        public void Dispose()
        {
            Block.PointerPressed -= OnPressed;
            Block.DoubleTapped -= OnDoubleTapped;
            _menu.Dispose();
            Block.Detach();
        }
    }
}
