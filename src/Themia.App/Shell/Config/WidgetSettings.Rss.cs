using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.System;

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
            Body = feed => FeedAddress(feed, save),
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

        rows.Add(FluentLayout.Row(Glyphs.Link, Str.WidgetsC_Rss_Settings_Source_Label.Text(),
            Str.WidgetsC_Rss_Settings_Source_Desc.Text(),
            FluentLayout.Toggle(config.ShowSource, value => { config.ShowSource = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Preview, Str.WidgetsC_Rss_Settings_Preview_Label.Text(), Str.WidgetsC_Rss_Settings_Preview_Desc.Text(),
            FluentLayout.Toggle(config.ShowPreview, value => { config.ShowPreview = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Clock, Str.WidgetsC_Rss_Settings_Date_Label.Text(), Str.WidgetsC_Rss_Settings_Date_Desc.Text(),
            FluentLayout.Toggle(config.ShowDate, value => { config.ShowDate = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Count, Str.WidgetsC_Rss_Settings_Articles.Text(),
            null,
            FluentLayout.Slider(config.EffectiveMaxItems, 5, 50, 1, string.Empty,
                value => { config.MaxItems = (int)Math.Round(value); save(); })));
    }

    /// <summary>
    /// One feed's address box, checked on commit (Enter or leaving the box), with its verdict
    /// under it. A refused address is not written; an empty one is kept as a blank to fill in.
    /// </summary>
    private static FrameworkElement FeedAddress(RssFeed feed, Action save)
    {
        var address = new TextBox
        {
            Text = feed.Url,
            MinWidth = 260,
            MaxLength = WebInput.MaxUrlLength,
            PlaceholderText = "https://example.com/feed.xml",
        };

        var verdict = FluentLayout.Verdict();

        void Commit()
        {
            var typed = address.Text.Trim();

            if (typed == feed.Url)
            {
                return;
            }

            var checkedUrl = WebInput.Url(typed);

            // Refused here, where the user can see why, rather than accepted and then refused again
            // silently on every tick. Never repaired: prefixing "https://" onto "example.com/feed"
            // would mean the widget fetches something the settings do not say.
            if (typed.Length > 0 && !checkedUrl.IsValid)
            {
                FluentLayout.Refuse(verdict, WebInput.Explain(checkedUrl.Problem));
                return;
            }

            FluentLayout.Confirm(verdict, string.Empty);
            feed.Url = typed;
            save();
        }

        // On commit, not per keystroke: a URL is only valid once it has been fully typed.
        address.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Enter)
            {
                args.Handled = true;
                Commit();
            }
        };

        address.LostFocus += (_, _) => Commit();

        return new StackPanel { Spacing = Fluent.Space1, Children = { address, verdict } };
    }
}
