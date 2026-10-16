using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// A line worth rereading, on a rotation.
/// <para>
/// Offline by construction: the quotes are the user's own list, seeded with a bundled set, and
/// which one shows is a pure function of the clock, the seed and how many times the widget has been
/// clicked — see <see cref="QuoteRotation"/>. There is no quote service and nothing to be signed in
/// to, which is why this widget is as good on a train as on a desk.
/// </para>
/// <para>
/// Clicking shows the next one. That step is deliberately <b>not</b> persisted: it is a nudge, not a
/// setting, and writing to the state file every time someone clicks a quote would be a file write
/// per click for something that is supposed to come back to today's quote in the morning.
/// </para>
/// </summary>
internal sealed partial class QuoteView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSizeAware, IWidgetKeys
{
    private readonly Button _next;

    private readonly TextBlock _text = Fluent.Text(FluentType.Body);

    /// <summary>[S-6405] The large-marks style's opening mark, in the accent, over the quote.</summary>
    private readonly FontIcon _mark = new()
    {
        Glyph = Glyphs.Quote,
        Foreground = FluentLayout.Themed("AccentTextFillColorPrimaryBrush", Colors.SteelBlue),
        HorizontalAlignment = HorizontalAlignment.Left,
        MirroredWhenRightToLeft = true,
        Visibility = Visibility.Collapsed,
    };

    private readonly TextBlock _author = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly Grid _body;

    private WidgetArea _area;


    private QuoteConfig _config = new();
    private Ticker? _ticker;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;

    /// <summary>
    /// How many times the user has asked for the next one. Ephemeral on purpose: a reload comes back
    /// to the quote the rotation says it should be showing.
    /// </summary>
    private long _step;

    private bool _detached;

    public QuoteView()
    {
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _next = CommandButton(Glyphs.Reset, Str.WidgetsC_Quote_ShowAnother.Text());
        _next.Click += OnNextClicked;

        _body = BuildBody();

        Children.Add(_body);

        // The whole card advances the quote, not only the button: the widget is one sentence and a
        // name, and a target that size should not need aiming at.
        //
        // Tapped rather than PointerPressed, and deliberately not marked handled. In edit mode the
        // card captures the pointer on press to drag the widget, so the gesture never completes here
        // and no quote changes while a widget is being moved. See ScratchpadView for the whole story.
        Tapped += OnTapped;
    }

    private Grid BuildBody()
    {
        _text.TextWrapping = TextWrapping.Wrap;
        _text.TextTrimming = TextTrimming.CharacterEllipsis;

        _author.TextWrapping = TextWrapping.NoWrap;
        _author.TextTrimming = TextTrimming.CharacterEllipsis;
        _author.Margin = new Thickness(0, Fluent.Space2, 0, 0);

        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(WidgetLayout.ContentInset, 0, WidgetLayout.ContentInset, WidgetLayout.ContentInset),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
        };

        SetRow(_mark, 0);
        SetRow(_text, 1);
        SetRow(_author, 2);
        grid.Children.Add(_mark);
        grid.Children.Add(_text);
        grid.Children.Add(_author);

