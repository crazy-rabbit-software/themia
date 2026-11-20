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
/// order, read from every referenced assembly. The host declares none itself: every widget and
/// provider is a module project, or Core's for the host's own providers (THEMIAMOD005). Ids are
/// checked here, so a malformed one (THEMIAMOD001), one declared twice (THEMIAMOD002), a type that
/// is not a module (THEMIAMOD003) or a module project declaring another id than its name
/// (THEMIAMOD004) fails the build instead of a start, and so does a declaration that cannot be read (THEMIAMOD006: it
/// was skipped in silence). A widget project marked a family (<c>[assembly: ThemiaWidgetFamily]</c>) declares several
/// widgets of ids of their own.
/// <para>
/// Incremental per reference: each referenced assembly's declarations are read into a value
/// (<see cref="Declared"/>), so the registry is written again only when what some reference declares changed, not on
/// every edit of the host.
/// </para>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ModuleRegistryGenerator : IIncrementalGenerator
{
    public const string WidgetAttribute = "Themia.Sdk.ThemiaWidgetAttribute";
    public const string ProviderAttribute = "Themia.Sdk.ThemiaProviderAttribute";
    public const string FamilyAttribute = "Themia.Sdk.ThemiaWidgetFamilyAttribute";
    public const string WidgetContract = "Themia.Sdk.WinUI.IWidgetModule";
    public const string ProviderContract = "Themia.Sdk.IProviderModule";

    /// <summary>The same rule as <c>Themia.Sdk.ModuleIds.IsValid</c> ([S-6900]).</summary>
    public const int MaxIdLength = 64;

    /// <summary>The tracked step of what the references declare (the generator's tests see it cached across host edits).</summary>
    public const string DeclaredStep = "Declared";

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

    private static readonly DiagnosticDescriptor NotItsName = new(
        "THEMIAMOD004", "Module id is not its project's name",
        "{0} declares the {1} '{2}', but a module project declares exactly the one its name says: the {3} '{4}'",
        "Modules", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InTheHost = new(
        "THEMIAMOD005", "The host declares a module",
        "{0} declares the module '{1}' itself; a module is its own project (src/Widgets/Themia.Widget.<Name>, src/Providers/Themia.Provider.<Name>) that the host references",
        "Modules", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Unreadable = new(
        "THEMIAMOD006", "A module declaration that cannot be read",
        "{0} declares a {1} the registry cannot read: it takes an id (a string) and a module type (typeof(...))",
        "Modules", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>The assembly name prefix of a widget module project and of a provider module project.</summary>
    public const string WidgetProject = "Themia.Widget.";

    public const string ProviderProject = "Themia.Provider.";

    /// <summary>
    /// [S-6901] The id a module project's name gives (<c>Themia.Widget.WeatherNow</c>: widget
    /// <c>weatherNow</c>), or null for an assembly that is not a module project (the app, Core).
    /// </summary>
    public static (bool Widget, string Id)? IdOfProject(string assembly)
    {
        foreach (var (prefix, widget) in new[] { (WidgetProject, true), (ProviderProject, false) })
        {
            if (assembly.StartsWith(prefix, StringComparison.Ordinal) && assembly.Length > prefix.Length)
            {
                var suffix = assembly.Substring(prefix.Length);
                return (widget, char.ToLowerInvariant(suffix[0]) + suffix.Substring(1));
            }
        }

        return null;
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var ns = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
            options.GlobalOptions.TryGetValue("build_property.RootNamespace", out var value) && !string.IsNullOrEmpty(value) ? value : "Themia");

        // What the host's compilation is: its name, whether the two contracts exist, what it declares itself.
        var host = context.CompilationProvider.Select((compilation, _) => Host.Of(compilation));

        // Per reference: what each referenced assembly declares, as a value.
        var declared = context.MetadataReferencesProvider
            .Combine(context.CompilationProvider)
            .Select((pair, _) => pair.Right.GetAssemblyOrModuleSymbol(pair.Left) is IAssemblySymbol assembly ? Declared.Of(assembly) : null)
            .Where(d => d is not null)
            .Collect()
            .WithTrackingName(DeclaredStep);

        context.RegisterSourceOutput(declared.Combine(host).Combine(ns), (spc, all) => Emit(spc, all.Left.Left, all.Left.Right, all.Right));
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

    /// <summary>One <c>[assembly: ThemiaWidget/ThemiaProvider]</c> as a value: the id and type (null: it cannot be read).</summary>
    private sealed class Declaration : IEquatable<Declaration>
    {
        public Declaration(bool widget, string? id, string? type, string? typeName, bool fits, Location where)
        {
            Widget = widget;
            Id = id;
            Type = type;
            TypeName = typeName;
            Fits = fits;
            Where = where;
        }

        public bool Widget { get; }

        public string? Id { get; }

        public string? Type { get; }

        public string? TypeName { get; }

        public bool Fits { get; }

        public Location Where { get; }

        public bool Equals(Declaration? other)
            => other is not null && Widget == other.Widget && Id == other.Id && Type == other.Type && Fits == other.Fits && Where.Equals(other.Where);

        public override bool Equals(object? obj) => Equals(obj as Declaration);

        public override int GetHashCode() => (Id ?? "").GetHashCode() ^ (Type ?? "").GetHashCode();
    }

    /// <summary>What one referenced assembly declares: its name, whether it is a family, its declarations in order.</summary>
    private sealed class Declared : IEquatable<Declared>
    {
        private Declared(string assembly, bool family, Declaration[] declarations)
        {
            Assembly = assembly;
            Family = family;
            Declarations = declarations;
        }

        public string Assembly { get; }

        public bool Family { get; }

        public Declaration[] Declarations { get; }

        public static Declared? Of(IAssemblySymbol assembly)
        {
            var family = false;
            var found = new List<Declaration>();
            foreach (var attribute in assembly.GetAttributes())
            {
                var name = attribute.AttributeClass?.ToDisplayString();
                family |= name == FamilyAttribute;
                if (name != WidgetAttribute && name != ProviderAttribute)
                {
                    continue;
                }

                var where = attribute.ApplicationSyntaxReference is { } syntax ? Location.Create(syntax.SyntaxTree, syntax.Span) : Location.None;
                var widget = name == WidgetAttribute;
                if (attribute.ConstructorArguments.Length != 2
                    || attribute.ConstructorArguments[0].Value is not string id
                    || attribute.ConstructorArguments[1].Value is not INamedTypeSymbol type)
                {
                    found.Add(new Declaration(widget, null, null, null, false, where));
                    continue;
                }

                found.Add(new Declaration(widget, id, "global::" + type.ToDisplayString(), type.ToDisplayString(), Fits(type, widget), where));
            }

            return found.Count == 0 && !family ? null : new Declared(assembly.Name, family, [.. found]);
        }

        private static bool Fits(INamedTypeSymbol type, bool widget)
        {
            var contract = widget ? WidgetContract : ProviderContract;
            return type.TypeKind == TypeKind.Class
                && !type.IsAbstract
                && type.DeclaredAccessibility == Accessibility.Public
                && type.AllInterfaces.Any(i => i.ToDisplayString() == contract)
                && type.InstanceConstructors.Any(k => k.Parameters.Length == 0 && k.DeclaredAccessibility == Accessibility.Public);
        }

        public bool Equals(Declared? other)
            => other is not null && Assembly == other.Assembly && Family == other.Family && Declarations.SequenceEqual(other.Declarations);

        public override bool Equals(object? obj) => Equals(obj as Declared);

        public override int GetHashCode() => Assembly.GetHashCode() ^ Declarations.Length;
    }

    /// <summary>The host's compilation as a value: its name, which contracts it has, and what it declares itself (THEMIAMOD005).</summary>
    private sealed class Host : IEquatable<Host>
    {
        private Host(string name, bool widgets, bool providers, (string Id, Location Where)[] own)
        {
            Name = name;
            Widgets = widgets;
            Providers = providers;
            Own = own;
        }

        public string Name { get; }

        public bool Widgets { get; }

        public bool Providers { get; }

        public (string Id, Location Where)[] Own { get; }

        public static Host Of(Compilation compilation)
        {
            var own = new List<(string, Location)>();
            foreach (var attribute in compilation.Assembly.GetAttributes())
            {
                var name = attribute.AttributeClass?.ToDisplayString();
                if (name == WidgetAttribute || name == ProviderAttribute)
                {
                    var where = attribute.ApplicationSyntaxReference is { } syntax ? Location.Create(syntax.SyntaxTree, syntax.Span) : Location.None;
                    var id = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
                    own.Add((id ?? "", where));
                }
            }

            return new Host(
                compilation.AssemblyName ?? "",
                compilation.GetTypeByMetadataName(WidgetContract) is not null,
                compilation.GetTypeByMetadataName(ProviderContract) is not null,
                [.. own]);
        }

        public bool Equals(Host? other)
            => other is not null && Name == other.Name && Widgets == other.Widgets && Providers == other.Providers && Own.SequenceEqual(other.Own);

        public override bool Equals(object? obj) => Equals(obj as Host);

        public override int GetHashCode() => Name.GetHashCode();
    }

    private static void Emit(SourceProductionContext context, System.Collections.Immutable.ImmutableArray<Declared?> declared, Host host, string ns)
    {
        var widgets = new List<Found>();
        var providers = new List<Found>();

        foreach (var (id, where) in host.Own)
        {
            context.ReportDiagnostic(Diagnostic.Create(InTheHost, where, host.Name, id));
        }

        foreach (var assembly in declared.OfType<Declared>().OrderBy(a => a.Assembly, StringComparer.Ordinal))
        {
            foreach (var d in assembly.Declarations)
            {
                var kind = d.Widget ? "widget" : "provider";
                if (d.Id is not { } id || d.Type is not { } type)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Unreadable, d.Where, assembly.Assembly, kind));
                    continue;
                }

                if (!IsValidId(id))
                {
                    context.ReportDiagnostic(Diagnostic.Create(BadId, d.Where, id, assembly.Assembly));
                    continue;
                }

                // [S-6901] A family project's widgets are its kinds, each its own id; its name is the family's.
                if (IdOfProject(assembly.Assembly) is { } named && (named.Widget != d.Widget || (named.Id != id && !(assembly.Family && named.Widget))))
                {
                    context.ReportDiagnostic(Diagnostic.Create(NotItsName, d.Where,
                        assembly.Assembly, kind, id, named.Widget ? "widget" : "provider", named.Id));
                    continue;
                }

                if (!(d.Widget ? host.Widgets : host.Providers) || !d.Fits)
                {
                    context.ReportDiagnostic(Diagnostic.Create(WrongType, d.Where, d.TypeName, id, d.Widget ? WidgetContract : ProviderContract));
                    continue;
                }

                var list = d.Widget ? widgets : providers;
                var clash = list.FirstOrDefault(m => m.Id == id);
                if (clash is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Duplicate, d.Where, id, clash.By, d.TypeName));
                    continue;
                }

                list.Add(new Found(id, type, d.TypeName!));
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
        if (host.Widgets)
        {
            Append(sb, "Widgets", "global::" + WidgetContract, widgets);
        }

        if (host.Providers)
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
