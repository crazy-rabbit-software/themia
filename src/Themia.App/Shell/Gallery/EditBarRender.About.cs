#if DEBUG
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Licensing;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Storage;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-3583] ⚙ › Themia Pro, Back up &amp; reset and About Themia open on the stand-in display:
/// <c>-pro-&lt;state&gt;</c> for each licence state the License page's render showed (<c>free</c>,
/// <c>refused</c> (a refusal said on top), <c>pro</c>, <c>grace</c>, <c>offline</c>), and
/// <c>-pro-developer</c>, <c>-pro-onsale</c> (the Store's price on Upgrade), <c>-pro-buying</c> (the
/// Store's dialog waited for); <c>-backup</c>, <c>-backup-done</c> (how an export and an import went,
/// under their lines), <c>-backup-import</c> (the question an import asks, over a made-up file, its
/// <c>-popup0</c>), <c>-backup-reset</c> (the reset's "are you sure?"); <c>-about</c>,
/// <c>-uninstall-packaged</c>, <c>-uninstall-loose</c>, <c>-uninstall-loose-data</c> (also delete
/// ticked: the folders named) and <c>-uninstall-confirm</c> (its "are you sure?"). Stand-in hosts:
/// no Store, file dialog, file or install is reached.
/// </summary>
internal static partial class EditBarRender
{
    private static async Task ProShots(Grid screen, string outDir, string theme)
    {
        var now = DateTimeOffset.UtcNow;
        const string p = SettingsMenu.Keys.Pro;
        var refusal = LicenseText.Refusal(LicenseUsage.Of(AppState.CreateDefault()), 0, 1);

        (string Name, RenderSettingsHost Host, string? Click)[] cases =
        [
            ("-pro-free", new RenderSettingsHost { Status = new(LicenseTier.Free, LicenseBasis.StoreFree) }, null),
            ("-pro-refused", new RenderSettingsHost { Status = new(LicenseTier.Free, LicenseBasis.StoreFree), Refusal = refusal }, null),
            ("-pro-pro", new RenderSettingsHost { Status = new(LicenseTier.Pro, LicenseBasis.StorePro) }, null),
            ("-pro-grace", new RenderSettingsHost { Status = new(LicenseTier.Pro, LicenseBasis.OfflineGrace, now.AddDays(5)) }, null),
            ("-pro-offline", new RenderSettingsHost { Status = new(LicenseTier.Free, LicenseBasis.StoreUnavailable) }, null),
            ("-pro-developer", new RenderSettingsHost(), null),
            ("-pro-onsale", new RenderSettingsHost { Status = new(LicenseTier.Free, LicenseBasis.StoreFree), CanPurchase = true, Price = "CHF 19.00" }, null),
            ("-pro-buying", new RenderSettingsHost { Status = new(LicenseTier.Free, LicenseBasis.StoreFree), CanPurchase = true, Price = "CHF 19.00" }, SettingsMenu.Keys.ProUpgrade),
        ];

        // The first level too, its Themia Pro line saying the tier.
        await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}-root-free.png"), cases[0].Host, []).ConfigureAwait(true);

        foreach (var (name, host, click) in cases)
        {
            await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}{name}.png"), host, [p], menu => Click(menu, click)).ConfigureAwait(true);
        }
    }

    private static async Task BackupShots(Grid screen, string outDir, string theme)
    {
        const string b = SettingsMenu.Keys.Backup;
        ConfirmFlyout.RenderInWindow = true;

        await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}-backup.png"), new RenderSettingsHost(), [b]).ConfigureAwait(true);

        // [S-3576] How an export and an import went, under their lines; the reset's refusal under its.
        var done = new RenderSettingsHost();
        done.Outcomes.Say(SettingsMenu.Keys.BackupExport, new MenuOutcome(Str.SettingsMenu_Exported.Format(("file", "Themia settings 2026-10-25.json"))));
        done.Outcomes.Say(SettingsMenu.Keys.BackupImport, new MenuOutcome(Str.SettingsMenu_Imported.Format(("backup", "state.before-import-20261025-104200.json"))));
        done.Outcomes.Say(SettingsMenu.Keys.BackupReset, new MenuOutcome(Str.Shell_Transfer_NotAllowed.Text(), Failed: true));
        await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}-backup-done.png"), done, [b]).ConfigureAwait(true);

        // [S-3577] The question an import asks, over a made-up file: a view renamed, one added, the look changed.
        var asking = new RenderSettingsHost();
        var incoming = SettingsTransfer.Read(SettingsTransfer.Serialize(asking.State), "render").State!;
        incoming.Views[0].Name = "Desk";
        incoming.Views[1].Widgets.RemoveAt(0);
        incoming.Views.Add(ViewState.Create("Evening"));
        incoming.Design.CornerRadiusPx = 4;
        incoming.Design.Theme = AppTheme.Light;
        var preview = SettingsTransfer.Compare(asking.State, incoming, [], ["\\\\.\\DISPLAY1"]);
        await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}-backup-import.png"), asking, [b], menu =>
            menu.Confirm(SettingsMenu.ImportQuestion(preview, "state.json", "state.before-import-20261025-104200.json", () => { }))).ConfigureAwait(true);

        await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}-backup-reset.png"), new RenderSettingsHost(), [b], menu => Click(menu, SettingsMenu.Keys.BackupReset)).ConfigureAwait(true);
    }

    private static async Task AboutShots(Grid screen, string outDir, string theme)
    {
        const string a = SettingsMenu.Keys.About;
        const string u = SettingsMenu.Keys.Uninstall;
        ConfirmFlyout.RenderInWindow = true;

        (string Name, RenderSettingsHost Host, string[] Open, string? Click)[] cases =
        [
            ("-about", new RenderSettingsHost(), [a], null),
            ("-uninstall-packaged", new RenderSettingsHost { Packaged = true }, [a, u], null),
            ("-uninstall-loose", new RenderSettingsHost(), [a, u], null),
            ("-uninstall-loose-data", new RenderSettingsHost { DeleteData = true }, [a, u], null),
            ("-uninstall-confirm", new RenderSettingsHost { DeleteData = true }, [a, u], u + "/now"),
            ("-uninstall-packaged-confirm", new RenderSettingsHost { Packaged = true }, [a, u], u + "/now"),
        ];

        foreach (var (name, host, open, click) in cases)
        {
            await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}{name}.png"), host, open, menu => Click(menu, click)).ConfigureAwait(true);
        }
    }

    /// <summary>A line clicked by its key once the menu is open, as a click would.</summary>
    private static void Click(BarMenuView menu, string? key)
    {
        if (key is not null && !menu.DebugClickKey(key))
        {
            Console.WriteLine($"editbar: no line {key} to click");
        }
    }
}
#endif
