using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

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

    private readonly FontIcon _chipDot = Dot();
    private readonly TextBlock _chipText = Fluent.Text(FluentType.Caption);
    private readonly FontIcon _stripDot = Dot();
    private readonly TextBlock _stripText = Fluent.Text(FluentType.Caption);
    private readonly TextBlock _stripDetails = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly Button _pauseButton = FluentLayout.IconCommand(Glyphs.Pause, Str.VideoStream_Pause.Text());
    private readonly StackPanel _controls = new() { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1, HorizontalAlignment = HorizontalAlignment.Right };

    /// <summary>Built by the constructor through <see cref="BuildOverlay"/>.</summary>
    private readonly Border _chip;
    private readonly Border _strip;

    private readonly Brush _liveBrush = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed);
    private readonly Brush _waitBrush = ThemeBrush.Of("SystemFillColorCautionBrush", Colors.Goldenrod);
    private readonly Brush _idleBrush = ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray);

    private WidgetArea _area;
    private StreamChrome _chrome;
    private bool _hovered;
    private Ticker? _ticker;
    private IDisposable? _ageTick;

    /// <summary>Failures in a row since the last frame: the "reconnecting 3" of the status.</summary>
    private int _reconnects;

    private FrameFacts _facts;
    private StreamTransport _transport = StreamTransport.Unsupported;

    private (Border Chip, Border Strip) BuildOverlay()
    {
        var chip = Pill(_chipDot, _chipText);
        chip.HorizontalAlignment = HorizontalAlignment.Left;
        chip.VerticalAlignment = VerticalAlignment.Top;
        chip.Margin = new Thickness(Fluent.Space2);

        _stripText.VerticalAlignment = VerticalAlignment.Center;
        _stripDetails.VerticalAlignment = VerticalAlignment.Center;
        _stripDetails.TextTrimming = TextTrimming.CharacterEllipsis;
        var info = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space1,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _stripDot, _stripText, _stripDetails },
        };

        // [S-1312] The context menu's three actions, one click away while the pointer is here.
        _pauseButton.Click += (_, _) => SetPaused(!_config.Paused);

        var rotate = FluentLayout.IconCommand(Glyphs.Rotate, Str.VideoStream_Rotate.Text());
        rotate.Click += (_, _) => Rotate();

        var refresh = FluentLayout.IconCommand(Glyphs.Refresh, Str.Common_Refresh.Text());
        refresh.Click += (_, _) => RefreshNow();

        _controls.Children.Add(_pauseButton);
        _controls.Children.Add(rotate);
        _controls.Children.Add(refresh);

        var row = new Grid
        {
            ColumnSpacing = Fluent.Space2,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        row.Children.Add(info);
        Grid.SetColumn(_controls, 1);
        row.Children.Add(_controls);

        var strip = new Border
        {
            Child = row,
            Background = ThemeBrush.Of("AcrylicInAppFillColorDefaultBrush", Colors.Black),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            Padding = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space1, Fluent.Space1),
            Margin = new Thickness(Fluent.Space2),
            VerticalAlignment = VerticalAlignment.Bottom,
            MinHeight = Fluent.RowHeightCompact,
            Visibility = Visibility.Collapsed,
        };

        return (chip, strip);
    }

    private static FontIcon Dot() => new()
    {
        Glyph = Glyphs.Dot,
        FontSize = Fluent.Space2,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Border Pill(FontIcon dot, TextBlock text)
    {
        text.VerticalAlignment = VerticalAlignment.Center;

        return new Border
        {
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1, Children = { dot, text } },
            Background = ThemeBrush.Of("AcrylicInAppFillColorDefaultBrush", Colors.Black),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            Padding = new Thickness(Fluent.Space2, Fluent.Space1 / 2, Fluent.Space2, Fluent.Space1 / 2),
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
        var live = _phase == StreamPhase.Live && !_config.Paused && _session is not null;
        var status = _config.Paused ? Str.WidgetsC_VideoStream_Status_Paused.Text() : StreamChromeRules.Status(live, _reconnects, _config.EffectiveRefreshSeconds);
        var dot = live ? _liveBrush : _config.Paused ? _idleBrush : _waitBrush;

        // [S-1312] The chip: XL, the user's choice, and only once there is a stream to describe.
        var chip = _chrome.StatusChip && configured;
        _chip.Visibility = chip ? Visibility.Visible : Visibility.Collapsed;

        if (chip)
        {
            var transport = StreamChromeRules.Transport(_transport);
            _chipText.Text = transport.Length > 0 && live ? $"{status} · {transport}" : status;
            _chipDot.Foreground = dot;
        }

        var strip = _chrome.HoverStrip && _hovered && configured;
        _strip.Visibility = strip ? Visibility.Visible : Visibility.Collapsed;
        _controls.Visibility = _chrome.HoverDetails ? Visibility.Visible : Visibility.Collapsed;

        if (strip)
        {
            _stripDot.Foreground = dot;
            _stripText.Text = live && _rate.Last is { } last
                ? $"{status} · {StreamChromeRules.FrameAge(_clock.Elapsed - last)}"
                : status;

            _stripDetails.Text = _chrome.HoverDetails && live ? Details() : string.Empty;
            _stripDetails.Visibility = _stripDetails.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

            ((FontIcon)_pauseButton.Content).Glyph = _config.Paused ? Glyphs.Play : Glyphs.Pause;
            ToolTipService.SetToolTip(_pauseButton, (_config.Paused ? Str.VideoStream_Resume : Str.VideoStream_Pause).Text());
        }

        // The age counts up while it is on screen, and only then.
        var ticking = strip && live;
        if (ticking && _ageTick is null && _ticker is not null)
        {
            _ageTick = _ticker.Subscribe(TimeSpan.FromSeconds(1), RenderChrome);
        }
        else if (!ticking && _ageTick is not null)
        {
            _ageTick.Dispose();
            _ageTick = null;
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
        _ageTick?.Dispose();
        _ageTick = null;
        _hovered = false;
        _chip.Visibility = Visibility.Collapsed;
        _strip.Visibility = Visibility.Collapsed;
    }
}
