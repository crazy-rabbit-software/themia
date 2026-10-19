using System.Text.Json;
using System.Text.RegularExpressions;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-7066]..[S-7071] The kit's conformance checks over source (docs/WIDGET-KIT.md section 9): colours,
/// type, text, thin kit views, kit actions, fixtures and the generic kit. Each starts from an explicit
/// list of today's offenders, named with the reason, that may only shrink.
/// </summary>
public sealed partial class KitConformanceTests
{
    /// <summary>The kit's shared code: what every kit widget is built from, and what must know no service.</summary>
    /// <summary>The kit's shared code: its pure and WinUI parts, and the service layer in the SDK, Core and the app ([S-9902]).</summary>
    private static IEnumerable<string> KitSources()
        => RepoFiles.CSharp(RepoFiles.At("src", "Themia.Sdk", "Kit"))
            .Concat(RepoFiles.CSharp(RepoFiles.SdkKit))
            .Concat(RepoFiles.CSharp(RepoFiles.At("src", "Themia.Sdk", "Services")))
            .Concat(RepoFiles.CSharp(RepoFiles.At("src", "Themia.Core", "Services")))
            .Concat(RepoFiles.CSharp(RepoFiles.At("src", "Themia.App", "Services", "Kit")));

    [GeneratedRegex(@"\bColors\.\w+|\bColor\.FromArgb\(|\bColorHelper\.")]
    private static partial Regex NamesAColour();

    /// <summary>[S-7066] The kit's parts name no colour at all, not even a fallback: every brush is a theme key (<c>Fluent</c>, <c>ThemeBrush</c>) or the item layer's.</summary>
    [Fact]
    public void S7066_the_kits_parts_name_no_colour()
    {
        var offending = KitSources().Where(f => NamesAColour().IsMatch(Code(f))).Select(Path.GetFileName).ToList();

        Assert.Empty(offending);
    }

    [GeneratedRegex(@"\bFontSize\s*=\s*\d|\bnew\s+FontFamily\(")]
    private static partial Regex LiteralType();

