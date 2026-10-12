#if DEBUG
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1109] The debug renders' language switch: <c>--lang &lt;code&gt;</c> (any code of <see cref="LanguageResolver.All"/>, e.g. <c>de</c>, <c>ar</c>) on any
/// render flag (<c>--settings-png</c>, <c>--picker-png</c>, …) draws it in that language, so a
/// translation can be checked without changing anyone's settings.
/// </summary>
internal static class RenderLanguage
{
    private const string Scope = "render";

    /// <summary>The language <c>--lang</c> asked for; null when absent or not one Themia has.</summary>
    public static Locale? Requested(string[] args)
    {
        var at = Array.IndexOf(args, "--lang");
        if (at < 0)
        {
            return null;
        }

        if (at + 1 < args.Length && LanguageResolver.FromTag(args[at + 1]) is { } locale)
        {
            return locale;
        }

        Log.Error(Scope, "usage: --lang <en|de|es|fr|it|ja|pl|pt|zh|hi|ar|bn|ru|id|ur|mr|vi|ko|nl|tr|zh-Hant|te|ha|pa|sw|fil|ta|fa|th|gu>; rendering in English");
        return null;
    }

    /// <summary>Switch the string layer before anything is drawn. Renders that build an app host also pass <see cref="Setting"/> into its state.</summary>
    public static void Apply(string[] args)
    {
        if (Requested(args) is { } locale)
        {
            Strings.SetLocale(locale);
        }
    }

    /// <summary>The language setting that resolves to the requested locale, so a host built over a demo state keeps it.</summary>
    public static AppLanguage? Setting(string[] args)
        => Requested(args) is { } locale ? LanguageResolver.Setting(locale) : null;
}
#endif
