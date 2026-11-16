using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Storage.Streams;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-5609] A desktop item's icon is the shell's, with its overlay and shortcut arrow ([S-5512]), from
/// the host's cache (<see cref="IDesktopItems.IconAsync"/>).
/// [S-9503] An own icon's, best first (<see cref="OwnIcons.Choose"/>): the user's picture while
/// it reads, the user's emoji, the icon a dropped <c>.url</c> named, then the target's own: Explorer's icon
/// for a file, a folder, an app or a Windows location (never for a network path), the site's own favicon
/// for a web address (no third-party service), the Settings app's for a Settings page, and for an app
/// layout up to four of its panes' icons. The kind's glyph stands in until the icon arrives and stays
/// when there is none. Asked at the display's own pixels ([S-5563]) and again when its scale changes.
/// </summary>
internal sealed partial class DesktopItemView
{
    /// <summary>The glyph (or the emoji) drawn until, or instead of, a picture.</summary>
    private FrameworkElement? _fallback;

    /// <summary>The newest icon request: an answer to an older one is dropped.</summary>
    private int _iconRequest;

    /// <summary>[S-5563] The pixels the icon was last asked at.</summary>
    private int _iconPixels;

    /// <summary>[S-9510] The Recycle Bin's icon last asked for (empty, full, unknown), so it is not asked twice.</summary>
    private string? _iconVariant;

    /// <summary>The newest icon load, for a picture that waits for it (the sample).</summary>
    private Task _iconLoad = Task.CompletedTask;

    /// <summary>[S-5563] The root the view is drawn in, watched for its scale (a display's scale changed).</summary>
    private XamlRoot? _scaleRoot;

    /// <summary>[S-5564] The icon's picture for a drag's ghost, as last kept; null until there is one.</summary>
    private GhostIcon? _dragIcon;

    /// <summary>The newest keeping of that picture: an older render's answer is dropped.</summary>
    private int _dragIconRequest;

    /// <summary>
    /// [S-5564] Keep the picture a drag's ghost shows of this icon current: called whenever the icon
    /// drawn changes (built, loaded, its bitmap decoded, the view loaded), never when a drag starts. The
    /// shell's icon is a bitmap whose pixels are read at once; anything else (the glyph, an emoji, a
    /// picture file, a site's icon, a layout's panes) is rendered once, here and ahead of any drag, so
    /// the drag itself only composes pixels. The last picture stays until the new one is there.
    /// </summary>
    private void KeepDragIcon()
    {
        var request = ++_dragIconRequest;
        if (_image.Visibility == Visibility.Visible && ItemDrag.IconOf(_image.Source) is { } pixels)
        {
            _dragIcon = pixels;
            return;
        }

        _ = RenderDragIconAsync(request);
    }

