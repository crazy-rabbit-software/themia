using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3321] Everything one render of a screen-time widget draws from, worked out before anything
/// is drawn: the period's apps (hidden ones left out, [S-3322]), its days, and the shared limits.
/// </summary>
public sealed record ScreenTimeData(
    bool Window,
    int Days,
    long Total,
    IReadOnlyList<ScreenTimeApp> Apps,
    IReadOnlyList<ScreenTimeDay> Range,
    IReadOnlyList<(string Date, long Seconds)> Totals,
    long Busiest,
    ScreenTimePreferences Preferences)
{
    public IReadOnlySet<string> Hidden => Preferences.Hidden;

    /// <summary>A period of one day (pinned to <paramref name="pinned"/>, or today), or a rolling window.</summary>
    public static ScreenTimeData Build(IScreenTimeSource source, ScreenTimePeriod period, string? pinned)
    {
        var preferences = source.Preferences;
        var hidden = preferences.Hidden;

        if (ScreenTimeRules.ClampPeriod(period) == ScreenTimePeriod.Day)
        {
            var day = source.Day(pinned);
            var apps = ScreenTimeRules.Visible(day.Apps, hidden);
            var total = ScreenTimeRules.Sum(apps);
            return new ScreenTimeData(false, 1, total, ScreenTimeRules.Top(apps, apps.Count), [day], [(day.Date, total)], total, preferences);
        }

        var length = ScreenTimeRules.RangeLength(period);
        var range = source.Range(length);
        var aggregate = ScreenTimeRules.Aggregate(range, hidden);
        var totals = ScreenTimeRules.DayTotals(range, hidden);

        return new ScreenTimeData(true, length, ScreenTimeRules.Sum(aggregate), aggregate, range, totals, ScreenTimeRules.Busiest(totals), preferences);
    }

    /// <summary>[S-457] The daily limit on the whole, or null.</summary>
    public long? DayLimit => ScreenTimeRules.Limit(Preferences.GoalSeconds);

    /// <summary>[S-457] An app's own daily limit, or null.</summary>
    public long? AppLimit(string app) => Preferences.AppLimits.TryGetValue(app, out var limit) ? ScreenTimeRules.Limit(limit) : null;
}

/// <summary>
/// [S-3310] A screen-time widget's place on its view's links: what is hovered and what is selected,
/// kept in step with the other screen-time widgets on the view. Without a board (no view) it is the
/// same state, unshared. UI thread only, as the board is ([S-3306]).
/// </summary>
public sealed class ScreenTimeLinks
{
    private readonly Action _changed;
    private LinkMember? _member;

    /// <param name="changed">Called when another widget hovered or selected something; never for this one's own change.</param>
    public ScreenTimeLinks(Action changed) => _changed = changed;

    /// <summary>The app under the pointer or the keyboard cursor, in this widget or another.</summary>
    public string? Hovered { get; private set; }

    /// <summary>The selected app, sticky until changed or cleared ([S-3302]).</summary>
    public string? Selected { get; private set; }

    /// <summary>What to emphasise: the hover, else the selection ([S-3310]).</summary>
    public string? Focus => ScreenTimeRules.Focus(Hovered, Selected);

    /// <summary>Join the board of the view this widget is on (Attach). Reads what is selected there already.</summary>
    public void Join(LinkBoard? board)
    {
        Leave();

        if (board is null)
        {
            return;
        }

        _member = board.Join();
        Hovered = _member.Get(LinkTopics.HoveredApp)?.App;
        Selected = _member.Get(LinkTopics.SelectedApp)?.App;
        _member.Watch(LinkTopics.HoveredApp, value => { Hovered = value?.App; _changed(); });
        _member.Watch(LinkTopics.SelectedApp, value => { Selected = value?.App; _changed(); });
    }

    /// <summary>Leave the board (Detach): this widget's hover goes with it, the selection stays ([S-3304]).</summary>
    public void Leave()
    {
        _member?.Dispose();
        _member = null;
        Hovered = null;
        Selected = null;
        Cursor = null;
    }

    /// <summary>Light an app (null: let go of this widget's hover). Returns whether the focus moved.</summary>
    public bool Hover(string? app)
    {
        var before = Focus;

        if (_member is { } member)
        {
            member.Set(LinkTopics.HoveredApp, app is null ? null : new AppLink(app));
            Hovered = member.Get(LinkTopics.HoveredApp)?.App;
        }
        else
        {
            Hovered = app;
        }

        return before != Focus;
    }

    /// <summary>[S-3302] Select an app, or let it go when it is the selection.</summary>
    public void Toggle(string app)
    {
        Selected = _member is { } member
            ? member.Toggle(LinkTopics.SelectedApp, new AppLink(app))?.App
            : Selected == app ? null : app;
    }

