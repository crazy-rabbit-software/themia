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
using Themia.Widget.Rss;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// Headlines from one or several feeds, merged newest first, as a Windows 11 list: a kit list
/// ([S-7060]) over the feed sync, shaped by <see cref="RssSpec"/>.
/// <para>
/// Everything on this widget was written by somebody else and fetched over the network: a title, a
/// summary, a link. All of it is <b>text</b> here and nothing else. The parser strips tags before
/// any of it arrives and refuses a link that is not http or https, and the kit's item rules check
/// it again ([S-7002]), so the worst a hostile feed can do to this widget is put rude words in it.
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
    private readonly TextBlock _freshness = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly ProgressRing _spinner = new() { Width = 12, Height = 12, IsActive = false, Visibility = Visibility.Collapsed };
    private readonly StackPanel _trailing = new() { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1 };

    private readonly Brush _freshBrush = ThemeBrush.Of("TextFillColorTertiaryBrush", Colors.Gray);
    private readonly Brush _staleBrush = ThemeBrush.Of("SystemFillColorCautionBrush", Colors.Goldenrod);

    private readonly KitBinding _binding;
    private readonly FeedSource _source;

    private RssConfig _config = new();

    /// <summary>
    /// [S-13a] A copy of the settings this view last acted on, which is what a change is measured
    /// against. Not <c>_config</c>: the settings page edits the live config object in place.
    /// </summary>
    private RssConfig _applied = new();

    private Ticker? _ticker;

    public RssView(IWidgetHost host)
    {
        _source = new FeedSource(host.Feeds);
        _binding = new KitBinding(host, RssSpec.Spec, Glyphs.Rss, RssSpec.Actions);
        _binding.Rendered += OnRendered;

        _spinner.VerticalAlignment = VerticalAlignment.Center;
        _trailing.Children.Add(_spinner);
        _trailing.Children.Add(_freshness);

        Children.Add(_binding.List);
        Children.Add(_binding.State);
    }

    /// <summary>
    /// The age of the feeds and, while a refresh is out, a spinner ([S-1303]). Built in the
    /// constructor because the card reads this once, when it builds that header.
    /// </summary>
    public FrameworkElement? HeaderTrailing => _trailing;

    /// <summary>[S-1303] Refresh: the network now, past the cache and any back-off.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => _binding.CardActions;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Configure(config);

        // [S-1300] What the sync service holds is drawn at once: a feed restored at launch, or one
        // another widget already reads, is not "Loading" until its next fetch.
        _binding.Attach(ticker, _source, _config, _config.TickInterval);
    }

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _binding.Surface(surface);

    public void ApplySize(WidgetArea area) => _binding.Size(area);

    public void ApplyConfig(WidgetConfig config)
    {
        if (_ticker is null)
        {
            Configure(config);
            return;
        }

        // Only the addresses and the cap change what comes back. Toggling the preview or the source
        // line redraws what is already in hand — it must not fetch the feeds again.
        var needsFetch = Configure(config);
        if (needsFetch)
        {
            _binding.ReleaseCursor();
        }

        _binding.Apply(_source, _config, _config.TickInterval, show: needsFetch);
    }

    /// <summary>Take the config; true when the feeds it reads changed.</summary>
    private bool Configure(WidgetConfig config)
    {
        var next = config as RssConfig ?? new RssConfig();
        var previous = _applied;
        _config = next;
        _applied = (RssConfig)next.Clone();

        var needsFetch = !previous.FeedUrls.SequenceEqual(next.FeedUrls)
            || previous.EffectiveMaxItems != next.EffectiveMaxItems;

        _source.Configure(next, needsFetch);
        return needsFetch;
    }

    /// <summary>
    /// [S-1306] [S-1568] A key from the shell's keyboard lease ([S-800], a click in the widget takes
    /// it). Returns whether the key was used.
    /// </summary>
    public bool HandleKey(WidgetKey key) => _binding.HandleKey(key);

    /// <summary>After every draw: the header's age and spinner.</summary>
    private void OnRendered()
    {
        ShowSpinner(_source.Busy);
        UpdateFreshness(DateTimeOffset.Now);
    }

    private void ShowSpinner(bool on)
    {
        _spinner.IsActive = on;
        _spinner.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// [S-1303] The header's age: of the oldest list in hand, in the caution colour when a feed
    /// failed, when it is overdue, or when it was restored from disk and not yet refreshed
    /// ([S-1300]); the tooltip names every failing feed and its reason.
    /// </summary>
    private void UpdateFreshness(DateTimeOffset now)
    {
        var held = _source.Held;

        if (_binding.Screen.Kind != KitScreenKind.Items || held.Count == 0)
        {
            _freshness.Text = string.Empty;
            ToolTipService.SetToolTip(_freshness, null);
            return;
        }

        var at = held.Min(p => p.At);
        var restored = held.Any(p => p.Restored);
        var problems = _binding.Screen.HeaderProblems;
        var failed = problems.Count > 0;
        var stale = failed || restored || FetchState.IsStale(at, now, _config.TickInterval);
        var age = FetchState.Freshness(at, now);

        _freshness.Text = age;
        _freshness.Foreground = stale ? _staleBrush : _freshBrush;

        // [S-1303] A compact tile's header has room for its title or the age, not both: the age is a
        // phrase in some languages ("vor 3 Min."), and it would sit on top of the title. The title
        // wins; the age stays one step away in the tooltip and on every larger size.
        var area = _binding.Area;
        _freshness.Visibility = area.IsMeasured && area.WidthClass == SizeClass.Compact ? Visibility.Collapsed : Visibility.Visible;

        var tip = failed
            ? Str.WidgetUi_RefreshFailedSince.Format(("reason", string.Join("; ", problems)), ("time", age))
            : restored
                ? RssStr.WidgetsC_Rss_SavedRefreshing.Format(("age", age))
                : RssStr.WidgetsC_Rss_Refreshed.Format(("age", age));

        ToolTipService.SetToolTip(_freshness, tip);
    }

    public void Detach()
    {
        _binding.Dispose();
        _source.Detach();
        ShowSpinner(false);
        _ticker = null;
    }

    /// <summary>
    /// [S-2661] The widget's feeds as a kit source: what the feed sync holds for each address, kept
    /// through failures ([S-25]), merged newest first ([S-1302]). The sync is the one place a feed is
    /// fetched; this only reads it and says which feeds are on screen.
    /// </summary>
    private sealed class FeedSource(IFeedAnswers feeds) : IKitSource
    {
        /// <summary>The last answer per feed address, held through failures ([S-25]).</summary>
        private readonly Dictionary<string, Fetched<FeedDocument>> _parts = new(StringComparer.Ordinal);

        private RssConfig _config = new();
        private bool _listening;

        public event Action? Changed;

        /// <summary>The held answers of the feeds read now (for the header's age).</summary>
        public IReadOnlyList<Fetched<FeedDocument>> Held => [.. _parts.Values.Where(p => p.Value is not null)];

        public bool Busy => _config.FeedUrls.Any(url => feeds.Busy(url, _config.EffectiveMaxItems));

        public void Configure(RssConfig config, bool changed)
        {
            _config = config;
            if (changed)
            {
                _parts.Clear();
            }

            if (!_listening)
            {
                _listening = true;
                feeds.Updated += OnFeedUpdated;
            }
        }

        /// <summary>[S-2661] Tell the sync service the feeds are on screen; <paramref name="refresh"/> (the user's Refresh) asks now.</summary>
        public void Show(bool refresh)
        {
            var urls = _config.FeedUrls;
            if (urls.Count == 0)
            {
                return;
            }

            feeds.Show(urls, _config.EffectiveMaxItems, _config.TickInterval, refresh);
            Take();
        }

        /// <summary>[S-2661] A feed's answer or state changed in the sync service.</summary>
        private void OnFeedUpdated(string key)
        {
            var cap = _config.EffectiveMaxItems;
            if (!_config.FeedUrls.Any(url => feeds.Key(url, cap) == key))
            {
                return;
            }

            Take();
            Changed?.Invoke();
        }

        /// <summary>Take what the service holds for each feed, keeping the articles through a failure.</summary>
        private void Take()
        {
            var cap = _config.EffectiveMaxItems;

            foreach (var url in _config.FeedUrls)
            {
                var last = feeds.Last(url, cap);
                var held = feeds.Held(url, cap);

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
            }
        }

        /// <summary>The feeds in the order the user listed them, each with whatever is held for it.</summary>
        private List<FeedPart> Parts()
            => [.. _config.FeedUrls.Select(url => _parts.TryGetValue(url, out var part)
                ? new FeedPart(url, part.Value, part.Status, part.Detail, part.At)
                : new FeedPart(url, null, FetchStatus.Loading, null))];

        public KitRead Read()
        {
            if (_config.FeedUrls.Count == 0)
            {
                // No feed at all is the card's Set up button ([S-1862]); refused ones say why.
                var refused = _config.ResolvedFeeds.FirstOrDefault(r => !r.IsValid);
                return new KitRead([], [], Refused: _config.ResolvedFeeds.Count == 0 ? string.Empty : WebInput.Explain(refused.Problem));
            }

            var parts = Parts();
            var merged = FeedAggregate.Merge(parts, _config.EffectiveMaxItems);
            return new KitRead(
                [.. merged.Entries.Select(RssSpec.Item)],
                FeedAggregate.Outcomes(parts),
                FeedAggregate.Title(parts));
        }

#if DEBUG
        /// <summary>Debug renders only: hold these answers as if the sync had brought them.</summary>
        public void HoldForPreview(RssConfig config, IReadOnlyDictionary<string, Fetched<FeedDocument>> parts)
        {
            _config = config;
            _parts.Clear();
            foreach (var (url, part) in parts)
            {
                _parts[url] = part;
            }

            Changed?.Invoke();
        }
#endif

        public void Detach()
        {
            if (_listening)
            {
                _listening = false;
                feeds.Updated -= OnFeedUpdated;
            }
        }
    }
}
