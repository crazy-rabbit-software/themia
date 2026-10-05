using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;

namespace Themia.App.Shell.Config;

/// <summary>One suggestion: what matched, where it lives, and the icon its row shows.</summary>
internal sealed record SettingHit(SettingEntry Entry, ConfigTarget Target, string Glyph);

/// <summary>
/// Everything "Find a setting" can find, read off the pages themselves. [S-1542]
/// <para>
/// The index is the rows the pages build, collected through <see cref="SettingsIndex"/> — never a
/// list written beside them. It is filled the first time the search box is used (every app page,
/// Displays and Views included, and one page per widget kind in use, built off-screen and thrown away),
/// refreshed with the live page each time a page is shown, and dropped when the navigation changes
/// shape. A widget kind's rows are indexed once and offered for every widget of that kind, under
/// "View › Widget", because the rows of a kind's page are the same for each of its widgets. A row in
/// a group of the Displays or Views page is offered as "Views › Desktop" and opens that group ([S-1832]).
/// </para>
/// </summary>
internal sealed class SettingsSearchIndex(AppHost host)
{
    private const string Scope = "settings-search";

    private readonly Dictionary<string, List<SettingHit>> _pages = new(StringComparer.Ordinal);
    private bool _built;

    /// <summary>Forget everything; the next search rebuilds. Called when pages come and go.</summary>
    public void Invalidate()
    {
        _pages.Clear();
        _built = false;
    }

    /// <summary>The live page was just built: what it shows now is what search knows of it.</summary>
    public void Update(ConfigTarget target, FrameworkElement page)
    {
        if (!_built)
        {
            return; // the full build will read it with the rest
        }

        // A list page opened on one of its groups is still the one page.
        var key = target is ConfigTarget.App ? target.NavEntry() : target;
        Index(key, page, PathOf(key), Glyph(key));
    }

    /// <summary>The best suggestions for what was typed, best first.</summary>
    public IReadOnlyList<SettingHit> Find(string query)
    {
        EnsureBuilt();

        var hits = new Dictionary<SettingEntry, SettingHit>();
        foreach (var page in _pages.Values)
        {
            foreach (var hit in page)
            {
                hits.TryAdd(hit.Entry, hit);
            }
        }

        return [.. SettingsSearch.Find(hits.Keys, query).Select(m => hits[m.Entry])];
    }

    private void EnsureBuilt()
    {
        if (_built)
        {
            return;
        }

        _built = true;

        foreach (var entry in ConfigPages.AppPages)
        {
            Build(new ConfigTarget.App(entry.Page));
        }

        // One page per kind in use, offered for every widget of that kind.
        foreach (var group in host.State.Views
                     .SelectMany(v => v.Widgets.Select(w => (View: v, Widget: w)))
                     .GroupBy(x => x.Widget.Kind))
        {
            SettingsPage page;
            try
            {
                page = ConfigPages.BuildWidgetForIndex(host, group.Key);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, $"could not index the {group.Key} settings", ex);
                continue;
            }

            foreach (var (view, widget) in group)
            {
                var target = new ConfigTarget.Widget(widget.Id);
                Index(target, page, PathOf(target), Glyph(target));
            }
        }
    }

    private void Build(ConfigTarget target)
    {
        try
        {
            Index(target, ConfigPages.Build(new PageContext(host, _ => { }, new SettingsExpansion()), target), PathOf(target), Glyph(target));
        }
        catch (Exception ex)
        {
            // One page that cannot be built leaves the others searchable.
            Log.Error(Scope, $"could not index {target}", ex);
        }
    }

    private void Index(ConfigTarget target, FrameworkElement page, string path, string pageGlyph)
    {
        var key = target.ToString();
        var hits = new List<SettingHit>
        {
            // The page itself, so its name finds it.
            new(new SettingEntry(key, path, null, LastCrumb(path), null, [], IsPage: true), target, pageGlyph),
        };

        var rows = SettingsIndex.Collect(page);

        // [S-1832] The groups of a list page by item: a row inside one is offered under its name.
        var groups = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (row, _) in rows)
        {
            if (row.Item is { } item && row.Parent is null)
            {
                groups.TryAdd(item, row.Title);
            }
        }

        foreach (var (row, section) in rows)
        {
            var rowTarget = target;
            var rowPath = path;

            if (row.Item is { } item && target is ConfigTarget.App app)
            {
                rowTarget = app with { Item = item };
                if (row.Parent is not null && groups.TryGetValue(item, out var group))
                {
                    rowPath = $"{path} › {group}";
                }
            }

            var entry = new SettingEntry(key, rowPath, section, row.Title, row.Description, row.AllKeywords());
            hits.Add(new SettingHit(entry, rowTarget, GlyphOf(row) ?? pageGlyph));
        }

        _pages[key] = hits;
    }

    private static string LastCrumb(string path)
    {
        var at = path.LastIndexOf(" › ", StringComparison.Ordinal);
        return at < 0 ? path : path[(at + 3)..];
    }

    /// <summary>Where a page is, as a suggestion shows it: "Appearance", "Views › Desktop › Clock".</summary>
    public string PathOf(ConfigTarget target) => target switch
    {
        ConfigTarget.App app => ConfigPages.AppPages.FirstOrDefault(p => p.Page == app.Page)?.Title ?? app.Page.ToString(),
        ConfigTarget.Widget widget when host.Find(widget.WidgetId) is var (view, instance) && instance is not null =>
            $"{Str.App_Views.Text()} › {view.Name} › {Label(instance)}",
        _ => Str.Common_Settings.Text(),
    };

    private string Glyph(ConfigTarget target) => target switch
    {
        ConfigTarget.App app => ConfigPages.AppPages.FirstOrDefault(p => p.Page == app.Page)?.Glyph ?? Glyphs.Settings,
        ConfigTarget.Widget widget when host.Find(widget.WidgetId) is var (_, instance) && instance is not null =>
            WidgetCatalog.Definition(instance.Kind).Glyph,
        _ => Glyphs.Settings,
    };

    private static string Label(WidgetInstance widget)
        => string.IsNullOrWhiteSpace(widget.Label) ? WidgetCatalog.Definition(widget.Kind).Label : widget.Label!;

    private static string? GlyphOf(SettingRow row) => row.Card switch
    {
        SettingsCard { HeaderIcon: FontIcon icon } => icon.Glyph,
        SettingsExpander { HeaderIcon: FontIcon icon } => icon.Glyph,
        _ => null,
    };
}
