using System.Xml.Linq;
using Themia.Core.Localization;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1073] The Store package's languages and Windows target.</summary>
public sealed class StorePackageTests
{
    private static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";

    private static XDocument Manifest() => XDocument.Load(RepoFiles.At("src", "Themia.App", "Package.appxmanifest"));

    /// <summary>The Store's code for an interface language: the file's code, with the variant the Store needs where it differs.</summary>
    private static string StoreCode(Locale locale) => LanguageResolver.Code(locale) switch
    {
        "en" => "en-US",
        "pt" => "pt-BR",
        "zh" => "zh-Hans",
        var code => code,
    };

    [Fact]
    public void S1073_the_manifest_declares_exactly_the_interface_languages_english_first()
    {
        var declared = Manifest().Descendants(Foundation + "Resource").Select(r => (string)r.Attribute("Language")!).ToList();

        Assert.Equal("en-US", declared[0]);
        Assert.Equal(LanguageResolver.All.Select(StoreCode).Order(StringComparer.Ordinal), declared.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void S1073_the_package_targets_windows_11_on_the_desktop()
    {
        var family = Assert.Single(Manifest().Descendants(Foundation + "TargetDeviceFamily"));

        Assert.Equal("Windows.Desktop", (string)family.Attribute("Name")!);
        Assert.Equal("10.0.22000.0", (string)family.Attribute("MinVersion")!);

        // The MSIX tooling writes its TargetDeviceFamily item over the manifest's versions, so the
        // package build's item must carry the same ones.
        var project = XDocument.Load(RepoFiles.At("src", "Themia.App", "Themia.App.csproj"));
        var item = Assert.Single(project.Descendants("TargetDeviceFamily"));
        Assert.Equal("Windows.Desktop", (string)item.Attribute("Include")!);
        Assert.Equal((string)family.Attribute("MinVersion")!, item.Element("TargetPlatformMinVersion")!.Value);
        Assert.Equal((string)family.Attribute("MaxVersionTested")!, item.Element("TargetPlatformVersion")!.Value);
    }
}
