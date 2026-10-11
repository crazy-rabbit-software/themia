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
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// The General page: language, start with Windows and the desktop. [S-3833] Edit mode is entered on
/// the desktop (and from a view's menu), the view new displays show is on the Displays page, and
/// the local network has no switch ([S-3840]).
/// </summary>
internal static partial class ConfigPages
{
    private static FrameworkElement BuildGeneral(AppHost host)
    {
        var rows = new List<FrameworkElement> { FluentLayout.Header(Str.Shell_Settings_General_System.Text()) };

        rows.Add(LanguageRow(host));

        AddStartupRows(rows, host);

        // [S-930] / [S-920] The two desktop-integration switches, as in the original's General page.
        rows.Add(DesktopIconsRow(host));
        rows.Add(GameModeRow(host));

        return Page(Str.Settings_Nav_General.Text(), rows);
    }

    /// <summary>
    /// [S-1109] The app's language: Windows' own by default, or one of the nine, switching live.
    /// Each language is listed by its own name ("Deutsch", "日本語"), the same in every language, so
    /// someone who landed in a language they cannot read still finds theirs.
    /// </summary>
    private static Border LanguageRow(AppHost host)
    {
        var options = new List<(AppLanguage Value, string Label)>
        {
            (AppLanguage.System, Str.Settings_Language_System.Text()),
            (AppLanguage.English, Str.Settings_Language_English.Text()),
            (AppLanguage.German, Str.Settings_Language_German.Text()),
            (AppLanguage.Spanish, Str.Settings_Language_Spanish.Text()),
            (AppLanguage.French, Str.Settings_Language_French.Text()),
            (AppLanguage.Portuguese, Str.Settings_Language_Portuguese.Text()),
            (AppLanguage.Italian, Str.Settings_Language_Italian.Text()),
            (AppLanguage.Polish, Str.Settings_Language_Polish.Text()),
            (AppLanguage.Japanese, Str.Settings_Language_Japanese.Text()),
            (AppLanguage.Chinese, Str.Settings_Language_Chinese.Text()),
        };

        return FluentLayout.Row(Glyphs.Language, Str.Settings_Language_Label.Text(), Str.Settings_Language_Desc.Text(),
            FluentLayout.Choice(options, host.State.Preferences.Language, host.SetLanguage),
            "language", "locale", "translation");
    }
}
