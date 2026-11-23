using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Items;
using Themia.Widget.Drives;
using Themia.Core.Geometry;

namespace Themia.App.Widgets;

/// <summary>
/// [S-16650] [S-16657] Every drive of its kind (<see cref="DriveListKind"/>) as an item (<see cref="ItemList"/>) over
/// the host's one walk (<see cref="IDrives"/>, walked again on <c>WM_DEVICECHANGE</c>: a stick plugged in or out comes and
/// goes at once), the drives shown and their order its config's (<see cref="DrivesRules.Arrange"/>). A click opens a drive in File Explorer; its menu has Open, Eject (a
/// removable drive) and Refresh; Delete ejects the one under the keyboard cursor. With the block gap on every drive stands
/// as a card of its own (<see cref="IWidgetGrounds"/>). Nothing to show keeps the face: one item, "--" and why (WR-10).
/// </summary>
internal sealed partial class DrivesView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions, IWidgetGrounds
{
    private readonly IWidgetHost _host;
    private readonly ItemList _list;
    private IDisposable? _watch;
    private IWidgetSurface? _surface;
    private DriveListConfig _config = new DrivesConfig();
    private readonly DriveListKind _kind;

    /// <summary>[S-11850] A sample's or a render's drives in place of the walk, or null.</summary>
    private IReadOnlyList<DriveEntry>? _held;

    public DrivesView(IWidgetHost host, DriveListKind kind)
    {
        _host = host;
        _kind = kind;
        _list = new ItemList(host)
        {
            Opened = Open,
            CanOpen = () => _surface is not { EditMode: true } && !_host.IsPreview,
            Menu = item => DriveItemMenu.Of(DriveOf(item), () => Open(item), () => Eject(item), _host.Drives.Refresh),
            Deleted = item => _ = Eject(item),
            Decorate = LinkItem,
            Redrawing = ForgetItems,
            CursorMoved = PointCursor,
        };
        _list.GroundsChanged += () => GroundsChanged?.Invoke();
        Children.Add(_list);
    }

    /// <inheritdoc />
    public event Action? GroundsChanged;

    /// <summary>[S-16653] With the gap on, every drive's box (the list stands the inset over the view's top).</summary>
    public IReadOnlyList<DipRect>? Grounds => _list.ViewGrounds;

    /// <summary>[S-16657] Refresh: walk the drives again now.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), _host.Drives.Refresh)];

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _config = config as DriveListConfig ?? new DrivesConfig();
        _watch?.Dispose();
        _watch = _host.Drives.Watch(ticker, Show);
        _links.Join(_surface?.Links);
        Show();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as DriveListConfig ?? new DrivesConfig();
        Show();
    }

    public void ApplySize(WidgetArea area) => _list.Size(area);

    public bool HandleKey(WidgetKey key) => _list.HandleKey(key);

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _watch?.Dispose();
        _watch = null;
        _links.Leave();
    }

    private void OnLanguage(Locale locale) => Show();

    /// <summary>[S-16650] The drives the config shows, in its order, as device items; none: the face with why.</summary>
    private void Show()
    {
        if (_held is null && !_host.Drives.HasValue)
        {
            return;
        }

        var locale = Strings.Current;
        var drives = DrivesRules.Arrange(_held ?? _host.Drives.Drives, _kind, _config);
        IReadOnlyList<Item> items = drives.Count > 0 ? [.. drives.Select(d => DeviceItems.Drive(locale, d))] : [Nothing(locale)];
        _list.PartTitle = ItemMenuLines.DevicePartName;
        _list.Show(items, _config, DeviceItems.Parts, DeviceItems.Shape(_config.HiddenParts), stock: _held is not null || _host.IsPreview);
    }

    /// <summary>
    /// [S-16657] WR-10: nothing to show keeps the face, one item: the kind's glyph, "--", the empty bar and why ("Nothing
    /// plugged in", "No network drive", "No drive to show").
    /// </summary>
    private Item Nothing(Locale locale)
    {
        var why = _kind.Nothing(locale);
        return new Item(string.Empty, string.Empty, new ItemLead(_kind.Glyph), why, Tooltip: why);
    }

    private DriveEntry? DriveOf(Item item) => item.Lead?.Drive;

    /// <summary>[S-2032] A click opens the drive in File Explorer.</summary>
    private void Open(Item item)
    {
        if (DriveOf(item) is { State: not DriveState.Missing } drive)
        {
            _host.Launcher.Open(drive.Root);
        }
    }

    /// <summary>[S-4661] The shell's own eject on a removable drive; a render never ejects.</summary>
    private bool Eject(Item item)
    {
        if (_host.IsPreview || DriveOf(item) is not { } drive || !DriveRules.IsRemovable(drive))
        {
            return false;
        }

        _host.Launcher.Eject(drive.Root);
        return true;
    }

    /// <summary>[S-11850] Hold <paramref name="drives"/> in place of the walk.</summary>
    internal void Hold(IReadOnlyList<DriveEntry> drives)
    {
        _held = drives;
        Show();
    }
}
