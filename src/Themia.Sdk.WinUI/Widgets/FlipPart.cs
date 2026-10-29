using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.App.Shell.Motion;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6012] [S-6740] One split-flap card, the clock's Flip style and the Countdown's ([S-6032]): an item
/// of the card's item layer ([S-6513]) in the family's radius, the figures centred on a family step in
/// their role, and the hairline split across its middle in the Track role that makes it a flap rather
/// than a box. Its size is <see cref="FlipRules"/>' (the figures' widest case and 12 either side, the
/// step's line tall). New figures on a tick turn the card over (it folds away, 110 ms, and the new
/// figures fold in, 170 ms); a redraw shows them at once, and nothing turns while Windows' animation
/// effects are off ([S-1010]).
/// </summary>
public sealed class FlipCard
{
    private static readonly TimeSpan Fold = TimeSpan.FromMilliseconds(110);
    private static readonly TimeSpan Open = TimeSpan.FromMilliseconds(170);

    private readonly Border _face;
    private readonly TextBlock _digits;
    private readonly PlaneProjection _projection = new();
    private Storyboard? _running;
    private ColourRole _role = ColourRole.TextPrimary;

    /// <summary>What the card says, or null when it is to be drawn at once (new, or redrawn).</summary>
    private string? _shown;

    public FlipCard()
    {
        _face = new Border { CornerRadius = Family.Radius };
        ItemFill.Paint(_face, ItemRole.Chip);

        _digits = new TextBlock
        {
            Foreground = Family.Brush(_role),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
        };

        var split = new Rectangle
        {
            Height = FamilyShape.Hairline,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = Family.Brush(ColourRole.Track),
        };

        Root = new Grid { Projection = _projection, Children = { _face, _digits, split } };

        // [S-6546] The figures are laid over the face, not inside it.
        ItemFill.OnItem(Root);
    }

    /// <summary>The card.</summary>
    public Grid Root { get; }

    /// <summary>The figures' role: the countdown's Critical once its target has gone by, disabled for an alarm that is off.</summary>
    public ColourRole Role
    {
        get => _role;
        set
        {
            if (_role != value)
            {
                _role = value;
                _digits.Foreground = Family.Brush(value);
            }
        }
    }

    /// <summary>[S-6740] Size the card for <paramref name="plan"/>: its width, the step's line tall, the figures on the step.</summary>
    public void Lay(FlipPlan plan)
    {
        Root.Width = plan.CardWidth;
        Root.Height = plan.CardHeight;
        Family.Step(_digits, plan.Step);
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
