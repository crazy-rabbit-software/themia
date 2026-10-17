using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Themia.Core.Localization;
using Themia.Localization.Generator;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6909] A module's own strings: the generator's module mode (a project that sets
/// <c>ThemiaStringsEnum</c> gets its enum, tables and extensions in its own namespace) and the
/// shared runtime every table looks up through (<see cref="StringTable"/>, <see cref="LocalText"/>).
/// </summary>
public sealed class LocaleModuleTests
{
    private const string Root = "C:/mod/Localization/Locales/";

    private sealed class MemoryText(string path, string text) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text, Encoding.UTF8);
    }

    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
    }

    private sealed class Provider(Dictionary<string, string> global) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(global);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options([]);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Options([]);
    }

    private static GeneratorDriverRunResult Run(string? stringsEnum, params (string Path, string Json)[] files)
    {
        var global = new Dictionary<string, string> { ["build_property.RootNamespace"] = "Themia.Widget.Sample" };
        if (stringsEnum is not null)
        {
            global["build_property.ThemiaStringsEnum"] = stringsEnum;
        }

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new StringTableGenerator().AsSourceGenerator()],
            [.. files.Select(f => (AdditionalText)new MemoryText(Root + f.Path, f.Json))],
            optionsProvider: new Provider(global));
        return driver.RunGenerators(CSharpCompilation.Create("t")).GetRunResult();
    }

    private static string Source(GeneratorDriverRunResult run, string name)
        => run.GeneratedTrees.FirstOrDefault(t => Path.GetFileName(t.FilePath) == name)?.ToString() ?? string.Empty;

    [Fact]
    public void S6909_a_module_gets_its_own_enum_tables_and_extensions_in_its_namespace()
    {
        var run = Run("SampleStr",
            ("en.json", """{ "sample.label": "Sample", "sample.count": { "one": "{count} item", "other": "{count} items" } }"""),
            ("de.json", """{ "sample.label": "Beispiel" }"""));

        Assert.Empty(run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        var keys = Source(run, "SampleStr.g.cs");
        var tables = Source(run, "SampleStrTables.g.cs");
        Assert.Contains("namespace Themia.Widget.Sample;", keys, StringComparison.Ordinal);
        Assert.Contains("public enum SampleStr", keys, StringComparison.Ordinal);
        Assert.Contains("Sample_Label = 0", keys, StringComparison.Ordinal);
        Assert.Contains("public static class SampleStrExtensions", tables, StringComparison.Ordinal);
        Assert.Contains("public static class SampleStrTables", tables, StringComparison.Ordinal);
        Assert.Contains("StringTable Table = new(\"SampleStr\"", tables, StringComparison.Ordinal);

        // Never the host's names: a module's tables cannot shadow Str.
        Assert.Empty(Source(run, "Str.g.cs") is var host && host.Contains("public enum Str\n", StringComparison.Ordinal) ? host : string.Empty);
        Assert.DoesNotContain("class StringTables", tables, StringComparison.Ordinal);
    }

    [Fact]
    public void S6909_without_a_strings_enum_the_project_gets_the_hosts_Str()
    {
        var run = Run(null, ("en.json", """{ "a.b": "x" }"""));

        Assert.Contains("public enum Str", Source(run, "Str.g.cs"), StringComparison.Ordinal);
        Assert.Contains("internal static class StringTables", Source(run, "StringTables.g.cs"), StringComparison.Ordinal);
        Assert.Contains("namespace Themia.Core.Localization;", Source(run, "Str.g.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void S6909_a_strings_enum_that_is_not_a_Str_name_fails_the_build()
    {
        var run = Run("sample", ("en.json", """{ "a.b": "x" }"""));

        Assert.Contains(run.Diagnostics, d => d.Id == "THEMIALOC010");
    }

    [Fact]
    public void S6909_a_table_looks_up_like_Str_with_english_fallback_plurals_and_the_key_last()
    {
        var english = new LocaleTable(Locale.En, ["Save", "{count} files"], [(1, [null, "{count} file", null, null, null, "{count} files"])]);
        var german = new LocaleTable(Locale.De, [null, "{count} Dateien"], [(1, [null, "{count} Datei", null, null, null, "{count} Dateien"])]);
        var table = new StringTable("SampleStr", ["sample.save", "sample.files"], [], l => l == Locale.De ? german : english);

        Assert.Equal("Save", table.Get(Locale.De, 0));
        Assert.Equal("1 Datei", table.Plural(Locale.De, 1, 1));
        Assert.Equal("3 files", table.Plural(Locale.En, 1, 3));
        Assert.Equal("#7", table.Get(Locale.En, 7));
        Assert.Equal("sample.files", table.Key(1));
        Assert.Equal(1, table.IdOf("sample.files"));
    }

    [Fact]
    public void S6909_local_text_reads_its_table_in_the_asked_language_and_a_fixed_text_never_changes()
    {
        var english = new LocaleTable(Locale.En, ["Hello {name}"], []);
        var german = new LocaleTable(Locale.De, ["Hallo {name}"], []);
        var table = new StringTable("SampleStr", ["sample.hello"], [], l => l == Locale.De ? german : english);
        var text = new LocalText(table, 0);

        Assert.Equal("Hallo Ada", text.Format(Locale.De, ("name", "Ada")));
        Assert.Equal("sample.hello", text.Key);
        Assert.Equal("Microsoft", LocalText.Fixed("Microsoft").Text(Locale.Ja));
        Assert.True(default(LocalText).IsEmpty);
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocalText(table, 1));
        Assert.Equal(Strings.Get(Locale.En, Str.Common_Save), Str.Common_Save.Local().Text(Locale.En));
    }
}
