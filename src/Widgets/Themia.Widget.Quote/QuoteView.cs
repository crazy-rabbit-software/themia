using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Quote;

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
internal sealed partial class QuoteView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetKeys, IWidgetAligned, IWidgetContextActions
{
    /// <summary>[S-1495] The header's one button, "Show another quote", wanted only with somewhere to go.</summary>
    private readonly HeaderParts _header = new();
    private readonly Border _next;

    /// <summary>[S-1496] The quote: the information, primary, wrapping, on its step.</summary>
    private readonly TextBlock _text = Family.Text(FamilyType.Body);

    /// <summary>[S-6405] The large-marks style's opening mark over the quote, in the secondary role.</summary>
    private readonly FontIcon _mark = Family.Glyph(Glyphs.Quote);

    /// <summary>[S-1496] The attribution, on the quote's line under, secondary.</summary>
    private readonly TextBlock _author = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly StackPanel _body;


    private QuoteConfig _config = new();
    private Ticker? _ticker;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;

    /// <summary>
    /// How many times the user has asked for the next one. Ephemeral on purpose: a reload comes back
    /// to the quote the rotation says it should be showing.
    /// </summary>
    private long _step;

    /// <summary>A debug render's fixed quote (by index), so a render compares; never set outside one.</summary>
#if DEBUG
    private int? _pinned;
#else
    // Release has no renders: the field's Debug-only writer (the .Preview.cs part) is not compiled.
    private int? _pinned => null;
#endif

    private bool _detached;

    public QuoteView()
    {
        _next = _header.Add(HeaderPartKind.Button, HeaderBlock.Button(Glyphs.Reset, QuoteStr.WidgetsC_Quote_ShowAnother.Text(), OnNext));

        _text.TextWrapping = TextWrapping.Wrap;
        _mark.HorizontalAlignment = HorizontalAlignment.Left;
        _mark.Margin = new Thickness(0, 0, 0, FamilySpace.S2);
        _mark.Visibility = Visibility.Collapsed;

        // [S-6703] At the view's top (the content top under the header), in the card's side inset.
        _body = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Top,
            Margin = Family.SideInset,
            Children = { _mark, _text, _author },
        };

        Children.Add(_body);

        // The whole card advances the quote, not only the button: the widget is one sentence and a
        // name, and a target that size should not need aiming at.
        //
        // Tapped rather than PointerPressed, and deliberately not marked handled. In edit mode the
        // card captures the pointer on press to drag the widget, so the gesture never completes here
        // and no quote changes while a widget is being moved. See ScratchpadView for the whole story.
        Tapped += OnTapped;
    }

    /// <summary>
    /// The "show another quote" button, in the card's header (the Header block's one button). Built in
    /// the constructor because the card reads this once, when it builds that header.
    /// </summary>
    public FrameworkElement? HeaderTrailing => _header;

    /// <summary>
    /// [S-5711] Show another quote on the card's right-click menu too, with two quotes or more: the widget
    /// starts headerless ([S-2590]), and the header's button is then not there to press.
    /// </summary>
    public IReadOnlyList<WidgetAction> ContextActions => !_detached && _config.EffectiveQuotes.Count > 1
        ? [new(Glyphs.Reset, QuoteStr.WidgetsC_Quote_ShowAnother.Text(), OnNext)]
        : [];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1110] Re-read the fixed texts and set the quote again, in the new language's marks.</summary>
    private void OnLanguage(Locale locale)
    {
        ToolTipService.SetToolTip(_next, QuoteStr.WidgetsC_Quote_ShowAnother.Text());
        AutomationProperties.SetName(_next, QuoteStr.WidgetsC_Quote_ShowAnother.Text());
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as QuoteConfig ?? new QuoteConfig();

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
    private void OnNext()
    {
        if (_detached)
        {
            return;
        }

        var count = _config.EffectiveQuotes.Count;
        var period = QuoteRotation.Period(DateTimeOffset.Now, _config.Cadence) + _step;
        _step += QuoteRotation.RandomStep(count, period, _config.Seed, _config.Shuffle, Random.Shared.Next());
        Render();
    }

    /// <summary>A click shows the next quote, and is the widget's own ([S-2004]).</summary>
    private void OnTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;
        ShowNext();
    }

    private void ShowNext()
    {
        _step++;
        Render();
    }

    /// <summary>[S-1990] Only a tile with somewhere to go takes the keyboard on a click.</summary>
    public bool WantsKeys => !_detached && _config.EffectiveQuotes.Count > 1;

    /// <summary>[S-1990] Right, Space or Enter the next quote, Left the one before (<see cref="QuoteKeys.Of"/>).</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        switch (QuoteKeys.Of(key, _config.EffectiveQuotes.Count))
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
        var index = _pinned is { } pinned && pinned < quotes.Count ? pinned : QuoteRotation.Select(quotes.Count, period, _config.Seed, _config.Shuffle);

        if (index < 0)
        {
            // An empty list is a real state — the user cleared it — not a failure to load. The card
            // shows its Set up button over this ([S-1862]).
            _body.Visibility = Visibility.Collapsed;
            _header.Want(_next, false);
            return;
        }

        var quote = quotes[index];

        _body.Visibility = Visibility.Visible;

        // One quote has nowhere to go, and a button that does nothing is worse than none.
        _header.Want(_next, quotes.Count > 1);

        _text.Text = QuoteLayout.Set(Strings.Current, quote.Text, _config.Style);
        _author.Text = QuoteLayout.Attribution(quote.Author);

        Lay();
    }

    /// <summary>[S-6583] Where the mark, the quote and its author sit: the widget's alignment, from the start by default ([S-6582]).</summary>
    private ContentAlign _align = ContentAlign.Left;

    /// <summary>[S-6583] The widget's alignment, from the properties panel.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        if (align == _align)
        {
            return;
        }

        _align = align;
        Lay();
    }

    /// <summary>
    /// [S-11306] The quote on the step its Size names, in at most its Lines (a longer one ends in an
    /// ellipsis), the attribution on the step's line under while it is shown, the large mark on the
    /// Marks style ([S-6405]); nothing else at any size.
    /// </summary>
    private void Lay()
    {
        if (_text.Text.Length == 0)
        {
            return;
        }

        // [S-6583] The mark, the quote's lines and the author where the widget's alignment puts them.
        _mark.HorizontalAlignment = Aligned.Horizontal(_align);
        _text.TextAlignment = Aligned.Text(_align);
        _author.TextAlignment = Aligned.Text(_align);
        _author.HorizontalAlignment = Aligned.Horizontal(_align);

        // [S-11306] The step, the lines and the mark are the config's; the attribution's line is kept
        // while it is shown, so a quote without an author moves nothing.
        var step = QuoteLayout.StepAt(_config.Size);
        var mark = QuoteLayout.MarkOf(_config);
        _mark.Visibility = Show(mark > 0);
        if (mark > 0)
        {
            _mark.FontSize = mark;
            _mark.Width = mark;
            _mark.Height = mark;
        }

        Family.Step(_text, step);
        _text.MaxLines = QuoteLayout.LinesOf(_config);

        var under = FamilyType.Under(step);
        Family.Step(_author, under);
        _author.MinHeight = under.LineHeight;
        _author.Visibility = Show(_config.ShowAuthor);
    }

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

        Tapped -= OnTapped;
    }
}
