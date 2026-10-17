using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Themia.Modules.Generator;

/// <summary>
/// [S-6901] Emits <c>ModuleRegistry</c> into the compilation that hosts the modules (the app): one
/// <c>new M()</c> per <c>[assembly: ThemiaWidget(id, typeof(M))]</c> / <c>ThemiaProvider</c>, in id
/// order, read from every referenced assembly and from the compilation itself (the widgets still
/// compiled inside the app, docs/WIDGET-KIT.md section 11.1). Ids are checked here, so a malformed
/// one (THEMIAMOD001), one declared twice (THEMIAMOD002) or a type that is not a module
/// (THEMIAMOD003) fails the build instead of a start.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ModuleRegistryGenerator : IIncrementalGenerator
{
    public const string WidgetAttribute = "Themia.Sdk.ThemiaWidgetAttribute";
    public const string ProviderAttribute = "Themia.Sdk.ThemiaProviderAttribute";
    public const string WidgetContract = "Themia.Sdk.WinUI.IWidgetModule";
    public const string ProviderContract = "Themia.Sdk.IProviderModule";

    /// <summary>The same rule as <c>Themia.Sdk.ModuleIds.IsValid</c> ([S-6900]).</summary>
    public const int MaxIdLength = 64;

    private static readonly DiagnosticDescriptor BadId = new(
        "THEMIAMOD001", "Bad module id",
        "Module id '{0}' declared by {1} must be camelCase: a lowercase letter, then letters and digits, at most 64 characters",
        "Modules", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Duplicate = new(
        "THEMIAMOD002", "Duplicate module id",
        "Module id '{0}' is declared twice, by {1} and by {2}",
        "Modules", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor WrongType = new(
        "THEMIAMOD003", "Module type does not implement its contract",
        "{0} (module '{1}') must be a public, non-abstract class implementing {2} with a public parameterless constructor",
        "Modules", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var ns = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
            options.GlobalOptions.TryGetValue("build_property.RootNamespace", out var value) && !string.IsNullOrEmpty(value) ? value : "Themia");
        context.RegisterSourceOutput(context.CompilationProvider.Combine(ns), (spc, pair) => Emit(spc, pair.Left, pair.Right));
    }

    public static bool IsValidId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id!.Length > MaxIdLength || id[0] < 'a' || id[0] > 'z')
        {
            return false;
        }

        foreach (var c in id)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class Found
    {
        public Found(string id, string type, string by)
        {
            Id = id;
            Type = type;
            By = by;
        }

        public string Id { get; }

        public string Type { get; }

        public string By { get; }
    }

    private static void Emit(SourceProductionContext context, Compilation compilation, string ns)
    {
        var widgetContract = compilation.GetTypeByMetadataName(WidgetContract);
        var providerContract = compilation.GetTypeByMetadataName(ProviderContract);
        var widgets = new List<Found>();
        var providers = new List<Found>();

        var assemblies = new List<IAssemblySymbol> { compilation.Assembly };
        assemblies.AddRange(compilation.SourceModule.ReferencedAssemblySymbols.OrderBy(a => a.Name, StringComparer.Ordinal));

        foreach (var assembly in assemblies)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                var name = attribute.AttributeClass?.ToDisplayString();
                var isWidget = name == WidgetAttribute;
                if (!isWidget && name != ProviderAttribute)
                {
                    continue;
                }

                var location = attribute.ApplicationSyntaxReference is { } syntax
                    ? Location.Create(syntax.SyntaxTree, syntax.Span)
                    : Location.None;
                if (attribute.ConstructorArguments.Length != 2
                    || attribute.ConstructorArguments[0].Value is not string id
                    || attribute.ConstructorArguments[1].Value is not INamedTypeSymbol type)
                {
                    continue;
                }

                if (!IsValidId(id))
                {
                    context.ReportDiagnostic(Diagnostic.Create(BadId, location, id, assembly.Name));
                    continue;
                }

                var contract = isWidget ? widgetContract : providerContract;
                if (contract is null
                    || type.TypeKind != TypeKind.Class
                    || type.IsAbstract
                    || type.DeclaredAccessibility != Accessibility.Public
                    || !type.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)
                    || !type.InstanceConstructors.Any(k => k.Parameters.Length == 0 && k.DeclaredAccessibility == Accessibility.Public))
                {
                    context.ReportDiagnostic(Diagnostic.Create(WrongType, location, type.ToDisplayString(), id, isWidget ? WidgetContract : ProviderContract));
                    continue;
                }

                var list = isWidget ? widgets : providers;
                var clash = list.FirstOrDefault(m => m.Id == id);
                if (clash is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Duplicate, location, id, clash.By, type.ToDisplayString()));
                    continue;
                }

                list.Add(new Found(id, "global::" + type.ToDisplayString(), type.ToDisplayString()));
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/> Themia.Modules.Generator: the modules compiled into this build ([S-6901]).");
        sb.AppendLine("#nullable enable");
        sb.Append("namespace ").Append(ns).AppendLine(".Modules;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Every module compiled into this build, in id order. Generated; never edited by hand.</summary>");
        sb.AppendLine("internal static class ModuleRegistry");
        sb.AppendLine("{");
        if (widgetContract is not null)
        {
            Append(sb, "Widgets", "global::" + WidgetContract, widgets);
        }

        if (providerContract is not null)
        {
            Append(sb, "Providers", "global::" + ProviderContract, providers);
        }

        sb.AppendLine("}");
        context.AddSource("ModuleRegistry.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void Append(StringBuilder sb, string name, string contract, List<Found> modules)
    {
        var ordered = modules.OrderBy(m => m.Id, StringComparer.Ordinal).ToList();
        sb.Append("    public static readonly global::System.Collections.Generic.IReadOnlyList<").Append(contract).Append("> ").Append(name).AppendLine(" =");
        sb.AppendLine("    [");
        foreach (var m in ordered)
        {
            sb.Append("        new ").Append(m.Type).AppendLine("(),");
        }

        sb.AppendLine("    ];");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>The declared ids, in the same order: a module's descriptor must carry its own.</summary>");
        sb.Append("    public static readonly global::System.Collections.Generic.IReadOnlyList<string> ")
            .Append(name.Substring(0, name.Length - 1)).Append("Ids = [")
            .Append(string.Join(", ", ordered.Select(m => "\"" + m.Id + "\"")))
            .AppendLine("];");
    }
}
