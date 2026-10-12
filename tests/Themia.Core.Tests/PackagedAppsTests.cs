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
}
