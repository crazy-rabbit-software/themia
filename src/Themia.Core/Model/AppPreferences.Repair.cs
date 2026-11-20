using Themia.Core.Settings;
using Themia.Core.Sync;
using Themia.Sdk;

namespace Themia.Core.Model;

public sealed partial class AppPreferences
{
    /// <summary>
    /// [S-702] Bring read preferences back inside their invariants, saying what changed: the language, the desktop
    /// icons' size and layout, the recent colours, the regional formats and the update cadences. Pure and total; part
    /// of <see cref="Storage.StateRepair"/>'s walk.
    /// </summary>
    public IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (!Enum.IsDefined(Language))
        {
            Language = AppLanguage.System;
            repairs.Add("unknown language; following Windows");
        }

        // [S-5593] A number that names no icon size (the names are read strictly) is Explorer's default.
        if (!Enum.IsDefined(DesktopIconSize))
        {
            DesktopIconSize = DesktopIconSize.Medium;
            repairs.Add("unknown desktop icon size; medium icons");
        }

        // [S-13970] A number that names no icon layout (the names are read strictly) is text below, Explorer's.
        if (!Enum.IsDefined(DesktopIconLayout))
        {
            DesktopIconLayout = DesktopIconLayout.Below;
            repairs.Add("unknown desktop icon layout; text below the icons");
        }

        // [S-1575] The recent colours: what is no colour dropped, each once, one line at most.
        var recent = ColourPicker.Recent(RecentColours);
        if (RecentColours is null || !recent.SequenceEqual(RecentColours))
        {
            RecentColours = [.. recent];
            repairs.Add("recent colours that are no colours, repeated or too many; kept the newest valid ones");
        }

        // [S-3772] [S-3781] The shared formats and cadences: a hand-edited null is the defaults.
        if (Region is null)
        {
            Region = new RegionPreferences();
            repairs.Add("region formats were null; following Windows");
        }

        if (Refresh is null)
        {
            Refresh = RefreshPreferences.WithDefaults();
            repairs.Add("refresh cadences were null; reset to defaults");
        }

        repairs.AddRange(Region.Repair());
        repairs.AddRange(UpdateCadence.Repair(Refresh));
        return repairs;
    }
}
