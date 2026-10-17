using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.App.Shell.Motion;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6012] One split-flap card, the clock's Flip style and the Countdown's ([S-6032]): a rounded
/// face in the subtle fill, the figures centred, and the hairline split across its middle that
/// makes it a flap rather than a box. New figures on a tick turn the card over (it folds away,
/// 110 ms, and the new figures fold in, 170 ms); a redraw shows them at once, and nothing turns
/// while Windows' animation effects are off ([S-1010]).
/// </summary>
internal sealed class FlipCard
{
    /// <summary>A card's height against its width, and the figures' share of the card.</summary>
    public const double Aspect = 1.1;

    public const double DigitShare = 0.82;

    private const double Hairline = 1;

    private static readonly TimeSpan Fold = TimeSpan.FromMilliseconds(110);
    private static readonly TimeSpan Open = TimeSpan.FromMilliseconds(170);

    private readonly Border _face;
    private readonly TextBlock _digits;
    private readonly PlaneProjection _projection = new();
    private Storyboard? _running;

    /// <summary>What the card says, or null when it is to be drawn at once (new, or redrawn).</summary>
    private string? _shown;

    public FlipCard()
    {
        _face = new Border
        {
            BorderBrush = ThemeBrush.Of("ControlStrokeColorDefaultBrush", Colors.Gray),
            BorderThickness = new Thickness(Hairline),
        };
        ItemFill.Paint(_face, ItemRole.Chip);

        _digits = new TextBlock
        {
            Foreground = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        };

        var split = new Rectangle
        {
            Height = Hairline,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = ThemeBrush.Of("CardStrokeColorDefaultBrush", Colors.Gray),
        };

        Root = new Grid { Projection = _projection, Children = { _face, _digits, split } };

        // [S-6546] The figures (and a caption a caller adds) are laid over the face, not inside it.
        ItemFill.OnItem(Root);
    }

    /// <summary>The card; a caller may add a corner caption to it (the clock's AM/PM).</summary>
    public Grid Root { get; }

    /// <summary>The figures' colour: the countdown's critical one once its target has gone by.</summary>
    public Brush Foreground
    {
        get => _digits.Foreground;
        set => _digits.Foreground = value;
    }

    /// <summary>Size the card and its figures.</summary>
    public void Lay(double width, double height, TypeStep digits)
    {
        Root.Width = width;
        Root.Height = height;
        _face.CornerRadius = new CornerRadius(Math.Min(Fluent.OverlayRadius, height / 8));
        _digits.Apply(digits);
    }

    /// <summary>The next <see cref="Show"/> draws at once rather than turning: a config, language or size change.</summary>
    public void Redraw() => _shown = null;

    /// <summary>Show <paramref name="text"/>: at once when it is new or the card was just redrawn, otherwise turned over.</summary>
    public void Show(string text, bool animate)
    {
        if (string.Equals(_shown, text, StringComparison.Ordinal))
        {
            return;
        }

        animate = animate && _shown is not null && SystemMotion.AnimationsEnabled;
        _shown = text;
        _running?.Stop();
        _running = null;

        if (!animate)
        {
            _projection.RotationX = 0;
            _digits.Text = text;
            return;
        }

        var fold = Turn(0, -90, Fold, EasingMode.EaseIn);
        fold.Completed += (_, _) =>
        {
            if (!ReferenceEquals(_running, fold))
            {
                return; // a newer flip or a redraw took over
            }

            _digits.Text = _shown ?? string.Empty;
            var open = Turn(90, 0, Open, EasingMode.EaseOut);
            _running = open;
            open.Begin();
        };

        _running = fold;
        fold.Begin();
    }

    private Storyboard Turn(double from, double to, TimeSpan duration, EasingMode easing)
    {
        var turn = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(duration),
            EasingFunction = new SineEase { EasingMode = easing },
        };

        Storyboard.SetTarget(turn, _projection);
        Storyboard.SetTargetProperty(turn, nameof(PlaneProjection.RotationX));
        var board = new Storyboard();
        board.Children.Add(turn);
        return board;
    }
}
