using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3960] A packaged app's executable is drawn by the app's Apps-folder item, found through its
/// package's manifest: WhatsApp's WhatsApp.Root.exe has no icon of its own.
/// </summary>
public class PackagedAppsTests
{
    private const string WhatsAppRoot = @"C:\Program Files\WindowsApps\5319275A.WhatsAppDesktop_2.2639.100.0_x64__cv1g1gvanyjgm";

    // The shape of WhatsApp's manifest: the app, plus ExeServer extensions naming the same file.
    private const string Manifest = """
        <?xml version="1.0" encoding="utf-8"?>
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                 xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
                 xmlns:com="http://schemas.microsoft.com/appx/manifest/com/windows10">
          <Applications>
            <Application Id="Helper" Executable="helper\helper.exe" EntryPoint="Windows.FullTrustApplication" />
            <Application Id="App" Executable="WhatsApp.Root.exe" EntryPoint="Windows.FullTrustApplication">
              <uap:VisualElements DisplayName="WhatsApp" Square44x44Logo="Assets\AppList.png" />
              <Extensions>
                <com:Extension Category="windows.comServer">
                  <com:ComServer><com:ExeServer Executable="WhatsApp.Root.exe" DisplayName="PushTask" /></com:ComServer>
                </com:Extension>
              </Extensions>
            </Application>
          </Applications>
        </Package>
        """;

    private const string OneApp = """
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
          <Applications><Application Id="App" Executable="CalculatorApp.exe" /></Applications>
        </Package>
        """;

    [Fact]
    public void S3960_a_packaged_executable_is_its_app_as_Start_lists_it()
    {
        string? asked = null;
        string? Read(string file)
        {
            asked = file;
            return Manifest;
        }

        Assert.Equal(
            @"shell:AppsFolder\5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App",
            PackagedApps.AppsFolderTarget(WhatsAppRoot + @"\WhatsApp.Root.exe", Read));
        Assert.Equal(WhatsAppRoot + @"\AppxManifest.xml", asked);

        // Case and slashes do not matter; a file in a subfolder is matched by its path inside the package.
        Assert.Equal(
            @"shell:AppsFolder\5319275A.WhatsAppDesktop_cv1g1gvanyjgm!Helper",
            PackagedApps.AppsFolderTarget(WhatsAppRoot.Replace('\\', '/') + "/HELPER/Helper.EXE", Read));
    }

    [Fact]
    public void S3960_a_package_with_one_app_is_that_app_for_any_of_its_files()
    {
        const string root = @"C:\Program Files\WindowsApps\Microsoft.WindowsCalculator_11.2607.0.0_x64__8wekyb3d8bbwe";

        Assert.Equal(
            @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
            PackagedApps.AppsFolderTarget(root + @"\CalculatorApp.exe", _ => OneApp));
        Assert.Equal(
            @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
            PackagedApps.AppsFolderTarget(root + @"\tools\other.exe", _ => OneApp));
    }

    [Fact]
    public void S3960_an_unpackaged_path_is_never_looked_up()
    {
        var asked = false;
        string? Read(string file)
        {
            asked = true;
            return Manifest;
        }

        Assert.Null(PackagedApps.AppsFolderTarget(@"C:\Users\me\AppData\Local\Programs\Microsoft VS Code\Code.exe", Read));
        Assert.Null(PackagedApps.AppsFolderTarget(@"C:\Windows\explorer.exe", Read));
        Assert.Null(PackagedApps.AppsFolderTarget("", Read));
        Assert.Null(PackagedApps.AppsFolderTarget(null, Read));
        Assert.Null(PackagedApps.AppsFolderTarget("shell:RecycleBinFolder", Read));

        // Not a package full name (five fields), the package folder itself, or a path that climbs out.
        Assert.Null(PackagedApps.AppsFolderTarget(@"C:\Program Files\WindowsApps\NotAFullName\app.exe", Read));
        Assert.Null(PackagedApps.AppsFolderTarget(WhatsAppRoot, Read));
        Assert.Null(PackagedApps.AppsFolderTarget(WhatsAppRoot + @"\..\other.exe", Read));
        Assert.False(asked);
    }

