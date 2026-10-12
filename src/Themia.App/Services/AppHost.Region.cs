using Themia.App.Interop;
using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-3770] [S-3780] The settings every widget shares instead of carrying its own: the regional
/// formats and the update cadences of the online sources. Put in force before anything is drawn and
/// again on every state change, before the sync hears of it, so a widget and the sync never disagree
/// about the unit or the interval.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>Called from the constructor right after the language, before any surface subscribes.</summary>
    private void StartRegion()
    {
        StateChanged += ApplyRegion;
        ApplyRegion();
    }

    /// <summary>
    /// Cheap and idempotent: Windows' format is re-read (the user may have changed it in Windows
    /// Settings), and <see cref="RegionFormat"/> announces only a real change.
    /// </summary>
    private void ApplyRegion()
    {
        RegionFormat.SetWindows(SystemRegion.Read(RegionFormat.Language(Strings.Current)));
        RegionCurrency.Country = SystemRegion.Country();
        RegionFormat.Apply(RegionFormat.For(State.Preferences.Region));
        UpdateCadence.Current = State.Preferences.Refresh;
    }
}
