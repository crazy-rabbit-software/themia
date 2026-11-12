using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.MusicCover;

/// <summary>
/// [S-11320] The cover of what Windows is playing, drawn to the card's edges: the picture cropped to
/// fill the square, the music glyph on the control fill while there is none. It reads the same media
/// session as the track atom (<see cref="NowPlayingFeed"/>), fetching the art once per track.
/// </summary>
internal sealed partial class MusicCoverView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetTopEdge
{
    /// <summary>[S-5700] It owns its top edge: the picture fills the card.</summary>
    public bool OwnsTopEdge => true;

    /// <inheritdoc />
    public CardOpen? Opens => _feed.Now.State is MusicState.NoSession or MusicState.Loading ? null : CardOpen.App(_feed.Now.AppId);

    private readonly Image _picture = new() { Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed };
    private readonly FontIcon _placeholder;
    private readonly NowPlayingFeed _feed;

    private MusicCoverConfig _config = new();
    private IWidgetSurface? _surface;
    private CancellationTokenSource? _artwork;
    private string _artKey = string.Empty;
    private bool _preview;
    private bool _hidden;

    public MusicCoverView(IWidgetHost host)
    {
        _feed = new NowPlayingFeed(host.Media, Present);
        _placeholder = Family.Glyph(ModuleCatalog.Of(new WidgetId("music")).Glyph, FamilyShape.IconLarge, ColourRole.TextTertiary);
        _placeholder.HorizontalAlignment = HorizontalAlignment.Center;
        _placeholder.VerticalAlignment = VerticalAlignment.Center;
        Background = Family.Brush(ColourRole.Control);
        Children.Add(_placeholder);
        Children.Add(_picture);
    }

    public void Bind(IWidgetSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        _preview = surface.IsPreview;
        _feed.Preview = surface.IsPreview;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _feed.Attach(DispatcherQueue);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MusicCoverConfig ?? new MusicCoverConfig();
        _feed.Reload();
        Present();
    }

    /// <summary>The snapshot in hand: hide or show the card, and fetch the art when the track changed.</summary>
    private void Present()
    {
        var now = _feed.Now;
        var hide = !_preview && MusicRules.Hides(_config.HideWhenIdle, now.State);
        if (hide != _hidden)
        {
            _hidden = hide;
            _surface?.SetHidden(hide);
        }

        if (!now.HasTrack)
        {
            Show(null, string.Empty);
            return;
        }

        if (now.TrackKey != _artKey && !_feed.Frozen)
        {
            _ = LoadAsync(now.TrackKey);
        }
    }

    private async Task LoadAsync(string key)
    {
        _artwork?.Cancel();
        var cts = new CancellationTokenSource();
        _artwork = cts;
        try
        {
            var image = await _feed.ArtworkAsync(cts.Token).ConfigureAwait(true);
            if (!cts.IsCancellationRequested)
            {
                // Remembered even when there is none, so a track with no cover is not fetched again on every push.
                Show(image, key);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore: a newer track's art is already on its way.
        }
        finally
        {
            if (ReferenceEquals(_artwork, cts))
            {
                _artwork = null;
            }

            cts.Dispose();
        }
    }

    /// <summary>The picture, or the placeholder without one.</summary>
    private void Show(ImageSource? image, string key)
    {
        _picture.Source = image;
        _picture.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
        _placeholder.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
        _artKey = key;
    }

    public void Detach()
    {
        _artwork?.Cancel();
        _artwork = null;
        _feed.Dispose();
        _picture.Source = null;
        _artKey = string.Empty;
    }
}
