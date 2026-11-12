using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Services.Office;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
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
/// [S-4820] The one event that matters now: the next one (or the one under way) from the synced
/// calendars, with when, title, time, place and organiser as there is room, and Join when it is an
/// online meeting.
/// <para>
/// It reads through the calendar widgets' seam (<see cref="ICalendarData"/>, the offline store and
/// the shared sync behind it, [S-2701]) and never fetches on its own: a 5-minute poll and the
/// accounts' change event, as the calendars. Which event is <see cref="NextMeetingRules"/>; the
/// minute tick only re-picks and rewords from what is in hand. [S-4825] On a view, pointing at it
/// lights the event in the calendar widgets there (<see cref="LinkTopics.HoveredEvent"/>).
/// </para>
/// </summary>
internal sealed partial class NextMeetingView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => NextMeetingModule.Opens(_config.AccountIds, _widgetHost.AccountsWith(HostFeatures.Calendar));

    private const string Scope = "next-meeting";

    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    /// <summary>[S-4825] How far the event fades while another widget points at a different one.</summary>
    private const double DimOpacity = 0.35;

    private readonly KitStatePanel _state;

    /// <summary>
    /// [S-4824] [S-2160] The meeting: its lines from the content top, the meetings after it under them,
    /// Join in the Controls row at the foot. The bottom inset is the content's; the side inset is each
    /// part's, so the following meetings' rows reach both edges as every Row does.
    /// </summary>
    private readonly Grid _content = new() { Margin = new Thickness(0, 0, 0, FamilySpace.Inset), Visibility = Visibility.Collapsed };

    private readonly StackPanel _text = new() { Spacing = NextMeetingLayout.Gap, Margin = Family.SideInset };

    /// <summary>[S-2160] The meetings after this one: a Caption label in the secondary role over one-line Rows.</summary>
    private readonly StackPanel _after = new() { Margin = new Thickness(0, NextMeetingFollowing.Gap, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock _afterLabel = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly List<RowBlock> _afterRows = [];

    /// <summary>"in 12 min.", "Now · ends 11:30 AM": the secondary role; the accent is never a text's ([S-6706]).</summary>
    private readonly TextBlock _when = Family.Text(FamilyType.Body, ColourRole.TextSecondary);

    /// <summary>The one prominent line ([S-6700]: Title).</summary>
    private readonly TextBlock _title = Family.Text(FamilyType.Title, ColourRole.TextPrimary);

    private readonly (Grid Row, TextBlock Text) _range = DetailLine(Glyphs.Clock);
    private readonly (Grid Row, TextBlock Text) _location = DetailLine(Glyphs.Place);
    private readonly (Grid Row, TextBlock Text) _organizer = DetailLine(Glyphs.Contact);

    /// <summary>[S-4824] Join: the one main action ([S-6716]), at the content's foot.</summary>
    private readonly ControlsBlock _controls = new();
    private readonly Button _join;

    private IWidgetSurface? _surface;
    private ICalendarData? _data;
    private IDisposable? _poll;
    private IDisposable? _tick;
    private LinkMember? _member;
    private NextMeetingConfig _config = new();
    private WidgetArea _area;
    private Screen _screen = Screen.Loading;
    private Func<string> _problemText = () => string.Empty;
    private bool _problemOffersAccounts;
    private IReadOnlyList<CalendarEvent> _events = [];
    private IReadOnlyDictionary<string, ProviderId> _providers = new Dictionary<string, ProviderId>();
    private CalendarEvent? _shown;
    private bool _refreshing;
    private bool _again;
    private bool _detached;

    /// <summary>The clock the event is picked against: the machine's, which a preview pins.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;

    /// <summary>[S-6913] What the view asks of the host: the launcher, ⚙ › Accounts.</summary>
    private readonly IWidgetHost _widgetHost;

    public NextMeetingView(IWidgetHost host)
    {
        _widgetHost = host;
        _state = new(Glyphs.Meeting, host) { Visibility = Visibility.Collapsed };

        _join = _controls.Main(NextMeetingStr.Meeting_Join.Text(), Join);

        _text.Children.Add(_when);
        _text.Children.Add(_title);
        _text.Children.Add(_range.Row);
        _text.Children.Add(_location.Row);
        _text.Children.Add(_organizer.Row);

        _afterLabel.Margin = Family.SideInset;
        _after.Children.Add(_afterLabel);
        _controls.Margin = Family.SideInset;
        _content.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Top, Children = { _text, _after } });
        _content.Children.Add(_controls);
        _content.Background = Family.Brush(ColourRole.Clear);
        _content.PointerEntered += OnPointerEntered;
        _content.PointerExited += OnPointerExited;
        _content.Tapped += OnTapped;

        Children.Add(_content);
        Children.Add(_state);
    }

    /// <summary>Which of the widget's states is on screen.</summary>
    private enum Screen
    {
        Loading,
        NoAccounts,
        AccountsGone,
        Problem,
        Ready,
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as NextMeetingConfig ?? new NextMeetingConfig();
        UseData(_widgetHost.Office is { } source ? new OfficeCalendarData(source) : null);
        Strings.Changed += OnLanguage;

        // [S-4825] The view's links: the event under the pointer is shared with the calendars there.
        _member = _surface?.Links?.Join();
        _member?.Watch(LinkTopics.HoveredEvent, OnHoveredElsewhere);

        // [S-588] The poll fires once on subscribing: the first load.
        _poll = ticker.Subscribe(Poll, () => _ = RefreshAsync(force: false));
        _tick = ticker.Subscribe(Minute, Render);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as NextMeetingConfig ?? new NextMeetingConfig();
        _ = RefreshAsync(force: false);
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    /// <summary>Refresh, as every calendar widget has.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => [new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => _ = RefreshAsync(force: true))];

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
        UseData(null);
        _member?.Dispose();
        _member = null;

        _content.PointerEntered -= OnPointerEntered;
        _content.PointerExited -= OnPointerExited;
        _content.Tapped -= OnTapped;
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
    // Data
    // -----------------------------------------------------------------------------------------

    private void UseData(ICalendarData? data)
    {
        if (_data is not null)
        {
            _data.Changed -= OnAccountsChanged;
        }

        _data = data;

        if (_data is not null)
        {
            _data.Changed += OnAccountsChanged;
        }
    }

    private void OnAccountsChanged() => _ = RefreshAsync(force: false);

    /// <summary>One load at a time; one asked for meanwhile runs after it.</summary>
    private async Task RefreshAsync(bool force)
    {
        if (_detached)
        {
            return;
        }

        if (_refreshing)
        {
            _again = true;
            return;
        }

        if (_data is not { } data)
        {
            _screen = Screen.NoAccounts;
            Render();
            return;
        }

        var (first, last) = NextMeetingRules.Window(DateOnly.FromDateTime(_clock().LocalDateTime));
        _refreshing = true;

        try
        {
            var slice = await data.LoadAsync(_config, first, last, force, CancellationToken.None).ConfigureAwait(true);
            if (_detached)
            {
                return;
            }

            Take(slice);
        }
        catch (Exception ex)
        {
            // Reached from a ticker callback and a menu click: nothing above may see it.
            Log.Error(Scope, "could not read the calendar", ex);
            _screen = Screen.Problem;
            _problemText = () => Str.WidgetsA_Calendar_ReadFailed.Text();
            _problemOffersAccounts = false;
        }
        finally
        {
            _refreshing = false;
        }

        Render();

        if (_again && !_detached)
        {
            _again = false;
            await RefreshAsync(force: false).ConfigureAwait(true);
        }
    }

    private void Take(CalendarSlice slice)
    {
        switch (slice.State)
        {
            case CalendarSliceState.NoAccounts:
                _screen = Screen.NoAccounts;
                return;

            case CalendarSliceState.AccountsGone:
                _screen = Screen.AccountsGone;
                return;
        }

        if (!slice.AnyData && slice.Problems.Count > 0)
        {
            var worst = slice.Problems.OrderByDescending(p => SyncProblems.NeedsUser(p.Problem)).First();
            _screen = Screen.Problem;
            _problemText = () => SyncProblems.Message(worst.Problem, SyncSubject.Calendar, Strings.Current);
            _problemOffersAccounts = SyncProblems.NeedsUser(worst.Problem);
            return;
        }

        _screen = Screen.Ready;
        _events = slice.Events;
        _providers = slice.Providers;
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

        _content.Visibility = Visibility.Collapsed;

        switch (_screen)
        {
            case Screen.Loading:
                _state.Show(Str.Common_Loading.Text(), offerAccounts: false);
                return;

            case Screen.NoAccounts:
                // [S-1862] The card shows its Set up row over this.
                _state.Hide();
                return;

            case Screen.AccountsGone:
                _state.Show(SyncProblems.Message(SyncProblem.NoAccount, SyncSubject.Calendar, Strings.Current), offerAccounts: true, problem: true);
                return;

            case Screen.Problem:
                _state.Show(_problemText(), _problemOffersAccounts, problem: true);
                return;
        }

        var now = _clock();
        var item = NextMeetingRules.Pick(_events, now);
        SetShown(item);

        if (item is null)
        {
            // [S-4822] Nothing coming up: the state row, where the meeting's first line would be.
            _state.Show(NextMeetingStr.Meeting_None.Text(), offerAccounts: false);
            return;
        }

        _state.Hide();
        var locale = Strings.Current;
        var zone = TimeZoneInfo.Local;
        var join = item.JoinUrl is not null;
        var box = new WidgetArea(Math.Max(0, _area.Width - (2 * FamilySpace.Inset)), Math.Max(0, _area.Height - FamilySpace.Inset));
        var plan = NextMeetingLayout.Plan(
            box,
            join,
            !string.IsNullOrWhiteSpace(item.Location),
            !string.IsNullOrWhiteSpace(item.Organizer));

        _when.Text = NextMeetingRules.When(locale, item, now, zone);
        _when.Visibility = Show(plan.When);
        _title.Text = string.IsNullOrWhiteSpace(item.Subject) ? Str.Calendar_NoTitle.Text() : item.Subject;
        _title.Visibility = Show(plan.Title);
        _range.Text.Text = NextMeetingRules.Range(locale, item, zone);
        _range.Row.Visibility = Show(plan.Range);
        _location.Text.Text = item.Location ?? string.Empty;
        _location.Row.Visibility = Show(plan.Location);
        _organizer.Text.Text = item.Organizer ?? string.Empty;
        _organizer.Row.Visibility = Show(plan.Organizer);

        ShowAfter(NextMeetingFollowing.After(_events, item, now, zone, NextMeetingFollowing.LaterDays(_area)), NextMeetingFollowing.Rows(box.Height, plan, join, int.MaxValue), locale, now, zone);

        _controls.Visibility = Show(join);
        ToolTipService.SetToolTip(_join, NextMeetingStr.Meeting_JoinTip.Text());
        ToolTipService.SetToolTip(_text, Describe(item, locale, zone));
        _content.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// [S-2160] [S-2161] The meetings after this one, as many whole one-line rows as the plan leaves room
    /// for (the title the name, the start the value), under their label; nothing where not one fits.
    /// </summary>
    private void ShowAfter(IReadOnlyList<CalendarEvent> after, int room, Locale locale, DateTimeOffset now, TimeZoneInfo zone)
    {
        var count = Math.Min(room, after.Count);
        _after.Visibility = Show(count > 0);
        _afterLabel.Text = NextMeetingStr.Meeting_After.Text();

        while (_afterRows.Count < count)
        {
            var row = new RowBlock(RowKind.List, lead: 0) { Lines = 1 };
            _afterRows.Add(row);
            _after.Children.Add(row);
        }

        for (var i = 0; i < _afterRows.Count; i++)
        {
            var row = _afterRows[i];
            row.Visibility = Show(i < count);
            if (i >= count)
            {
                row.Tag = null;
                continue;
            }

            var item = after[i];
            row.Tag = item;
            row.Show(string.IsNullOrWhiteSpace(item.Subject) ? Str.Calendar_NoTitle.Text() : item.Subject, NextMeetingFollowing.Start(locale, item, now, zone));
            ToolTipService.SetToolTip(row, Describe(item, locale, zone));
        }
    }

    /// <summary>[S-2160] The following meeting a click landed on, or null for the shown one.</summary>
    private static CalendarEvent? RowItem(DependencyObject? source)
    {
        for (var node = source; node is not null; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
        {
            if (node is RowBlock { Tag: CalendarEvent item })
            {
                return item;
            }
        }

        return null;
    }

    private void SetShown(CalendarEvent? item)
    {
        if (_shown is { } old && (item is null || CalendarShare.Key(old) != CalendarShare.Key(item)))
        {
            _member?.Clear(LinkTopics.HoveredEvent);
        }

        _shown = item;
        _content.Opacity = 1;
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

    /// <summary>
    /// A click on the event opens it in its calendar, as a calendar widget's does ([S-1268]); a click on a
    /// following meeting's row opens that one ([S-2160]).
    /// </summary>
    private void OnTapped(object sender, TappedRoutedEventArgs args)
    {
        if (_surface?.IsPreview == true || _surface?.EditMode == true || _shown is not { } shown || args.OriginalSource is DependencyObject source && IsInJoin(source))
        {
            return;
        }

        var item = RowItem(args.OriginalSource as DependencyObject) ?? shown;

        ProviderId? provider = _providers.TryGetValue(item.AccountId, out var known) ? known : null;
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
        if (_shown is { } item)
        {
            _member?.Set(LinkTopics.HoveredEvent, CalendarShare.Key(item));
            _content.Opacity = 1;
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs args) => _member?.Clear(LinkTopics.HoveredEvent);

    /// <summary>[S-4825] Another widget points at an event: this one fades unless it is the same.</summary>
    private void OnHoveredElsewhere(EventLink? value)
    {
        _content.Opacity = _shown is { } item && CalendarShare.Dimmed(value, item) ? DimOpacity : 1;
    }

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