    [Fact]
    public void S3960_a_manifest_that_does_not_say_is_no_app()
    {
        var exe = WhatsAppRoot + @"\other.exe";

        Assert.Null(PackagedApps.AppsFolderTarget(exe, _ => null));
        Assert.Null(PackagedApps.AppsFolderTarget(exe, _ => string.Empty));
        Assert.Null(PackagedApps.AppsFolderTarget(exe, _ => "<not xml"));

        // Two apps, neither this file: none is guessed.
        Assert.Null(PackagedApps.AppsFolderTarget(exe, _ => Manifest));

        // A DTD is refused, not expanded.
        Assert.Null(PackagedApps.AppsFolderTarget(exe, _ => "<!DOCTYPE p [<!ENTITY x \"y\">]><Package><Applications><Application Id=\"App\" Executable=\"other.exe\" /></Applications></Package>"));

        // An id that is no app id is not put into a shell name.
        Assert.Null(PackagedApps.AppsFolderTarget(exe, _ => "<Package><Applications><Application Id=\"..\\x\" Executable=\"other.exe\" /></Applications></Package>"));

        // A document past the cap is not read.
        Assert.Null(PackagedApps.AppsFolderTarget(exe, _ => OneApp + new string(' ', PackagedApps.MaxManifestCharacters)));
    }

    private const string CodexRoot = @"C:\Program Files\WindowsApps\OpenAI.Codex_26.1007.2314.0_x64__2p2nqsd0c76g0";

    // The shape of Codex's manifest: a white logo for the dark taskbar, a light-theme one beside it.
    private const string CodexManifest = """
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                 xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10">
          <Applications>
            <Application Id="App" Executable="app/ChatGPT.exe" EntryPoint="Windows.FullTrustApplication">
              <uap:VisualElements DisplayName="ChatGPT" Square44x44Logo="assets/Square44x44Logo.png" BackgroundColor="#3143ff" />
            </Application>
          </Applications>
        </Package>
        """;

    private static readonly string[] CodexAssets =
    [
        "Square150x150Logo.png",
        "Square44x44Logo.png",
        "Square44x44Logo.scale-200.png",
        "Square44x44Logo.targetsize-24_altform-lightunplated.png",
        "Square44x44Logo.targetsize-24_altform-unplated.png",
        "Square44x44Logo.targetsize-48_altform-lightunplated.png",
        "Square44x44Logo.targetsize-48_altform-unplated.png",
        "Square44x44Logo.targetsize-48_altform-lightunplated_contrast-black.png",
        "Square44x44Logo.targetsize-64_altform-colorful_theme-light.png",
        "Square44x44Logo.targetsize-96_altform-lightunplated.png",
        "Square44x44Logo.targetsize-96_altform-unplated.png",
    ];

    private static PackagedLogo? Codex(LogoTheme theme, int pixels, IReadOnlyList<string> files, string manifest = CodexManifest)
    {
        string? listed = null;
        var logo = PackagedApps.Logo(
            CodexRoot + @"\app\ChatGPT.exe",
            _ => manifest,
            folder =>
            {
                listed = folder;
                return files;
            },
            theme,
            pixels);

        Assert.True(listed is null || listed == CodexRoot + @"\assets", listed);
        return logo;
    }

    [Fact]
    public void S3965_a_light_card_draws_the_light_theme_logo_and_a_dark_card_the_dark_one()
    {
        // Regression: the shell's Apps-folder item drew Codex's white dark-theme logo on a light card.
        Assert.Equal(
            new PackagedLogo(CodexRoot + @"\assets\Square44x44Logo.targetsize-48_altform-lightunplated.png", null),
            Codex(LogoTheme.Light, 48, CodexAssets));
        Assert.Equal(
            new PackagedLogo(CodexRoot + @"\assets\Square44x44Logo.targetsize-48_altform-unplated.png", null),
            Codex(LogoTheme.Dark, 48, CodexAssets));
    }

