using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Services.Office;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.NextMeeting;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4820] [S-11407] The one event that matters now: the next one (or the one under way) from the
/// synced calendars, its title, time and the lines its config picks (place, organiser), with the
/// countdown over Join at the end side when it is an online meeting. The meetings after it are the
/// After this widget's ([S-11408]).
/// <para>
/// It reads through the meeting atoms' feed (<see cref="MeetingFeed"/> over the calendars' seam, the
/// offline store and the shared sync behind it, [S-2701]) and never fetches on its own: a 5-minute poll
/// and the accounts' change event, as the calendars. Which event is <see cref="NextMeetingRules"/>; the
/// minute tick only re-picks and rewords from what is in hand. [S-4825] On a view, pointing at it
/// lights the event in the calendar widgets there (<see cref="LinkTopics.HoveredEvent"/>).
/// </para>
/// </summary>
internal sealed partial class NextMeetingView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => NextMeetingModule.Opens(_config.AccountIds, _widgetHost.AccountsWith(HostFeatures.Calendar));

    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private readonly KitStatePanel _state;

    /// <summary>
    /// [S-5710] [S-11407] The meeting: its lines a column at the start, the end column (the countdown over
    /// Join) beside them at every width. The bottom inset is the content's (the top one the card's, [S-5700]).
    /// [S-12410] [S-12814] One block, scaled up as one in its fit (<see cref="_fit"/>), its title's ink on the top inset.
    /// </summary>
    private readonly Grid _content = new()
    {
        VerticalAlignment = VerticalAlignment.Top,
        Visibility = Visibility.Collapsed,
        ColumnDefinitions =
        {
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            new ColumnDefinition { Width = GridLength.Auto },
        },
    };

    /// <summary>[S-12814] The block in the card: the sides' and the foot's insets, scaled up as one, no band over it.</summary>
    private readonly ReadingFit _fit = new();

    private readonly StackPanel _text = new() { Spacing = NextMeetingJoin.LineGap, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>[S-14261] The lines, cut at the width the block gives them, keeping their least card's room beside the end column.</summary>
    private readonly CutLine _cut;

    /// <summary>[S-5710] The end column: the countdown over Join, one unit (how long, then the action), end-aligned.</summary>
    private readonly StackPanel _end = new() { Spacing = NextMeetingJoin.Stack, HorizontalAlignment = HorizontalAlignment.Right };

    /// <summary>
    /// [S-5710] The countdown, "in 12 min", "Now · ends 11:30 AM": BodyStrong in the primary role, at the
    /// end side; the accent is for on and selected only, never a text's ([S-6706]).
    /// </summary>
    private readonly TextBlock _when = Family.Text(FamilyType.BodyStrong, ColourRole.TextPrimary);

    /// <summary>[S-5710] Measures the countdown's wordings off the tree, on its step.</summary>
    private readonly TextBlock _whenProbe = Family.Text(FamilyType.BodyStrong, ColourRole.TextPrimary);

    /// <summary>The one prominent line, and the first ([S-6700]: Title).</summary>
    private readonly TextBlock _title = Family.Text(FamilyType.Title, ColourRole.TextPrimary);

    private readonly (Grid Row, TextBlock Text) _range = DetailLine(Glyphs.Clock);
    private readonly (Grid Row, TextBlock Text) _location = DetailLine(Glyphs.Place);
    private readonly (Grid Row, TextBlock Text) _organizer = DetailLine(Glyphs.Contact);

    /// <summary>[S-2591] Join: the one main action ([S-6716]), under the countdown.</summary>
    private readonly ControlsBlock _controls = new();
    private readonly Button _join;

    private readonly MeetingFeed _feed;
    private IWidgetSurface? _surface;
    private IDisposable? _poll;
    private IDisposable? _tick;
    private LinkMember? _member;

    /// <summary>[S-11962] The pointer is on the card's content: the card shows its hover.</summary>
    private bool _pointing;
    private NextMeetingConfig _config = new();
    private CalendarEvent? _shown;
    private bool _detached;

    /// <summary>[S-2050] The clock the countdown reads: the real one, or a preview's sample moment.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;

    /// <summary>[S-6913] What the view asks of the host: the calendars and the launcher.</summary>
    private readonly IWidgetHost _widgetHost;

    public NextMeetingView(IWidgetHost host)
    {
        _widgetHost = host;
        // [S-5710] The calendar's glyph: with no header the state row says what the card is about.
        // [S-14262] Only its line: the meeting's face stands under it, no list's rows (the block is no list).
        _state = new(Glyphs.Calendar, host) { Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Top };
        _feed = new MeetingFeed(() => _config);
        _feed.Changed += Render;

        _join = _controls.Main(NextMeetingStr.Meeting_Join.Text(), Join);
        _join.Loaded += OnJoinLoaded;
        _controls.HorizontalAlignment = HorizontalAlignment.Right;

        _cut = new CutLine(_text);

        // [S-12833] In a smaller card the meeting's lines and the end column give way before its title.
        _text.Children.Add(_title);
        _text.Children.Add(_fit.GivesWayBox(_range.Row, FamilyType.Body.FontSize));
        _text.Children.Add(_fit.GivesWayBox(_location.Row, FamilyType.Body.FontSize));
        _text.Children.Add(_fit.GivesWayBox(_organizer.Row, FamilyType.Body.FontSize));

        _when.HorizontalAlignment = HorizontalAlignment.Right;
        _when.TextAlignment = TextAlignment.Right;
        _end.Children.Add(_when);
        _end.Children.Add(_controls);
        var end = _fit.GivesWayBox(_end, FamilyType.BodyStrong.FontSize);
        SetColumn(end, 1);

        // [S-14301] The gap is the end column's own: where the column gives way its gap goes with it and the title takes
        // the whole width (a grid's column spacing stayed, so the cut title stood 40 short of the end at 350x70).
        end.Margin = new Thickness(NextMeetingJoin.Gap, 0, 0, 0);

        // [S-14261] The lines are cut with their ellipsis at the width the block gives them, never stopping its scale.
        _content.Children.Add(_cut);
        _content.Children.Add(end);
        _content.Background = Family.Brush(ColourRole.Clear);
        _content.PointerEntered += OnPointerEntered;
        _content.PointerExited += OnPointerExited;
        _content.Tapped += OnTapped;
        _organizer.Row.Background = Family.Brush(ColourRole.Clear);
        _organizer.Row.PointerEntered += OnOrganizerEntered;
        _organizer.Row.PointerExited += OnOrganizerExited;

        _fit.Parts.Add(_content);

        // [S-14301] Its lines are cut to the width they are given and the end column stands at the end: the row runs across.
        _fit.Span(_content);
        Children.Add(_fit);
        Children.Add(_state);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as NextMeetingConfig ?? new NextMeetingConfig();
        _fit.Face = NextMeetingJoin.Face(_config);
        _feed.Start();
        _feed.Use(_widgetHost.Office is { } source ? new OfficeCalendarData(source) : null);
        Strings.Changed += OnLanguage;

        // [S-4825] The view's links: the event under the pointer is shared with the calendars there.
        _member = _surface?.Links?.Join();
        _member?.Watch(LinkTopics.HoveredEvent, OnHoveredElsewhere);

        // [S-588] The poll fires once on subscribing: the first load.
        _poll = ticker.Subscribe(Poll, () => _ = _feed.RefreshAsync(force: false));
        _tick = ticker.Subscribe(Minute, Render);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as NextMeetingConfig ?? new NextMeetingConfig();
        _fit.Face = NextMeetingJoin.Face(_config);
        Render();
        _ = _feed.RefreshAsync(force: false);
    }

    /// <summary>Refresh, as every calendar widget has.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => [new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => _ = _feed.RefreshAsync(force: true))];

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        _poll?.Dispose();
        _poll = null;
        _tick?.Dispose();
        _tick = null;
        _feed.Stop();
        _member?.Dispose();
        _member = null;
        _pointing = false;
        LightCard();

        _content.PointerEntered -= OnPointerEntered;
        _content.PointerExited -= OnPointerExited;
        _content.Tapped -= OnTapped;
        _organizer.Row.PointerEntered -= OnOrganizerEntered;
        _organizer.Row.PointerExited -= OnOrganizerExited;
        _join.Loaded -= OnJoinLoaded;
        _state.Detach();
    }

    private void OnLanguage(Locale locale)
    {
        if (_join.Content is TextBlock label)
        {
            label.Text = NextMeetingStr.Meeting_Join.Text();
        }

        Render();
    }

    // -----------------------------------------------------------------------------------------
    // Drawing
    // -----------------------------------------------------------------------------------------

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        switch (_feed.Screen)
        {
            case MeetingScreen.Loading:
                _state.Show(Str.Common_Loading.Text(), offerAccounts: false);
                ShowFace();
                return;

            case MeetingScreen.NoAccounts:
                // [S-1862] The card shows its Set up row over this, and the view stands under it.
                _state.Hide();
                ShowFace();
                return;

            case MeetingScreen.AccountsGone:
                _state.Show(SyncProblems.Message(SyncProblem.NoAccount, SyncSubject.Calendar, Strings.Current), offerAccounts: true, problem: true);
                ShowFace();
                return;

            case MeetingScreen.Problem:
                _state.Show(_feed.ProblemText(), _feed.ProblemOffersAccounts, problem: true);
                ShowFace();
                return;
        }

        var now = _clock();
        var item = _feed.Shown(now);
        SetShown(item);

        if (item is null)
        {
            // [S-4822] Nothing coming up: the state row, where the meeting's first line would be, over the block's face.
            _state.Show(NextMeetingStr.Meeting_None.Text(), offerAccounts: false);
            ShowFace();
            return;
        }

        _state.Hide();
        Paint(available: true);
        _fit.Margin = _fit.Margin with { Top = 0 };
        var locale = Strings.Current;
        var zone = TimeZoneInfo.Local;
        var hasJoin = item.JoinUrl is not null;

        // [S-5710] The end column is as wide as the countdown's widest wording in its form, so it never moves.
        _end.MinWidth = NextMeetingJoin.EndWidth(CountdownWidth(NextMeetingJoin.Wordings(locale, item, now, zone)), hasJoin ? JoinWidth() : 0, hasJoin);
        _cut.Least = NextMeetingJoin.TextLeast(_end.MinWidth);
        _end.VerticalAlignment = hasJoin ? VerticalAlignment.Center : VerticalAlignment.Top;
        _when.Margin = hasJoin ? new Thickness(0) : new Thickness(0, NextMeetingJoin.TitleLineOffset, 0, 0);

        // Without Join the lines start at the top too, so the countdown stands on the title's line.
        _text.VerticalAlignment = hasJoin ? VerticalAlignment.Center : VerticalAlignment.Top;

        _when.Text = NextMeetingRules.When(locale, item, now, zone);
        _title.Text = string.IsNullOrWhiteSpace(item.Subject) ? Str.Calendar_NoTitle.Text() : item.Subject;
        PartInk.InkStart(_title);
        _range.Text.Text = NextMeetingRules.Range(locale, item, zone);
        _location.Text.Text = item.Location ?? string.Empty;
        _location.Row.Visibility = Show(_config.ShowPlace && !string.IsNullOrWhiteSpace(item.Location));
        _organizer.Text.Text = item.Organizer ?? string.Empty;
        _organizer.Row.Visibility = Show(_config.ShowOrganizer && !string.IsNullOrWhiteSpace(item.Organizer));
        LightCard();

        _controls.Visibility = Show(hasJoin);
        ToolTipService.SetToolTip(_join, NextMeetingStr.Meeting_JoinTip.Text());
        ToolTipService.SetToolTip(_text, Describe(item, locale, zone));
        _content.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// [S-14262] With no meeting to show (nothing coming up, loading, a problem, no account) the block keeps its face (owner,
    /// 2026-11-19, WR-10: "No data, no account, not set up, offline: the widget keeps its face (its graph, its rows, its
    /// month, its reading as "--") and says why in a compact line; it never swaps its face for a status line alone"): its
    /// title and the lines its config shows read "--" in the unavailable role, no countdown, no Join, under the state's line
    /// (or the card's Set up line); scaled as the meeting is, it fills the card under the line, so no band is left under it.
    /// </summary>
    private void ShowFace()
    {
        SetShown(null);
        Paint(available: false);
        _end.MinWidth = 0;
        _cut.Least = NextMeetingJoin.TextLeast(0);
        _end.VerticalAlignment = VerticalAlignment.Top;
        _when.Margin = new Thickness(0, NextMeetingJoin.TitleLineOffset, 0, 0);
        _when.Text = EmptyRows.Placeholder;
        _text.VerticalAlignment = VerticalAlignment.Top;
        _title.Text = EmptyRows.Placeholder;
        PartInk.InkStart(_title);
        _range.Text.Text = EmptyRows.Placeholder;
        _location.Text.Text = EmptyRows.Placeholder;
        _location.Row.Visibility = Show(_config.ShowPlace);
        _organizer.Text.Text = EmptyRows.Placeholder;
        _organizer.Row.Visibility = Show(_config.ShowOrganizer);
        _controls.Visibility = Visibility.Collapsed;
        ToolTipService.SetToolTip(_text, null);

        // Under the state's line, which stands on the card's top edge in whole cells; the card's own Set up line lays the
        // view under it already.
        var under = _state.Visibility == Visibility.Visible ? LineRules.EdgeRow(ItemGrids.Of(this)) - FamilySpace.Inset : 0;
        _fit.Margin = _fit.Margin with { Top = Math.Max(0, under) };
        _content.Visibility = Visibility.Visible;
    }

    /// <summary>[S-14262] The block's texts in their roles, or in the unavailable role while it shows its face with nothing in it.</summary>
    private void Paint(bool available)
    {
        _title.Foreground = available ? Family.Brush(ColourRole.TextPrimary) : UnavailableLook.Text;
        _when.Foreground = available ? Family.Brush(ColourRole.TextPrimary) : UnavailableLook.Text;
        foreach (var (row, text) in new[] { _range, _location, _organizer })
        {
            text.Foreground = available ? Family.Brush(ColourRole.TextSecondary) : UnavailableLook.Text;
            foreach (var glyph in row.Children.OfType<FontIcon>())
            {
                glyph.Foreground = available ? Family.Brush(ColourRole.TextSecondary) : UnavailableLook.Text;
            }
        }
    }

    /// <summary>
    /// [S-2591] Join's width as the button measures it in this language (its template's own minimum
    /// and padding included); before its template is applied, its label on the Body step and the
    /// family's padding either side, and the button's first layout draws again (<see cref="OnJoinLoaded"/>).
    /// </summary>
    private double JoinWidth()
    {
        _join.Measure(new Windows.Foundation.Size(double.PositiveInfinity, FamilyShape.Button));
        if (_join.DesiredSize.Width > 0)
        {
            return _join.DesiredSize.Width;
        }

        if (_join.Content is not TextBlock label)
        {
            return 0;
        }

        label.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        return label.DesiredSize.Width + (2 * FamilyShape.ButtonPadding);
    }

    /// <summary>[S-2591] Join measures its real width once laid out: draw again on that.</summary>
    private void OnJoinLoaded(object sender, RoutedEventArgs args) => Render();

    /// <summary>
    /// [S-5710] The countdown's widest wording in its form, measured on the countdown's own step and font
    /// (the text size may have changed it), so the end column does not move as the minutes go.
    /// </summary>
    private double CountdownWidth(IReadOnlyList<string> wordings)
    {
        _whenProbe.FontSize = _when.FontSize;
        _whenProbe.FontWeight = _when.FontWeight;
        _whenProbe.FontFamily = _when.FontFamily;
        var widest = 0.0;
        foreach (var wording in wordings)
        {
            _whenProbe.Text = wording;
            _whenProbe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, Math.Ceiling(_whenProbe.DesiredSize.Width));
        }

        return widest;
    }

    private void SetShown(CalendarEvent? item)
    {
        if (_shown is { } old && (item is null || CalendarShare.Key(old) != CalendarShare.Key(item)))
        {
            _member?.Clear(LinkTopics.HoveredEvent);
            _member?.Clear(LinkTopics.HoveredPerson);
        }

        _shown = item;
        _content.Opacity = 1;
        LightCard();
    }

    private static string Describe(CalendarEvent item, Locale locale, TimeZoneInfo zone)
    {
        var lines = new List<string>
        {
            string.IsNullOrWhiteSpace(item.Subject) ? Str.Calendar_NoTitle.Text() : item.Subject,
            NextMeetingRules.Range(locale, item, zone),
        };

        if (!string.IsNullOrWhiteSpace(item.Location))
        {
            lines.Add(item.Location);
        }

        if (!string.IsNullOrWhiteSpace(item.Organizer))
        {
            lines.Add(item.Organizer);
        }

        return string.Join(Environment.NewLine, lines);
    }

    // -----------------------------------------------------------------------------------------
    // Input and links
    // -----------------------------------------------------------------------------------------

    private void Join()
    {
        if (_surface?.IsPreview == true || _shown?.JoinUrl is not { } link)
        {
            return;
        }

        _widgetHost.Launcher.OpenMeetingLink(link);
    }

    /// <summary>A click on the event opens it in its calendar, as a calendar widget's does ([S-1268]).</summary>
    private void OnTapped(object sender, TappedRoutedEventArgs args)
    {
        if (_surface?.IsPreview == true || _surface?.EditMode == true || _shown is not { } item || args.OriginalSource is DependencyObject source && IsInJoin(source))
        {
            return;
        }

        ProviderId? provider = _feed.Providers.TryGetValue(item.AccountId, out var known) ? known : null;
        if (CalendarLinks.Open(item, provider, provider == HostProviders.Microsoft && _widgetHost.Launcher.OutlookInstalled) is { } link)
        {
            // [S-2004] The meeting's own click: the card does not open the calendar over it.
            args.Handled = true;
            _widgetHost.Launcher.OpenCalendarLink(link);
        }
    }

    private bool IsInJoin(DependencyObject source)
    {
        for (var node = source; node is not null; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, _join))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>[S-4825] Pointing at the event lights it in the calendar widgets on the view.</summary>
    private void OnPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        _pointing = true;
        if (_shown is { } item)
        {
            _member?.Set(LinkTopics.HoveredEvent, CalendarShare.Key(item));
        }

        LightCard();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs args)
    {
        _pointing = false;
        _member?.Clear(LinkTopics.HoveredEvent);
        LightCard();
    }

    /// <summary>
    /// [S-4825] [S-11903] Another widget points at an event: when it is this one's, the whole card takes the
    /// item layer's hover fill; another event changes nothing here (this widget does not show it).
    /// </summary>
    private void OnHoveredElsewhere(EventLink? value) => LightCard();

    /// <summary>
    /// [S-11903] [S-11962] [S-11965] The whole card takes the item layer's hover fill under the pointer, and while its
    /// event is pointed at elsewhere on the view; anything else, its organiser pointed at among them, changes nothing
    /// here (the card is the meeting, not the people in it). [S-11964] What is pointed at is what the board holds now,
    /// never a value remembered from the last change heard. [S-11963] The fill is the card's, over its whole box.
    /// </summary>
    private void LightCard()
    {
        var pointedEvent = _member?.Get(LinkTopics.HoveredEvent);
        var lit = _shown is { } item
            && (_pointing || (pointedEvent is not null && pointedEvent.Equals(CalendarShare.Key(item))));
        LinkLook.Card(this, lit ? LinkLight.Pointed : LinkLight.None);
    }

    /// <summary>
    /// [S-11944] Pointing at the organiser's line publishes the organiser, a person (and, as the card, the meeting): the
    /// contact that is them lights. Nothing lights the line or the card for a person.
    /// </summary>
    private void OnOrganizerEntered(object sender, PointerRoutedEventArgs args)
    {
        if (_shown is { } item && CalendarShare.Person(item) is { } person)
        {
            _member?.Set(LinkTopics.HoveredPerson, person);
        }
    }

    private void OnOrganizerExited(object sender, PointerRoutedEventArgs args) => _member?.Clear(LinkTopics.HoveredPerson);

    /// <summary>A line of the meeting: its 16 glyph, 8, the text in Body secondary.</summary>
    private static (Grid Row, TextBlock Text) DetailLine(string glyph)
    {
        var text = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
        text.VerticalAlignment = VerticalAlignment.Center;
        var icon = Family.Glyph(glyph);
        icon.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };

        SetColumn(text, 1);
        row.Children.Add(icon);
        row.Children.Add(text);
        return (row, text);
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
