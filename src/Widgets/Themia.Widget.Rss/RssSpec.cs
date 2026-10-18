using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.Kit;
using Themia.Widget.Rss;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1305] [S-1306] The Rss widget on the kit: its shape (the rich look, every optional field, up
/// to three columns, ages), the item a feed entry is, and the actions a headline offers. The
/// breakpoints are the kit's ladder's ([S-7031]); the feed is its headlines at every size, with no
/// reading pane beside the list (user decision).
/// </summary>
public static class RssSpec
{
    /// <summary>The rich look with all four fields and up to three columns.</summary>
    public static KitSpec Spec { get; } = new(
        KitRowLook.Rich,
        KitShow.All,
        () => RssStr.WidgetsC_Rss_Untitled.Text(),
        () => RssStr.WidgetsC_Rss_NoArticles.Text(),
        KitTime.Age,
        MaxColumns: 3);

    /// <summary>The fields the settings turn on: the source is the site's icon and its name, together.</summary>
    public static KitShow Shows(bool source, bool preview, bool date)
        => (source ? KitShow.Lead | KitShow.Subtitle : KitShow.None)
            | (preview ? KitShow.Preview : KitShow.None)
            | (date ? KitShow.When : KitShow.None);

    /// <summary>
    /// A headline as a kit item: its site's name over it (the subtitle), the site's own icon (the
    /// lead), the summary (the preview), its age. Its place in the merged list is its id, so the
    /// keyboard cursor stays on the place it was, as it always has, when the list is merged again.
    /// </summary>
    public static KitItem Item(FeedEntry entry, int index)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var link = entry.Link.Length > 0 && Uri.TryCreate(entry.Link, UriKind.Absolute, out var uri) ? uri : null;
        var host = FeedAggregate.SiteHost(entry.Link);

        return new KitItem(
            index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            entry.Title,
            Subtitle: host.Length > 0 ? host : null,
            Preview: entry.Summary.Length > 0 ? entry.Summary : null,
            Lead: link is not null ? KitLead.Site(link) : KitLead.None,
            When: entry.Published is { } published ? KitWhen.Instant(published) : null,
            Link: link);
    }

    /// <summary>[S-1306] A headline's menu: open, copy the link, refresh the feeds.</summary>
    public static IReadOnlyList<KitAction> Actions(KitItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return
        [
            new KitAction(KitActionKind.Open, Str.Common_Open.Text(), Target: item.Link),
            new KitAction(KitActionKind.CopyLink, RssStr.WidgetsC_Rss_CopyLink.Text(), Text: item.Link?.OriginalString),
            new KitAction(KitActionKind.Refresh, Str.Common_Refresh.Text()),
        ];
    }
}
