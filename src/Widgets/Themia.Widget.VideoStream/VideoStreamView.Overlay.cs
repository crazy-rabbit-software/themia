using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services.Web;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.VideoStream;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1312]/[S-1313] The camera's optional chrome, kept off the picture unless there is room for it
/// and a reason to look: a hover strip (state and frame age from M; controls, resolution and frame
/// rate from L) and, at XL when the user asks for it, a persistent status chip.
/// <para>
/// Everything here is driven by events — a frame, a phase change, the pointer, a resize. The one
/// timer is the shared ticker at one second, subscribed only while the strip with the frame's age
/// is on screen, so an unwatched camera wakes nothing.
/// </para>
/// </summary>
internal sealed partial class VideoStreamView
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly FrameRateMeter _rate = new();

    private readonly Ellipse _chipDot = Dot();
    private readonly TextBlock _chipText = Family.Text(FamilyType.Caption, ColourRole.TextPrimary);
    private readonly Ellipse _stripDot = Dot();
    private readonly TextBlock _stripText = Family.Text(FamilyType.Caption, ColourRole.TextPrimary);
    private readonly TextBlock _stripDetails = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly Button _pauseButton;
    private readonly StackPanel _controls = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

    /// <summary>Built by the constructor through <see cref="BuildOverlay"/>.</summary>
    private readonly Border _chip;
    private readonly Border _strip;

    private WidgetArea _area;
    private StreamChrome _chrome;
    private bool _hovered;
    private Ticker? _ticker;
    private readonly TickLease _ageTick = new();

    /// <summary>Failures in a row since the last frame: the "reconnecting 3" of the status.</summary>
    private int _reconnects;

    private FrameFacts _facts;
    private StreamTransport _transport = StreamTransport.Unsupported;

    private (Border Chip, Border Strip, Button Pause) BuildOverlay()
    {
        var chip = Pill(_chipDot, _chipText);
        chip.HorizontalAlignment = HorizontalAlignment.Left;
        chip.VerticalAlignment = VerticalAlignment.Top;
        chip.Margin = new Thickness(FamilySpace.S2);

        _stripText.VerticalAlignment = VerticalAlignment.Center;
        _stripDetails.VerticalAlignment = VerticalAlignment.Center;
        _stripDetails.TextTrimming = TextTrimming.CharacterEllipsis;
        var info = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = FamilySpace.S1,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _stripDot, _stripText, _stripDetails },
        };

        // [S-1312] The context menu's three actions, one click away while the pointer is here: the
        // family's icon buttons, clear on the bar as a header's buttons are ([S-6716]).
        var pause = BarButton(Glyphs.Pause, VideoStreamStr.VideoStream_Pause.Text(), () => SetPaused(!_config.Paused));
        var rotate = BarButton(Glyphs.Rotate, VideoStreamStr.VideoStream_Rotate.Text(), Rotate);
        var refresh = BarButton(Glyphs.Refresh, Str.Common_Refresh.Text(), RefreshNow);

        _controls.Children.Add(pause);
        _controls.Children.Add(rotate);
        _controls.Children.Add(refresh);

        var row = new Grid
        {
            ColumnSpacing = FamilySpace.S2,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        row.Children.Add(info);
        Grid.SetColumn(_controls, 1);
        row.Children.Add(_controls);

        // [S-1314] Over the picture: the solid role with a hairline edge, the one radius, a row of 32 at
        // least (42 with the buttons), 8 in from the picture's edges.
        var strip = new Border
        {
            Child = row,
            Background = Family.Brush(ColourRole.Solid),
            BorderBrush = Family.Brush(ColourRole.ControlStroke),
            BorderThickness = new Thickness(FamilyShape.Hairline),
            CornerRadius = Family.Radius,
            Padding = new Thickness(FamilySpace.S2, FamilySpace.S1, FamilySpace.S1, FamilySpace.S1),
            Margin = new Thickness(FamilySpace.S2),
            VerticalAlignment = VerticalAlignment.Bottom,
            MinHeight = FamilyShape.Row1,
            Visibility = Visibility.Collapsed,
        };

        return (chip, strip, pause);
    }

    /// <summary>[S-1314] The state dot: a round 8, its role by <see cref="StreamChromeRules.DotRole"/>.</summary>
    private static Ellipse Dot() => new()
    {
        Width = FamilySpace.S2,
        Height = FamilySpace.S2,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Button BarButton(string glyph, string tooltip, Action run)
    {
        var button = ControlsBlock.IconButton(glyph, tooltip, run);
        button.Background = Family.Brush(ColourRole.Clear);
        button.BorderThickness = new Thickness(0);
        return button;
    }

    /// <summary>[S-1314] The status chip: the strip's look, one Caption line, 8 and 4 around it.</summary>
    private static Border Pill(Ellipse dot, TextBlock text)
    {
        text.VerticalAlignment = VerticalAlignment.Center;

        return new Border
        {
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = FamilySpace.S1, Children = { dot, text } },
            Background = Family.Brush(ColourRole.Solid),
            BorderBrush = Family.Brush(ColourRole.ControlStroke),
            BorderThickness = new Thickness(FamilyShape.Hairline),
            CornerRadius = Family.Radius,
            Padding = new Thickness(FamilySpace.S2, FamilySpace.S1, FamilySpace.S2, FamilySpace.S1),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
    }

    // ---- IWidgetSizeAware ------------------------------------------------------------------------

    /// <summary>The card's slot: the decode size, the message's shedding and the chrome all follow it.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        UpdateTarget();
        Render();
    }

    // ---- inputs --------------------------------------------------------------------------------

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hovered = true;
        RenderChrome();
    }

    private void OnPointerGone(object sender, PointerRoutedEventArgs e)
    {
        _hovered = false;
        RenderChrome();
    }

    /// <summary>[S-1313] A frame arrived: count it for the rate and remember what the camera sent.</summary>
    private void NoteFrame(FrameFacts facts)
    {
        _facts = facts;
        _transport = facts.Transport;
        _reconnects = 0;
        _rate.Add(_clock.Elapsed);

        if (_strip.Visibility == Visibility.Visible || _chip.Visibility == Visibility.Visible)
        {
            RenderChrome();
        }
    }

    private void NotePhase(StreamPhase phase)
    {
        if (phase == StreamPhase.Retrying)
        {
            _reconnects++;
        }
        else if (phase == StreamPhase.Live)
        {
            _reconnects = 0;
        }
    }

    /// <summary>A new connection: nothing measured about the old one applies to it.</summary>
    private void ForgetFrames()
    {
        _rate.Reset();
        _facts = default;
        _transport = StreamTransport.Unsupported;
    }

    // ---- drawing --------------------------------------------------------------------------------

    private void RenderChrome()
    {
        if (_detached)
        {
            return;
        }

        _chrome = StreamChromeRules.Chrome(_area, _config.HoverControls, _config.ShowStatus);

        var configured = _config.ResolvedUrl.IsValid;
        var live = _phase == StreamPhase.Live && !_config.Paused && (_session is not null || _still);
        var status = _config.Paused ? VideoStreamStr.WidgetsC_VideoStream_Status_Paused.Text() : StreamChromeRules.Status(live, _reconnects, _config.EffectiveRefreshSeconds);
        var dot = Family.Brush(StreamChromeRules.DotRole(live, _config.Paused));

        // [S-1312] The chip: XL, the user's choice, and only once there is a stream to describe; never
        // beside the Status row, which says the state itself ([S-1314]).
        var chip = _chrome.StatusChip && configured && _status.Visibility == Visibility.Collapsed;
        _chip.Visibility = chip ? Visibility.Visible : Visibility.Collapsed;

        if (chip)
        {
            var transport = StreamChromeRules.Transport(_transport);
            _chipText.Text = transport.Length > 0 && live ? $"{status} · {transport}" : status;
            _chipDot.Fill = dot;
        }

        var strip = _chrome.HoverStrip && _hovered && configured;
        _strip.Visibility = strip ? Visibility.Visible : Visibility.Collapsed;
        _controls.Visibility = _chrome.HoverDetails ? Visibility.Visible : Visibility.Collapsed;

        if (strip)
        {
            _stripDot.Fill = dot;
            _stripText.Text = live && _rate.Last is { } last
                ? $"{status} · {StreamChromeRules.FrameAge(_clock.Elapsed - last)}"
                : status;

            _stripDetails.Text = _chrome.HoverDetails && live ? Details() : string.Empty;
            _stripDetails.Visibility = _stripDetails.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

            ((FontIcon)_pauseButton.Content).Glyph = _config.Paused ? Glyphs.Play : Glyphs.Pause;
            ToolTipService.SetToolTip(_pauseButton, (_config.Paused ? VideoStreamStr.VideoStream_Resume : VideoStreamStr.VideoStream_Pause).Text());
        }

        // The age counts up while it is on screen, and only then.
        var ticking = strip && live;
        if (_ticker is { } ticker)
        {
            _ageTick.Want(ticking, () => ticker.Subscribe(TimeSpan.FromSeconds(1), RenderChrome));
        }
        else
        {
            _ageTick.Dispose();
        }
    }

    /// <summary>"1280×720 · 10 fps"; a rate only for an open MJPEG stream, where it means something.</summary>
    private string Details()
    {
        var size = StreamChromeRules.Resolution(_facts.SourceWidth, _facts.SourceHeight);
        var rate = _transport == StreamTransport.Mjpeg && _config.EffectiveRefreshSeconds == VideoStreamRules.LiveRefreshSeconds
            ? StreamChromeRules.Rate(_rate.Rate)
            : string.Empty;

        return string.Join(" · ", new[] { size, rate }.Where(part => part.Length > 0)) is { Length: > 0 } text ? $"· {text}" : string.Empty;
    }

    private void StopChrome()
    {
        _ageTick.Dispose();
        _hovered = false;
        _chip.Visibility = Visibility.Collapsed;
        _strip.Visibility = Visibility.Collapsed;
    }
}
