using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Music;

/// <summary>
/// [S-14922] One transport button as a widget (owner, 2026-11-21: "now playing widget should be split into the individual
/// buttons"): previous, play or pause, or next, a tile like the power buttons and the quick toggles (the Tile block, one
/// face that scales with its card, [S-11882]); at home in the Now playing family. Its one option is when it hides ([S-1464]).
/// </summary>
public abstract class MusicButtonModule : IWidgetModule
{
    private readonly MediaTransport _transport;

    /// <param name="id">The kind's id.</param>
    /// <param name="create">A new config of the kind: which button it is.</param>
    protected MusicButtonModule(string id, Func<MusicButtonConfig> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        var config = create();
        var transport = config.Transport;
        _transport = transport;
        Descriptor = new()
        {
            Id = new(id),
            ConfigType = config.GetType(),
            CreateDefault = create,
            Json = MusicJsonContext.Default,
            Keywords = ["media controls", "transport", "playback", "now playing", "music", .. MusicButtons.Words(transport)],
            Label = MusicButtons.Label(transport),
            Description = MusicButtons.Description(transport),
            Glyph = MusicButtons.Glyph(transport, playing: false),
            Sizing = _ => TileRules.Sizing(TileLook.Tile, stateLine: false),
            Traits = WidgetTraits.HasItems | WidgetTraits.OneReadout,
            Add = new(AddFolderId.NowPlaying, 0, MusicKinds.Rank(transport)),
        };
    }

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; }

    /// <summary>[S-1479] When it hides.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config) => MusicMenu.Lines(menu, (MusicPartConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new MusicButtonView(_transport, host);
}

/// <summary>[S-14922] A transport button's words and looks, pure: its glyph, its name and what its colours say.</summary>
public static class MusicButtons
{
    /// <summary>[S-14922] The add page's name: "Previous track", "Play or pause", "Next track".</summary>
    public static LocalText Label(MediaTransport transport) => (transport switch
    {
        MediaTransport.Previous => MusicStr.Music_Previous_Label,
        MediaTransport.Next => MusicStr.Music_Next_Label,
        _ => MusicStr.WidgetsB_Music_PlayPause,
    }).Local();

    /// <summary>The add page's line under the name.</summary>
    public static LocalText Description(MediaTransport transport) => (transport switch
    {
        MediaTransport.Previous => MusicStr.Music_Previous_Desc,
        MediaTransport.Next => MusicStr.Music_Next_Desc,
        _ => MusicStr.Music_PlayPause_Desc,
    }).Local();

    /// <summary>Its search words beside the family's.</summary>
    public static IReadOnlyList<string> Words(MediaTransport transport) => transport switch
    {
        MediaTransport.Previous => ["previous", "back", "skip back", "rewind"],
        MediaTransport.Next => ["next", "skip", "forward"],
        _ => ["play", "pause", "resume", "stop"],
    };

    /// <summary>[S-14922] The glyph: previous, next, and play or (while it plays) pause.</summary>
    public static string Glyph(MediaTransport transport, bool playing) => transport switch
    {
        MediaTransport.Previous => Glyphs.Previous,
        MediaTransport.Next => Glyphs.Next,
        _ => playing ? Glyphs.Pause : Glyphs.Play,
    };

    /// <summary>
    /// [S-14922] What the tile shows for <paramref name="now"/>: its glyph, its name under it (what a click does: "Previous",
    /// "Play", "Pause", "Next") and its state. A button the player cannot do now, or any with no track, keeps its tile drawn
    /// unavailable ([S-1940], WR-10); play or pause is the accent while it plays (On, as a toggle that is on), the others the
    /// item layer's tile at rest.
    /// </summary>
    public static (string Glyph, string Name, TileState State) Face(MediaTransport transport, NowPlaying now)
    {
        var playing = now.HasTrack && now.State == MusicState.Playing;
        var name = (transport switch
        {
            MediaTransport.Previous => MusicStr.WidgetsB_Music_Previous,
            MediaTransport.Next => MusicStr.WidgetsB_Music_Next,
            _ => playing ? MusicStr.WidgetsB_Music_Pause : MusicStr.WidgetsB_Music_Play,
        }).Text();
        var can = now.HasTrack && transport switch
        {
            MediaTransport.Previous => now.CanPrevious,
            MediaTransport.Next => now.CanNext,
            _ => now.CanPlayPause,
        };
        var state = !can ? TileState.Unavailable : playing && transport == MediaTransport.PlayPause ? TileState.On : TileState.Off;
        return (Glyph(transport, playing), name, state);
    }
}

