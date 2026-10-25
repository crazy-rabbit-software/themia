using Themia.Core.Licensing;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3570]..[S-3584] ⚙ › Themia Pro, Back up &amp; reset and About Themia: the License page and the
/// About page as lines, what each line asks for and what stands under it.
/// </summary>
public sealed partial class SettingsMenuTests
{
    /// <summary>A licence of the test's own: what the Store said, and what was asked of it.</summary>
    private sealed class FakeLicence : ILicenceMenuHost
    {
        public LicenseStatus Status { get; set; } = new(LicenseTier.Unrestricted, LicenseBasis.DeveloperBuild);

        public bool CanPurchase { get; set; }

        public string? Price { get; set; }

        public string? Refusal { get; set; }

        public int Purchases { get; private set; }

        public int Restores { get; private set; }

        public void Purchase() => Purchases++;

        public void Restore() => Restores++;
    }

    /// <summary>A settings file of the test's own: what was asked of it.</summary>
    private sealed class FakeBackup : IBackupMenuHost
    {
        public string StatePath => @"C:\Users\Alex\AppData\Roaming\Themia\state.json";

        public List<string> Asked { get; } = [];

        public void Export() => Asked.Add("export");

        public void Import() => Asked.Add("import");

        public void OpenFile() => Asked.Add("open");

        public void Reset() => Asked.Add("reset");
    }

    /// <summary>An install of the test's own: packaged or loose, and what was opened or uninstalled.</summary>
    private sealed class FakeAbout : IAboutMenuHost
    {
        public bool Packaged { get; set; }

        public string Version => "1.2.3";

        public string Runtime => ".NET 10.0.0 · Windows App SDK 1.8";

        public List<Uri> Opened { get; } = [];

        public List<bool> Uninstalled { get; } = [];

        public void Open(Uri page) => Opened.Add(page);

        public UninstallPlan Uninstall => UninstallPlan.Create(Packaged, deleteData: true, @"C:\Users\Alex\AppData\Roaming", @"C:\Users\Alex\AppData\Local", "Themia", Version);

        public bool DeleteData { get; set; }

        public void SetDeleteData(bool on) => DeleteData = on;

        public void UninstallNow(bool deleteData) => Uninstalled.Add(deleteData);
    }

    private static string[] KeysOf(SettingsSubmenu submenu) => [.. submenu.Lines.Select(l => l is SettingsSeparator ? "-" : l.Key)];

    private static FakeHost Licensed(LicenseTier tier, LicenseBasis basis, DateTimeOffset? grace = null)
    {
        var host = new FakeHost();
        host.FakeLicence.Status = new LicenseStatus(tier, basis, grace);
        return host;
    }

    // ---- [S-3570] Themia Pro -----------------------------------------------------------------------

    [Fact]
    public void S3570_a_developer_build_says_what_it_is_and_nothing_else()
    {
        var pro = At<SettingsSubmenu>(new FakeHost(), SettingsMenu.Keys.Pro);

        Assert.Equal("Themia Pro", pro.Text);
        Assert.Equal(["pro/status", "pro/status/detail"], KeysOf(pro));
        Assert.Equal(Str.Shell_License_DeveloperBuild.Text(), Assert.IsType<SettingsReading>(pro.Lines[0]).Text);
    }

    [Theory]
    [InlineData(LicenseTier.Free, LicenseBasis.StoreFree)]
    [InlineData(LicenseTier.Free, LicenseBasis.Checking)]
    [InlineData(LicenseTier.Free, LicenseBasis.StoreUnavailable)]
    [InlineData(LicenseTier.Pro, LicenseBasis.StorePro)]
    [InlineData(LicenseTier.Pro, LicenseBasis.OfflineGrace)]
    [InlineData(LicenseTier.Unrestricted, LicenseBasis.DeveloperBuild)]
    public void S3570_every_basis_says_what_this_copy_is_and_why(LicenseTier tier, LicenseBasis basis)
    {
        var status = new LicenseStatus(tier, basis, basis == LicenseBasis.OfflineGrace ? DateTimeOffset.UtcNow.AddDays(5) : null);
        var host = Licensed(tier, basis, status.GraceUntil);

        Assert.Equal(LicenseText.Title(status), At<SettingsReading>(host, "pro", "pro/status").Text);
        var detail = At<SettingsNote>(host, "pro", "pro/status/detail");
        Assert.Equal(LicenseText.Detail(status), detail.Text);
        Assert.NotEmpty(detail.Text);
    }

