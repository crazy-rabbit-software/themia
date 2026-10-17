using Themia.Core.Diagnostics;
using Windows.UI.ViewManagement;

namespace Themia.App.Shell.Motion;

/// <summary>
/// [S-1010] Windows' "Animation effects" switch (Settings › Accessibility › Visual effects), the
/// desktop's equivalent of the original's <c>prefers-reduced-motion</c>. Read at the moment an intro
/// would start, so flipping it applies to the next one without a restart.
/// </summary>
public static class SystemMotion
{
    private const string Scope = "motion";

    private static UISettings? _settings;

    /// <summary>False when the user turned animation effects off: then no intro plays and no sound sounds.</summary>
    public static bool AnimationsEnabled
    {
        get
        {
            try
            {
                _settings ??= new UISettings();
                return _settings.AnimationsEnabled;
            }
            catch (Exception ex)
            {
                // Unknowable is treated as "on": the intro is short, cosmetic and never blocks input,
                // so guessing wrong costs half a second of motion, not a broken desktop.
                Log.Warn(Scope, $"could not read the Windows animation setting; assuming animations are on ({ex.Message})");
                return true;
            }
        }
    }
}