    private async Task RenderDragIconAsync(int request)
    {
        try
        {
            if (!IsLoaded || _iconHost.ActualWidth <= 0 || _iconHost.ActualHeight <= 0)
            {
                return; // not drawn yet: Loaded keeps it again
            }

            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(_iconHost);
            if (request != _dragIconRequest || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
            {
                return;
            }

            var rendered = new GhostIcon(bitmap.PixelWidth, bitmap.PixelHeight, (await bitmap.GetPixelsAsync()).ToArray());
            if (request == _dragIconRequest && rendered.Drawable)
            {
                _dragIcon = rendered;
            }
        }
        catch (Exception ex)
        {
            // The ghost is decoration: a drag without this icon's picture still drags.
            Log.Debug(Scope, $"a desktop icon's picture for the drag's ghost could not be rendered ({ex.GetType().Name})");
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WatchScale(XamlRoot);
        KeepDragIcon();
        if (_attached && IconPixels() != _iconPixels)
        {
            _ = LoadIconAsync();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => WatchScale(null);

    private void WatchScale(XamlRoot? root)
    {
        if (ReferenceEquals(root, _scaleRoot))
        {
            return;
        }

        if (_scaleRoot is not null)
        {
            _scaleRoot.Changed -= OnRootChanged;
        }

        _scaleRoot = root;
        if (root is not null)
        {
            root.Changed += OnRootChanged;
        }
    }

    /// <summary>[S-5563] A new scale wants the icon at its own pixels, never the old one's stretched.</summary>
    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (_attached && IconPixels() != _iconPixels)
        {
            _ = LoadIconAsync();
        }
    }

    /// <summary>[S-5563] [S-5593] The icon's pixels on this display at the desktop's icon size: a medium icon is 48 at 100 %, 72 at 150 %, 96 at 200 %.</summary>
    private int IconPixels() => DesktopIconPixels.For(DesktopCell.Icon(_size), XamlRoot?.RasterizationScale ?? 1.0);

    private string BinVariant() => _host.RecycleBin.Current is not { } bin ? "unknown" : bin.IsEmpty ? "empty" : "full";

    /// <summary>
    /// The icon's square, built anew: the glyph of what the shortcut is (a warning for a refused target),
    /// or the user's emoji in its place at once; the picture is laid over it when it arrives.
    /// </summary>
    private void BuildIcon()
    {
        _iconHost.Children.Clear();
        _image.Source = null;
        _image.Visibility = Visibility.Collapsed;

        if (!Own)
        {
            // A desktop item draws the shell's icon alone; the cached one is laid in at once (no flash of nothing).
            _fallback = null;
            _iconHost.Children.Add(_image);
            if (Item is { } item && _host.DesktopItems.TryIcon(item, IconPixels(), out var cached) && cached is not null)
            {
                _image.Source = cached;
                _image.Visibility = Visibility.Visible;
            }

            KeepDragIcon();
            return;
        }

        if (_config.IconEmoji is { } emoji)
        {
            _fallback = EmojiIcon(emoji);
        }
        else
        {
            var glyph = Family.Glyph(GlyphNow(), DesktopCell.Icon(_size), Refused ? ColourRole.Critical : ColourRole.OnWallpaper);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            _fallback = glyph;
        }

        _iconHost.Children.Add(_fallback);
        _iconHost.Children.Add(_image);
        KeepDragIcon();
    }

    /// <summary>The glyph that stands for the shortcut until its real icon arrives, or for good when it has none.</summary>
    private string GlyphNow() => _config.IsLayout ? Glyphs.AppLayout
        : string.IsNullOrWhiteSpace(_config.Target) ? DesktopItemModule.Glyph
        : _target.Kind switch
        {
            LaunchKind.Web => Glyphs.Globe,
            LaunchKind.LocalPath => Glyphs.File,
            LaunchKind.ShellNamespace => Glyphs.Folder,
            LaunchKind.Settings => Glyphs.Settings,
            _ => Glyphs.Warning,
        };

    /// <summary>
    /// An emoji as an icon: colour glyphs from the system's emoji font (XAML falls back to it for emoji
    /// code points), its tight ink box scaled to fill the icon's square as the shell's icons do.
    /// </summary>
    private Viewbox EmojiIcon(string emoji) => new()
    {
        Width = DesktopCell.Icon(_size),
        Height = DesktopCell.Icon(_size),
        Stretch = Stretch.Uniform,
        Child = new TextBlock
        {
            Text = emoji,

            // A colour glyph draws its own colours; a monochrome fallback draws as the name does.
            Foreground = Family.Brush(ColourRole.OnWallpaper),
            IsColorFontEnabled = true,
            TextLineBounds = TextLineBounds.Tight,
            IsTextScaleFactorEnabled = false,
        },
    };

    private Task LoadIconAsync() => _iconLoad = LoadIconCoreAsync();

    private async Task LoadIconCoreAsync()
    {
        var request = ++_iconRequest;
        var config = _config;
        var target = _target;
        var pixels = IconPixels();
        _iconPixels = pixels;
        var variant = IsBin ? BinVariant() : null;
        _iconVariant = variant;

        try
        {
            if (!Own)
            {
                if (Item is { } item && await _host.DesktopItems.IconAsync(item, pixels).ConfigureAwait(true) is { } shell && request == _iconRequest)
                {
                    _image.Source = shell;
                    _image.Visibility = Visibility.Visible;
                    KeepDragIcon();
                }

                return;
            }

            // The user's own icon first: a picture while its file reads, else the emoji already drawn;
            // only then what Windows gives the target.
            ImageSource? source = config.IconPicture is { } picture ? await PictureAsync(picture, pixels).ConfigureAwait(true) : null;

            if (source is null)
            {
                switch (OwnIcons.Choose(config, pictureReadable: false))
                {
                    case OwnIconSource.Emoji:
                        return;

                    case OwnIconSource.Named when LaunchTargets.Classify(config.IconPath) is { Kind: LaunchKind.LocalPath } named && LaunchTargets.MayProbe(named):
                        source = await AppIconSource.GetAsync(named.Value, pixels, AppIconSource.ThemeOf(this)).ConfigureAwait(true);
                        break;
                }
            }

            if (source is null && config.IsLayout)
            {
                await LoadLayoutIconAsync(config, pixels, request).ConfigureAwait(true);
                return;
            }

            source ??= target.Kind switch
            {
                LaunchKind.Web => await FaviconAsync(target.Value).ConfigureAwait(true),
                LaunchKind.LocalPath or LaunchKind.ShellNamespace when LaunchTargets.MayProbe(target)
                    => await AppIconSource.GetAsync(target.Value, pixels, AppIconSource.ThemeOf(this), variant).ConfigureAwait(true),

                // [S-9514] A Settings page has no icon of its own: the Settings app's.
                LaunchKind.Settings => await AppIconSource.GetAsync(LaunchTargets.SettingsAppTarget, pixels, AppIconSource.ThemeOf(this)).ConfigureAwait(true),
                _ => null,
            };

            if (source is null || request != _iconRequest)
            {
                return;
            }

            _image.Source = source;
            _image.Visibility = Visibility.Visible;
            if (_fallback is not null)
            {
                _fallback.Visibility = Visibility.Collapsed;
            }

            KeepDragIcon();
        }
        catch (Exception ex)
        {
            // The glyph stays; an icon is decoration.
            Log.Warn(Scope, "could not load a desktop icon's icon", ex);
        }
    }

    /// <summary>
    /// The user's picture at <paramref name="pixels"/>, or null when it cannot be drawn: a file that is
    /// gone, too big or will not decode (logged; the automatic icon is drawn instead, no notice: a moved
    /// picture is not an error the user must act on). An <c>.ico</c> goes through the shell, which picks
    /// its best size; an <c>.svg</c> is rasterised at the size; the rest are decoded.
    /// </summary>
    private async Task<ImageSource?> PictureAsync(string path, int pixels)
    {
        if (!OwnIcons.IsPicture(path))
        {
            return null; // repair refuses these on load; a picture set since is checked as it is set.
        }

        try
        {
            var bytes = await Task.Run(() =>
            {
                var file = new FileInfo(path);
                if (!file.Exists)
                {
                    Log.Info(Scope, $"a desktop icon's picture is missing ({file.Name}); its automatic icon is drawn");
                    return null;
                }

                if (file.Length > OwnIcons.MaxPictureBytes)
                {
                    Log.Warn(Scope, $"a desktop icon's picture is too big to draw ({file.Name}, {file.Length} bytes); its automatic icon is drawn");
                    return null;
                }

                return OwnIcons.IsIconFile(path) ? [] : File.ReadAllBytes(path);
            }).ConfigureAwait(true);

            if (bytes is null)
            {
                return null;
            }

            if (OwnIcons.IsIconFile(path))
            {
                return await AppIconSource.GetAsync(path, pixels, AppIconSource.ThemeOf(this)).ConfigureAwait(true);
            }

            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            if (OwnIcons.IsSvg(path))
            {
                var svg = new SvgImageSource { RasterizePixelWidth = pixels, RasterizePixelHeight = pixels };
                var status = await svg.SetSourceAsync(stream);
                if (status != SvgImageSourceLoadStatus.Success)
                {
                    Log.Warn(Scope, $"a desktop icon's picture would not draw ({Path.GetFileName(path)}: {status}); its automatic icon is drawn");
                    return null;
                }

                return svg;
            }

            // An animated GIF holds still: a desktop icon that moves is a distraction and a cost.
            var bitmap = new BitmapImage { DecodePixelWidth = pixels, AutoPlay = false };
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or ArgumentException or InvalidOperationException)
        {
            Log.Warn(Scope, $"a desktop icon's picture would not load ({Path.GetFileName(path)}); its automatic icon is drawn", ex);
            return null;
        }
    }

    /// <summary>A site's own icon, decoded: the bytes are shared app-wide by the host's cache (<see cref="IWidgetHost.Favicons"/>).</summary>
    private async Task<BitmapImage?> FaviconAsync(string url)
    {
        if (SiteIcons.FaviconOf(url) is null || await _host.Favicons.GetAsync(url).ConfigureAwait(true) is not { } bytes)
        {
            return null;
        }

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            var bitmap = new BitmapImage { DecodePixelWidth = DesktopIconPixels.Jumbo };
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        {
            // Bytes that passed the magic-number check and still would not decode: the globe stays.
            Log.Debug(Scope, $"a site icon would not decode: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// [S-1405] An app layout's icon previews what it opens: up to four of its panes' icons, overlapping
    /// from the top left to the bottom right, in tree order. Web panes show their site's icon, the rest
    /// the shell's. With nothing assigned, or nothing loaded, the layout glyph stays.
    /// </summary>
    private async Task LoadLayoutIconAsync(DesktopItemConfig config, int pixels, int request)
    {
        var sources = new List<ImageSource>();

        foreach (var assignment in LayoutPlan.Preview(config.Layout))
        {
            try
            {
                var target = LaunchTargets.Classify(assignment.Target);
                ImageSource? source = target.Kind switch
                {
                    LaunchKind.Web => await FaviconAsync(target.Value).ConfigureAwait(true),
                    LaunchKind.LocalPath when LaunchTargets.MayProbe(target)
                        => await AppIconSource.GetAsync(target.Value, pixels, AppIconSource.ThemeOf(this)).ConfigureAwait(true),
                    _ => null,
                };

                if (source is not null)
                {
                    sources.Add(source);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, "could not load an app layout's pane icon", ex);
            }
        }

        if (sources.Count == 0 || request != _iconRequest)
        {
            return;
        }

        var whole = DesktopCell.Icon(_size);
        var each = sources.Count == 1 ? whole : Math.Round(whole * 0.66);
        var step = sources.Count == 1 ? 0 : (whole - each) / (sources.Count - 1);

        for (var i = 0; i < sources.Count; i++)
        {
            _iconHost.Children.Add(new Image
            {
                Source = sources[i],
                Width = each,
                Height = each,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = new TranslateTransform { X = step * i, Y = step * i },
            });
        }

        if (_fallback is not null)
        {
            _fallback.Visibility = Visibility.Collapsed;
        }

        KeepDragIcon();
    }
}
