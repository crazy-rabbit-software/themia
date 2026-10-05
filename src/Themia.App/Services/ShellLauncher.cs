using System.Diagnostics;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>What happened when something was asked to open.</summary>
public enum LaunchOutcome
{
    /// <summary>Handed to the shell.</summary>
    Opened = 0,

    /// <summary><see cref="LaunchTargets"/> would not have it.</summary>
    Refused = 1,

    /// <summary>A well-formed local path that is not there any more.</summary>
    Missing = 2,

    /// <summary>The shell was asked and said no: no association, a cancelled elevation prompt, a dead share.</summary>
    Failed = 3,
}

/// <summary>
/// The one place in this app that opens anything.
/// <para>
/// The launcher widgets — apps, shortcuts, search, and the clipboard's copy-back — all come through
/// here, and that is the entire security design. A widget layer that sits under every window and
/// opens whatever a config file names is a persistence and execution primitive; the only way that
/// is defensible is if there is exactly one function deciding what may be opened, one rule it
/// enforces, and no second path anywhere that skips it.
/// </para>
/// <para>
/// Two properties hold on every path out of this file:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Nothing is ever a command line.</b> The target goes into <see cref="ProcessStartInfo.FileName"/>
/// with <see cref="ProcessStartInfo.UseShellExecute"/> set, and <see cref="ProcessStartInfo.Arguments"/>
/// is never touched. There is no string being concatenated, so there is nothing for a quote, an
/// ampersand or a caret in a config file to escape out of.
/// </item>
/// <item>
/// <b>Nothing is opened that was not classified.</b> <see cref="LaunchTargets.Classify"/> is a pure
/// allow list of three shapes — an http/https URL, a fully qualified local or UNC path, and a
/// <c>shell:</c> name — and it runs on the literal string that is about to be launched, not on an
/// earlier version of it.
/// </item>
/// </list>
/// <para>
/// A local path is also checked for <i>existence</i> here, which the pure rule cannot do. That is
/// not security — a path that exists can still be anything — it is honesty: a broken shortcut says
/// so in the log instead of producing a shell error box over the desktop.
/// </para>
/// </summary>
internal static class ShellLauncher
{
    private const string Scope = "launcher";

    /// <summary>
    /// Open a target, or say why not. Never throws: this is called from a click handler on a
    /// surface that must stay up.
    /// </summary>
    public static LaunchOutcome Open(string? target) => Open(target, out _);

    /// <summary>
    /// [S-1407] <see cref="Open(string?)"/>, also saying which process the shell started — what an
    /// app layout needs to tell the window it opened from the rest. 0 when the shell handed none back
    /// (a URL opening in a running browser, a single-instance app passing the request on).
    /// </summary>
    public static LaunchOutcome Open(string? target, out int processId)
    {
        processId = 0;
        var classified = LaunchTargets.Classify(target);

        if (!classified.IsValid)
        {
            // Loud, because a refusal is either a typo the user needs to see or a config that has
            // been edited by something other than the user.
            Log.Warn(Scope, $"refused to open '{Excerpt(target)}': {classified.Refusal}");
            return LaunchOutcome.Refused;
        }

        if (classified.Kind == LaunchKind.LocalPath && !Exists(classified.Value))
        {
            Log.Warn(Scope, $"'{classified.Value}' is not there any more");
            return LaunchOutcome.Missing;
        }

        return Launch(classified.Value, out processId);
    }