    /// <summary>
    /// [S-7067] [S-210] Type comes from the Fluent ramp (<c>FluentType</c>, <c>Fluent.Apply</c>), never a
    /// literal size or a font of a view's own. Today's views that still set one: a glyph run's size
    /// (<c>WidgetParts</c>), the clipboard page's preview of a colour code, the folder's tile glyphs, the
    /// habit tracker's rows ([S-822]'s kind of list: it may only shrink).
    /// </summary>
    [Fact]
    public void S7067_no_view_sets_a_literal_font_size_or_family_beyond_todays_list()
    {
        var offending = RepoFiles.ViewSources().Where(f => LiteralType().IsMatch(Code(f))).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["ClipboardSettings.cs", "FolderView.Render.cs", "HabitTrackerView.Rows.cs", "WidgetParts.cs"], offending);
    }

    [GeneratedRegex(@"\b(Text|Content|PlaceholderText|Header|Label)\s*=\s*""(?<text>[^""]*[A-Za-z][^""]*)""")]
    private static partial Regex LiteralText();

    [GeneratedRegex(@"ToolTipService\.SetToolTip\([^,]+,\s*""[^""]*[A-Za-z]")]
    private static partial Regex LiteralTooltip();

    /// <summary>
    /// [S-7068] [S-1102] Every word a widget or the kit shows goes through the strings layer: no
    /// literal text, content, placeholder or tooltip in a view or a kit part. An example web address
    /// in a placeholder is allowed (it is an address, not a sentence); so is a debug preview's sample
    /// data (<c>Preview/</c>, never shipped).
    /// </summary>
    [Fact]
    public void S7068_no_view_shows_a_literal_sentence()
    {
        var offending = new List<string>();
        foreach (var file in RepoFiles.ViewSources().Concat(KitSources()).Distinct()
            .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("Preview") && !f.EndsWith(".Preview.cs", StringComparison.Ordinal)))
        {
            var code = Code(file);
            foreach (Match m in LiteralText().Matches(code))
            {
                var text = m.Groups["text"].Value;
                if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                {
                    offending.Add($"{Path.GetFileName(file)}: {m.Value}");
                }
            }

            offending.AddRange(LiteralTooltip().Matches(code).Select(m => $"{Path.GetFileName(file)}: {m.Value}"));
        }

        Assert.Empty(offending);
    }

    [GeneratedRegex(@"\b(ScrollViewer|RowFocus|ItemMenu|ItemMenuEntry|ProgressRing|ItemCursor|ListKeys|Process)\b|Fluent\.EmptyState")]
    private static partial Regex ListPlumbing();

    /// <summary>
    /// [S-7069] Kit views stay thin: a widget whose config is an <c>IKitConfig</c> builds no list
    /// plumbing of its own (no scroll viewer, row focus, menu entries, ring, empty state, cursor or
    /// key map, no process): <c>KitList</c>, <c>KitHeader</c>, <c>KitStatePanel</c> and <c>KitBinding</c> do.
    /// </summary>
    [Fact]
    public void S7069_a_kit_widgets_view_builds_no_list_of_its_own()
    {
        var kitModules = RepoFiles.WidgetModules()
            .Where(dir => RepoFiles.CSharp(dir).Any(f => Regex.IsMatch(Code(f), @"class \w+Config\b[^{]*\bIKitConfig\b")))
            .ToList();
        Assert.Contains(kitModules, d => Path.GetFileName(d) == "Themia.Widget.Rss");

        var offending = kitModules
            .SelectMany(RepoFiles.CSharp)
            .Where(f => Path.GetFileName(f).Contains("View", StringComparison.Ordinal) && !RepoFiles.IsSettingsPage(f))
            .SelectMany(f => ListPlumbing().Matches(Code(f)).Select(m => $"{Path.GetFileName(f)}: {m.Value}"))
            .ToList();

        Assert.Empty(offending);
    }

    /// <summary>
    /// [S-7063] One accounts row: a widget page that lets the user choose among the signed-in accounts
    /// builds the choice with <c>KitRows.Accounts</c> (directly, or through the SDK's office rows), never
    /// its own switches, so every page says "every account" the same way.
    /// </summary>
    [Fact]
    public void S7063_a_page_that_chooses_accounts_builds_the_kits_accounts_row()
    {
        var offending = RepoFiles.WidgetModules()
            .SelectMany(RepoFiles.CSharp)
            .Where(RepoFiles.IsSettingsPage)
            .Where(f => Code(f) is var code && code.Contains("SettingsHost.AccountsWith(", StringComparison.Ordinal)
                && !code.Contains("KitRows.Accounts(", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offending);
    }

    /// <summary>
    /// [S-7069] A kit view builds no menu entry: its item menus are its actions, through <c>KitMenu</c>
    /// in <c>KitList</c>; the header's view buttons ask between their choices in <c>KitHeader</c>.
    /// </summary>
    [Fact]
    public void S7069_only_the_kits_list_turns_actions_into_menu_entries()
    {
        var offending = KitSources()
            .Where(f => Path.GetFileName(f) is not ("KitList.cs" or "KitHeader.cs") && Regex.IsMatch(Code(f), @"\bnew\s+ItemMenuEntry\b|\bItemMenuEntry\("))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offending);
    }

    [GeneratedRegex(@"\b(ghp|gho|ghu|ghr|github_pat|lin_api|lin_oauth)_[A-Za-z0-9_]{8,}|\b[0-9a-f]{40}\b|Bearer\s+\S|\bAuthorization\b")]
    private static partial Regex TokenShape();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@(?<host>[A-Za-z0-9.-]+\.[A-Za-z]{2,})")]
    private static partial Regex MailAddress();

    /// <summary>
    /// [S-7070] Recorded answers and state fixtures are scrubbed: every JSON file under a
    /// <c>Fixtures/</c> folder (a module's, a provider's, a test project's) parses, and none holds a
    /// token's shape, a bearer header or an e-mail address outside the reserved example names
    /// (<c>example.com</c> and its subdomains, <c>*.example</c>, <c>*.test</c>) and
    /// <c>users.noreply.github.com</c>.
    /// </summary>
    [Fact]
    public void S7070_fixtures_parse_and_hold_no_secret_or_real_address()
    {
        var fixtures = Directory.EnumerateFiles(RepoFiles.Src, "*", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(RepoFiles.At("tests"), "*", SearchOption.AllDirectories))
            .Where(f => f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) is var parts
                && parts.Contains("Fixtures") && !parts.Any(p => p is "bin" or "obj"))
            .ToList();
        Assert.NotEmpty(fixtures);

        var problems = new List<string>();
        foreach (var file in fixtures)
        {
            var text = File.ReadAllText(file);
            if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var _ = JsonDocument.Parse(text);
                }
                catch (JsonException ex)
                {
                    problems.Add($"{Path.GetFileName(file)} does not parse: {ex.Message}");
                }
            }

            problems.AddRange(TokenShape().Matches(text).Select(m => $"{Path.GetFileName(file)} holds a token's shape: {m.Value}"));
            problems.AddRange(MailAddress().Matches(text)
                .Where(m => !IsReservedHost(m.Groups["host"].Value))
                .Select(m => $"{Path.GetFileName(file)} holds an address: {m.Value}"));
        }

        Assert.Empty(problems);
    }

    private static bool IsReservedHost(string host)
        => host is "example.com" or "users.noreply.github.com"
            || host.EndsWith(".example.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".example", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".test", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\b(GitHub|Github|Todoist)\b|\blinear\.app\b|""(github|linear|todoist)""")]
    private static partial Regex KitService();

    [GeneratedRegex(@"""(?i:x-[a-z0-9-]+|if-none-match|if-modified-since|authorization|www-authenticate)""")]
    private static partial Regex ServiceHeader();

    [GeneratedRegex(@"""[A-Z][A-Z0-9]*_[A-Z0-9_]+""|""[A-Z]{4,}""")]
    private static partial Regex ErrorCodeLiteral();

    /// <summary>
    /// [S-7071] Generic kit: the kit's shared code knows no service. It names no kit service and no
    /// registered provider, reads no service's own header (only the standard <c>ETag</c>,
    /// <c>Last-Modified</c>, <c>Link</c>, <c>Retry-After</c> are generic) and holds no error code: a
    /// service is its provider project's descriptor and rules (section 5.7).
    /// </summary>
    [Fact]
    public void S7071_the_kits_shared_code_names_no_service_header_or_error_code()
    {
        var providers = new[] { "microsoft", "google", "imap", "caldav", "carddav" };
        var offending = new List<string>();
        foreach (var file in KitSources())
        {
            var code = Code(file);
            offending.AddRange(KitService().Matches(code).Select(m => $"{Path.GetFileName(file)}: {m.Value}"));
            offending.AddRange(ServiceHeader().Matches(code).Select(m => $"{Path.GetFileName(file)}: {m.Value}"));
            offending.AddRange(ErrorCodeLiteral().Matches(code).Select(m => $"{Path.GetFileName(file)}: {m.Value}"));
            offending.AddRange(providers.Where(p => Regex.IsMatch(code, $@"""{p}""", RegexOptions.IgnoreCase)).Select(p => $"{Path.GetFileName(file)}: {p}"));
        }

        Assert.Empty(offending);
    }

    /// <summary>
    /// [S-7071] Outside the provider projects no code names a kit service (its name, its web host or
    /// its provider id). Today: none (comments are not code).
    /// </summary>
    [Fact]
    public void S7071_no_code_outside_a_provider_names_a_kit_service_beyond_todays_list()
    {
        var offending = RepoFiles.CSharp(RepoFiles.Src)
            .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("Providers"))
            .Where(f => KitService().IsMatch(Code(f)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(KitServiceNamedToday.Order(StringComparer.Ordinal), offending);
    }

    private static readonly string[] KitServiceNamedToday = [];

    [GeneratedRegex(@"""[Xx]-[A-Za-z0-9-]+""")]
    private static partial Regex ExtensionHeader();

    /// <summary>
    /// [S-7071] Outside the provider projects no code reads a service's own header. Today's: the
    /// calendar's iCalendar properties (<c>X-MICROSOFT-*</c>, <c>X-GOOGLE-*</c>: the host's own ICS
    /// reader, not a kit service). The rate-limit rules read a descriptor's scheme ([S-9920]). The
    /// list may only shrink.
    /// </summary>
    [Fact]
    public void S7071_no_code_outside_a_provider_reads_a_services_own_header_beyond_todays_list()
    {
        var offending = RepoFiles.CSharp(RepoFiles.Src)
            .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("Providers"))
            .Where(f => ExtensionHeader().IsMatch(Code(f)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["IcsEvents.cs"], offending);
    }

    /// <summary>
    /// [S-9931] Polling: every kit request goes through the one client. The account service's service
    /// path is named only where it is defined and by the desktop's transport; the transport is asked
    /// only by the client; a reply is classified only by the client and a new sign-in's identity
    /// request. So no widget, provider or host part can ask a service around the conditional helper,
    /// the bucket's block or the one classifier.
    /// </summary>
    [Fact]
    public void S9931_every_kit_request_goes_through_the_one_client()
    {
        string[] Naming(string pattern) => [.. RepoFiles.CSharp(RepoFiles.Src)
            .Where(f => Regex.IsMatch(Code(f), pattern))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)];

        Assert.Equal(["AccountService.Services.cs", "ServiceSync.cs"], Naming(@"\bSendServiceAsync\("));
        Assert.Equal(["AccountService.SignIn.cs", "ServiceClient.cs"], Naming(@"\bServiceFailures\.Classify\("));
        Assert.Equal(["ServiceClient.cs"], Naming(@"\btransport\.SendAsync\("));
    }
}
