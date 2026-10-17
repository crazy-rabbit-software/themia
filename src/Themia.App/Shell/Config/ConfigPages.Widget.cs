using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>One widget's page: the shell rows every widget has, then the kind's own rows.</summary>
internal static partial class ConfigPages
{
    /// <summary>
    /// A widget's own settings.
    /// <para>
    /// Unlike the app pages these edit the live config directly and save on change, because the
    /// widget is visible on the desktop behind this window and the point is to see the effect.
    /// </para>
    /// </summary>
    private static FrameworkElement BuildWidget(AppHost host, string widgetId, Action<ConfigTarget> navigate)
    {
        if (host.Find(widgetId) is not var (_, widget) || widget is null)
        {
            return Page(Str.Settings_Style_Widget.Text(), [Note(Str.Shell_Widget_Gone.Text())]);
        }

        return WidgetPage(widget, navigate, () => host.Mutate(_ => { }));
    }

    /// <summary>
    /// [S-1542] [S-6073] A widget's page built over a throwaway copy of its config, for the search
    /// index only: the rows its page has in its current style and state ([S-6070]), so search never
    /// finds a row the page does not show, without touching the real widget. Never shown and never
    /// saved — the copy belongs to no view and its save does nothing.
    /// </summary>
    internal static SettingsPage BuildWidgetForIndex(WidgetInstance like)
    {
        ArgumentNullException.ThrowIfNull(like);
        var widget = WidgetInstance.Create(like.Kind, new Themia.Core.Geometry.FracRect(0, 0, 0.1, 0.1));
        widget.Config = like.Config.Clone();
        return WidgetPage(widget, _ => { }, () => { });
    }

    /// <summary>
    /// [S-1750] Whether a widget's page has anything on it. Read off the rows the page builder makes
    /// for a throwaway widget of the same kind and page shape ([S-6071]: its style and the rows that
    /// style and state offer), as the search index does, never a list kept beside the pages, so a
    /// kind that gains or loses its last row cannot disagree with its menus; a meter whose style
    /// draws no history has no page, the same meter as a graph has one. Built once per kind and
    /// shape and remembered. UI thread only, like the pages.
    /// </summary>
    internal static bool HasWidgetSettings(WidgetInstance like)
    {
        ArgumentNullException.ThrowIfNull(like);

        var key = (like.Kind, SettingRules.Shape(like.Config));
        if (HasSettingsByShape.TryGetValue(key, out var known))
        {
            return known;
        }

        bool has;
        try
        {
            var widget = WidgetInstance.Create(like.Kind, new Themia.Core.Geometry.FracRect(0, 0, 0.1, 0.1));
            widget.Config = like.Config.Clone();
            has = WidgetRows(widget, _ => { }, () => { }).Count > 0;
        }
        catch (Exception ex)
        {
            // Offer the page rather than hide settings that may well exist.
            Log.Error(Scope, $"could not read the rows of the {like.Kind} settings page", ex);
            return true;
        }

        HasSettingsByShape[key] = has;
        return has;
    }

    private static readonly Dictionary<(WidgetId Kind, string Shape), bool> HasSettingsByShape = [];

    /// <summary>
    /// [S-6536] The widget's own options and nothing else: its title, style, header, background,
    /// item background, item text and scale are the properties panel's ([S-3707]), their one place.
    /// </summary>
    private static SettingsPage WidgetPage(WidgetInstance widget, Action<ConfigTarget> navigate, Action save)
    {
        var definition = ModuleCatalog.Of(widget.Kind);
        var rows = WidgetRows(widget, navigate, save);
        return Page(string.IsNullOrWhiteSpace(widget.Label) ? definition.Name : widget.Label!, rows, definition.Describe());
    }

    /// <summary>The kind's own rows, in the page's order, the danger section last.</summary>
    private static List<FrameworkElement> WidgetRows(WidgetInstance widget, Action<ConfigTarget> navigate, Action save)
    {
        var rows = new List<FrameworkElement>();

        void Save() => save();

        // [S-6904] The widget's own rows are its module's; a widget with none has no page ([S-1750]).
        ModuleHost.Module(widget.Kind).SettingsRows(new WidgetSettingsRows(rows, Save, () => navigate(new ConfigTarget.App(AppPage.Accounts))), widget.Config);

        // Every widget page is laid out the same way: what it shows, then how it behaves, then how
        // often it updates, and destructive actions last. A row the widget built with
        // FluentLayout.DangerRow is lifted out of wherever it sat and into the one danger section
        // below, so nothing that cannot be undone is left sitting between two switches. [S-3705]
        // The card itself (title, style, header, background, items, scale) is set on the
        // properties panel ([S-6536]), and the widget is deleted from its card menu or the Delete
        // key: neither has a row here.
        var danger = rows.FindAll(FluentLayout.IsDanger);
        rows.RemoveAll(FluentLayout.IsDanger);

        if (danger.Count > 0)
        {
            rows.Add(FluentLayout.Header(Str.Shell_Settings_Danger.Text()));
            rows.AddRange(danger);
        }

        // [S-1752] No heading over nothing: a section whose rows this style or state leaves out
        // goes with them, and a page of headings alone is no page ([S-1750]).
        var keep = PageSections.Keep([.. rows.Select(FluentLayout.IsHeader)]);
        return [.. keep.Select(i => rows[i])];
    }
}