    /// <summary>
    /// Open the folder a file lives in, for the "show me where this is" action. Goes through the
    /// same classification as everything else — the parent of a validated path is still a path from
    /// a config file.
    /// </summary>
    public static LaunchOutcome OpenContainingFolder(string? target)
    {
        var classified = LaunchTargets.Classify(target);

        if (!classified.IsValid || classified.Kind != LaunchKind.LocalPath)
        {
            Log.Warn(Scope, $"refused to reveal '{Excerpt(target)}': {classified.Refusal}");
            return LaunchOutcome.Refused;
        }

        string? parent;

        try
        {
            parent = Directory.Exists(classified.Value)
                ? classified.Value
                : Path.GetDirectoryName(classified.Value);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
        {
            Log.Warn(Scope, $"could not work out the folder holding '{classified.Value}'", ex);
            return LaunchOutcome.Refused;
        }

        // Re-classified rather than trusted: GetDirectoryName is string surgery, and its result has
        // not been through the rule.
        return Open(parent);
    }

    /// <summary>
    /// [S-547] Open a mail link: a provider's https web link, an <c>ms-outlook://emails/…</c> deep
    /// link or a <c>mailto:</c>. A second allow list beside <see cref="LaunchTargets"/> rather than a
    /// widening of it: these two schemes are right for a message and wrong for a shortcut, so they
    /// are accepted here and nowhere else. <see cref="Themia.Core.Mail.MailLinks.IsLaunchable"/> is
    /// checked on the literal string about to be launched.
    /// </summary>
    public static LaunchOutcome OpenMailLink(string? link)
    {
        if (!Themia.Core.Mail.MailLinks.IsLaunchable(link))
        {
            Log.Warn(Scope, $"refused to open mail link '{Excerpt(link)}'");
            return LaunchOutcome.Refused;
        }

        return Launch(link!);
    }

    /// <summary>
    /// [S-1268] Open a calendar event: a provider's https page or an <c>ms-outlook://events/…</c>
    /// deep link, both made by <see cref="Themia.Core.Office.CalendarLinks"/>, which is also the gate
    /// re-applied to the literal string here. Its own allow list, like mail links.
    /// </summary>
    public static LaunchOutcome OpenCalendarLink(string? link)
    {
        if (!Themia.Core.Office.CalendarLinks.IsLaunchable(link))
        {
            Log.Warn(Scope, $"refused to open calendar link '{Excerpt(link)}'");
            return LaunchOutcome.Refused;
        }

        return Launch(link!);
    }

    /// <summary>
    /// [S-547] / [S-1268] Whether Outlook for Windows' <c>ms-outlook:</c> protocol is registered, so
    /// mail and calendar items can hand off to it. Read once per run: installing Outlook mid-session
    /// is rare, and the registry is not worth asking on every click.
    /// </summary>
    public static bool OutlookInstalled => OutlookProtocol.Value;

    private static readonly Lazy<bool> OutlookProtocol = new(() =>
    {
        try
        {
            using var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(Themia.Core.Mail.MailLinks.OutlookScheme);
            return key is not null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Warn(Scope, "could not tell whether Outlook is installed; opening items on the web", ex);
            return false;
        }
    });

    /// <summary>
    /// [S-587] Write to, call or message a contact: a <c>mailto:</c>, <c>tel:</c> or <c>sms:</c> built
    /// by <see cref="Themia.Core.Office.ContactLinks"/> from the contact's own address or number, which
    /// refuses anything but a plain one. Like mail links, a separate allow list rather than a widening
    /// of <see cref="LaunchTargets"/>: right for a person, wrong for a shortcut.
    /// </summary>
    public static LaunchOutcome OpenContactLink(Themia.Core.Office.ContactLinkKind kind, string? value)
    {
        if (Themia.Core.Office.ContactLinks.Build(kind, value) is not { } link)
        {
            Log.Warn(Scope, $"refused to open a {kind} link for '{Excerpt(value)}'");
            return LaunchOutcome.Refused;
        }

        return Launch(link);
    }

    /// <summary>
    /// Open a page of Windows Settings (<c>ms-settings:</c>). Its own allow list, like mail
    /// links: the pages are fixed URIs in code, and the scheme is checked on the literal string.
    /// </summary>
    public static LaunchOutcome OpenWindowsSettings(Uri page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!string.Equals(page.Scheme, "ms-settings", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warn(Scope, $"refused to open '{Excerpt(page.OriginalString)}' as a Settings page");
            return LaunchOutcome.Refused;
        }

        return Launch(page.OriginalString);
    }

    /// <summary>
    /// The shell call itself, and the only one in the app.
    /// </summary>
    private static LaunchOutcome Launch(string value) => Launch(value, out _);

    /// <inheritdoc cref="Launch(string)"/>
    private static LaunchOutcome Launch(string value, out int processId)
    {
        processId = 0;

        try
        {
            // using: ShellExecute hands back a process handle even when nothing waits on it, and a
            // surface that lives for weeks leaks one per click without this.
            //
            // No Arguments, ever. UseShellExecute with a bare FileName is ShellExecuteW's "open"
            // verb on one string; the moment an argument string exists, so does quoting, and so
            // does a way out of it.
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = value,
                UseShellExecute = true,
            });

            processId = ProcessIdOf(process);
            return LaunchOutcome.Opened;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
            or InvalidOperationException
            or IOException
            or ObjectDisposedException)
        {
            // No association, a cancelled elevation prompt, an unreachable share. The desktop stays
            // up and the log says why nothing opened.
            Log.Warn(Scope, $"the shell would not open '{value}'", ex);
            return LaunchOutcome.Failed;
        }
    }

    /// <summary>The started process's id, or 0 when there is none (or it already ended).</summary>
    private static int ProcessIdOf(Process? process)
    {
        if (process is null)
        {
            return 0;
        }

        try
        {
            return process.Id;
        }
        catch (InvalidOperationException)
        {
            // ignore: the shell handed back a handle to a process that has already exited (a
            // launcher stub); the layout then finds the window by its exe.
            return 0;
        }
    }

    /// <summary>
    /// Whether a validated path names something that is there. Answers false rather than throwing:
    /// an unreachable network share throws from both of these.
    /// </summary>
    private static bool Exists(string path)
    {
        try
        {
            return File.Exists(path) || Directory.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Debug(Scope, $"could not tell whether '{path}' exists: {ex.Message}");

            // Unknown, not absent. Let the shell be the one to fail, rather than refusing something
            // that is merely slow to answer.
            return true;
        }
    }

    /// <summary>
    /// A refused target, short enough to log. The whole of a rejected string is exactly the thing
    /// not worth pasting into a log file at full length.
    /// </summary>
    private static string Excerpt(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();

        if (value.Length <= 120)
        {
            return value.ReplaceLineEndings(" ");
        }

        return string.Concat(value.AsSpan(0, 120).ToString().ReplaceLineEndings(" "), "…");
    }
}
