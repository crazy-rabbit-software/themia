using Themia.App.Services.Licensing;
using Themia.App.Shell.Config;
using Themia.Core.Diagnostics;
using Themia.Core.Licensing;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Services;

/// <summary>
/// [S-1065] The free-tier gate, at every place a widget or a view is created: placement (when it
/// starts and when it lands), paste, duplicate, a new view, and the view a
/// new display would get. A refusal creates nothing, deletes nothing, and says why in a notice
/// ([S-881]) whose button opens Settings on the License page, which offers the upgrade. A notice
/// rather than the settings window: a refusal is news, and a whole window thrown up over the
/// desktop in the middle of arranging it was a heavier answer than the question deserved.
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
            Action = new Themia.Core.Shell.NoticeAction(Str.Shell_License_SeePlans.Text(), () =>
            {
                // The License page says the refusal once, at the top, when it is opened from here.
                LastRefusal = refusal;
                ShowConfig(new ConfigTarget.App(AppPage.License));
            }),
        });
        return false;
    }

    /// <summary>Why the last create was refused, for the License page to say once; cleared when read.</summary>
    public string? LastRefusal { get; set; }

    /// <summary>[S-1065] The paste gate: what the paste adds once a cut's original is gone.</summary>
    public bool AllowPaste(PastePlan plan) => AllowCreate(LicenseUsage.AddedByPaste(State, plan));
}