    [Fact]
    public void S3571_the_free_tier_reads_out_the_widgets_and_views_it_holds()
    {
        var host = Licensed(LicenseTier.Free, LicenseBasis.StoreFree);
        var usage = LicenseUsage.Of(host.State);
        var pro = At<SettingsSubmenu>(host, "pro");

        Assert.Equal(
            ["pro/status", "pro/status/detail", "-", "pro/widgets", "pro/widgets/what", "pro/views", "-", "pro/upgrade", "pro/upgrade/why", "pro/upgrade/outcome", "pro/restore", "pro/restore/outcome"],
            KeysOf(pro));
        Assert.Equal(usage.Widgets + " of " + LicenseRules.FreeWidgetLimit, At<SettingsReading>(host, "pro", "pro/widgets").Value);
        Assert.Equal(usage.Views + " of " + LicenseRules.FreeViewLimit, At<SettingsReading>(host, "pro", "pro/views").Value);
        Assert.Equal("Widgets", At<SettingsReading>(host, "pro", "pro/widgets").Text);
    }

    [Theory]
    [InlineData(LicenseBasis.StorePro)]
    [InlineData(LicenseBasis.OfflineGrace)]
    public void S3571_pro_has_no_allowance_and_no_upgrade_but_restore(LicenseBasis basis)
    {
        var host = Licensed(LicenseTier.Pro, basis, DateTimeOffset.UtcNow.AddDays(3));

        Assert.Equal(["pro/status", "pro/status/detail", "-", "pro/restore", "pro/restore/outcome"], KeysOf(At<SettingsSubmenu>(host, "pro")));
    }

    [Fact]
    public void S3572_upgrade_is_greyed_with_why_while_pro_is_not_on_sale()
    {
        var host = Licensed(LicenseTier.Free, LicenseBasis.StoreFree);

        var upgrade = At<SettingsCommand>(host, "pro", "pro/upgrade");
        Assert.True(upgrade.Disabled);
        Assert.Equal("Upgrade", upgrade.Text);
        Assert.Equal(LicenseText.Purchase(PurchaseOutcome.NotConfigured), At<SettingsNote>(host, "pro", "pro/upgrade/why").Text);
    }

