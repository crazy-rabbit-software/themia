using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Themia.Modules.Generator;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6901] The registry generator, run in-process over small compilations that declare stand-in
/// contracts under the real names, so its build errors (which a real build only shows as a failed
/// build) and its output are asserted directly.
/// </summary>
public sealed class ModuleRegistryGeneratorTests
{
    private const string Contracts = """
        namespace Themia.Sdk
        {
            [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class ThemiaWidgetAttribute(string id, System.Type module) : System.Attribute;
            [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class ThemiaProviderAttribute(string id, System.Type module) : System.Attribute;
            public interface IProviderModule;
        }
        namespace Themia.Sdk.WinUI
        {
            public interface IWidgetModule;
        }
        """;

    private static readonly ImmutableArray<MetadataReference> Framework =
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))];

    /// <summary>The contracts as their own assembly, as Themia.Sdk and Themia.Sdk.WinUI are to the app.</summary>
    private static readonly MetadataReference Sdk = Emit("Stub.Sdk", Contracts, Framework);

    private sealed record Result(ImmutableArray<Diagnostic> Errors, string Registry)
    {
        public IEnumerable<string> Ids => Errors.Select(e => e.Id);
    }

    private static Result Run(string source, params MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(
            "App",
            [CSharpSyntaxTree.ParseText(source)],
            [.. Framework, Sdk, .. references],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ModuleRegistryGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var run = driver.GetRunResult();
        var errors = run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        var compileErrors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(compileErrors.Count == 0, string.Join("\n", compileErrors));
        return new Result(errors, run.GeneratedTrees.Single().ToString());
    }

    private static MetadataReference Library(string name, string source) => Emit(name, source, [.. Framework, Sdk]);

    private static MetadataReference Emit(string name, string source, IEnumerable<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        var emitted = compilation.Emit(image);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
        return MetadataReference.CreateFromImage(image.ToArray());
    }

    [Fact]
    public void S6901_lists_the_compilations_own_and_referenced_modules_in_id_order()
    {
        var module = Library("Themia.Widget.Weather", """
            [assembly: Themia.Sdk.ThemiaWidget("weatherNow", typeof(Weather.WeatherModule))]
            namespace Weather { public sealed class WeatherModule : Themia.Sdk.WinUI.IWidgetModule; }
            """);

        var result = Run("""
            [assembly: Themia.Sdk.ThemiaWidget("clock", typeof(App.ClockModule))]
            [assembly: Themia.Sdk.ThemiaProvider("google", typeof(App.GoogleModule))]
            namespace App
            {
                public sealed class ClockModule : Themia.Sdk.WinUI.IWidgetModule;
                public sealed class GoogleModule : Themia.Sdk.IProviderModule;
            }
            """, module);

        Assert.Empty(result.Errors);
        Assert.Contains("new global::App.ClockModule(),", result.Registry, StringComparison.Ordinal);
        Assert.Contains("new global::Weather.WeatherModule(),", result.Registry, StringComparison.Ordinal);
        Assert.Contains("WidgetIds = [\"clock\", \"weatherNow\"];", result.Registry, StringComparison.Ordinal);
        Assert.Contains("ProviderIds = [\"google\"];", result.Registry, StringComparison.Ordinal);
        Assert.True(
            result.Registry.IndexOf("ClockModule", StringComparison.Ordinal) < result.Registry.IndexOf("WeatherModule", StringComparison.Ordinal),
            "modules are emitted in id order");
    }

    [Theory]
    [InlineData("Clock")]
    [InlineData("weather-now")]
    [InlineData("2d")]
    [InlineData("")]
    public void S6901_a_malformed_id_is_THEMIAMOD001(string id)
    {
        var result = Run($$"""
            [assembly: Themia.Sdk.ThemiaWidget("{{id}}", typeof(App.M))]
            namespace App { public sealed class M : Themia.Sdk.WinUI.IWidgetModule; }
            """);

        Assert.Equal(["THEMIAMOD001"], result.Ids);
        Assert.DoesNotContain("new global::App.M()", result.Registry, StringComparison.Ordinal);
    }

    [Fact]
    public void S6901_an_id_declared_twice_is_THEMIAMOD002()
    {
        var result = Run("""
            [assembly: Themia.Sdk.ThemiaWidget("clock", typeof(App.A))]
            [assembly: Themia.Sdk.ThemiaWidget("clock", typeof(App.B))]
            namespace App
            {
                public sealed class A : Themia.Sdk.WinUI.IWidgetModule;
                public sealed class B : Themia.Sdk.WinUI.IWidgetModule;
            }
            """);

        Assert.Equal(["THEMIAMOD002"], result.Ids);
    }

    [Theory]
    [InlineData("internal sealed class M : Themia.Sdk.WinUI.IWidgetModule;")]
    [InlineData("public abstract class M : Themia.Sdk.WinUI.IWidgetModule;")]
    [InlineData("public sealed class M;")]
    [InlineData("public sealed class M : Themia.Sdk.IProviderModule;")]
    [InlineData("public sealed class M(int x) : Themia.Sdk.WinUI.IWidgetModule { public int X => x; }")]
    public void S6901_a_type_that_is_not_a_widget_module_is_THEMIAMOD003(string type)
    {
        var result = Run($$"""
            [assembly: Themia.Sdk.ThemiaWidget("clock", typeof(App.M))]
            namespace App { {{type}} }
            """);

        Assert.Equal(["THEMIAMOD003"], result.Ids);
    }

    [Fact]
    public void S6901_an_id_and_its_rule_are_the_sdks()
    {
        foreach (var id in new[] { "clock", "weatherNow", "Clock", "a-b", "", new string('a', 64), new string('a', 65) })
        {
            Assert.Equal(Themia.Sdk.ModuleIds.IsValid(id), ModuleRegistryGenerator.IsValidId(id));
        }
    }
}
