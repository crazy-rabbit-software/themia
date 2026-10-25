using Themia.App.Interop;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Services;

/// <summary>
/// [S-1109] The language setting: resolved to a <see cref="Locale"/> before anything is drawn, and
/// again whenever it may have changed, switching <see cref="Strings"/> live. Whatever shows text
/// follows through <see cref="Strings.Changed"/> — no restart.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>[S-3536] The language Windows gives Themia: what its "Windows setting" speaks.</summary>
    public Locale WindowsLanguage => LanguageResolver.Resolve(AppLanguage.System, SystemLanguages.Preferred());

    /// <summary>[S-3536] Choose the language from ⚙ › Language &amp; region. Applies at once.</summary>
    public void SetLanguage(AppLanguage language)
    {
        Mutate(state => state.Preferences.Language = language, relayout: false);
        ApplyLanguage();
    }

    /// <summary>Called from the constructor, before any window exists, so the first frame is already translated.</summary>
    private void StartLanguage()
    {
        // An import or a settings reset changes the language through a state change.
        StateChanged += ApplyLanguage;
        ApplyLanguage();
    }

    /// <summary>Cheap and idempotent: <see cref="Strings.SetLocale"/> does nothing when the locale is unchanged.</summary>
    private void ApplyLanguage()
        => Strings.SetLocale(LanguageResolver.Resolve(State.Preferences.Language, SystemLanguages.Preferred()));
}
