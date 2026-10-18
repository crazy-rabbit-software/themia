using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Themia.Widget.Drive;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4670]..[S-4673] One drive's information (Karpo §99, §122): its name in the header as This PC
/// names it, its picture and kind with the file system, the capacity bar and "419 GB free of 931 GB";
/// on a large tile the figures each on their own. A mapped network drive Windows cannot reach is drawn
/// as Explorer draws it (§75): the red cross on its picture, "Disconnected network drive", no bar.
/// The drives come from the host's one walk (<see cref="IDrives"/>), never read here.
/// </summary>
internal sealed partial class DriveView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetContextActions
{
    private readonly StackPanel _stack = new() { Spacing = Fluent.Space2, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _top = new() { ColumnSpacing = Fluent.Space3 };
    private readonly DriveIcon _icon = new();
    private readonly TextBlock _kind = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
    private readonly TextBlock _used = Fluent.Text(FluentType.BodyStrong);
    private readonly ProgressBar _bar = DriveBar.Create();
    private readonly TextBlock _space = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly Grid _facts = new() { ColumnSpacing = Fluent.Space4, RowSpacing = Fluent.Space2 };
    private readonly List<(TextBlock Label, TextBlock Value)> _factCells = [];

    private IWidgetSurface? _surface;
    private IDisposable? _watch;
    private DriveConfig _config = new();
    private DriveEntry? _drive;
    private WidgetArea _area;

    /// <summary>[S-6913] What the view asks of the host: the launcher, the Accounts page.</summary>
    private readonly IWidgetHost _widgetHost;

    public DriveView(IWidgetHost host)
    {
        _widgetHost = host;
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_used, FontNumeralAlignment.Tabular);
        _kind.TextTrimming = TextTrimming.CharacterEllipsis;
        _space.TextTrimming = TextTrimming.CharacterEllipsis;
        _used.VerticalAlignment = VerticalAlignment.Center;
        _kind.VerticalAlignment = VerticalAlignment.Center;

        _top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        SetColumn(_kind, 1);
        SetColumn(_used, 2);
        _top.Children.Add(_icon);
        _top.Children.Add(_kind);
        _top.Children.Add(_used);

        for (var i = 0; i < 4; i++)
        {
            _facts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        _facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var i = 0; i < 4; i++)
        {
            var label = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            var value = Fluent.Text(FluentType.BodyStrong);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            value.TextTrimming = TextTrimming.CharacterEllipsis;
            SetColumn(label, i);
            SetColumn(value, i);
            SetRow(value, 1);
            _facts.Children.Add(label);
            _facts.Children.Add(value);
            _factCells.Add((label, value));
        }

        _stack.Children.Add(_top);
        _stack.Children.Add(_bar);
        _stack.Children.Add(_space);
        _stack.Children.Add(_facts);
        _stack.Margin = WidgetLayout.SideInset;
        Children.Add(_stack);
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

    /// <summary>
    /// [S-4672] The ladder: the space line and the bar always; the picture and the kind where a row
    /// fits; the four figures under them on a tile tall and wide enough. Costs are line heights.
    /// </summary>
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

        // [S-6274] The ring style lays its own parts; the bar's below.
        if (_ringParts is not null)
        {
            LayRing(drive, area);
            return;
        }

        var large = area.Least >= SizeClass.Expanded;
        var narrow = area.WidthClass == SizeClass.Compact;
        var box = area.Deflate(Fluent.Space4, Fluent.Space2);
        var bar = large ? 8.0 : 4.0;
        var spaceStep = large ? FluentType.Body : FluentType.Caption;
        var iconSize = large ? 48.0 : area.HeightClass >= SizeClass.Regular ? 32.0 : 24.0;
        var gap = _stack.Spacing;

        var room = new Disclosure(box, gap);
        var known = drive.UsedFraction is not null;
        if (known)
        {
            room.Reserve(bar);
        }

        room.Reserve(spaceStep.LineHeight);
        var top = room.Take(iconSize);
        var facts = known && area.HeightClass >= SizeClass.Regular && area.WidthClass >= SizeClass.Regular
            && room.Take(FluentType.Caption.LineHeight + FluentType.BodyStrong.LineHeight + _facts.RowSpacing, 280);

        _icon.Show(drive, iconSize);
        _top.Visibility = top ? Visibility.Visible : Visibility.Collapsed;
        _kind.Apply(large ? FluentType.BodyLarge : FluentType.Body);
        // [S-4671] The card starts headerless ([S-4675]), so the body names the drive; a drive Windows
        // cannot reach says why instead ("Disconnected network drive"), its name is in the tooltip.
        _kind.Text = drive.State == DriveState.Disconnected ? DriveRules.Description(locale, drive) : DriveRules.Title(locale, drive);
        _kind.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        _used.Apply(large ? FluentType.Subtitle : FluentType.BodyStrong);
        _used.Text = drive.UsedFraction is { } used ? Formatting.Percent(locale, used) : string.Empty;
        _used.Visibility = known ? Visibility.Visible : Visibility.Collapsed;

        DriveBar.Show(_bar, drive, bar);

        _space.Apply(spaceStep);

        // [S-4673] Narrow, the whole line does not fit: what is used says it in a word.
        _space.Text = narrow && known
            ? DriveStr.Drives_Used.Format(("percent", Formatting.Percent(locale, drive.UsedFraction!.Value)))
            : DriveRules.SpaceLine(locale, drive);
        if (narrow && known)
        {
            _used.Visibility = Visibility.Collapsed;
        }

        // Disconnected, the kind already says it ("Disconnected network drive"): the line would repeat it.
        _space.Visibility = drive.State == DriveState.Disconnected && top && !narrow ? Visibility.Collapsed : Visibility.Visible;

        _facts.Visibility = facts ? Visibility.Visible : Visibility.Collapsed;
        if (facts)
        {
            Fact(0, DriveStr.Drives_Fact_Used, Formatting.Bytes(locale, drive.Used));
            Fact(1, DriveStr.Drives_Fact_Free, Formatting.Bytes(locale, drive.Free));
            Fact(2, DriveStr.Drives_Fact_Capacity, Formatting.Bytes(locale, drive.Total));
            Fact(3, DriveStr.Drives_Fact_FileSystem, drive.FileSystem.Length > 0 ? drive.FileSystem : "--");
        }
    }

    private void Fact(int index, DriveStr label, string value)
    {
        _factCells[index].Label.Text = label.Text();
        _factCells[index].Value.Text = value;
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
