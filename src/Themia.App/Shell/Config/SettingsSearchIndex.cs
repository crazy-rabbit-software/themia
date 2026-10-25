using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell.Config;

/// <summary>One suggestion: what matched, where it lives, and the icon its row shows.</summary>
internal sealed record SettingHit(SettingEntry Entry, ConfigTarget Target, string Glyph);

/// <summary>
/// Everything "Find a setting" can find, read off the pages themselves. [S-1542]
/// <para>
/// The index is the rows the pages build, collected through <see cref="SettingsIndex"/> — never a
/// list written beside them. It is filled the first time the search box is used (every app page
/// built off-screen and thrown away), refreshed with the live page each time a page is shown, and
/// dropped when the language changes. A widget's own options are on its properties menu, not in this
/// window ([S-6536]), so search finds none of them.
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

        Index(target, page, PathOf(target), Glyph(target));
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
    }

    private void Build(ConfigTarget target)
    {
        try
        {
            Index(target, ConfigPages.Build(new PageContext(host), target), PathOf(target), Glyph(target));
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

        foreach (var (row, section) in SettingsIndex.Collect(page))
        {
            var entry = new SettingEntry(key, path, section, row.Title, row.Description, row.AllKeywords());
            hits.Add(new SettingHit(entry, target, GlyphOf(row) ?? pageGlyph));
        }

        _pages[key] = hits;
    }

    private static string LastCrumb(string path)
    {
        var at = path.LastIndexOf(" › ", StringComparison.Ordinal);
        return at < 0 ? path : path[(at + 3)..];
    }

    /// <summary>Where a page is, as a suggestion shows it: "Accounts".</summary>
    private static string PathOf(ConfigTarget target) => target switch
    {
        ConfigTarget.App app => ConfigPages.AppPages.FirstOrDefault(p => p.Page == app.Page)?.Title ?? app.Page.ToString(),
        _ => Str.Common_Settings.Text(),
    };

    private static string Glyph(ConfigTarget target) => target switch
    {
        ConfigTarget.App app => ConfigPages.AppPages.FirstOrDefault(p => p.Page == app.Page)?.Glyph ?? Glyphs.Settings,
        _ => Glyphs.Settings,
    };

    private static string? GlyphOf(SettingRow row) => row.Card switch
    {
        SettingsCard { HeaderIcon: FontIcon icon } => icon.Glyph,
        SettingsExpander { HeaderIcon: FontIcon icon } => icon.Glyph,
        _ => null,
    };
}
