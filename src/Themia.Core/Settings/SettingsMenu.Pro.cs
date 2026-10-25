using Themia.App.Shell;
using Themia.Core.Licensing;
using Themia.Core.Localization;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3570] What ⚙ › Themia Pro reads and asks for: the licence as the Store last gave it, whether Pro
/// can be bought here and at what price, why a create was refused (said once), and the Store's purchase
/// and its asking again. How each ended is said under its line (<see cref="ISettingsMenuHost.Outcomes"/>).
/// </summary>
public interface ILicenceMenuHost
{
    /// <summary>[S-1064] The tier and why, as the licence service has it now.</summary>
    LicenseStatus Status { get; }

    /// <summary>[S-1067] Packaged, and the add-on's Store id is a real one.</summary>
    bool CanPurchase { get; }

    /// <summary>Pro's price as the Store shows it to this user ("CHF 19.00"), once it is known; null until then.</summary>
    string? Price { get; }

    /// <summary>[S-3573] Why the last create was refused, when that is what opened the menu; null otherwise.</summary>
    string? Refusal { get; }

    /// <summary>[S-3572] Open the Store's purchase dialog for Pro; how it ended stands under Upgrade.</summary>
    void Purchase();

    /// <summary>[S-3572] Ask the Store again ("Restore purchases"); what it said stands under the line.</summary>
    void Restore();
}

/// <summary>
/// [S-3570]..[S-3574] Themia Pro ›: the settings window's License page as lines, after Karpo's ⚙ ›
/// Karpo Pro (karpo-app, docs/widgets.md §339): what this copy is and why, the free allowance where it
/// applies, Upgrade with the Store's price, and Restore purchases.
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>[S-3574] "Themia Pro": the add-on's name, as Windows' Store writes it in every language.</summary>
    private const string ProName = "Themia Pro";

    /// <summary>[S-3570] The Themia Pro line and its submenu; its value the tier this copy has.</summary>
    public static SettingsSubmenu Pro(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var licence = host.Licence;
        var status = licence.Status;
        var lines = new List<SettingsLine>();

        // [S-3573] A refusal that opened the menu: said once, on top, while the limits apply.
        if (licence.Refusal is { Length: > 0 } refusal && status.IsLimited)
        {
            lines.Add(new SettingsReading(Keys.Pro + "/refused", Str.Shell_License_LimitReached.Text(), Glyphs.Warning, null));
            lines.Add(new SettingsNote(Keys.Pro + "/refused/why", refusal));
            lines.Add(new SettingsSeparator(Keys.Pro + "/-status"));
        }

        lines.Add(new SettingsReading(Keys.Pro + "/status", LicenseText.Title(status), status.Tier == LicenseTier.Free ? Glyphs.Certificate : Glyphs.Themia, null));
        lines.Add(new SettingsNote(Keys.Pro + "/status/detail", LicenseText.Detail(status)));

        // [S-1064] A developer build has no Store to ask and nothing limited: the status is all there is.
        if (status.Basis != LicenseBasis.DeveloperBuild)
        {
            if (status.IsLimited)
            {
                lines.AddRange(Allowance(host));
            }

            lines.Add(new SettingsSeparator(Keys.Pro + "/-store"));
            lines.AddRange(Store(host, licence, status));
        }

        return new SettingsSubmenu(Keys.Pro, ProName, Glyphs.Certificate, lines) { Value = Tier(status) };
    }

    /// <summary>[S-3574] What the Themia Pro line says at its end: "Free", "Pro", or that this is a developer build.</summary>
    public static string Tier(LicenseStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return status.Tier switch
        {
            LicenseTier.Unrestricted => Str.Shell_License_DeveloperBuild.Text(),
            LicenseTier.Pro => "Pro",
            _ => Str.SettingsMenu_ProFree.Text(),
        };
    }

    /// <summary>
    /// [S-3571] The free allowance as readings, the way Windows reads out storage: "12 of 20" widgets
    /// (every view, groups and their members included) and "2 of 2" views.
    /// </summary>
    private static IEnumerable<SettingsLine> Allowance(ISettingsMenuHost host)
    {
        var usage = LicenseUsage.Of(host.State);
        yield return new SettingsSeparator(Keys.Pro + "/-usage");
        yield return new SettingsReading(Keys.Pro + "/widgets", Str.Settings_License_QuotaWidgets.Text(), Glyphs.Grid, Of(usage.Widgets, LicenseRules.FreeWidgetLimit));
        yield return new SettingsNote(Keys.Pro + "/widgets/what", Str.Shell_License_WidgetsDesc.Text());
        yield return new SettingsReading(Keys.Pro + "/views", Str.Settings_License_QuotaViews.Text(), Glyphs.View, Of(usage.Views, LicenseRules.FreeViewLimit));
    }

    /// <summary>[S-3571] "12 of 20", in the language's numbers.</summary>
    public static string Of(int used, int limit)
        => Str.Shell_View_Position.Format(("n", used), ("total", limit));

    /// <summary>
    /// [S-3572] The Store's lines: Upgrade where Pro is not had (its price once known; greyed with why
    /// while it is not on sale), then Restore purchases; each greyed while the Store is being asked,
    /// how it ended under it.
    /// </summary>
    private static IEnumerable<SettingsLine> Store(ISettingsMenuHost host, ILicenceMenuHost licence, LicenseStatus status)
    {
        var outcomes = host.Outcomes;
        var asking = outcomes.Busy(Keys.ProUpgrade) || outcomes.Busy(Keys.ProRestore);

        if (status.Tier != LicenseTier.Pro)
        {
            var words = licence.Price is { Length: > 0 } price
                ? Str.Shell_License_UpgradePrice.Format(("price", price))
                : Str.Common_Upgrade.Text();
            yield return new SettingsCommand(Keys.ProUpgrade, words, Glyphs.Shop, licence.Purchase) { Disabled = !licence.CanPurchase || asking };
            yield return new SettingsNote(Keys.ProUpgrade + "/why", licence.CanPurchase
                ? Str.Shell_License_ProPitch.Text()
                : LicenseText.Purchase(PurchaseOutcome.NotConfigured));
            yield return OutcomeNote(outcomes, Keys.ProUpgrade);
        }

        yield return new SettingsCommand(Keys.ProRestore, Str.Shell_License_Restore.Text(), Glyphs.Refresh, licence.Restore) { Disabled = asking };
        yield return outcomes.Of(Keys.ProRestore) is null
            ? new SettingsNote(Keys.ProRestore + "/outcome", Str.Shell_License_RestoreDesc.Text())
            : OutcomeNote(outcomes, Keys.ProRestore);
    }

    /// <summary>[S-3572] [S-3576] What stands under a line of <paramref name="row"/>: a note that takes no room while there is none.</summary>
    private static SettingsNote OutcomeNote(SettingsOutcomes outcomes, string row)
        => outcomes.Of(row) is { } said
            ? new SettingsNote(row + "/outcome", said.Text) { Busy = said.Busy, Critical = said.Failed }
            : new SettingsNote(row + "/outcome", string.Empty);
}
