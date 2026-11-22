using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
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
    public CardOpen? Opens => DriveModule.Opens(_config.Drive, Path.GetPathRoot(Environment.SystemDirectory));

    private readonly SpreadStack _figure = new() { Justify = true };

    /// <summary>[S-12808] The share, its line and the bar: one block, planned at the natural box and scaled up as one.</summary>
    private readonly ReadingFit _fit = new();
    private readonly ReadoutBlock _readout = new();

    /// <summary>
    /// [S-12810] The drive's own icon, as This PC draws it (the shell's: a fixed disk, the Windows drive, a USB stick, a
    /// mapped network drive), leading the share at the readout's step.
    /// </summary>
    /// <summary>[S-12810] [S-6631] The drive's own icon, none until it comes (and no room while there is none).</summary>
    private readonly IconSlot _icon = new(FamilyType.Big28.LineHeight) { Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0), VerticalAlignment = VerticalAlignment.Center };
    private string? _iconRoot;
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
        // [S-15759] The icon and the share centred over the bar that spans the card (owner, 2026-11-20, of a reading that fills
        // one axis: "Centred is fine"): the room the width leaves is shared alike on both sides, not all on the right.
        _figure.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Children = { _icon, _readout } });
        _figure.Children.Add(_bar);

        _status.Hide();
        _fit.Parts.Add(_figure);

        // [S-14301] The bar runs across the content, the share and its line over it from the start.
        _fit.Span(_figure);
        Children.Add(_fit);
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
        _config = config as DriveConfig ?? new DriveConfig();
        UseStyle();

        _watch?.Dispose();
        _watch = _widgetHost.Drives.Watch(ticker, Show);
        JoinLinks();
        _opensLight.Attach();

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

        _status.Hide();
        if (DriveFace.Status(locale, drive) is { } status)
        {
            // [S-12813] Not connected, no media: the drive's default (its icon, "--", the empty bar), the reason under it.
            if (_ringParts is not null)
            {
                _ringParts.Root.Visibility = Visibility.Collapsed;
            }

            _figure.Visibility = Visibility.Visible;
            HoldFigures(locale);
            _readout.Show("--", "%", status.Sentence, valueRole: ColourRole.TextDisabled);
            ShowIcon(drive);
            DriveBar.Show(_bar, drive);
            return;
        }

        if (_ringParts is not null)
        {
            LayRing(drive);
            return;
        }

        _figure.Visibility = Visibility.Visible;
        HoldFigures(locale);
        var used = ReadoutRules.Split(Formatting.Percent(locale, drive.UsedFraction!.Value));
        _readout.Show(used.Value, used.Unit, DriveRules.SpaceLine(locale, drive));
        ShowIcon(drive);
        DriveBar.Show(_bar, drive);
    }

    /// <summary>
    /// [S-15750] WR-15: the share held to its widest ("100%") and its line to the widest of its forms, so the block keeps its
    /// size and place as the drive fills.
    /// </summary>
    private void HoldFigures(Locale locale)
    {
        // [S-15763] The share stands on its own, left over its line (held to "100%" it stood right-aligned, off its line).
        if (locale == _heldLocale)
        {
            return;
        }

        _heldLocale = locale;
        var line = _readout.UnderText;
        var widest = 0.0;
        foreach (var template in DriveFace.SpaceTemplates(locale))
        {
            var probe = new TextBlock { Text = template, FontSize = line.FontSize, FontFamily = line.FontFamily, FontWeight = line.FontWeight, CharacterSpacing = line.CharacterSpacing };
            probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, probe.DesiredSize.Width);
        }

        line.MinWidth = Math.Ceiling(widest);

        // The line's box is its held template's: the place every space line stands in (WR-15's holds).
        HoldMark.Mark(line);
    }

    private Locale? _heldLocale;

    /// <summary>
    /// [S-12810] Ask the shell for the drive's icon once per drive; the root's own (system) icon, never the plain folder's.
    /// [S-15165] A preview's drive that is not this PC's system drive (a mapped share, a stick) is drawn with its kind's stock
    /// icon: the shell's answer for a letter this PC may not have changed from run to run (a plain fixed drive for a
    /// disconnected share, or the share's icon with a stray row over it).
    /// </summary>
    private async void ShowIcon(DriveEntry drive)
    {
        // [S-15750] A drive that is not there keeps its face (WR-10): the plain drive's stock icon, as the shell gives none.
        var missing = drive.State == DriveState.Missing;
        var stocked = missing || (_previewDrive is not null && !string.Equals(drive.Root, SystemDrive.Root, StringComparison.OrdinalIgnoreCase));
        var stock = stocked ? DriveRules.StockIcon(drive) ?? DriveStockIcon.Fixed : (DriveStockIcon?)null;
        var key = stocked ? $"{drive.Root}|{stock}" : drive.Root;
        if (key == _iconRoot)
        {
            return;
        }

        _iconRoot = key;
        _icon.Reset(null);
        _icon.Visibility = Visibility.Collapsed;
        if (stocked && stock is null)
        {
            // A preview's drive of no known kind (one that is not there): no icon, as the shell gives none.
            return;
        }
        const int system = 0x4;
        var pixels = (int)Math.Ceiling(FamilyType.Big28.LineHeight * 4 * (XamlRoot?.RasterizationScale ?? 1));

        // async void: the slot's load logs and swallows a failure (an icon is decoration), so nothing escapes here.
        if (await _icon.LoadAsync(() => stock is { } held
                ? _widgetHost.FileIcons.DriveIconAsync(held, pixels)
                : _widgetHost.FileIcons.IconAsync(new FolderEntry(drive.Letter, drive.Root, true, 0, default, false, system), pixels)).ConfigureAwait(true))
        {
            // No icon to draw takes no room: the share stands where its ink is.
            _icon.Visibility = _icon.Source is null ? Visibility.Collapsed : Visibility.Visible;
        }
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