        return grid;
    }

    /// <summary>
    /// A header command: a glyph with the stock subtle fill behind it and nothing at rest.
    /// <para>
    /// The brushes go in through the template's own resource keys rather than onto
    /// <c>Background</c>, because the stock button's visual states assign the pointer-over and
    /// pressed fills straight onto the border element and would outrank a local value. Same reason
    /// as the note editor: see <c>NotesView.BuildEditor</c>.
    /// </para>
    /// </summary>
    private static Button CommandButton(string glyph, string tooltip)
    {
        var clear = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);

        var button = new Button
        {
            Content = Fluent.Icon(glyph),
            Padding = new Thickness(Fluent.Space1),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // [S-6513] The fills are the item layer's row, in each state.
        ItemFill.Button(button, "Button");
        button.Resources["ButtonBorderBrush"] = clear;
        button.Resources["ButtonBorderBrushPointerOver"] = clear;
        button.Resources["ButtonBorderBrushPressed"] = clear;
        button.Resources["ButtonBorderBrushDisabled"] = clear;

        ToolTipService.SetToolTip(button, tooltip);
        return button;
    }

    /// <summary>
    /// The "show another quote" command, in the card's header. Built in the constructor because the
    /// card reads this once, when it builds that header.
    /// </summary>
    public FrameworkElement? HeaderTrailing => _next;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1110] Re-read the fixed texts and set the quote again, in the new language's marks.</summary>
    private void OnLanguage(Locale locale)
    {
        ToolTipService.SetToolTip(_next, Str.WidgetsC_Quote_ShowAnother.Text());
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as QuoteConfig ?? new QuoteConfig();

        _author.Visibility = _config.ShowAuthor ? Visibility.Visible : Visibility.Collapsed;

        Subscribe();
        Render();
    }

    /// <summary>
    /// Wake only as often as the cadence needs, and not at all for a quote that only changes when it
    /// is clicked. A widget that is a sentence has no business waking the process once a second.
    /// </summary>
    private void Subscribe()
    {
        var wanted = _config.TickInterval;

        if (_detached || _ticker is null || wanted == _subscribedInterval)
        {
            return;
        }

        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = wanted;

        if (wanted > TimeSpan.Zero)
        {
            _subscription = _ticker.Subscribe(wanted, Render);
        }
    }

    /// <summary>
    /// [S-1495] The header button shows a <i>random</i> other quote, as the original's shuffle did;
    /// a tap on the quote itself steps to the next one. Both are ephemeral (S-02).
    /// </summary>
    private void OnNextClicked(object sender, RoutedEventArgs args)
    {
        var count = _config.EffectiveQuotes.Count;
        var period = QuoteRotation.Period(DateTimeOffset.Now, _config.Cadence) + _step;
        _step += QuoteRotation.RandomStep(count, period, _config.Seed, _config.Shuffle, Random.Shared.Next());
        Render();
    }

    /// <summary>
    /// Deliberately not marked handled: the card's own double-click still has to reach it, which is
    /// how the desktop is put into edit mode. The cost is that a double-click advances twice, which
    /// is a quote, and the benefit is that the gesture works the same on every widget.
    /// </summary>
    private void OnTapped(object sender, TappedRoutedEventArgs args) => ShowNext();

    private void ShowNext()
    {
        _step++;
        Render();
    }

    /// <summary>[S-1990] Only a tile with somewhere to go takes the keyboard on a click.</summary>
    public bool WantsKeys => !_detached && _config.EffectiveQuotes.Count > 1;

    /// <summary>[S-1990] Right, Space or Enter the next quote, Left the one before (<see cref="ControlKeys.Quote"/>).</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        switch (ControlKeys.Quote(key, _config.EffectiveQuotes.Count))
        {
            case ControlKey.Next:
                ShowNext();
                return true;

            case ControlKey.Previous:
                _step--;
                Render();
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Pick the quote for right now and set it. Cheap enough to run on every tick and every resize,
    /// because choosing is arithmetic and the two text blocks are already built.
    /// </summary>
    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var quotes = _config.EffectiveQuotes;
        var period = QuoteRotation.Period(DateTimeOffset.Now, _config.Cadence) + _step;
        var index = QuoteRotation.Select(quotes.Count, period, _config.Seed, _config.Shuffle);

        if (index < 0)
        {
            // An empty list is a real state — the user cleared it — not a failure to load. The card
            // shows its Set up button over this ([S-1862]).
            _body.Visibility = Visibility.Collapsed;
            _next.IsEnabled = false;
            return;
        }

        var quote = quotes[index];

        _body.Visibility = Visibility.Visible;

        // One quote has nowhere to go, and a button that visibly does nothing is worse than one
        // that says so.
        _next.IsEnabled = quotes.Count > 1;

        _text.Text = QuoteLayout.Set(Strings.Current, quote.Text, _config.Style);
        _author.Text = QuoteLayout.Attribution(quote.Author);
        _author.Visibility = _config.ShowAuthor && _author.Text.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        Lay();
    }

    /// <summary>[S-663] The card's word on how much room the quote has.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>
    /// [S-210]/[S-653]/[S-1496] The ladder. The quote is the widget, so the attribution is planned
    /// around it and given up for it; nothing else is drawn (no rotation caption). What is left sets the quote at the largest ramp step a paragraph of it fits
    /// at — up to TitleLarge, and Display on a tile Huge both ways — and a quote too long even at the
    /// floor ends in an ellipsis on the last line that fits.
    /// </summary>
    private void Lay()
    {
        var box = _area.Deflate(WidgetLayout.ContentInset, WidgetLayout.ContentInset / 2);
        var characters = _text.Text.Length;

        if (!_area.IsMeasured || characters == 0)
        {
            return;
        }

        // [S-6405] The large mark takes its row off the top before the quote is fitted to the rest.
        var marks = _config.Style == QuoteStyle.Marks;
        _mark.Visibility = Show(marks);
        if (marks)
        {
            var mark = QuoteLayout.MarkSize(_area);
            _mark.FontSize = mark;
            _mark.Height = mark;
            _mark.Margin = new Thickness(0, 0, 0, Fluent.Space1);
            box = new WidgetArea(box.Width, Math.Max(0, box.Height - mark - Fluent.Space1));
        }

        var wantAuthor = _config.ShowAuthor && _author.Text.Length > 0;
        var ceiling = _area.AtLeast(SizeClass.Huge, SizeClass.Huge) ? FluentType.Display : FluentType.TitleLarge;

        var plan = Plan(box, characters, wantAuthor, ceiling);

        if (!plan.Fits && wantAuthor)
        {
            plan = Plan(box, characters, author: false, ceiling);
        }

        _author.Visibility = Show(plan.Author);

        _text.Apply(plan.Step);
        _text.MaxLines = Math.Max(1, (int)Math.Floor((plan.TextHeight + Disclosure.Tolerance) / plan.Step.LineHeight));

        // The attribution sits one step down, never above Body: it is a name, not the quote.
        _author.Apply(AuthorStep(plan.Step));
    }

    private readonly record struct QuotePlan(TypeStep Step, double TextHeight, bool Author, bool Fits);

    private static QuotePlan Plan(WidgetArea box, int characters, bool author, TypeStep ceiling)
    {
        var room = new Disclosure(box, Fluent.Space2);
        room.Reserve(FluentType.Caption.LineHeight);

        var showAuthor = author && room.Take(FluentType.Body.LineHeight);

        var height = room.Remaining + FluentType.Caption.LineHeight;
        var step = QuoteLayout.Fit(box.Width, height, characters, FluentType.Caption, ceiling);
        var fits = QuoteLayout.Capacity(step, box.Width, height) >= characters;

        return new QuotePlan(step, height, showAuthor, fits);
    }

    private static TypeStep AuthorStep(TypeStep quote)
        => quote.FontSize > FluentType.Body.FontSize ? FluentType.Body : FluentType.Caption;

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
        _ticker = null;

        _next.Click -= OnNextClicked;
        Tapped -= OnTapped;
    }
}