    [Fact]
    public void S3572_on_sale_upgrade_carries_the_stores_price_and_asks_the_store()
    {
        var host = Licensed(LicenseTier.Free, LicenseBasis.StoreFree);
        host.FakeLicence.CanPurchase = true;
        Assert.Equal("Upgrade", At<SettingsCommand>(host, "pro", "pro/upgrade").Text);

        host.FakeLicence.Price = "CHF 19.00";
        var upgrade = At<SettingsCommand>(host, "pro", "pro/upgrade");
        Assert.False(upgrade.Disabled);
        Assert.Equal("Upgrade · CHF 19.00", upgrade.Text);
        Assert.Equal(Str.Shell_License_ProPitch.Text(), At<SettingsNote>(host, "pro", "pro/upgrade/why").Text);

        upgrade.Run();
        At<SettingsCommand>(host, "pro", "pro/restore").Run();
        Assert.Equal(1, host.FakeLicence.Purchases);
        Assert.Equal(1, host.FakeLicence.Restores);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3572_while_the_store_is_asked_its_lines_are_greyed_and_how_it_ends_stands_under_them()
    {
        var host = Licensed(LicenseTier.Free, LicenseBasis.StoreFree);
        host.FakeLicence.CanPurchase = true;
        Assert.Equal(Str.Shell_License_RestoreDesc.Text(), At<SettingsNote>(host, "pro", "pro/restore/outcome").Text);
        Assert.Empty(At<SettingsNote>(host, "pro", "pro/upgrade/outcome").Text);

        host.Outcomes.Say(SettingsMenu.Keys.ProUpgrade, new MenuOutcome("Waiting", Busy: true));
        Assert.True(At<SettingsCommand>(host, "pro", "pro/upgrade").Disabled);
        Assert.True(At<SettingsCommand>(host, "pro", "pro/restore").Disabled);
        Assert.True(At<SettingsNote>(host, "pro", "pro/upgrade/outcome").Busy);

        host.Outcomes.Say(SettingsMenu.Keys.ProUpgrade, new MenuOutcome(LicenseText.Purchase(PurchaseOutcome.NetworkError), Failed: true));
        var said = At<SettingsNote>(host, "pro", "pro/upgrade/outcome");
        Assert.True(said.Critical);
        Assert.False(said.Busy);
        Assert.False(At<SettingsCommand>(host, "pro", "pro/upgrade").Disabled);

        host.Outcomes.Say(SettingsMenu.Keys.ProRestore, new MenuOutcome("Themia Free"));
        Assert.Equal("Themia Free", At<SettingsNote>(host, "pro", "pro/restore/outcome").Text);
    }

    [Fact]
    public void S3573_a_refusal_that_opened_the_menu_is_said_on_top_while_the_limits_apply()
    {
        var host = Licensed(LicenseTier.Free, LicenseBasis.StoreFree);
        host.FakeLicence.Refusal = LicenseText.Refusal(new LicenseUsage(20, 1), 1, 0);

        var pro = At<SettingsSubmenu>(host, "pro");
        Assert.Equal(["pro/refused", "pro/refused/why", "-", "pro/status"], KeysOf(pro).Take(4));
        Assert.Equal(Str.Shell_License_LimitReached.Text(), Assert.IsType<SettingsReading>(pro.Lines[0]).Text);
        Assert.Equal(host.FakeLicence.Refusal, Assert.IsType<SettingsNote>(pro.Lines[1]).Text);

        // Pro since: nothing is refused any more, so nothing is said.
        host.FakeLicence.Status = new LicenseStatus(LicenseTier.Pro, LicenseBasis.StorePro);
        Assert.DoesNotContain(At<SettingsSubmenu>(host, "pro").Lines, l => l.Key.StartsWith("pro/refused", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(LicenseTier.Free, LicenseBasis.StoreFree, "Free")]
    [InlineData(LicenseTier.Free, LicenseBasis.Checking, "Free")]
    [InlineData(LicenseTier.Pro, LicenseBasis.StorePro, "Pro")]
    [InlineData(LicenseTier.Pro, LicenseBasis.OfflineGrace, "Pro")]
    [InlineData(LicenseTier.Unrestricted, LicenseBasis.DeveloperBuild, "Developer build")]
    public void S3574_the_themia_pro_line_says_the_tier(LicenseTier tier, LicenseBasis basis, string value)
        => Assert.Equal(value, At<SettingsSubmenu>(Licensed(tier, basis), "pro").Value);

    // ---- [S-3575] Back up & reset ------------------------------------------------------------------

    [Fact]
    public void S3575_back_up_and_reset_holds_export_import_and_the_file_then_reset()
    {
        var host = new FakeHost();
        var backup = At<SettingsSubmenu>(host, SettingsMenu.Keys.Backup);

        Assert.Equal("Back up & reset", backup.Text);
        Assert.Equal(
            ["backup/export", "backup/export/outcome", "backup/import", "backup/import/outcome", "backup/open", "backup/open/path", "-", "backup/reset", "backup/reset/outcome"],
            KeysOf(backup));
        Assert.Equal("Export settings…", At<SettingsCommand>(host, "backup", "backup/export").Text);
        Assert.Equal("Import settings…", At<SettingsCommand>(host, "backup", "backup/import").Text);
        Assert.Equal(host.FakeBackup.StatePath, At<SettingsNote>(host, "backup", "backup/open/path").Text);

        At<SettingsCommand>(host, "backup", "backup/export").Run();
        At<SettingsCommand>(host, "backup", "backup/import").Run();
        At<SettingsCommand>(host, "backup", "backup/open").Run();
        Assert.Equal(["export", "import", "open"], host.FakeBackup.Asked);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3576_how_it_went_stands_under_its_line_and_a_dialog_up_greys_the_files_lines()
    {
        var host = new FakeHost();
        host.Outcomes.Say(SettingsMenu.Keys.BackupExport, new MenuOutcome(string.Empty, Busy: true));

        Assert.True(At<SettingsCommand>(host, "backup", "backup/export").Disabled);
        Assert.True(At<SettingsCommand>(host, "backup", "backup/import").Disabled);
        Assert.True(At<SettingsCommand>(host, "backup", "backup/reset").Disabled);
        Assert.False(At<SettingsCommand>(host, "backup", "backup/open").Disabled);

        host.Outcomes.Say(SettingsMenu.Keys.BackupExport, new MenuOutcome("Saved as Themia settings.json"));
        host.Outcomes.Say(SettingsMenu.Keys.BackupImport, new MenuOutcome("This isn't a Themia settings file.", Failed: true));

        Assert.Equal("Saved as Themia settings.json", At<SettingsNote>(host, "backup", "backup/export/outcome").Text);
        Assert.False(At<SettingsNote>(host, "backup", "backup/export/outcome").Critical);
        Assert.True(At<SettingsNote>(host, "backup", "backup/import/outcome").Critical);
        Assert.False(At<SettingsCommand>(host, "backup", "backup/export").Disabled);
    }

    [Fact]
    public void S3576_closing_the_last_menu_forgets_what_was_said_but_not_what_is_still_out()
    {
        var outcomes = new SettingsOutcomes();
        var changes = 0;
        outcomes.Changed += () => changes++;

        outcomes.Say("pro/upgrade", new MenuOutcome("Waiting", Busy: true));
        outcomes.Say("backup/export", new MenuOutcome("Saved"));
        outcomes.Clear();

        Assert.Null(outcomes.Of("backup/export"));
        Assert.True(outcomes.Busy("pro/upgrade"));
        Assert.Equal(3, changes);

        outcomes.Clear();
        Assert.Equal(3, changes);
    }

    [Fact]
    public void S3578_reset_is_asked_first_as_a_destructive_question_and_only_the_answer_resets()
    {
        var host = new FakeHost();
        var reset = At<SettingsCommand>(host, "backup", "backup/reset");
        Assert.True(reset.Danger);
        Assert.Equal("Reset all settings…", reset.Text);

        reset.Run();
        var asked = Assert.Single(host.Asked);
        Assert.True(asked.Danger);
        Assert.Equal(Str.Shell_About_ResetConfirm.Text(), asked.Question);
        Assert.Equal("Reset", asked.Accept);
        Assert.Contains("state.json", asked.Detail, StringComparison.Ordinal);
        Assert.Empty(host.FakeBackup.Asked);

        asked.Done();
        Assert.Equal(["reset"], host.FakeBackup.Asked);
    }

    [Fact]
    public void S3577_an_imports_question_says_what_changes_then_what_is_good_to_know()
    {
        var current = AppState.CreateDefault();
        var incoming = AppState.CreateDefault();
        incoming.Views[0].Name = "Desk";
        incoming.Design.CornerRadiusPx = 4;
        var preview = SettingsTransfer.Compare(current, incoming, [], []);
        var imported = 0;

        var question = SettingsMenu.ImportQuestion(preview, "state.json", "state.before-import-1.json", () => imported++);

        Assert.Equal(Str.Shell_About_ImportConfirm.Text(), question.Question);
        Assert.Equal(Str.Common_Import.Text(), question.Accept);
        Assert.False(question.Danger);
        var lines = question.Detail!.Split('\n');
        Assert.Equal(Str.Shell_About_WhatChanges.Text(), lines[0]);
        Assert.All(preview.Changes, c => Assert.Contains("• " + c, lines));
        Assert.Contains(Str.Shell_About_GoodToKnow.Text(), lines);
        Assert.Equal("• " + Str.Shell_About_KeptAs.Format(("file", "state.json"), ("backup", "state.before-import-1.json")), lines[^1]);
        Assert.Equal(0, imported);

        question.Done();
        Assert.Equal(1, imported);
    }

    [Fact]
    public void S3577_the_same_settings_change_nothing_and_a_long_list_is_cut_at_twelve()
    {
        var same = SettingsMenu.ImportQuestion(new ImportPreview([], []), "state.json", "b.json", () => { });
        Assert.Contains("• " + Str.Shell_About_NoChanges.Text(), same.Detail!.Split('\n'));

        var many = SettingsMenu.ImportQuestion(new ImportPreview([.. Enumerable.Range(1, 15).Select(i => "change " + i)], []), "state.json", "b.json", () => { });
        var lines = many.Detail!.Split('\n');
        Assert.Contains("• change 12", lines);
        Assert.DoesNotContain("• change 13", lines);
        Assert.Contains(Str.Shell_About_AndMore.Plural(3), lines);
    }

    // ---- [S-3580] About Themia ---------------------------------------------------------------------

    [Fact]
    public void S3580_about_holds_the_name_card_the_studio_its_links_the_data_then_uninstall()
    {
        var host = new FakeHost();
        var about = At<SettingsSubmenu>(host, SettingsMenu.Keys.About);

        Assert.Equal("About Themia", about.Text);
        Assert.Equal(
            ["about/name", "about/version", "-", "about/lometics", "about/lometics/studio", "about/privacy", "about/support", "-", "about/openMeteo", "about/openMeteo/what", "-", "about/uninstall"],
            KeysOf(about));

        var name = At<SettingsReading>(host, "about", "about/name");
        Assert.Equal("Themia", name.Text);
        Assert.Equal(MenuPicture.ThemiaLogo, name.Picture);
        Assert.Equal("Version 1.2.3\n\u200E.NET 10.0.0 · Windows App SDK 1.8\u200E", At<SettingsNote>(host, "about", "about/version").Text);
        Assert.Equal(MenuPicture.Lometics, At<SettingsCommand>(host, "about", "about/lometics").Picture);
        Assert.Equal(AboutLinks.SupportAddress, At<SettingsCommand>(host, "about", "about/support").Value);
        Assert.Equal(SettingsMenu.Keys.Uninstall, about.Lines[^1].Key);
    }

    [Fact]
    public void S3580_the_links_open_the_studio_the_policy_in_its_language_support_and_open_meteo()
    {
        var host = new FakeHost();
        foreach (var key in new[] { "about/lometics", "about/privacy", "about/support", "about/openMeteo" })
        {
            At<SettingsCommand>(host, "about", key).Run();
        }

        Assert.Equal(
            [SettingsMenu.LometicsSite, AboutLinks.Privacy(Locale.En), AboutLinks.Support, SettingsMenu.OpenMeteoSite],
            host.FakeAbout.Opened);

        try
        {
            Strings.SetLocale(Locale.De);
            At<SettingsCommand>(host, "about", "about/privacy").Run();
            Assert.Equal(new Uri("https://lometics.com/de/legal/privacy/"), host.FakeAbout.Opened[^1]);
        }
        finally
        {
            Strings.SetLocale(Locale.En);
        }

        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3582_a_packaged_uninstall_offers_no_choice_and_its_data_goes()
    {
        var host = new FakeHost();
        host.FakeAbout.Packaged = true;
        var uninstall = At<SettingsSubmenu>(host, "about", SettingsMenu.Keys.Uninstall);

        Assert.Equal(["about/uninstall/what", "-", "about/uninstall/now"], KeysOf(uninstall));
        Assert.Equal(Str.Shell_Uninstall_Package.Text(), Assert.IsType<SettingsNote>(uninstall.Lines[0]).Text);

        At<SettingsCommand>(host, "about", "about/uninstall", "about/uninstall/now").Run();
        Assert.Empty(host.FakeAbout.Uninstalled);
        var asked = Assert.Single(host.Asked);
        Assert.True(asked.Danger);
        Assert.Equal(Str.Shell_Uninstall_Confirm.Text(), asked.Question);
        Assert.Equal(Str.Shell_Uninstall_Action.Text(), asked.Accept);

        asked.Done();
        Assert.Equal([false], host.FakeAbout.Uninstalled);
    }

    [Fact]
    public void S3582_a_loose_uninstall_offers_also_delete_unticked_and_ticked_names_the_folders()
    {
        var host = new FakeHost();
        var keys = KeysOf(At<SettingsSubmenu>(host, "about", "about/uninstall"));
        Assert.Equal(["about/uninstall/what", "-", "about/uninstall/data", "about/uninstall/data/what", "about/uninstall/data/folders", "-", "about/uninstall/now"], keys);

        var data = At<SettingsToggle>(host, "about", "about/uninstall", "about/uninstall/data");
        Assert.False(data.On);
        Assert.Equal("Also delete my data", data.Text);
        Assert.Empty(At<SettingsNote>(host, "about", "about/uninstall", "about/uninstall/data/what").Text);
        Assert.Empty(At<SettingsNote>(host, "about", "about/uninstall", "about/uninstall/data/folders").Text);

        data.Set(true);
        Assert.Equal(Str.SettingsMenu_UninstallDataGoes.Text(), At<SettingsNote>(host, "about", "about/uninstall", "about/uninstall/data/what").Text);
        Assert.Equal(
            @"C:\Users\Alex\AppData\Roaming\Themia" + "\n" + @"C:\Users\Alex\AppData\Local\Themia",
            At<SettingsNote>(host, "about", "about/uninstall", "about/uninstall/data/folders").Text);

        At<SettingsCommand>(host, "about", "about/uninstall", "about/uninstall/now").Run();
        var asked = Assert.Single(host.Asked);
        Assert.Contains(Str.SettingsMenu_UninstallDataGoes.Text(), asked.Detail, StringComparison.Ordinal);
        Assert.Empty(host.FakeAbout.Uninstalled);

        asked.Done();
        Assert.Equal([true], host.FakeAbout.Uninstalled);
    }

    [Fact]
    public void S3580_the_new_sections_keep_their_keys_in_every_language()
    {
        static List<string> Keys(IReadOnlyList<SettingsLine> lines) => [.. lines.SelectMany(l => l is SettingsSubmenu s ? (IEnumerable<string>)[s.Key, .. Keys(s.Lines)] : [l.Key])];

        var host = Licensed(LicenseTier.Free, LicenseBasis.StoreFree);
        var english = Keys([SettingsMenu.Pro(host), SettingsMenu.Backup(host), SettingsMenu.About(host)]);
        try
        {
            Strings.SetLocale(Locale.Ar);
            Assert.Equal(english, Keys([SettingsMenu.Pro(host), SettingsMenu.Backup(host), SettingsMenu.About(host)]));
            Assert.Equal("حول Themia", SettingsMenu.About(host).Text);
        }
        finally
        {
            Strings.SetLocale(Locale.En);
        }
    }
}
