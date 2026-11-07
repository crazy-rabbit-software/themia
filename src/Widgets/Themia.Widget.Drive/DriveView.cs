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
/// [S-4670]..[S-4672] One drive's information (Karpo §99, §122) on the family's blocks: the header
/// names it as This PC does; the used share is a Readout with "419 GB free of 931 GB" under it and the
/// Chart block's bar 8 under that; the facts (Used, Free, Capacity, File system) are fact rows beside
/// the figure on an expanded width or under the bar (<see cref="DriveFace.Bar"/>). A drive without
/// figures (a mapped share Windows cannot reach, one still answering, an empty reader, a drive that is
/// gone) is one Status row at the top. The drives come from the host's one walk
/// (<see cref="IDrives"/>), never read here.
/// </summary>
internal sealed partial class DriveView : Grid, IWidgetView, IWidgetOpens, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => DriveModule.Opens(_config.Drive, Path.GetPathRoot(Environment.SystemDirectory));

    private readonly StackPanel _figure = new();
    private readonly ReadoutBlock _readout = new();
    private readonly ChartBar _bar = DriveBar.Create();
    private readonly StackPanel _facts = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly RowBlock[] _factRows = new RowBlock[DriveFace.FactCount];
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
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _bar.Margin = new Thickness(0, FamilySpace.S2, 0, 0);
        _figure.Children.Add(_readout);
        _figure.Children.Add(_bar);
        _figure.VerticalAlignment = VerticalAlignment.Top;

        for (var i = 0; i < _factRows.Length; i++)
        {
            _factRows[i] = new RowBlock(RowKind.Fact, lead: 0);
            _facts.Children.Add(_factRows[i]);
        }

        // The Status row spans the card whichever column the figure had.
        SetColumnSpan(_status, 2);
        _status.Hide();
        Children.Add(_figure);
        Children.Add(_facts);
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

        Show();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as DriveConfig ?? new DriveConfig();
        UseStyle();
        Show();
    }

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
        _surface?.SetHeaderTitle(DriveRules.Title(Strings.Current, _drive));
        Lay();
    }

    /// <summary>[S-4671] [S-4672] The figure, the bar and the facts that fit, or the Status row.</summary>
    private void Lay()
    {
        if (_drive is not { } drive)
        {
            return;
        }

        var area = _area.IsMeasured ? _area : new WidgetArea(ActualWidth, ActualHeight);
        if (!area.IsMeasured)
        {
            return;
        }

        var locale = Strings.Current;
        ToolTipService.SetToolTip(this, $"{DriveRules.Title(locale, drive)} · {DriveRules.Description(locale, drive)} · {DriveRules.SpaceLine(locale, drive)}");

        if (DriveFace.Status(locale, drive) is { } status)
        {
            _status.Show(status.Kind, DriveRules.Glyph(drive), status.Sentence);
            _figure.Visibility = Visibility.Collapsed;
            _facts.Visibility = Visibility.Collapsed;
            if (_ringParts is not null)
            {
                _ringParts.Root.Visibility = Visibility.Collapsed;
            }

            return;
        }

        _status.Hide();
        if (_ringParts is not null)
        {
            LayRing(drive, area);
            return;
        }

        var plan = DriveFace.Bar(area);
        Columns(plan.Side, plan.Column);
        _figure.Visibility = Visibility.Visible;
        _figure.Margin = plan.Side ? new Thickness(FamilySpace.Inset, 0, 0, 0) : Family.SideInset;
        _readout.Size(area, plan.Width, Math.Max(0, area.Height - FamilySpace.Inset - DriveFace.BarRoom), DriveFace.Worst);
        var used = ReadoutRules.Split(Formatting.Percent(locale, drive.UsedFraction!.Value));
        _readout.Show(used.Value, used.Unit, plan.Under ? DriveRules.SpaceLine(locale, drive) : null);
        DriveBar.Show(_bar, drive);
        Facts(drive, locale, plan.Side, plan.RowsTop > 0, plan.Facts);
    }

    /// <summary>The figure's column and the facts' place: beside it (an expanded width) or under it.</summary>
    private void Columns(bool side, double column)
    {
        ColumnDefinitions[0].Width = side ? new GridLength(column) : new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    /// <summary>[S-4672] The first <paramref name="count"/> facts as fact rows, beside the figure or 8 under it.</summary>
    private void Facts(DriveEntry drive, Locale locale, bool side, bool under, int count)
    {
        SetColumn(_facts, side ? 1 : 0);
        SetRow(_facts, side ? 0 : 1);
        SetRowSpan(_facts, side ? 2 : 1);
        _facts.Margin = new Thickness(0, under ? FamilySpace.S2 : 0, 0, 0);
        _facts.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Fact(0, DriveStr.Drives_Fact_Used, Formatting.Bytes(locale, drive.Used), count);
        Fact(1, DriveStr.Drives_Fact_Free, Formatting.Bytes(locale, drive.Free), count);
        Fact(2, DriveStr.Drives_Fact_Capacity, Formatting.Bytes(locale, drive.Total), count);
        Fact(3, DriveStr.Drives_Fact_FileSystem, drive.FileSystem.Length > 0 ? drive.FileSystem : "--", count);
    }

    private void Fact(int index, DriveStr label, string value, int count)
    {
        var row = _factRows[index];
        row.Visibility = index < count ? Visibility.Visible : Visibility.Collapsed;
        row.Show(label.Text(), value);
        row.Available = value != "--";
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
    }

    /// <summary>Debug renders: a drive of the render's own, in place of the walk.</summary>
#if DEBUG
    private DriveEntry? _previewDrive;
#else
    // Release has no renders: the field's Debug-only writer (the .Preview.cs part) is not compiled.
    private DriveEntry? _previewDrive => null;
#endif
}
