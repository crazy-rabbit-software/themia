using Themia.App.Services.Licensing;
using Themia.Core.Diagnostics;
using Themia.Core.Licensing;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Services;

/// <summary>
/// [S-1065] The free-tier gate, at every place a widget or a view is created: placement (when it
/// starts and when it lands), paste, duplicate, a new view, and the view a
/// new display would get. A refusal creates nothing, deletes nothing, and says why in a notice
/// ([S-881]) whose button opens ⚙ › Themia Pro on that display, which offers the upgrade ([S-3573]). A notice
/// rather than a menu thrown open: a refusal is news, and opening something over the desktop in
/// the middle of arranging it is a heavier answer than the question deserves.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>The Store licence. A developer build (no package identity) is unrestricted.</summary>
    public LicenseService License { get; } = new(PackageIdentity.IsPackaged);

    /// <summary>
    /// Whether creating <paramref name="widgets"/> widgets and <paramref name="views"/> views is within
    /// the licence. When it is not, logs it, says why in a notice, and returns false.
    /// </summary>
    public bool AllowCreate(int widgets, int views = 0)
    {
        var usage = LicenseUsage.Of(State);
        if (!LicenseRules.WouldExceed(License.Status.Tier, usage, widgets, views))
        {
            return true;
        }

        Log.Info("license", $"refused to create {widgets} widget(s), {views} view(s): free tier holds {usage.Widgets} widgets, {usage.Views} views");
        var refusal = LicenseText.Refusal(usage, widgets, views);
        Notices.Show(new Themia.Core.Shell.Notice(Themia.Core.Shell.NoticeKind.Warning, Str.Shell_License_PlanLimit.Text())
        {
            Message = refusal,
            Key = "license",
            Action = new Themia.Core.Shell.NoticeAction(Str.Shell_License_SeePlans.Text(), () => SeePlans(refusal)),
        });
        return false;
    }

    /// <summary>
    /// [S-3573] "See plans": edit mode, and ⚙ open at Themia Pro on the display the notice was
    /// clicked on (the one under the pointer), the refusal said once on top until the menu closes.
    /// </summary>
    private void SeePlans(string refusal)
    {
        if ((SurfaceUnderCursor() ?? _windows.FirstOrDefault()) is not { } surface)
        {
            Log.Warn("license", "no display to open Themia Pro on");
            return;
        }

        LicenceMenu.Refusal = refusal;
        surface.RequestSettings([Themia.Core.Settings.SettingsMenu.Keys.Pro]);
    }

    /// <summary>[S-1065] The paste gate: what the paste adds once a cut's original is gone.</summary>
    public bool AllowPaste(PastePlan plan) => AllowCreate(LicenseUsage.AddedByPaste(State, plan));
}
