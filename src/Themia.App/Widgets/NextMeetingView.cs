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
using Themia.Sdk;

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
internal sealed partial class NextMeetingView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetContextActions
{
    private const string Scope = "next-meeting";

    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    /// <summary>[S-4825] How far the event fades while another widget points at a different one.</summary>
    private const double DimOpacity = 0.35;

    private readonly OfficeStatePanel _state = new(Glyphs.Meeting);
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();
    private readonly Grid _content = new() { Margin = WidgetLayout.Inset, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _text = new() { Spacing = NextMeetingLayout.Gap, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _when = Fluent.Text(FluentType.Caption, "AccentTextFillColorPrimaryBrush");
    private readonly TextBlock _title = Fluent.Text(FluentType.BodyStrong);
    private readonly TextBlock _range = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly (Grid Row, TextBlock Text) _location = DetailLine(Glyphs.Place);
    private readonly (Grid Row, TextBlock Text) _organizer = DetailLine(Glyphs.Contact);
    private readonly Button _join;
    private readonly TextBlock _joinLabel = new() { Text = Str.Meeting_Join.Text() };

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

    public NextMeetingView()
    {
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _title.TextWrapping = TextWrapping.Wrap;
        _when.TextTrimming = TextTrimming.CharacterEllipsis;
        _range.TextTrimming = TextTrimming.CharacterEllipsis;
        Tabular(_when);
        Tabular(_range);

        _join = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Fluent.Space2,
                Children = { Fluent.Icon(Glyphs.Meeting, "TextOnAccentFillColorPrimaryBrush"), _joinLabel },
            },
            MinHeight = NextMeetingLayout.JoinHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var accent) && accent is Style style)
        {
            _join.Style = style;
        }
        else
        {
            Log.Warn(Scope, "AccentButtonStyle is missing; Join falls back to the standard button style");
        }

        _join.Click += OnJoinClicked;

        _text.Children.Add(_when);
        _text.Children.Add(_title);
        _text.Children.Add(_range);
        _text.Children.Add(_location.Row);
        _text.Children.Add(_organizer.Row);

        _content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _content.Children.Add(_text);
        _content.Children.Add(_join);

        _content.PointerEntered += OnPointerEntered;
        _content.PointerExited += OnPointerExited;
        _content.Tapped += OnTapped;

        _empty = Fluent.EmptyState(Glyphs.Meeting, _emptyText, out _);

        Children.Add(_content);
        Children.Add(_empty);
        Children.Add(_state);
    }

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
        UseData(OfficeSources.Current is { } source ? new OfficeCalendarData(source) : null);
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

        _join.Click -= OnJoinClicked;
        _content.PointerEntered -= OnPointerEntered;
        _content.PointerExited -= OnPointerExited;
        _content.Tapped -= OnTapped;
        _state.Detach();
    }

    private void OnLanguage(Locale locale)
    {
        _joinLabel.Text = Str.Meeting_Join.Text();
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
        _empty.Visibility = Visibility.Collapsed;

        switch (_screen)
        {
            case Screen.Loading:
                _state.Show(Str.Common_Loading.Text(), offerAccounts: false);
                return;

            case Screen.NoAccounts:
                // [S-1862] The card shows its Set up button over this.
                _state.Hide();
                return;

            case Screen.AccountsGone:
                _state.Show(SyncProblems.Message(SyncProblem.NoAccount, SyncSubject.Calendar, Strings.Current), offerAccounts: true);
                return;

            case Screen.Problem:
                _state.Show(_problemText(), _problemOffersAccounts);
                return;
        }

        _state.Hide();

        var now = _clock();
        var item = NextMeetingRules.Pick(_events, now);
        SetShown(item);

        if (item is null)
        {
            _emptyText.Text = Str.Meeting_None.Text();
            _empty.Visibility = Visibility.Visible;
            return;
        }

        var locale = Strings.Current;
        var zone = TimeZoneInfo.Local;
        // A short tile keeps a half inset top and bottom, so Join and the when line both fit.
        var vertical = _area.HeightClass == SizeClass.Compact ? Fluent.Space2 : WidgetLayout.ContentInset;
        _content.Margin = new Thickness(WidgetLayout.ContentInset, vertical, WidgetLayout.ContentInset, vertical);
        var plan = NextMeetingLayout.Plan(
            _area.Deflate(WidgetLayout.ContentInset, vertical),
            item.JoinUrl is not null,
            !string.IsNullOrWhiteSpace(item.Location),
            !string.IsNullOrWhiteSpace(item.Organizer));

        _when.Apply(NextMeetingLayout.WhenStep(plan.Large));
        _when.Text = NextMeetingRules.When(locale, item, now, zone);
        _when.Visibility = Show(plan.When);
        _title.Apply(FluentType.Strong(NextMeetingLayout.TitleStep(plan.Large)));
        _title.Text = string.IsNullOrWhiteSpace(item.Subject) ? Str.Calendar_NoTitle.Text() : item.Subject;
        _title.MaxLines = Math.Max(1, plan.Title);
        _title.Visibility = Show(plan.Title > 0);
        _range.Text = NextMeetingRules.Range(locale, item, zone);
        _range.Visibility = Show(plan.Range);
        _location.Text.Text = item.Location ?? string.Empty;
        _location.Row.Visibility = Show(plan.Location);
        _organizer.Text.Text = item.Organizer ?? string.Empty;
        _organizer.Row.Visibility = Show(plan.Organizer);

        _join.Visibility = Show(item.JoinUrl is not null);
        ToolTipService.SetToolTip(_join, Str.Meeting_JoinTip.Text());
        SetRow(_join, plan.JoinBeside ? 0 : 1);
        SetColumn(_join, plan.JoinBeside ? 1 : 0);
        _join.Margin = plan.JoinBeside ? new Thickness(Fluent.Space2, 0, 0, 0) : new Thickness(0, NextMeetingLayout.Gap, 0, 0);

        ToolTipService.SetToolTip(_text, Describe(item, locale, zone));
        _content.Visibility = Visibility.Visible;
    }

    /// <summary>Remember the event on screen; a different one drops the hover this widget published for the old.</summary>
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

    private void OnJoinClicked(object sender, RoutedEventArgs args)
    {
        if (_surface?.IsPreview == true || _shown?.JoinUrl is not { } link)
        {
            return;
        }

        ShellLauncher.OpenMeetingLink(link);
    }

    /// <summary>A click on the event opens it in its calendar, as a calendar widget's does ([S-1268]).</summary>
    private void OnTapped(object sender, TappedRoutedEventArgs args)
    {
        if (_surface?.IsPreview == true || _surface?.EditMode == true || _shown is not { } item || args.OriginalSource is DependencyObject source && IsInJoin(source))
        {
            return;
        }

        ProviderId? provider = _providers.TryGetValue(item.AccountId, out var known) ? known : null;
        if (CalendarLinks.Open(item, provider, provider == HostProviders.Microsoft && ShellLauncher.OutlookInstalled) is { } link)
        {
            ShellLauncher.OpenCalendarLink(link);
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

    /// <summary>A glyph and one trimmed line: a grid, so the text is bounded and its ellipsis shows.</summary>
    private static (Grid Row, TextBlock Text) DetailLine(string glyph)
    {
        var text = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        var icon = Fluent.Icon(glyph, "TextFillColorSecondaryBrush", FluentType.Caption.FontSize);
        icon.VerticalAlignment = VerticalAlignment.Center;
        icon.Margin = new Thickness(0, 0, Fluent.Space1, 0);
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

    private static void Tabular(TextBlock text)
        => Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(text, FontNumeralAlignment.Tabular);
}
