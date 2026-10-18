using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell.Config;

/// <summary>
/// The Rss widget's settings rows.
/// <para>
/// The address boxes are the trust boundary on this page: whatever is typed is fetched on every
/// tick, so each is checked by <see cref="WebInput"/> before it is written and the reason it was
/// refused is shown under the box rather than swallowed. It is never repaired into something that
/// works. [S-1302] Several feeds, merged newest first, as the original's feed selection.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    public static void Rss(List<FrameworkElement> rows, RssConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_Rss_Settings_Feeds.Text()));

        FluentLayout.ListEditor(rows, new ListEditorOptions<RssFeed>
        {
            Items = config.Feeds,
            Body = feed => KitRows.InputBox(feed.Url, Refusal, typed => { feed.Url = typed; save(); }, "https://example.com/feed.xml", WebInput.MaxUrlLength),
            Save = save,
            EmptyText = Str.WidgetsC_Rss_Settings_Empty.Text(),
            AddTitle = Str.WidgetsC_Rss_Settings_Add_Title.Text(),
            AddDescription = Str.WidgetsC_Rss_Settings_Add_Desc.Text(),
            AddAction = Str.WidgetsC_Rss_Settings_Add_Action.Text(),
            Create = () => new RssFeed(),
            Capacity = RssConfig.MaxFeeds,
            RemoveTooltip = Str.WidgetsC_Rss_Settings_Remove.Text(),
        });

        rows.Add(FluentLayout.Header(Str.WidgetsC_Rss_Settings_Articles.Text()));

        KitRows.Shows(rows,
        [
            new(Glyphs.Link, Str.WidgetsC_Rss_Settings_Source_Label.Text(), Str.WidgetsC_Rss_Settings_Source_Desc.Text(), () => config.ShowSource, value => config.ShowSource = value),
            new(Glyphs.Preview, Str.WidgetsC_Rss_Settings_Preview_Label.Text(), Str.WidgetsC_Rss_Settings_Preview_Desc.Text(), () => config.ShowPreview, value => config.ShowPreview = value),
            new(Glyphs.Clock, Str.WidgetsC_Rss_Settings_Date_Label.Text(), Str.WidgetsC_Rss_Settings_Date_Desc.Text(), () => config.ShowDate, value => config.ShowDate = value),
        ], save);

        KitRows.Count(rows, Glyphs.Count, Str.WidgetsC_Rss_Settings_Articles.Text(), null, config.EffectiveMaxItems, 5, 50,
            value => { config.MaxItems = value; save(); });
    }

    /// <summary>
    /// [S-1302] Why a typed feed address is refused (the same gate the feed service checks again), or
    /// null to accept it.
    /// </summary>
    private static string? Refusal(string typed)
    {
        var checkedUrl = WebInput.Url(typed);
        return checkedUrl.IsValid ? null : WebInput.Explain(checkedUrl.Problem);
    }
}
