using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Drive;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4670]..[S-4672] One drive's share on the family's blocks: the header names it as This PC does;
/// the used share is a Readout at 28 with "419 GB free of 931 GB" under it and the Chart block's bar 8
/// under that (<see cref="DriveFace"/>). A drive without figures (a mapped share Windows cannot reach,
/// one still answering, an empty reader, a drive that is gone) is one Status row at the top. The drives
/// come from the host's one walk (<see cref="IDrives"/>), never read here. Its facts are the Drive facts
/// atom's.
/// </summary>
internal sealed partial class DriveView : Grid, IWidgetView, IWidgetOpens, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => DriveModule.Opens(_config.Drive, Path.GetPathRoot(Environment.SystemDirectory));

    private readonly StackPanel _figure = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly ReadoutBlock _readout = new();
    private readonly ChartBar _bar = DriveBar.Create();
    private readonly StatusBlock _status = new();

    private IWidgetSurface? _surface;
    private IDisposable? _watch;
    private DriveConfig _config = new();
    private DriveEntry? _drive;
    private WidgetArea _area;

    /// <summary>[S-6913] What the view asks of the host: the launcher, the drives.</summary>
    private readonly IWidgetHost _widgetHost;

    public DriveView(IWidgetHost host)
    {
        _widgetHost = host;
        _bar.Margin = new Thickness(0, FamilySpace.S2, 0, 0);
        _readout.Use(FamilyType.Big28);
        _figure.Children.Add(_readout);
        _figure.Children.Add(_bar);
        _figure.Margin = Family.SideInset;

        _status.Hide();
        Children.Add(_figure);
        Children.Add(_status);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _config = config as DriveConfig ?? new DriveConfig();
        UseStyle();

        _watch?.Dispose();
        _watch = _widgetHost.Drives.Watch(ticker, Show);
        JoinLinks();

        Show();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as DriveConfig ?? new DriveConfig();
        UseStyle();
        Show();
    }

    /// <summary>The box only: what the card shows does not change with it ([S-2438]).</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private void OnLanguage(Locale locale) => Show();

    /// <summary>The drive the config names, from the last walk.</summary>
    private void Show()
    {
        var service = _widgetHost.Drives;
        if (_previewDrive is null && !service.HasValue)
        {
            return;
        }

        _drive = _previewDrive ?? DriveRules.Pick(service.Drives, _config.Drive, SystemDrive.Root);
        LinkDrive();
        _surface?.SetHeaderTitle(DriveRules.Title(Strings.Current, _drive));
        Lay();
    }

    /// <summary>[S-4671] [S-4672] The share, its line and the bar (or the ring), or the Status row.</summary>
    private void Lay()
    {
        if (_drive is not { } drive)
        {
            return;
        }

        var locale = Strings.Current;
        ToolTipService.SetToolTip(this, $"{DriveRules.Title(locale, drive)} · {DriveRules.Description(locale, drive)} · {DriveRules.SpaceLine(locale, drive)}");

        if (DriveFace.Status(locale, drive) is { } status)
        {
            _status.Show(status.Kind, DriveRules.Glyph(drive), status.Sentence);
            _figure.Visibility = Visibility.Collapsed;
            if (_ringParts is not null)
            {
                _ringParts.Root.Visibility = Visibility.Collapsed;
            }

            return;
        }

        _status.Hide();
        if (_ringParts is not null)
        {
            LayRing(drive);
            return;
        }

        _figure.Visibility = Visibility.Visible;
        var used = ReadoutRules.Split(Formatting.Percent(locale, drive.UsedFraction!.Value));
        _readout.Show(used.Value, used.Unit, DriveRules.SpaceLine(locale, drive));
        DriveBar.Show(_bar, drive);
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
    }

    /// <summary>[S-11850] A sample's or a render's own drive, in place of the walk.</summary>
    private DriveEntry? _previewDrive;
}
