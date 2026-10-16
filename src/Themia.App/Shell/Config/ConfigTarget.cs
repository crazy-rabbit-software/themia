using Themia.Core.Settings;

namespace Themia.App.Shell.Config;

/// <summary>Which page of the config window to show. [S-1831]</summary>
internal abstract record ConfigTarget
{
    /// <summary>A page of the navigation.</summary>
    /// <param name="Item">
    /// On a list page (Displays, Views), the display or view whose group is opened and scrolled
    /// into view; null for the page as the reader left it. Ignored by every other page.
    /// </param>
    public sealed record App(AppPage Page = AppPage.General, string? Item = null) : ConfigTarget;

    /// <summary>One widget's settings, under Views.</summary>
    public sealed record Widget(string WidgetId) : ConfigTarget;

    /// <summary>The Views page with this view's group open: what "View settings" opens.</summary>
    public static App ForView(string viewId) => new(AppPage.Views, viewId);

    /// <summary>
    /// The page without the item it opened: what the navigation selects for it and what search
    /// keys it by. A widget's page sits under Views, so Views stays selected while one is open.
    /// </summary>
    public App NavEntry() => this switch
    {
        App app => app.Item is null ? app : new App(app.Page),
        _ => new App(AppPage.Views),
    };

    /// <summary>The list a page's groups belong to, or null for a page that is not a list.</summary>
    public static SettingsList? ListOf(AppPage page) => page switch
    {
        AppPage.Displays => SettingsList.Displays,
        AppPage.Views => SettingsList.Views,
        _ => null,
    };
}

/// <summary>
/// The app-wide pages. A value only becomes a page in the window once it has an entry in
/// <see cref="ConfigPages.AppPages"/>; until then it is reserved for the package building it.
/// </summary>
internal enum AppPage
{
    General,
    Appearance,
    Accounts,
    About,

    /// <summary>Microsoft Store licence state.</summary>
    License,

    /// <summary>[S-1830] Every connected display, one group each.</summary>
    Displays,

    /// <summary>[S-1830] Every view, one group each, with its widgets.</summary>
    Views,

    /// <summary>[S-3780] The update cadences every widget shares.</summary>
    Widgets,

    /// <summary>[S-3797] The display language and the regional formats every widget shares.</summary>
    LanguageRegion,
}

/// <summary>Where an app page sits in the settings navigation.</summary>
internal enum AppPageSlot
{
    /// <summary>Under the "Themia" heading at the top.</summary>
    Main,

    /// <summary>Pinned to the foot of the menu.</summary>
    Footer,
}

/// <summary>What a page is built with.</summary>
/// <param name="Navigate">Go to another page (a widget's row drills into its page).</param>
/// <param name="Expansion">[S-1833] The open groups of the list pages, kept by the window.</param>
internal sealed record PageContext(Services.AppHost Host, Action<ConfigTarget> Navigate, SettingsExpansion Expansion);

/// <summary>One app-wide settings page: its menu entry and the code that builds its body.</summary>
/// <param name="Glyph">The monochrome glyph search results show for the page.</param>
/// <param name="Icon">[S-1600] The full-colour icon the navigation shows for the page.</param>
internal sealed record AppPageEntry(
    AppPage Page,
    Themia.Core.Localization.Str TitleId,
    string Glyph,
    NavIcon Icon,
    AppPageSlot Slot,
    Func<PageContext, Microsoft.UI.Xaml.FrameworkElement> Build)
{
    /// <summary>The page's name in the current language (the list is static; the name is read when shown).</summary>
    public string Title => Themia.Core.Localization.StrExtensions.Text(TitleId);
}