    [Fact]
    public void S3965_the_size_is_the_smallest_not_under_the_one_drawn_else_the_largest()
    {
        Assert.EndsWith("targetsize-48_altform-lightunplated.png", Codex(LogoTheme.Light, 40, CodexAssets)!.File);
        Assert.EndsWith("targetsize-24_altform-unplated.png", Codex(LogoTheme.Dark, 20, CodexAssets)!.File);
        Assert.EndsWith("targetsize-96_altform-unplated.png", Codex(LogoTheme.Dark, 200, CodexAssets)!.File);
    }

    [Fact]
    public void S3965_without_an_unplated_logo_the_plated_one_stands_on_its_background_colour()
    {
        string[] plated = ["Square44x44Logo.png", "Square44x44Logo.scale-200.png", "Square44x44Logo.targetsize-48_contrast-white.png"];

        // scale-200 is 88 px: the smallest not under 48.
        Assert.Equal(
            new PackagedLogo(CodexRoot + @"\assets\Square44x44Logo.scale-200.png", "#3143FF"),
            Codex(LogoTheme.Light, 48, plated));

        // A transparent or named background is no plate.
        Assert.Null(Codex(LogoTheme.Dark, 48, plated, CodexManifest.Replace("#3143ff", "transparent"))!.Plate);
        Assert.Null(Codex(LogoTheme.Dark, 48, plated, CodexManifest.Replace("#3143ff", "blue"))!.Plate);

        // Only a dark-theme unplated logo: on a light card the plated one goes before it.
        string[] terminal = ["Square44x44Logo.targetsize-48.png", "Square44x44Logo.targetsize-48_altform-unplated.png"];
        Assert.EndsWith("targetsize-48.png", Codex(LogoTheme.Light, 48, terminal)!.File);
        Assert.EndsWith("targetsize-48_altform-unplated.png", Codex(LogoTheme.Dark, 48, terminal)!.File);

        // Nothing but the unplated dark one: drawn on a light card too, rather than nothing.
        Assert.EndsWith("_altform-unplated.png", Codex(LogoTheme.Light, 48, ["Square44x44Logo.targetsize-48_altform-unplated.png"])!.File);

        // theme-light with altform-unplated is the light-theme asset.
        Assert.EndsWith("_altform-unplated_theme-light.png", Codex(LogoTheme.Light, 48, [.. terminal, "Square44x44Logo.targetsize-48_altform-unplated_theme-light.png"])!.File);
    }

    [Fact]
    public void S3965_no_asset_or_an_unsafe_logo_path_leaves_the_icon_to_the_shell()
    {
        Assert.Null(Codex(LogoTheme.Light, 48, []));
        Assert.Null(Codex(LogoTheme.Light, 48, ["Square150x150Logo.png", "Square44x44Logo.targetsize-48_contrast-black.png", "Square44x44Logo.targetsize-48_altform-colorful.png"]));
        Assert.Null(Codex(LogoTheme.Light, 48, CodexAssets, CodexManifest.Replace("assets/Square44x44Logo.png", "../../evil/Logo.png")));
        Assert.Null(Codex(LogoTheme.Light, 48, CodexAssets, CodexManifest.Replace("assets/Square44x44Logo.png", @"C:\evil\Logo.png")));
        Assert.Null(Codex(LogoTheme.Light, 48, CodexAssets, CodexManifest.Replace(" Square44x44Logo=\"assets/Square44x44Logo.png\"", string.Empty)));
        Assert.Null(PackagedApps.Logo(@"C:\Windows\explorer.exe", _ => CodexManifest, _ => CodexAssets, LogoTheme.Light, 48));
    }
}
