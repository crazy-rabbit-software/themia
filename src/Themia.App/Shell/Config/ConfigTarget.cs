namespace Themia.App.Shell.Config;

/// <summary>Which page of the config window to show. [S-1831]</summary>
internal abstract record ConfigTarget
{
    /// <summary>
    /// A page of the navigation. A widget has no page here: its options are on its properties menu
    /// ([S-6536]); a display's and a view's are the edit bar's Display and View menus ([S-3540]).
    /// </summary>
    public sealed record App(AppPage Page = default) : ConfigTarget;
}

/// <summary>
/// The app-wide pages. A value only becomes a page in the window once it has an entry in
/// <see cref="ConfigPages.AppPages"/>. [S-3584] None is left: the licence, the settings file and
/// About are ⚙'s Themia Pro, Back up &amp; reset and About Themia.
/// </summary>
internal enum AppPage
{
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
internal sealed record PageContext(Services.AppHost Host);

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
