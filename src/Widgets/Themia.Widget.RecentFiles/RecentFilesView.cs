using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.RecentFiles;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4840] The files opened last, as Windows lists them in Recent Items, newest first: a kit list ([S-7074]) over the
/// host's one list (<see cref="IWidgetHost.RecentFiles"/>, redrawn when its folder changes; the minute tick only keeps
/// "2h" honest), shaped by <see cref="RecentFilesSpec"/>, the shell's icon for each file as its lead.
/// </summary>
internal sealed partial class RecentFilesView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetKeys, IWidgetSizeAware
{
    /// <inheritdoc />
    public CardOpen? Opens => RecentFilesModule.Opens;

    private readonly IWidgetHost _host;
    private readonly KitBinding _binding;
    private readonly RecentSource _source;
    private RecentFilesConfig _config = new();
    private IWidgetSurface? _surface;
    private bool _attached;

    public RecentFilesView(IWidgetHost host)
    {
        _host = host;
        _source = new RecentSource(host.RecentFiles);
        _binding = new KitBinding(host, RecentFilesSpec.Spec, RecentFilesModule.Glyph, RecentFilesSpec.Actions, Run)
        {
            Look = new KitRichLook { Lead = item => FileIcon(item.Lead.Value) },
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
        _config = config as RecentFilesConfig ?? new RecentFilesConfig();
        _attached = true;
        _links.Join(_surface?.Links);

        // A debug render never reads the user's Recent Items: its PNG must not carry their file names.
        if (_surface?.IsPreview != true)
        {
            _source.Start();
        }

        _binding.Attach(ticker, _source, _config, _config.TickInterval);
    }

    /// <summary>[S-11200] Rows and Lines are the config's; the size follows them, never the other way.</summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as RecentFilesConfig ?? new RecentFilesConfig();
        if (_attached)
        {
            _binding.Apply(_source, _config, _config.TickInterval, show: false);
        }
    }

    public void ApplySize(WidgetArea area) => _binding.Size(area);

    public bool HandleKey(WidgetKey key) => _binding.HandleKey(key);

    public void Detach()
    {
        _attached = false;
        _binding.Dispose();
        _source.Stop();
        _links.Leave();
    }

    /// <summary>[S-4841] Open, its location, or off the list; the card makes the widget inert in edit mode ([S-1751]); a preview opens nothing.</summary>
    private void Run(KitItem item, KitAction action)
    {
        if (_source.FileOf(item.Id) is not { } file || _surface?.IsPreview == true)
        {
            return;
        }

        switch (action.Id)
        {
            case RecentFilesSpec.OpenId when _host.Launcher.Open(file.Path) == LaunchOutcome.Missing:
                // Gone since the last read: read again, and the row goes.
                _host.RecentFiles.Refresh();
                break;

            case RecentFilesSpec.LocationId:
                _host.FileOps.ShowInExplorer([file.Path]);
                break;

            case RecentFilesSpec.RemoveId:
                _host.RecentFiles.Remove(file);
                break;
        }
    }

    /// <summary>The shell's icon for the file; a page glyph until it comes, and for a file that has none.</summary>
    private FrameworkElement FileIcon(string? path)
    {
        var image = new Image { Width = FamilyShape.Icon, Height = FamilyShape.Icon };
        var fallback = Family.Glyph(Glyphs.File);
        var icon = new Grid { Width = FamilyShape.Icon, Height = FamilyShape.Icon, VerticalAlignment = VerticalAlignment.Center, Children = { fallback, image } };
        if (path is null)
        {
            return icon;
        }

        var entry = new FolderEntry(System.IO.Path.GetFileName(path), path, false, 0, DateTimeOffset.MinValue, false);
        var pixels = (int)Math.Ceiling(FamilyShape.Icon * (XamlRoot?.RasterizationScale ?? 1));
        if (_host.FileIcons.TryIcon(entry, pixels, out var known))
        {
            ShowIcon(image, fallback, known);
        }
        else
        {
            _ = LoadIconAsync(image, fallback, entry, pixels);
        }

        return icon;
    }

    private async Task LoadIconAsync(Image target, FontIcon fallback, FolderEntry entry, int pixels)
    {
        // AppIcons logs and returns null for a file without an icon; nothing here throws.
        ShowIcon(target, fallback, await _host.FileIcons.IconAsync(entry, pixels).ConfigureAwait(true));
    }

    private static void ShowIcon(Image target, FontIcon fallback, ImageSource? source)
    {
        target.Source = source;
        fallback.Visibility = source is null ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>[S-4840] Windows' Recent Items as a kit source: the files, an unreadable folder the state's problem.</summary>
    private sealed class RecentSource(IRecentItems items) : IKitSource
    {
        private IDisposable? _watch;

        /// <summary>[S-11850] The sample's files in place of Recent Items, or null.</summary>
        private IReadOnlyList<RecentFile>? _held;

        public event Action? Changed;

        public bool Busy => false;

        private IReadOnlyList<RecentFile> Files => _held ?? (_watch is null ? null : items.Current) ?? [];

        public void Start() => _watch ??= items.Subscribe(() => Changed?.Invoke());

        public void Stop()
        {
            _watch?.Dispose();
            _watch = null;
        }

        public void Show(bool refresh)
        {
            if (refresh && _held is null && _watch is not null)
            {
                items.Refresh();
            }
        }

        public KitRead Read()
        {
            if (_held is null && _watch is not null && items.Unreadable && items.Current is null)
            {
                return new KitRead([], [], Stated: new KitStated(RecentFilesStr.RecentFiles_Unreadable.Text()));
            }

            var locale = Strings.Current;
            return new KitRead([.. Files.Select(file => RecentFilesSpec.Item(file, locale))], [new KitOutcome(null, FetchStatus.Ok)]);
        }

        public RecentFile? FileOf(string id) => Files.FirstOrDefault(f => KitItems.Fingerprint(f.Path) == id);

        public void HoldForPreview(IReadOnlyList<RecentFile> files)
        {
            _held = files;
            Changed?.Invoke();
        }
    }
}
