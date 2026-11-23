using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Drive;
using Themia.Sdk.WinUI.Devices;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4670]..[S-4672] One drive's share on the family's blocks: a Header widget over it names it ([S-12481]); the drive is on the reading's hover;
/// the used share is a Readout at 28 with "419 GB free of 931 GB" under it and the Chart block's bar 8
/// under that (<see cref="DriveFace"/>). A drive without figures (a mapped share Windows cannot reach,
/// one still answering, an empty reader, a drive that is gone) is one Status row at the top. The drives
/// come from the host's one walk (<see cref="IDrives"/>), never read here. Its facts are the Drive facts
/// atom's.
/// </summary>
internal sealed partial class DriveView : Grid, IWidgetView, IWidgetOpens, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => _drive is { State: not DriveState.Missing } drive ? OneDriveModule.Opens(drive.Root, null)
        : _kind.Group is null ? OneDriveModule.Opens(_config.Drive, Path.GetPathRoot(Environment.SystemDirectory)) : null;

    /// <summary>[S-16637] The bar style's face: the device family's one face of one device.</summary>
    private readonly DeviceFace _face;
    private readonly StatusBlock _status = new();

    private IWidgetSurface? _surface;
    private IDisposable? _watch;
    private OneDriveConfig _config;
    private DriveEntry? _drive;
    private readonly OneDriveKind _kind;

    /// <summary>[S-6913] What the view asks of the host: the launcher, the drives.</summary>
    private readonly IWidgetHost _widgetHost;

    public DriveView(IWidgetHost host, OneDriveKind kind)
    {
        _widgetHost = host;
        _kind = kind;
        _config = new DriveConfig();
        _face = new DeviceFace(host);
        _status.Hide();
        Children.Add(_face);
        Children.Add(_status);

        // [S-15745] A click opens the drive's root: the card lights as that one item under the pointer and pressed.
        _opensLight = new OpensLight(this, () => _surface?.EditMode != true && Opens is not null);
    }

    /// <summary>[S-15745] The whole card's hover and pressed light.</summary>
    private readonly OpensLight _opensLight;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _config = config as OneDriveConfig ?? new DriveConfig();
        UseStyle();

        _watch?.Dispose();
        _watch = _widgetHost.Drives.Watch(ticker, Show);
        JoinLinks();
        _opensLight.Attach();

        Show();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as OneDriveConfig ?? new DriveConfig();
        UseStyle();
        Show();
    }

    /// <summary>The box only: what the card shows does not change with it ([S-2438]).</summary>
    public void ApplySize(WidgetArea area)
    {
        _face.Size(area);
        Lay();
    }

    private void OnLanguage(Locale locale) => Show();

    /// <summary>[S-4670] [S-16646] The drive the config names, from the last walk; for a kind, the first of its kind there.</summary>
    private void Show()
    {
        var service = _widgetHost.Drives;
        if (_previewDrive is null && !service.HasValue)
        {
            return;
        }

        _drive = _previewDrive ?? DriveKindRules.Pick(_kind.Group, service.Drives, _config.Drive, SystemDrive.Root);
        LinkDrive();
        Lay();
    }

    /// <summary>
    /// [S-4671] [S-4672] [S-16645] The drive as the device family's item ([S-16630], <see cref="DeviceItems.Drive"/>) on its
    /// one face (<see cref="DeviceFace"/>) or the ring; a drive without figures keeps the face, "--", the empty bar and why;
    /// none of the kind there is the face with its kind's why ("Nothing plugged in").
    /// </summary>
    private void Lay()
    {
        var locale = Strings.Current;
        var item = _drive is { } drive ? DeviceItems.Drive(locale, drive) : DriveKindRules.Nothing(_kind, locale);
        ToolTipService.SetToolTip(this, item.Tooltip);

        _status.Hide();
        if (!item.Absent && _ringParts is not null)
        {
            LayRing(item);
            return;
        }

        // [S-12813] [S-16670] Not connected, no media: its icon and name, the reason under it, no percentage and no bar.
        if (_ringParts is not null)
        {
            _ringParts.Root.Visibility = Visibility.Collapsed;
        }

        _face.Visibility = Visibility.Visible;
        _face.Show(item, _config.HiddenParts, stock: _previewDrive is not null);
    }

    public IReadOnlyList<WidgetAction> ContextActions
    {
        get
        {
            if (_drive is not { } drive || drive.State == DriveState.Missing)
            {
                return [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), _widgetHost.Drives.Refresh)];
            }

            var actions = new List<WidgetAction>
            {
                new(Glyphs.Folder, Str.WidgetsB_Disk_OpenInExplorer.Format(("drive", drive.Letter)), () => _widgetHost.Launcher.Open(drive.Root)),
            };

            if (DriveRules.IsRemovable(drive))
            {
                actions.Add(new WidgetAction(Glyphs.Usb, Str.Drives_Eject.Text(), () => _widgetHost.Launcher.Eject(drive.Root)));
            }

            actions.Add(new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), _widgetHost.Drives.Refresh));
            return actions;
        }
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _watch?.Dispose();
        _watch = null;
        LeaveLinks();
        _opensLight.Detach();
    }

    /// <summary>[S-11850] A sample's or a render's own drive, in place of the walk.</summary>
    private DriveEntry? _previewDrive;
}
