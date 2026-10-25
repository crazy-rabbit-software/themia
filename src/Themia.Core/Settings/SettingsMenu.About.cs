using Themia.App.Shell;
using Themia.Core.Localization;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3581] A picture a line of the settings menu shows in its icon column instead of a glyph: the
/// app's own mark and the studio's, drawn by the app from the files beside <c>Themia.exe</c>.
/// </summary>
public enum MenuPicture
{
    /// <summary><c>Assets/themia-logo.png</c>, the mark of the app's name card.</summary>
    ThemiaLogo,

    /// <summary><c>Assets/lometics.svg</c>, the studio's mark.</summary>
    Lometics,
}

/// <summary>
/// [S-3580] What ⚙ › About Themia reads and does: the version and what it runs on, the links (through
/// the app's one launcher), and the uninstall: the plan for this install, the "also delete my data"
/// choice of a loose build (a draft, unticked again when the menu closes) and the uninstall itself.
/// </summary>
public interface IAboutMenuHost
{
    /// <summary>The running version ("1.0.0").</summary>
    string Version { get; }

    /// <summary>What the build runs on: ".NET 10.0.0 · Windows App SDK 1.8.x".</summary>
    string Runtime { get; }

    /// <summary>Open a page or a mail (lometics.com, the privacy policy, the support address, a data source's site).</summary>
    void Open(Uri page);

    /// <summary>[S-1552] [S-1553] What uninstalling would do here, with the data folders named (as if they went).</summary>
    UninstallPlan Uninstall { get; }

    /// <summary>[S-1553] A loose build's "Also delete my widgets, settings and accounts", as ticked now.</summary>
    bool DeleteData { get; }

    /// <summary>[S-1553] Tick or untick it.</summary>
    void SetDeleteData(bool on);

    /// <summary>[S-1552] [S-1071] Uninstall, the data too where <paramref name="deleteData"/> (packaged: always). Already asked.</summary>
    void UninstallNow(bool deleteData);
}

/// <summary>
/// [S-3580]..[S-3582] About Themia ›: the About page as lines, after Karpo's ⚙ › About Karpo
/// (karpo-app, docs/widgets.md §339): the name card (the mark, the version, what it runs on), the
/// studio, the privacy policy and support, the data the widgets credit, and at the very end
/// Uninstall Themia ›.
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>[S-3580] Lometics' site, which the studio's line opens.</summary>
    public static readonly Uri LometicsSite = new("https://lometics.com/");

    /// <summary>[S-3580] Open-Meteo's site: the weather, rain and air quality the widgets credit come from it.</summary>
    public static readonly Uri OpenMeteoSite = new("https://open-meteo.com/");

    /// <summary>[S-3580] The About Themia line and its submenu.</summary>
    public static SettingsSubmenu About(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var about = host.About;
        var lines = new List<SettingsLine>
        {
            // The name card: the mark beside the name, the version and what it runs on under it.
            new SettingsReading(Keys.About + "/name", "Themia", null, null) { Picture = MenuPicture.ThemiaLogo },
            // The runtime line between left-to-right marks (U+200E), so a right-to-left language keeps its dots in place.
            new SettingsNote(Keys.About + "/version", Str.Shell_About_Version.Format(("version", about.Version)) + "\n\u200E" + about.Runtime + "\u200E"),
            new SettingsSeparator(Keys.About + "/-made"),

            // [S-493] Made by, with its mark and its site.
            new SettingsCommand(Keys.About + "/lometics", "Lometics", null, () => about.Open(LometicsSite))
            {
                Picture = MenuPicture.Lometics,
                Value = "lometics.com",
            },
            new SettingsNote(Keys.About + "/lometics/studio", Str.Shell_About_Studio.Text()),

            // [S-1072] What the Store listing points to as well.
            new SettingsCommand(Keys.About + "/privacy", Str.Shell_About_Privacy.Text(), Glyphs.Lock, () => about.Open(AboutLinks.Privacy(Strings.Current))),
            new SettingsCommand(Keys.About + "/support", Str.Shell_About_Support.Text(), Glyphs.Mail, () => about.Open(AboutLinks.Support))
            {
                Value = AboutLinks.SupportAddress,
            },
            new SettingsSeparator(Keys.About + "/-data"),

            // The data the weather, rain and air-quality widgets credit in their own menus ("Source: Open-Meteo").
            new SettingsCommand(Keys.About + "/openMeteo", "Open-Meteo", Glyphs.Weather, () => about.Open(OpenMeteoSite)) { Value = "open-meteo.com" },
            new SettingsNote(Keys.About + "/openMeteo/what", Str.SettingsMenu_AboutOpenMeteo.Text()),
            new SettingsSeparator(Keys.About + "/-uninstall"),
            Uninstall(host, about),
        };

        return new SettingsSubmenu(Keys.About, Str.SettingsMenu_About.Text(), Glyphs.Info, lines);
    }

    /// <summary>
    /// [S-3582] Uninstall Themia ›, About's last line: what happens (packaged, the package goes with
    /// the data; loose, Installed apps opens), a loose build's "Also delete my widgets, settings and
    /// accounts" (unticked; ticked, the folders that go under it), then Uninstall…, asked first as
    /// Windows asks before it destroys something. Nothing is removed or deleted before that answer
    /// ([S-1555]).
    /// </summary>
    private static SettingsSubmenu Uninstall(ISettingsMenuHost host, IAboutMenuHost about)
    {
        var plan = about.Uninstall;
        var key = Keys.Uninstall;
        var packaged = plan.Route == UninstallRoute.RemovePackage;
        var what = packaged ? Str.Shell_Uninstall_Package.Text() : Str.Shell_Uninstall_InstalledApps.Text();

        var lines = new List<SettingsLine> { new SettingsNote(key + "/what", what) };

        // [S-1070] Packaged, the data goes with the package: keeping it is no choice to offer.
        var deleteData = !plan.DataGoesWithPackage && about.DeleteData;
        if (!plan.DataGoesWithPackage)
        {
            lines.Add(new SettingsSeparator(key + "/-data"));
            lines.Add(new SettingsToggle(key + "/data", Str.SettingsMenu_UninstallDeleteData.Text(), Glyphs.Delete, deleteData, about.SetDeleteData)
            {
                Disabled = plan.FoldersToDelete.Count == 0,
            });

            // [S-1553] Ticked, what goes is said and its folders named, so it is plain what goes.
            lines.Add(new SettingsNote(key + "/data/what", deleteData ? Str.SettingsMenu_UninstallDataGoes.Text() : string.Empty));
            lines.Add(new SettingsNote(key + "/data/folders", deleteData ? string.Join("\n", plan.FoldersToDelete) : string.Empty));
        }

        lines.Add(new SettingsSeparator(key + "/-now"));
        var detail = deleteData ? what + "\n" + Str.SettingsMenu_UninstallDataGoes.Text() : what;
        lines.Add(new SettingsCommand(key + "/now", Str.SettingsMenu_UninstallEllipsis.Text(), Glyphs.Delete, () => host.Confirm(new SettingsConfirm(
            Str.Shell_Uninstall_Confirm.Text(),
            Str.Shell_Uninstall_Action.Text(),
            () => about.UninstallNow(deleteData),
            detail,
            Danger: true)))
        {
            Danger = true,
        });

        return new SettingsSubmenu(key, Str.Shell_Uninstall_Title.Text(), Glyphs.Delete, lines);
    }
}
