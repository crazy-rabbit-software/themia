using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Rss;

/// <summary>
/// [S-7510] The Rss widget's options, all on its properties menu ([S-3719]): Appearance ([S-3723]),
/// the feeds as the menu's editable list ([S-3729]), the kit's switches for the optional fields and
/// how many articles ([S-7063]). It has no settings page.
/// <para>
/// The typed address is the trust boundary here: whatever is given is fetched on every tick, so it is
/// checked by <see cref="WebInput"/> as it is typed, cannot be given while refused, and the reason is
/// shown under the box. It is never repaired into something that works. [S-1302] Several feeds,
/// merged newest first.
/// </para>
/// </summary>
internal static class RssMenu
{
    /// <summary>What the address box shows while empty: a feed's shape on the reserved domain.</summary>
    private const string Example = "https://example.com/feed.xml";

    public static void Lines(WidgetMenuLines menu, RssConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        menu.Lines.Add(WidgetMenuLines.SetsUp(Feeds(menu, config)));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(KitMenuLines.Shows(menu,
        [
            new(RssStr.WidgetsC_Rss_Settings_Source_Label.Text(), () => config.ShowSource, on => config.ShowSource = on),
            new(RssStr.WidgetsC_Rss_Settings_Preview_Label.Text(), () => config.ShowPreview, on => config.ShowPreview = on),
            new(RssStr.WidgetsC_Rss_Settings_Date_Label.Text(), () => config.ShowDate, on => config.ShowDate = on),
        ]));
        menu.Lines.Add(KitMenuLines.Count(menu, Glyphs.Count, RssStr.WidgetsC_Rss_Settings_Articles.Text(), config.EffectiveMaxItems, 5, 50, 5, value => config.MaxItems = value));
    }

    /// <summary>
    /// [S-7510] The feeds, in their order: each a submenu of Change address…, Move up, Move down and
    /// Remove; Add… asks for an address; at twelve the add line is greyed.
    /// </summary>
    private static ItemMenuEntry Feeds(WidgetMenuLines menu, RssConfig config)
        => menu.List(Glyphs.Rss, RssStr.WidgetsC_Rss_Settings_Feeds.Text(), config.Feeds,
            new MenuList<RssFeed>(Name, RssStr.WidgetsC_Rss_Settings_Empty.Text())
            {
                Glyph = _ => Glyphs.Link,
                Extra = feed =>
                [
                    new(Glyphs.Edit, RssStr.WidgetsC_Rss_Menu_ChangeAddress.Text(), () => menu.Ask(new WidgetTextAsk(
                        RssStr.WidgetsC_Rss_Menu_ChangeAddress.Text().TrimEnd('…'),
                        Example,
                        Str.Common_Save.Text(),
                        Refusal,
                        typed => Readdress(feed, typed, menu),
                        Initial: feed.Url))),
                ],
                Add = new MenuListAdd<RssFeed>(RssStr.WidgetsC_Rss_Settings_Add_Title.Text(), Example, Refusal, typed => new RssFeed { Url = typed.Trim() }),
                Max = RssConfig.MaxFeeds,
            });

    /// <summary>[S-7510] A feed's line: its address without the scheme, which says nothing.</summary>
    internal static string Name(RssFeed feed)
    {
        var url = feed.Url.Trim();
        foreach (var scheme in (string[])["https://", "http://"])
        {
            if (url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            {
                return url[scheme.Length..].TrimEnd('/');
            }
        }

        return url;
    }

    /// <summary>A typed address, checked by <see cref="Refusal"/> before it gets here; saved only when it changed.</summary>
    private static void Readdress(RssFeed feed, string typed, WidgetMenuLines menu)
    {
        var url = typed.Trim();
        if (url != feed.Url)
        {
            feed.Url = url;
            menu.Save();
        }
    }

    /// <summary>
    /// [S-1302] Why a typed feed address is refused (the same gate the feed service checks again), or
    /// null to accept it.
    /// </summary>
    internal static string? Refusal(string typed)
    {
        var checkedUrl = WebInput.Url(typed);
        return checkedUrl.IsValid ? null : WebInput.Explain(checkedUrl.Problem);
    }
}
