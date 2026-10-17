using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// One feed the widget reads. A class rather than a bare string so the settings list can edit an
/// entry in place and tell two identical addresses apart.
/// </summary>
public sealed class RssFeed
{
    /// <summary>The feed address, exactly as typed. Never repaired; refused values stay refused.</summary>
    public string Url { get; set; } = string.Empty;

    public RssFeed Clone() => new() { Url = Url };
}

/// <summary>
/// Headlines from one or several feeds, merged newest first ([S-1302]).
/// <para>
/// The addresses come from a settings list and from a hand-editable JSON file, so each is checked
/// by <see cref="WebInput.Url"/> on the way in from both — see <see cref="ResolvedFeeds"/>. A feed
/// is also the one thing this app reads that is written by somebody else entirely: every title,
/// link and summary that comes back is attacker-controlled text, displayed and never interpreted.
/// </para>
/// </summary>
public sealed class RssConfig : WidgetConfig
{
    /// <summary>How many feeds one widget merges. Each is a request per refresh.</summary>
    public const int MaxFeeds = 12;

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Rss;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("rss"),
        Kind = WidgetKind.Rss,
        ConfigType = typeof(RssConfig),
        CreateDefault = () => new RssConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["rss", "feed", "news", "headlines", "atom", "blog", "reader"],
        LabelId = Str.WidgetsA_Catalog_Feed_Label,
        DescriptionId = Str.WidgetsA_Catalog_Feed_Description,
        Glyph = Glyphs.Rss,
        DefaultWidth = 360,
        DefaultHeight = 280,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
    };

    /// <summary>The feeds, in the order the user listed them.</summary>
    public List<RssFeed> Feeds { get; set; } = [];

    /// <summary>
    /// How many items to show, 5 to 50, across all the feeds together.
    /// <para>
    /// A display cap, not a fetch count. Themia asks each address for whatever it serves and then
    /// trims; a host that publishes ten items can never fill a setting of fifty, and raising the
    /// number changes nothing.
    /// </para>
    /// </summary>
    public int MaxItems { get; set; } = 20;

    /// <summary>Show the site the item came from: its favicon, and its name where there is room.</summary>
    public bool ShowSource { get; set; } = true;

    /// <summary>Show the summary under the title.</summary>
    public bool ShowPreview { get; set; } = true;

    /// <summary>Show how long ago the item was published.</summary>
    public bool ShowDate { get; set; } = true;

    [JsonIgnore]
    public int EffectiveMaxItems => Math.Clamp(MaxItems, 5, 50);

    /// <summary>
    /// Every listed address with its verdict, in order, blanks dropped and repeats folded. Read on
    /// every use rather than cached, so a value hand-edited into the state file goes through the
    /// same gate the settings box uses.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<WebUrlResult> ResolvedFeeds
    {
        get
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<WebUrlResult>();

            foreach (var feed in Feeds.Take(MaxFeeds))
            {
                if (string.IsNullOrWhiteSpace(feed?.Url))
                {
                    continue;
                }

                var result = WebInput.Url(feed.Url);
                var key = result.IsValid ? result.Url : feed.Url.Trim();

                if (seen.Add(key))
                {
                    list.Add(result);
                }
            }

            return list;
        }
    }

    /// <summary>The addresses that passed the gate, in order. What the widget fetches.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> FeedUrls => [.. ResolvedFeeds.Where(r => r.IsValid).Select(r => r.Url)];

    /// <summary>[S-3780] The shared feeds cadence (Settings › Widgets).</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => Sync.UpdateCadence.Interval(Sync.UpdateSource.Feeds);

    /// <summary>A hand-edited list with nulls in it, or more feeds than the cap, is put back in shape.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (Feeds is null)
        {
            Feeds = [];
            repairs.Add("rss: missing feed list");
        }

        var before = Feeds.Count;
        Feeds = [.. Feeds.Where(feed => feed is not null).Take(MaxFeeds)];

        if (Feeds.Count != before)
        {
            repairs.Add($"rss: dropped {before - Feeds.Count} empty or extra feed entries");
        }

        foreach (var feed in Feeds)
        {
            feed.Url ??= string.Empty;
        }

        return repairs;
    }

    public override WidgetConfig Clone() => new RssConfig
    {
        Feeds = [.. Feeds.Select(feed => feed.Clone())],
        MaxItems = MaxItems,
        ShowSource = ShowSource,
        ShowPreview = ShowPreview,
        ShowDate = ShowDate,
    };
}