/// <summary>
/// [S-14922] A transport button's view: the Tile block in the card's inset, its glyph on the step its box holds and its name
/// under it (<see cref="TileRules.Face"/>, [S-11882]), the item layer's hover and pressed fills across the whole card. It is
/// not a <see cref="Button"/>: a button takes the press for itself, and in edit mode the press must reach the card so the
/// widget is selected and moved, never run ([S-1911]). A click (and Space or Enter while it has the keyboard) asks the media
/// session through the host (<see cref="NowPlayingFeed.Press"/>), never optimistic: the session's own change notification
/// draws the result. A preview presses nothing.
/// </summary>
internal sealed partial class MusicButtonView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetTopEdge, IWidgetButton, IWidgetSizeAware,
    IWidgetCorners, IWidgetKeys
{
    /// <summary>[S-5700] It owns its top edge: the button is centred in its own inset.</summary>
    public bool OwnsTopEdge => true;

    private readonly MediaTransport _transport;
    private readonly TileBlock _tile = new(TileLook.Tile) { Padding = Family.Inset };
    private readonly NowPlayingFeed _feed;
    private MusicPartConfig _config = new MusicPlayPauseConfig();
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private bool _hover;
    private bool _pressed;
    private bool _hidden;
    private bool _detached = true;

    public MusicButtonView(MediaTransport transport, IWidgetHost host)
    {
        _transport = transport;
        _feed = new NowPlayingFeed(host.Media, Paint);
        Children.Add(_tile);

        // The whole slot is hit-testable, edge to edge: the tile is the button.
        Background = Family.Brush(ColourRole.Clear);
    }

    public void Bind(IWidgetSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        _feed.Preview = surface.IsPreview;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerLost;
        PointerCanceled += OnPointerLost;
        Strings.Changed += OnLanguage;
        _feed.Attach(DispatcherQueue);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MusicPartConfig ?? new MusicPlayPauseConfig();
        _feed.Reload();
        Lay();
    }

    /// <summary>[S-11882] The tile's box: the glyph and its name grow with it.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <inheritdoc />
    public void ApplyCorners(CornerRadius corners) => _tile.Corners(corners);

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        PointerEntered -= OnPointerEntered;
        PointerExited -= OnPointerExited;
        PointerPressed -= OnPointerPressed;
        PointerReleased -= OnPointerReleased;
        PointerCaptureLost -= OnPointerLost;
        PointerCanceled -= OnPointerLost;
        Strings.Changed -= OnLanguage;
        _feed.Dispose();
    }

    /// <summary>[S-14922] A click takes the keyboard while the button can act.</summary>
    public bool WantsKeys => !_hidden && Can;

    /// <summary>[S-14922] Space or Enter press the button.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (key is not (WidgetKey.Space or WidgetKey.Enter) || !Can)
        {
            return false;
        }

        _feed.Press(_transport);
        return true;
    }

    /// <summary>Whether the player can do what the button asks now.</summary>
    private bool Can => MusicButtons.Face(_transport, _feed.Now).State != TileState.Unavailable;

    /// <summary>[S-1911] In edit mode the card owns the press: the widget is selected, never run.</summary>
    private bool Inert => _detached || _surface?.EditMode == true || !Can;

    /// <summary>[S-11882] The Tile block in the card's inset, centred: the glyph on the step the box holds, the name under it.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var natural = TileRules.Sizing(TileLook.Tile, stateLine: false).Natural;
        var (width, height) = _area.IsMeasured ? (_area.Width, _area.Height) : (natural.Width, natural.Height);
        var face = TileRules.Face(width, height, stateLine: false);
        _tile.Padding = new Thickness(face.Inset);
        _tile.Use(face.Glyph, width - (2 * FamilySpace.Inset), label: face.Texted, state: false, face.Name, face.State, face.Mark);
        Paint();
    }

    /// <summary>The whole look from the session: the glyph, the name, the state, hover and pressed; hidden when idle ([S-1464]).</summary>
    private void Paint()
    {
        if (_detached)
        {
            return;
        }

        var now = _feed.Now;
        var hide = _surface?.IsPreview != true && MusicRules.Hides(_config.HideWhenIdle, now.State);
        if (hide != _hidden)
        {
            _hidden = hide;
            _surface?.SetHidden(hide);
        }

        var (glyph, name, state) = MusicButtons.Face(_transport, now);
        _tile.Show(glyph, name, string.Empty, state);
        _tile.Face(state == TileState.Unavailable ? ItemState.Rest : _pressed ? ItemState.Pressed : _hover ? ItemState.Hover : ItemState.Rest);
        ToolTipService.SetToolTip(this, name);
        AutomationProperties.SetName(this, name);
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hover = !Inert;
        Paint();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hover = false;
        _pressed = false;
        Paint();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Inert || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressed = CapturePointer(e.Pointer);
        e.Handled = true;
        Paint();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;

        // A press that slid off the tile before release is not a click, as with any button.
        var at = e.GetCurrentPoint(this).Position;
        if (at.X >= 0 && at.Y >= 0 && at.X <= ActualWidth && at.Y <= ActualHeight && !Inert)
        {
            _feed.Press(_transport);
        }

        Paint();
    }

    private void OnPointerLost(object sender, PointerRoutedEventArgs e)
    {
        _pressed = false;
        Paint();
    }

    /// <summary>[S-1110] The name again, in the new language.</summary>
    private void OnLanguage(Locale locale) => Lay();
}
