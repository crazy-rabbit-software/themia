using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Headlines from one or several feeds, merged newest first, as a Windows 11 list.
/// <para>
/// Everything on this widget was written by somebody else and fetched over the network: a title, a
/// summary, a link. All of it is <b>text</b> here and nothing else — set as
/// <see cref="TextBlock.Text"/>, never as markup, never as a path, never as a command. The parser
/// strips tags before any of it arrives and refuses a link that is not http or https, so the worst
/// a hostile feed can do to this widget is put rude words in it.
/// </para>
/// <para>
/// Double-click to open, not single. A widget sits under the pointer's path across the desktop, and
/// one stray click sending a browser to a stranger's URL is how a glanceable surface becomes a
/// hazard.
/// </para>
/// </summary>
internal sealed partial class RssView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware,
    IWidgetSizeAware, IWidgetContextActions, IWidgetKeys
{
    private const string Scope = "rss";

    private readonly TextBlock _freshness = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly ProgressRing _spinner = new() { Width = 12, Height = 12, IsActive = false, Visibility = Visibility.Collapsed };
    private readonly StackPanel _trailing = new() { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1 };

    private readonly Grid _rows = new();
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _emptyState;
    private readonly TextBlock _emptyMessage = new();

    private readonly List<IDisposable> _rowHooks = [];

    /// <summary>The row element of each shown entry, in entry order.</summary>
    private readonly List<Border> _rowElements = [];

    private readonly Brush _freshBrush = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray);
    private readonly Brush _staleBrush = FluentLayout.Themed("SystemFillColorCautionBrush", Colors.Goldenrod);

    private readonly RefreshSchedule _schedule = new(Scope, "feed");

    /// <summary>The last answer per feed address, held through failures ([S-25]).</summary>
    private readonly Dictionary<string, Fetched<FeedDocument>> _parts = new(StringComparer.Ordinal);

    private Ticker? _ticker;
    private RssConfig _config = new();

    /// <summary>
    /// [S-13a] A copy of the settings this view last acted on, which is what a change is measured
    /// against. Not <c>_config</c>: the settings page edits the live config object in place.
    /// </summary>
    private RssConfig _applied = new();

    private IWidgetSurface? _surface;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;
    private Services.Web.FeedSync? _feeds;
    private ScrollFade? _fade;

    private WidgetArea _area;
    private FeedPlan _plan;
    private MergedFeed _merged = new(string.Empty, [], []);

    /// <summary>[S-1306] The keyboard cursor and, in the reading layout, the selected item.</summary>
    private int? _cursor;

    private bool _detached;

    public RssView()
    {
        _scroll = new ScrollViewer
        {
            Content = _rows,
            Padding = new Thickness(0, FeedLayout.ListInset / 2, 0, FeedLayout.ListInset / 2),
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalAlignment = VerticalAlignment.Top,
        };

        _emptyState = Fluent.EmptyState(Glyphs.Rss, _emptyMessage, out _);
        _emptyState.Visibility = Visibility.Collapsed;

        _spinner.VerticalAlignment = VerticalAlignment.Center;
        _trailing.Children.Add(_spinner);
        _trailing.Children.Add(_freshness);

        Children.Add(_scroll);
        Children.Add(_emptyState);
    }

    /// <summary>
    /// The age of the feeds and, while a refresh is out, a spinner ([S-1303]). Built in the
    /// constructor because the card reads this once, when it builds that header.
    /// </summary>
    public FrameworkElement? HeaderTrailing => _trailing;

    /// <summary>[S-1303] Refresh: the network now, past the cache and any back-off.</summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
        _config.FeedUrls.Count == 0 ? [] : [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), () => Reload(force: true))];

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;
        _fade = new ScrollFade(_scroll, _rows);
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        _feeds = AppServices.Feeds;
        if (_feeds is not null)
        {
            _feeds.Updated += OnFeedUpdated;
        }

        ApplyConfig(config);
    }

    /// <summary>[S-1110] The list redrawn from what is in hand (no fetch).</summary>
    private void OnLanguage(Locale locale) => Render();

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as RssConfig ?? new RssConfig();
        var previous = _applied;
        _config = next;
        _applied = (RssConfig)next.Clone();

        // Only the addresses and the cap change what comes back. Toggling the preview or the source
        // line redraws what is already in hand — it must not fetch the feeds again.
        var needsFetch = !previous.FeedUrls.SequenceEqual(next.FeedUrls)
            || previous.EffectiveMaxItems != next.EffectiveMaxItems;

        if (needsFetch)
        {
            _parts.Clear();
            _cursor = null;
            _schedule.Reset();
        }

        PublishTitle();

        if (Resubscribe())
        {
            return;
        }

        if (needsFetch)
        {
            Reload(force: false);
        }
        else
        {
            Render();
        }
    }

    private bool Resubscribe()
    {
        var interval = _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return false;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, OnTick);
        return _subscription is not null;
    }

    private void OnTick()
    {
        if (_detached)
        {
            return;
        }

        // [S-2661] The feeds are kept fresh by the sync service; the tick only moves "4m ago" on.
        Render();
    }

    /// <summary>
    /// [S-2661] Draw what is held for the feeds and tell the sync service they are on screen. Nothing
    /// is fetched for a feed already held; <paramref name="force"/> (the user's Refresh) asks now.
    /// </summary>
    private void Reload(bool force)
    {
        if (_detached)
        {
            return;
        }

        var urls = _config.FeedUrls;

        if (urls.Count == 0)
        {
            // An empty list is "not set up"; a list of refused addresses is refused, and the reason
            // is kept so the widget can say which rule an address broke.
            var refused = _config.ResolvedFeeds.FirstOrDefault(r => !r.IsValid);
            var status = _config.ResolvedFeeds.Count == 0 ? FetchStatus.NotConfigured : FetchStatus.Refused;
            _schedule.Record(status, _config.TickInterval, WebInput.Explain(refused.Problem), DateTimeOffset.Now);
            Render();
            return;
        }

        var cap = _config.EffectiveMaxItems;

        if (AppServices.Feeds is { } feeds)
        {
            if (force)
            {
                // The user asked: the back-off of a failing feed must not swallow the click.
                _schedule.Reset();
                feeds.Refresh(urls, cap, _config.TickInterval);
            }
            else
            {
                feeds.Show(urls, cap, _config.TickInterval);
            }
        }

        Take(urls, cap);
    }

    /// <summary>[S-2661] A feed's answer or state changed in the sync service.</summary>
    private void OnFeedUpdated(string url)
    {
        if (_detached || !_config.FeedUrls.Contains(url, StringComparer.Ordinal))
        {
            return;
        }

        Take(_config.FeedUrls, _config.EffectiveMaxItems);
    }

    /// <summary>Take what the service holds for each feed, keeping the articles through a failure.</summary>
    private void Take(IReadOnlyList<string> urls, int cap)
    {
        var feeds = AppServices.Feeds;
        var answered = 0;

        foreach (var url in urls)
        {
            var last = feeds?.Last(url, cap);
            var held = FeedSync.Peek(url, cap);

            if (last is { Value: not null })
            {
                _parts[url] = last;
            }
            else if (held is not null)
            {
                // The articles stay on screen; the header says how old they are and what failed.
                _parts[url] = last is null ? held : held.Stale(last.Status, last.Detail);
            }
            else if (last is not null)
            {
                _parts[url] = last;
            }

            if (last is not null)
            {
                answered++;
            }
        }

        // One schedule for the widget, recorded once every feed has answered: any feed answering is
        // a success (the partial failure is in the header), all of them failing is the widget's state.
        if (answered == urls.Count)
        {
            var results = urls.Select(url => feeds!.Last(url, cap)!).ToList();
            var ok = results.Any(r => r.Status == FetchStatus.Ok);
            var first = results.FirstOrDefault(r => r.Status != FetchStatus.Ok);
            _schedule.Record(ok ? FetchStatus.Ok : first?.Status ?? FetchStatus.BadResponse, _config.TickInterval, first?.Detail, DateTimeOffset.Now);
        }

        ShowSpinner(feeds is not null && urls.Any(url => feeds.Busy(url, cap)));
        PublishTitle();
        Render();
    }

    private void ShowSpinner(bool on)
    {
        _spinner.IsActive = on;
        _spinner.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The feeds in the order the user listed them, each with whatever is held for it.</summary>
    private List<FeedPart> Parts()
        => [.. _config.FeedUrls.Select(url => _parts.TryGetValue(url, out var part)
            ? new FeedPart(url, part.Value, part.Status, part.Detail)
            : new FeedPart(url, null, FetchStatus.Loading, null))];

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        _merged = FeedAggregate.Merge(Parts(), _config.EffectiveMaxItems);

        if (_merged.Entries.Count == 0)
        {
            ClearRows();
            _emptyMessage.Text = EmptyText();
            _emptyState.Visibility = Visibility.Visible;
            _scroll.Visibility = Visibility.Collapsed;
            _freshness.Text = string.Empty;
            ToolTipService.SetToolTip(_freshness, null);
            return;
        }

        _emptyState.Visibility = Visibility.Collapsed;
        _scroll.Visibility = Visibility.Visible;

        _plan = FeedLayout.Plan(_area, _config.ShowSource, _config.ShowPreview, _config.ShowDate);

        if (_cursor is { } cursor && cursor >= _merged.Entries.Count)
        {
            _cursor = _merged.Entries.Count - 1;
        }

        BuildRows(DateTimeOffset.Now);
        UpdateFreshness(DateTimeOffset.Now);
    }

    private string EmptyText()
    {
        if (_config.FeedUrls.Count == 0)
        {
            // No feed at all is the card's Set up button ([S-1862]); refused ones say why.
            var refused = _config.ResolvedFeeds.FirstOrDefault(r => !r.IsValid);
            return _config.ResolvedFeeds.Count == 0 ? string.Empty : WebInput.Explain(refused.Problem);
        }

        if (_merged.Failures.Count > 0 && _parts.Count >= _config.FeedUrls.Count)
        {
            // Every reason, not the first (S-33): with several feeds, "the second one is 404" is
            // the answer. A refusal by the network guard carries its own sentence.
            return _merged.Failures.Count == 1 && _parts.Values.All(p => p.Status == FetchStatus.Empty)
                ? Str.WidgetsC_Rss_NoArticles.Text()
                : string.Join("\n", _merged.Failures);
        }

        return _schedule.Status == FetchStatus.Ok ? FetchState.Message(FetchStatus.Loading) : FetchState.Message(_schedule.Status);
    }

    /// <summary>
    /// [S-1303] The header's age: of the oldest list in hand, in the caution colour when a feed
    /// failed, when it is overdue, or when it was restored from disk and not yet refreshed
    /// ([S-1300]); the tooltip names every failing feed and its reason.
    /// </summary>
    private void UpdateFreshness(DateTimeOffset now)
    {
        var held = _parts.Values.Where(p => p.Value is not null).ToList();

        if (held.Count == 0)
        {
            _freshness.Text = string.Empty;
            return;
        }

        var at = held.Min(p => p.At);
        var restored = held.Any(p => p.Restored);
        var failed = _merged.Failures.Count > 0;
        var stale = failed || restored || FetchState.IsStale(at, now, _config.TickInterval);
        var age = FetchState.Freshness(at, now);

        _freshness.Text = age;
        _freshness.Foreground = stale ? _staleBrush : _freshBrush;

        // [S-1303] A compact tile's header has room for its title or the age, not both: the age is a
        // phrase in some languages ("vor 3 Min."), and it would sit on top of the title. The title
        // wins; the age stays one step away in the tooltip and on every larger size.
        _freshness.Visibility = _area.IsMeasured && _area.WidthClass == SizeClass.Compact ? Visibility.Collapsed : Visibility.Visible;

        var tip = failed
            ? Str.WidgetUi_RefreshFailedSince.Format(("reason", string.Join("; ", _merged.Failures)), ("time", age))
            : restored
                ? Str.WidgetsC_Rss_SavedRefreshing.Format(("age", age))
                : Str.WidgetsC_Rss_Refreshed.Format(("age", age));

        ToolTipService.SetToolTip(_freshness, tip);
    }

    /// <summary>
    /// Hand a link to the shell. The address was checked when it was parsed, and is checked once
    /// more here, beside the call that actually launches something.
    /// </summary>
    private static void Open(string link)
    {
        var checkedUrl = WebInput.Url(link);

        if (!checkedUrl.IsValid)
        {
            Log.Warn(Scope, $"refused to open a feed link: {WebInput.Explain(checkedUrl.Problem)}");
            return;
        }

        try
        {
            // using: ShellExecute hands back a process handle even when nothing waits on it.
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = checkedUrl.Url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            Log.Warn(Scope, "could not open a feed link", ex);
        }
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;

        ClearRows();

        _fade?.Dispose();
        _fade = null;

        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = TimeSpan.Zero;

        if (_feeds is not null)
        {
            _feeds.Updated -= OnFeedUpdated;
            _feeds = null;
        }

        ShowSpinner(false);

        _ticker = null;
    }

    /// <summary>
    /// The header title (S-04): the user's own, else the single feed's <c>&lt;title&gt;</c>, else
    /// several feeds' names joined, else the catalogue's name.
    /// </summary>
    private void PublishTitle()
    {
        var title = string.IsNullOrWhiteSpace(_config.Title) ? FeedAggregate.Title(Parts()) : _config.Title;
        _surface?.SetHeaderTitle(string.IsNullOrWhiteSpace(title) ? null : title);
    }
}