    /// <summary>[S-3307] The keyboard cursor: the app the arrows last landed on, published as the hover.</summary>
    public string? Cursor { get; private set; }

    /// <summary>The pointer entered a part (or left, with null): the keyboard cursor gives way to it.</summary>
    public bool PointerHover(string? app)
    {
        Cursor = null;
        return Hover(app);
    }

    /// <summary>
    /// [S-3307] [S-1989] The keys every screen-time widget shares, over the parts it shows in order
    /// (<paramref name="keys"/>): Up/Down, Page Up/Down and Home/End move the cursor, which is the
    /// hover the other widgets light; Enter and Space select it (again: let it go); the Menu key opens
    /// its Hide menu (never on Other: hiding has no undo, so never a bare key); Escape lets the cursor
    /// go and keeps the selection, because Escape also arrives whenever the lease ends.
    /// </summary>
    public bool HandleKey(WidgetKey key, IReadOnlyList<string> keys, Func<string, FrameworkElement?> anchor, IScreenTimeSource? source)
    {
        switch (key)
        {
            case WidgetKey.Escape:
                var had = Cursor is not null;
                Cursor = null;
                Hover(null);
                return had;

            case WidgetKey.Enter or WidgetKey.Space:
                if (Cursor is not { } chosen)
                {
                    return false;
                }

                Toggle(chosen);
                return true;

            case WidgetKey.Menu:
                return Cursor is { } app && app != ScreenTimeRules.OtherKey && anchor(app) is { } element
                    && ItemMenu.Open(element, [new ItemMenuEntry(Glyphs.Hide, Str.Screentime_HideApp.Text(), () => ScreenTimeLook.Hide(source, app))]);

            case WidgetKey.Up or WidgetKey.Down or WidgetKey.Home or WidgetKey.End or WidgetKey.PageUp or WidgetKey.PageDown:
                var at = keys.ToList().IndexOf(Cursor ?? Selected ?? string.Empty);
                if (ItemCursor.Move(keys.Count, at < 0 ? null : at, key) is not { } next)
                {
                    return false;
                }

                Cursor = keys[next];
                Hover(Cursor);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Select an app outright, whatever was selected (a debug render's preview state).</summary>
    public void Select(string app)
    {
        _member?.Set(LinkTopics.SelectedApp, new AppLink(app));
        Selected = app;
    }

    /// <summary>[S-3302] Clear the selection (a click on the widget's empty space). Returns whether there was one.</summary>
    public bool ClearSelection()
    {
        if (Selected is null)
        {
            return false;
        }

        _member?.Clear(LinkTopics.SelectedApp);
        Selected = null;
        return true;
    }
}

/// <summary>
/// The screen-time widgets' shared look on the family's roles ([S-6705]): an app is the accent at its
/// rank's step ([S-2272]), a part not in focus is drawn as its wash, durations in tooltips.
/// </summary>
public static class ScreenTimeLook
{
    /// <summary>
    /// [S-3312] A dimmed part's opacity: the family's wash ([S-6704]), the one "less" a chart has, so the
    /// whole still reads but is clearly not the point.
    /// </summary>
    public const double DimOpacity = FamilyShape.Wash;

    /// <summary>
    /// [S-2272] [S-6706] A part's brush: the accent for an app on the ramp, the tertiary text role for
    /// Other and the apps past it. Shared brushes: the tone's alpha goes on the shape
    /// (<see cref="ScreenTimeRules.Opacity"/>), never on the brush.
    /// </summary>
    public static Brush Brush(ScreenTimeTone tone) => Family.Brush(tone.Accent ? ColourRole.Accent : ColourRole.TextTertiary);

    /// <summary>"Name — 3h 24m · 41%".</summary>
    public static string Tip(ScreenTimeSegment segment, long total)
        => $"{segment.Name} — {ScreenTimeRules.FormatDuration(segment.Seconds)} · {Percent(segment.Seconds, total)}";

    public static string Percent(long seconds, long total)
        => LocaleFormat.Percent(Strings.Current, Math.Round(ScreenTimeRules.Share(seconds, total) * 100));

    /// <summary>A day's date as a tooltip reads it: "Today", "Yesterday", "Mon 6 Oct".</summary>
    public static string DayTip(string date, LocalDay today)
        => LocalDay.TryParse(date, out var day) ? Formatting.DayHeading(day.Date, today.Date) : date;

    /// <summary>
    /// [S-455] [S-3322] Hide an app from every screen-time widget. Its time is still recorded; the
    /// properties menu lists it with a way back. "Other" is never an app.
    /// </summary>
    public static void Hide(IScreenTimeSource? source, string app)
    {
        if (source is null || app == ScreenTimeRules.OtherKey || source.Preferences.Hidden.Contains(app))
        {
            return;
        }

        var next = source.Preferences.Copy();
        next.Hidden.Add(app);
        source.SetPreferences(next);
    }

    /// <summary>The right-click "Hide from list" on a part that is an app.</summary>
    public static void AttachHideMenu(FrameworkElement element, string key, Func<IScreenTimeSource?> source)
    {
        if (key == ScreenTimeRules.OtherKey)
        {
            return;
        }

        element.RightTapped += (_, args) =>
            ItemMenu.Show(element, args, [new ItemMenuEntry(Glyphs.Hide, Str.Screentime_HideApp.Text(), () => Hide(source(), key))]);
    }
}

/// <summary>
/// [S-454] The day navigation in a screen-time card's header, on the Header block's parts ([S-6710]):
/// the day ("Today", "Yesterday", "Mon 26 Oct") as the informing label and two header buttons, back and
/// forward, for a day period; the window's name alone for a week or a month. A button with nowhere to
/// go (forward on today, back on the oldest day) is not wanted and takes no room; back to today is the
/// card menu's (<see cref="ContextActions"/>). The donut and the app list each have one; the pinned day
/// is the widget's own.
/// </summary>
public sealed class ScreenTimeDayNav
{
    private static readonly string ChevronLeft = char.ConvertFromUtf32(0xE76B);
    private static readonly string ChevronRight = Glyphs.ChevronRight;

    private readonly HeaderParts _parts = new();
    private readonly TextBlock _label = HeaderBlock.Label(string.Empty);
    private readonly Border _back;
    private readonly Border _forward;
    private readonly Action _moved;

    /// <param name="moved">Called after the pinned day changed, to redraw.</param>
    public ScreenTimeDayNav(Action moved)
    {
        _moved = moved;

        // Centred on the header's line beside the 24 buttons, as a label alone is.
        _label.VerticalAlignment = VerticalAlignment.Center;
        _parts.Add(HeaderPartKind.Label, _label);
        _back = _parts.Add(HeaderPartKind.Button, HeaderBlock.Button(ChevronLeft, Str.Screentime_PrevDay.Text(), () => Step(-1)));
        _forward = _parts.Add(HeaderPartKind.Button, HeaderBlock.Button(ChevronRight, Str.Screentime_NextDay.Text(), () => Step(1)));

        // The arrows point the way time runs in the language's reading order.
        foreach (var button in new[] { _back, _forward })
        {
            if (button.Child is FontIcon icon)
            {
                UiLanguage.Mirrored(icon);
            }
        }
    }

    /// <summary>The header's trailing parts.</summary>
    public HeaderParts Host => _parts;

    /// <summary>[S-454] The day shown, or null for today (which follows the clock across midnight).</summary>
    public string? Pinned { get; set; }

    /// <summary>The source's today, so the navigation and the tracker never disagree about midnight.</summary>
    public Func<LocalDay>? Today { get; set; }

    /// <summary>[S-454] The card menu's "Back to today" while a past day is pinned.</summary>
    public IReadOnlyList<WidgetAction> ContextActions
        => Pinned is null ? [] : [new WidgetAction(Glyphs.Calendar, Str.Screentime_BackToToday.Text(), () => { Pinned = null; _moved(); })];

    /// <summary>[S-1110] The tooltips in the current language.</summary>
    public void Retext()
    {
        Tip(_back, Str.Screentime_PrevDay.Text());
        Tip(_forward, Str.Screentime_NextDay.Text());
    }

    private static void Tip(Border button, string text)
    {
        ToolTipService.SetToolTip(button, text);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, text);
    }

    /// <summary>Show the day's label and arrows, or the window's name, for this period.</summary>
    public void Update(ScreenTimePeriod period)
    {
        if (ScreenTimeRules.ClampPeriod(period) != ScreenTimePeriod.Day)
        {
            _label.Text = ScreenTimeRules.PeriodLabel(period);
            _parts.Want(_back, false);
            _parts.Want(_forward, false);
            return;
        }

        var today = CurrentDay();
        _label.Text = ScreenTimeRules.DayLabel(Pinned, today);
        _parts.Want(_back, ScreenTimeRules.CanGoBack(Pinned, today));
        _parts.Want(_forward, Pinned is not null);
    }

    /// <summary>[S-1989] Left / Right: the day before or after, never past today. Returns whether it moved.</summary>
    public bool StepKey(bool forward)
    {
        var allowed = forward ? Pinned is not null : ScreenTimeRules.CanGoBack(Pinned, CurrentDay());
        if (allowed)
        {
            Step(forward ? 1 : -1);
        }

        return allowed;
    }

    private LocalDay CurrentDay() => Today?.Invoke() ?? LocalDay.From(DateTimeOffset.Now);

    private void Step(int delta)
    {
        Pinned = ScreenTimeRules.Step(Pinned, CurrentDay(), delta);
        _moved();
    }
}
